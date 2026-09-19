using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.DependencyInjection;
using ResumeMatcher.Application;
using ResumeMatcher.Domain;
using ResumeMatcher.Infrastructure;

namespace ResumeMatcher.Tests;

public sealed class LayoutExportIntegrationTests
{
    [Theory]
    [InlineData("empty")]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    public async Task Partial_Export_Rejects_Invalid_Selection(string kind)
    {
        await using var factory = new ResumeMatcherApiFactory("Development");
        var (id, suggestionId, source) = await Seed(factory);
        using var client = factory.CreateAuthenticatedClient();
        using var form = Form(source);
        form.Add(new StringContent(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(source))), "sourceSha256");
        form.Add(new StringContent("2"), "version");
        var selected = new LayoutPlacementModel(suggestionId, "p:3");
        LayoutPlacementModel[] placements = kind switch {
            "empty" => [],
            "duplicate" => [selected, selected],
            _ => [selected, new(Guid.NewGuid(), "p:3")]
        };
        form.Add(new StringContent(JsonSerializer.Serialize(placements, new JsonSerializerOptions(JsonSerializerDefaults.Web))), "placements");
        using var response = await client.PostAsync($"/api/optimizations/{id}/layout/export", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Selected_Change_Is_Exported_Without_Unplaceable_Change()
    {
        await using var factory = new ResumeMatcherApiFactory("Development");
        var (id, selectedId, source) = await Seed(factory);
        var blockedId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ResumeMatcherDbContext>();
            var entity = db.Optimizations.Single(p => p.Id == id);
            entity.DecisionsJson = JsonSerializer.Serialize(new {
                decisions = new[] { new OptimizationDecisionModel(selectedId, true), new OptimizationDecisionModel(blockedId, true) },
                appliedItems = new[] {
                    new AppliedOptimizationItemModel(selectedId, OptimizationSafetyLevel.Safe, "API em C#", "APIs em C#", "Sintético", false, "CurriculoOriginal"),
                    new AppliedOptimizationItemModel(blockedId, OptimizationSafetyLevel.Safe, "Trecho inexistente", "Título sintético", "Sintético", false, "CurriculoOriginal") }
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateAuthenticatedClient();
        using var inspection = await client.PostAsync($"/api/optimizations/{id}/layout/inspect", Form(source));
        var snapshot = await inspection.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(snapshot.GetProperty("changes")[1].GetProperty("candidates").EnumerateArray());
        using var export = await client.PostAsync($"/api/optimizations/{id}/layout/export",
            ExportForm(source, snapshot.GetProperty("sourceSha256").GetString()!, 2, selectedId, "p:3"));
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        using var output = WordprocessingDocument.Open(new MemoryStream(await export.Content.ReadAsByteArrayAsync()), false);
        Assert.Contains("APIs em C#", output.MainDocumentPart!.Document!.InnerText);
        Assert.DoesNotContain("Título sintético", output.MainDocumentPart.Document.InnerText);
        using var before = WordprocessingDocument.Open(new MemoryStream(source), false);
        Assert.Equal(before.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().Where((_, index) => index != 3).Select(p => p.OuterXml),
            output.MainDocumentPart.Document.Body!.Elements<Paragraph>().Where((_, index) => index != 3).Select(p => p.OuterXml));
    }

    [Theory]
    [InlineData("Software Engineer | .NET | React")]
    [InlineData("Engenheiro de Software | .NET | React")]
    public async Task Approved_Professional_Title_Is_Exported_With_Original_Formatting(string title)
    {
        var source = DocumentWithTitle(title);
        await using var factory = new ResumeMatcherApiFactory("Development");
        var (id, suggestionId, _) = await Seed(factory, original: title,
            proposed: "Engenheiro de Software | APIs REST", document: source);
        using var client = factory.CreateAuthenticatedClient();
        using var inspect = await client.PostAsync($"/api/optimizations/{id}/layout/inspect", Form(source));
        Assert.Equal(HttpStatusCode.OK, inspect.StatusCode);
        var snapshot = await inspect.Content.ReadFromJsonAsync<JsonElement>();
        var change = snapshot.GetProperty("changes")[0];
        Assert.Equal(JsonValueKind.Null, change.GetProperty("blockedReason").ValueKind);
        Assert.Equal("p:2", Assert.Single(change.GetProperty("candidates").EnumerateArray()).GetProperty("id").GetString());
        using var export = await client.PostAsync($"/api/optimizations/{id}/layout/export",
            ExportForm(source, snapshot.GetProperty("sourceSha256").GetString()!, 2, suggestionId, "p:2"));
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        using var before = WordprocessingDocument.Open(new MemoryStream(source), false);
        using var after = WordprocessingDocument.Open(new MemoryStream(await export.Content.ReadAsByteArrayAsync()), false);
        var originalParagraphs = before.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().ToArray();
        var resultParagraphs = after.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().ToArray();
        Assert.Equal("Engenheiro de Software | APIs REST", resultParagraphs[2].InnerText);
        Assert.Equal(originalParagraphs[2].ParagraphProperties!.OuterXml, resultParagraphs[2].ParagraphProperties!.OuterXml);
        Assert.Equal(originalParagraphs[2].GetFirstChild<Run>()!.RunProperties!.OuterXml,
            resultParagraphs[2].GetFirstChild<Run>()!.RunProperties!.OuterXml);
        Assert.Equal(originalParagraphs.Where((_, i) => i != 2).Select(p => p.OuterXml),
            resultParagraphs.Where((_, i) => i != 2).Select(p => p.OuterXml));
        Assert.Equal(before.MainDocumentPart.Document.Body.GetFirstChild<SectionProperties>()!.OuterXml,
            after.MainDocumentPart.Document.Body.GetFirstChild<SectionProperties>()!.OuterXml);
    }

    [Theory]
    [InlineData("Pessoa Fictícia", "p:0")]
    [InlineData("pessoa@example.invalid", "p:1")]
    [InlineData("Software Engineer | 2022–2024", "p:2")]
    [InlineData("Software Engineer | contato@example.invalid", "p:2")]
    [InlineData("Rua Exemplo, 100", "p:2")]
    public async Task Protected_Preamble_Cannot_Be_Changed_As_A_Title(string original, string block)
    {
        var source = DocumentWithTitle(original);
        await using var factory = new ResumeMatcherApiFactory("Development");
        var (id, suggestionId, _) = await Seed(factory, original: original, proposed: "Outro título", document: source);
        using var client = factory.CreateAuthenticatedClient();
        using var inspect = await client.PostAsync($"/api/optimizations/{id}/layout/inspect", Form(source));
        var snapshot = await inspect.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(snapshot.GetProperty("changes")[0].GetProperty("candidates").EnumerateArray());
        using var export = await client.PostAsync($"/api/optimizations/{id}/layout/export",
            ExportForm(source, snapshot.GetProperty("sourceSha256").GetString()!, 2, suggestionId, block));
        Assert.Equal(HttpStatusCode.BadRequest, export.StatusCode);
    }

    private static byte[] DocumentWithTitle(string title)
    {
        using var stream = new MemoryStream();
        stream.Write(Document());
        stream.Position = 0;
        using (var doc = WordprocessingDocument.Open(stream, true))
        {
            var body = doc.MainDocumentPart!.Document!.Body!;
            body.InsertBefore(new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(new RunProperties(new Bold(), new FontSize { Val = "18" }), new Text(title))),
                body.Elements<Paragraph>().ElementAt(2));
        }
        return stream.ToArray();
    }

    [Theory]
    [InlineData("p:3")]
    [InlineData("p:5")]
    public async Task Replacement_And_Multiple_Additions_Are_All_Preserved(string destination)
    {
        await using var factory = new ResumeMatcherApiFactory("Development");
        var (id, replacementId, source) = await Seed(factory);
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ResumeMatcherDbContext>();
            var entity = db.Optimizations.Single(p => p.Id == id);
            entity.DecisionsJson = JsonSerializer.Serialize(new {
                decisions = new[] { new OptimizationDecisionModel(replacementId, true), new OptimizationDecisionModel(firstId, true, true), new OptimizationDecisionModel(secondId, true, true) },
                appliedItems = new[] {
                    new AppliedOptimizationItemModel(replacementId, OptimizationSafetyLevel.Safe, "API em C#", "APIs em C#", "Sintético", false, "CurriculoOriginal"),
                    new AppliedOptimizationItemModel(firstId, OptimizationSafetyLevel.NeedsConfirmation, "", "PostgreSQL", "Sintético", true, "DeclaradaPeloUsuario"),
                    new AppliedOptimizationItemModel(secondId, OptimizationSafetyLevel.NeedsConfirmation, "", "SQLite", "Sintético", true, "DeclaradaPeloUsuario") }
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateAuthenticatedClient();
        using var form = Form(source);
        form.Add(new StringContent(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(source))), "sourceSha256");
        form.Add(new StringContent("2"), "version");
        form.Add(new StringContent(JsonSerializer.Serialize(new[] {
            new { suggestionId = replacementId, blockId = "p:3" },
            new { suggestionId = firstId, blockId = destination },
            new { suggestionId = secondId, blockId = destination } })), "placements");
        using var result = await client.PostAsync($"/api/optimizations/{id}/layout/export", form);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        using var output = WordprocessingDocument.Open(new MemoryStream(await result.Content.ReadAsByteArrayAsync()), false);
        Assert.Contains("APIs em C#", output.MainDocumentPart!.Document!.InnerText);
        Assert.Contains("PostgreSQL", output.MainDocumentPart.Document.InnerText);
        Assert.Contains("SQLite", output.MainDocumentPart.Document.InnerText);
        using var untouched = WordprocessingDocument.Open(new MemoryStream(source), false);
        Assert.DoesNotContain("PostgreSQL", untouched.MainDocumentPart!.Document!.InnerText);
    }

    [Fact]
    public async Task Expired_Plan_Is_Not_Available_For_Layout_Export()
    {
        await using var factory = new ResumeMatcherApiFactory("Development");
        var (id, suggestionId, source) = await Seed(factory);
        factory.Clock.Advance(TimeSpan.FromDays(31));
        using var client = factory.CreateAuthenticatedClient();
        using var inspection = await client.PostAsync($"/api/optimizations/{id}/layout/inspect", Form(source));
        Assert.Equal(HttpStatusCode.NotFound, inspection.StatusCode);
        using var export = await client.PostAsync($"/api/optimizations/{id}/layout/export", ExportForm(source, "hash", 2, suggestionId));
        Assert.Equal(HttpStatusCode.NotFound, export.StatusCode);
    }

    [Fact]
    public void Multipart_Original_Is_Not_Spilled_To_Disk()
    {
        var limits = typeof(ResumeMatcher.Api.Controllers.LayoutExportsController)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.RequestFormLimitsAttribute), true)
            .Cast<Microsoft.AspNetCore.Mvc.RequestFormLimitsAttribute>().Single();
        Assert.True(limits.MemoryBufferThreshold >= 10 * 1024 * 1024);
        Assert.Equal(10 * 1024 * 1024, limits.MultipartBodyLengthLimit);
    }
    [Theory]
    [InlineData("PostgreSQL")]
    [InlineData("Atuação em revisão de código e orientação técnica.")]
    public async Task Confirmed_Addition_Has_A_Selectable_Destination(string proposed)
    {
        await using var factory = new ResumeMatcherApiFactory("Development");
        var (id, suggestionId, source) = await Seed(factory, "", proposed, OptimizationSafetyLevel.NeedsConfirmation, true);
        using var client = factory.CreateAuthenticatedClient();
        using var inspect = await client.PostAsync($"/api/optimizations/{id}/layout/inspect", Form(source));
        Assert.Equal(HttpStatusCode.OK, inspect.StatusCode);
        var snapshot = await inspect.Content.ReadFromJsonAsync<JsonElement>();
        var change = snapshot.GetProperty("changes")[0];
        Assert.True(change.GetProperty("candidates").GetArrayLength() > 0);
        var block = proposed == "PostgreSQL" ? "p:5" : "p:3";
        using var export = await client.PostAsync($"/api/optimizations/{id}/layout/export",
            ExportForm(source, snapshot.GetProperty("sourceSha256").GetString()!, 2, suggestionId, block));
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        using var output = WordprocessingDocument.Open(new MemoryStream(await export.Content.ReadAsByteArrayAsync()), false);
        Assert.Contains(proposed, output.MainDocumentPart!.Document!.InnerText);
        Assert.Contains(proposed == "PostgreSQL" ? "MySQL, PostgreSQL." : "API em C#. Atuação", output.MainDocumentPart!.Document!.InnerText);
    }

    [Theory]
    [InlineData(OptimizationSafetyLevel.NeedsConfirmation)]
    [InlineData(OptimizationSafetyLevel.Forbidden)]
    public async Task Unconfirmed_Or_Forbidden_Content_Cannot_Be_Exported(OptimizationSafetyLevel level)
    {
        await using var factory = new ResumeMatcherApiFactory("Development");
        var (id, suggestionId, source) = await Seed(factory, level: level);
        using var client = factory.CreateAuthenticatedClient();
        using var inspect = await client.PostAsync($"/api/optimizations/{id}/layout/inspect", Form(source));
        var snapshot = await inspect.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(snapshot.GetProperty("changes")[0].GetProperty("candidates").EnumerateArray());
        using var export = await client.PostAsync($"/api/optimizations/{id}/layout/export",
            ExportForm(source, snapshot.GetProperty("sourceSha256").GetString()!, 2, suggestionId));
        Assert.Equal(HttpStatusCode.BadRequest, export.StatusCode);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public async Task Stale_Version_Or_Source_Hash_Rejects_Export(int version, bool wrongHash)
    {
        await using var factory = new ResumeMatcherApiFactory("Development");
        var (id, suggestionId, source) = await Seed(factory);
        using var client = factory.CreateAuthenticatedClient();
        using var export = await client.PostAsync($"/api/optimizations/{id}/layout/export",
            ExportForm(source, wrongHash ? "wrong-hash" : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(source)), version, suggestionId));
        Assert.Equal(HttpStatusCode.Conflict, export.StatusCode);
    }

    [Fact]
    public async Task Unknown_Client_Text_And_Invalid_Docx_Are_Rejected()
    {
        await using var factory = new ResumeMatcherApiFactory("Development");
        var (id, suggestionId, source) = await Seed(factory);
        using var client = factory.CreateAuthenticatedClient();
        using var bad = await client.PostAsync($"/api/optimizations/{id}/layout/inspect", Form([1, 2, 3]));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        using var form = Form(source);
        form.Add(new StringContent("2"), "version");
        form.Add(new StringContent("hash"), "sourceSha256");
        form.Add(new StringContent(JsonSerializer.Serialize(new[] { new { suggestionId, blockId = "p:3", newText = "Invented content" } })), "placements");
        using var tampered = await client.PostAsync($"/api/optimizations/{id}/layout/export", form);
        Assert.Equal(HttpStatusCode.BadRequest, tampered.StatusCode);
    }

    private static MultipartFormDataContent ExportForm(byte[] source, string hash, int version, Guid suggestionId, string blockId = "p:3")
    {
        var form = Form(source);
        form.Add(new StringContent(hash), "sourceSha256");
        form.Add(new StringContent(version.ToString()), "version");
        form.Add(new StringContent(JsonSerializer.Serialize(new[] { new { suggestionId, blockId } })), "placements");
        return form;
    }

    [Fact]
    public async Task Inspect_And_Export_Use_Canonical_Applied_Changes()
    {
        await using var factory = new ResumeMatcherApiFactory("Development");
        using var client = factory.CreateAuthenticatedClient();
        var (id, suggestionId, source) = await Seed(factory);
        using var inspect = await client.PostAsync($"/api/optimizations/{id}/layout/inspect", Form(source));
        Assert.Equal(HttpStatusCode.OK, inspect.StatusCode);
        var snapshot = await inspect.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(snapshot.GetProperty("changes").EnumerateArray());
        var change = snapshot.GetProperty("changes")[0];
        var block = change.GetProperty("candidates")[0].GetProperty("id").GetString()!;
        using var form = Form(source);
        form.Add(new StringContent(snapshot.GetProperty("sourceSha256").GetString()!), "sourceSha256");
        form.Add(new StringContent("2"), "version");
        form.Add(new StringContent(JsonSerializer.Serialize(new[] { new { suggestionId, blockId = block } })), "placements");
        using var export = await client.PostAsync($"/api/optimizations/{id}/layout/export", form);
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal("no-store", export.Headers.CacheControl?.ToString());
        using var output = WordprocessingDocument.Open(new MemoryStream(await export.Content.ReadAsByteArrayAsync()), false);
        Assert.Contains("APIs em C#", output.MainDocumentPart!.Document!.InnerText);
        Assert.Equal(6, output.MainDocumentPart.Document.Body!.Elements<Paragraph>().Count());
        Assert.NotNull(output.MainDocumentPart!.Document!.Body!.Elements<SectionProperties>().Single());
    }

    [Fact]
    public async Task Original_Must_Match_And_Ownership_Is_Enforced()
    {
        await using var factory = new ResumeMatcherApiFactory("Development");
        var (id, _, _) = await Seed(factory);
        using var client = factory.CreateAuthenticatedClient();
        using var wrong = await client.PostAsync($"/api/optimizations/{id}/layout/inspect", Form(Document("Outro texto")));
        Assert.Equal(HttpStatusCode.Conflict, wrong.StatusCode);
        using var other = factory.CreateAuthenticatedClient("other-owner");
        using var forbidden = await other.PostAsync($"/api/optimizations/{id}/layout/inspect", Form(Document()));
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
        using var anonymous = factory.CreateClient();
        using var unauthenticated = await anonymous.PostAsync($"/api/optimizations/{id}/layout/inspect", Form(Document()));
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
    }

    [Fact]
    public async Task Local_Prototype_Is_Not_Available_In_Production()
    {
        await using var factory = new ResumeMatcherApiFactory("Production");
        using var client = factory.CreateAuthenticatedClient();
        using var response = await client.PostAsync($"/api/optimizations/{Guid.NewGuid()}/layout/inspect", Form(Document()));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    internal static MultipartFormDataContent Form(byte[] source)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(source), "file", "synthetic.docx");
        return form;
    }

    internal static byte[] Document(string summary = "API em C#")
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.AddNewPart<StyleDefinitionsPart>().Styles = new Styles(new Style(new StyleName { Val = "SectionHeader" })
                { StyleId = "SectionHeader", Type = StyleValues.Paragraph });
            Paragraph Heading(string title) => new(new ParagraphProperties(new ParagraphStyleId { Val = "SectionHeader" }), new Run(new Text(title)));
            main.Document = new Document(new Body(new Paragraph(new Run(new Text("Pessoa Fictícia"))),
                new Paragraph(new Run(new Text("pessoa@example.invalid"))), Heading("RESUMO"),
                new Paragraph(new Run(new Text(summary))), Heading("HABILIDADES"),
                new Paragraph(new Run(new RunProperties(new Bold()), new Text("Bancos: ")), new Run(new Text("MySQL."))),
                new SectionProperties(new PageSize { Width = 11906, Height = 16838 })));
        }
        return stream.ToArray();
    }

    internal static async Task<(Guid Id, Guid SuggestionId, byte[] Source)> Seed(ResumeMatcherApiFactory factory,
        string original = "API em C#", string proposed = "APIs em C#", OptimizationSafetyLevel level = OptimizationSafetyLevel.Safe,
        bool confirmed = false, byte[]? document = null)
    {
        var source = document ?? Document();
        var text = (await new DocxResumeTextExtractor().ExtractAsync(new MemoryStream(source), default)).Trim();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ResumeMatcherDbContext>();
        var resume = new ResumeEntity { OwnerUserId = "synthetic-owner-uid", FileName = "synthetic.docx", ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ExtractedText = text };
        var suggestionId = Guid.NewGuid();
        var item = new AppliedOptimizationItemModel(suggestionId, level, original, proposed, "Sintético", confirmed, "CurriculoOriginal");
        var entity = new ResumeOptimizationEntity { OwnerUserId = resume.OwnerUserId, ResumeId = resume.Id,
            AnalysisId = Guid.NewGuid(), OriginalText = text, Status = "Applied", Version = 2, SuggestionsJson = "[]", AdaptedText = proposed,
            DecisionsJson = JsonSerializer.Serialize(new { decisions = new[] { new OptimizationDecisionModel(suggestionId, true, confirmed) }, appliedItems = new[] { item } }, new JsonSerializerOptions(JsonSerializerDefaults.Web)) };
        db.Resumes.Add(resume);
        db.Analyses.Add(new AnalysisEntity { Id = entity.AnalysisId, ResumeId = resume.Id, OwnerUserId = resume.OwnerUserId, JobDescription = "Vaga sintética", ResultJson = "{}" });
        db.Optimizations.Add(entity);
        await db.SaveChangesAsync();
        return (entity.Id, suggestionId, source);
    }
}
