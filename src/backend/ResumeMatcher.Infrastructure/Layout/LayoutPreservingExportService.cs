using System.IO.Compression;
using System.Text.RegularExpressions;
using ResumeMatcher.Application;
using ResumeMatcher.DocumentLayout;

namespace ResumeMatcher.Infrastructure;

/// <summary>Local-only adapter. Neither originals nor generated documents are persisted.</summary>
public sealed class LayoutPreservingExportService(IResumeOptimizationService optimizations) : ILayoutPreservingExportService
{
    // Same 10 MiB ceiling as resume upload, also applied to decompressed package bytes.
    private const int MaxDocumentBytes = LayoutExportConstraints.MaxDocumentBytes;
    private static readonly DocxInspectionProfileModel Profile = new() { HeadingStyleIds = ["SectionHeader"] };

    public async Task<LayoutInspectionModel> InspectAsync(Guid optimizationId, byte[] original, CancellationToken cancellationToken)
    {
        var (plan, inspection) = await LoadAsync(optimizationId, original, cancellationToken);
        var changes = plan.AppliedChanges!.Select(item => Map(item, plan, inspection, original, cancellationToken)).ToArray();
        return new(plan.Version, inspection.SourceSha256, changes);
    }

    public async Task<byte[]> ExportAsync(Guid optimizationId, byte[] original, int version, string sourceSha256,
        IReadOnlyList<LayoutPlacementModel> placements, CancellationToken cancellationToken)
    {
        var (plan, inspection) = await LoadAsync(optimizationId, original, cancellationToken);
        if (version != plan.Version || !string.Equals(sourceSha256, inspection.SourceSha256, StringComparison.Ordinal))
            throw new OptimizationConflictException("O arquivo ou a versão mudou. Inspecione o DOCX novamente.");
        if (placements.Count != plan.AppliedChanges!.Count || placements.Select(p => p.SuggestionId).Distinct().Count() != placements.Count)
            throw new InvalidResumeException("Escolha um destino válido para cada alteração aprovada, sem duplicatas.");
        var operations = new List<DocxEditOperationModel>();
        foreach (var item in plan.AppliedChanges)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mapping = Map(item, plan, inspection, original, cancellationToken);
            var placement = placements.SingleOrDefault(p => p.SuggestionId == item.SuggestionId);
            if (placement is null || !mapping.Candidates.Any(b => b.Id == placement.BlockId))
                throw new InvalidResumeException("Uma alteração não tem destino compatível. Revise os destinos na tela.");
            operations.Add(Operation(item, inspection.Blocks.Single(b => b.Id == placement.BlockId)));
        }
        try
        {
            var output = original;
            var additionIds = plan.AppliedChanges.Where(i => string.IsNullOrWhiteSpace(i.OriginalText))
                .Select(i => i.SuggestionId.ToString("N")).ToHashSet();
            var replacements = operations.Where(o => !additionIds.Contains(o.Id)).ToArray();
            if (replacements.Length > 0)
                output = Apply(output, inspection.SourceSha256, replacements);
            // Insertions in the same category are sequential; each preserves the previous insertion.
            foreach (var insertion in operations.Where(o => additionIds.Contains(o.Id)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = DocxDocumentInspector.Inspect(output, Profile);
                var item = plan.AppliedChanges.Single(i => i.SuggestionId.ToString("N") == insertion.Id);
                output = Apply(output, current.SourceSha256,
                    [Operation(item, current.Blocks.Single(b => b.Id == insertion.BlockId))]);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return output;
        }
        catch (DocxReviewRequiredException error)
        {
            throw new InvalidResumeException($"Estas alterações não podem ser combinadas preservando o layout ({error.Code}). Revise o plano; nenhum arquivo foi gerado.");
        }
    }

    private async Task<(OptimizationPlanModel Plan, DocxInspectionModel Inspection)> LoadAsync(Guid id, byte[] original, CancellationToken ct)
    {
        var plan = await optimizations.GetPlanAsync(id, ct) ?? throw new ResourceNotFoundException("Adaptação não encontrada.");
        if (plan.Status != "Applied" || plan.AppliedChanges is not { Count: > 0 })
            throw new OptimizationConflictException("Aplique pelo menos uma alteração aprovada antes de exportar.");
        await ValidatePackageAsync(original, ct);
        DocxInspectionModel inspection;
        try { inspection = DocxDocumentInspector.Inspect(original, Profile); }
        catch (DocxReviewRequiredException error)
        {
            throw new InvalidResumeException($"Este layout ainda não é suportado ({error.Code}). Use um DOCX simples de uma coluna ou a exportação em modelo padrão.");
        }
        using var stream = new MemoryStream(original, writable: false);
        var extracted = await new DocxResumeTextExtractor().ExtractAsync(stream, ct);
        // Only whitespace is normalized, never wording, numbers, punctuation, or case.
        if (!string.Equals(Normalize(extracted), Normalize(plan.OriginalText), StringComparison.Ordinal))
            throw new OptimizationConflictException("O DOCX selecionado não corresponde ao currículo desta análise. Selecione o original usado no upload.");
        ct.ThrowIfCancellationRequested();
        return (plan, inspection);
    }

    private static LayoutChangeModel Map(AppliedOptimizationItemModel item, OptimizationPlanModel plan,
        DocxInspectionModel inspection, byte[] source, CancellationToken ct)
    {
        var decision = plan.AppliedDecisions?.SingleOrDefault(d => d.SuggestionId == item.SuggestionId);
        if (decision?.Accepted != true || item.Level == OptimizationSafetyLevel.Forbidden ||
            item.Level == OptimizationSafetyLevel.NeedsConfirmation && (!item.WasConfirmed || !decision.Confirmed))
            return new(item.SuggestionId, item.ProposedText, "blocked", [], "Esta informação não foi aprovada e confirmada no plano.");
        var insertion = string.IsNullOrWhiteSpace(item.OriginalText);
        var candidates = new List<LayoutBlockModel>();
        foreach (var block in inspection.Blocks.Where(b => b.Editable))
        {
            ct.ThrowIfCancellationRequested();
            if (insertion ? block.Kind is not ("skills" or "summary" or "experience") : block.Kind is not ("summary" or "experience" or "professional_title")) continue;
            if (!insertion)
            {
                var start = block.Text.IndexOf(item.OriginalText, StringComparison.OrdinalIgnoreCase);
                if (start < 0 || block.Text.IndexOf(item.OriginalText, start + 1, StringComparison.OrdinalIgnoreCase) >= 0) continue;
            }
            try
            {
                // Offer only destinations that this engine can actually edit, including formatting guards.
                Apply(source, inspection.SourceSha256, [Operation(item, block)]);
                candidates.Add(new(block.Id, block.Text, block.Section));
            }
            catch (DocxReviewRequiredException) { /* Not a safe destination. No personal text is logged. */ }
        }
        return new(item.SuggestionId, item.ProposedText, insertion ? "addition" : "replace_text", candidates,
            candidates.Count > 0 ? null : insertion
                ? "Não há destino compatível. Skills precisam de lista categorizada; frases precisam de resumo ou item de experiência com formatação simples."
                : "Não foi encontrado um trecho editável exato no resumo ou em um item de experiência. Cabeçalhos, datas e formatações complexas são protegidos.");
    }

    private static DocxEditOperationModel Operation(AppliedOptimizationItemModel item, DocxBlockModel block)
    {
        var addition = string.IsNullOrWhiteSpace(item.OriginalText);
        var skill = addition && block.Kind == "skills";
        var originalText = block.Text.TrimEnd();
        var separator = originalText.EndsWith('.') || originalText.EndsWith('!') || originalText.EndsWith('?') ? " " : ". ";
        return new()
        {
        Id = item.SuggestionId.ToString("N"), BlockId = block.Id, ExpectedText = block.Text,
        Kind = skill ? "insert_skill" : "replace_text",
        Start = addition ? 0 : block.Text.IndexOf(item.OriginalText, StringComparison.OrdinalIgnoreCase),
        Length = skill ? 0 : addition ? block.Text.Length : item.OriginalText.Length,
        NewText = addition && !skill ? originalText + separator + item.ProposedText : item.ProposedText,
        Approved = true, EvidenceIds = ["applied-plan:" + item.SuggestionId.ToString("N")]
        };
    }

    private static byte[] Apply(byte[] source, string hash, DocxEditOperationModel[] operations) =>
        DocxAdaptationEngine.Apply(source, new() { SourceSha256 = hash, Profile = Profile, Operations = operations }).Document;

    private static string Normalize(string text) => Regex.Replace(text, @"\s+", " ").Trim();

    private static async Task ValidatePackageAsync(byte[] source, CancellationToken ct)
    {
        if (source.Length is 0 or > MaxDocumentBytes) throw new InvalidResumeException("O DOCX deve ter no máximo 10 MiB.");
        try
        {
            using var archive = new ZipArchive(new MemoryStream(source, writable: false), ZipArchiveMode.Read);
            long total = 0;
            var buffer = new byte[8192];
            foreach (var entry in archive.Entries)
            {
                if (entry.Length > MaxDocumentBytes - total) throw new InvalidResumeException("O conteúdo descompactado do DOCX excede 10 MiB.");
                using var content = entry.Open();
                int read;
                while ((read = await content.ReadAsync(buffer, ct)) != 0)
                {
                    total += read;
                    if (total > MaxDocumentBytes) throw new InvalidResumeException("O conteúdo descompactado do DOCX excede 10 MiB.");
                }
            }
        }
        catch (InvalidDataException) { throw new InvalidResumeException("O arquivo não é um DOCX válido."); }
    }
}
