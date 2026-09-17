using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentFormat.OpenXml.Packaging;

namespace DocxLayoutProbe;

public static class DocxLocalCommands
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken = default)
    {
        if (args.Length != 4 || args[0] is not ("inspect" or "apply"))
        {
            await error.WriteLineAsync("Uso: inspect INPUT.docx PROFILE.json PASTA_NOVA | apply INPUT.docx PLAN.json PASTA_NOVA");
            return 2;
        }
        try
        {
            var source = await File.ReadAllBytesAsync(args[1], cancellationToken);
            var configuration = await File.ReadAllTextAsync(args[2], cancellationToken);
            Dictionary<string, byte[]> files;
            if (args[0] == "inspect")
            {
                var profile = JsonSerializer.Deserialize<DocxInspectionProfileModel>(configuration, JsonOptions)
                    ?? throw new DocxReviewRequiredException("invalid_profile");
                var inspection = DocxDocumentInspector.Inspect(source, profile);
                var plan = new DocxAdaptationPlanModel { SourceSha256 = inspection.SourceSha256, Profile = profile };
                files = new()
                {
                    ["inspection.json"] = JsonSerializer.SerializeToUtf8Bytes(inspection, JsonOptions),
                    ["plan.json"] = JsonSerializer.SerializeToUtf8Bytes(plan, JsonOptions)
                };
            }
            else
            {
                var plan = JsonSerializer.Deserialize<DocxAdaptationPlanModel>(configuration, JsonOptions)
                    ?? throw new DocxReviewRequiredException("invalid_plan");
                var result = DocxAdaptationEngine.Apply(source, plan);
                files = new()
                {
                    ["adaptado.docx"] = result.Document,
                    ["report.json"] = JsonSerializer.SerializeToUtf8Bytes(new
                    {
                        result.ReviewStatus,
                        result.AppliedOperationIds,
                        result.ExistingSchemaErrors,
                        structuralVerification = "passed",
                        newSchemaErrors = 0,
                        originalPreserved = true
                    }, JsonOptions)
                };
            }
            await LocalArtifactWriter.PublishAsync(args[3], files, cancellationToken);
            await output.WriteLineAsync($"Saída local: {Path.GetFullPath(args[3])}");
            await output.WriteLineAsync(args[0] == "inspect"
                ? "Inspeção concluída. O plano está vazio; revise os destinos e confirme o conteúdo antes de aplicar. Arquivos contêm dados privados: não versionar."
                : "visual_review_pending: candidato gerado; confira aparência e paginação no Word. Não integrado à API.");
            return 0;
        }
        catch (DocxReviewRequiredException exception)
        {
            // Code is produced by our guards; never print arbitrary JSON values or document/parser messages.
            await error.WriteLineAsync($"review_required: {exception.Code}. Operação: {exception.OperationId ?? "plano"}. Nenhum resultado adaptado foi publicado.");
        }
        catch (OperationCanceledException)
        {
            await error.WriteLineAsync("Operação cancelada; nenhum resultado foi publicado.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or
            JsonException or System.Xml.XmlException or OpenXmlPackageException or NotSupportedException or InvalidOperationException)
        {
            await error.WriteLineAsync("Falha local de leitura, formato ou gravação. Confira os arquivos, permissões e se a pasta de destino é nova.");
        }
        return 2;
    }
}
