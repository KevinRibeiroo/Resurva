using System.Text.RegularExpressions;
using System.Xml;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ResumeMatcher.DocumentLayout;

public static class DocxAdaptationEngine
{
    public static DocxAdaptationResultModel Apply(byte[] source, DocxAdaptationPlanModel plan)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Version != 1 || plan.Profile is null || plan.Operations is null || plan.Operations.Length == 0)
            throw new DocxReviewRequiredException("invalid_plan");
        var inspection = DocxDocumentInspector.Inspect(source, plan.Profile);
        if (!string.Equals(inspection.SourceSha256, plan.SourceSha256, StringComparison.OrdinalIgnoreCase))
            throw new DocxReviewRequiredException("source_hash_mismatch");
        using var memory = new MemoryStream();
        memory.Write(source);
        memory.Position = 0;
        var changedNodes = new HashSet<int>();
        var expectedParagraphs = new Dictionary<int, string>();
        using (var package = WordprocessingDocument.Open(memory, true, new OpenSettings { AutoSave = false }))
        {
            var xml = package.MainDocumentPart!.Document!;
            var paragraphs = xml.Body!.Elements<Paragraph>().ToArray();
            var allTexts = xml.Descendants<Text>().ToArray();
            var prepared = new List<PreparedEdit>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var operation in plan.Operations)
            {
                if (operation is null || string.IsNullOrEmpty(operation.Id) ||
                    !Regex.IsMatch(operation.Id, @"\A[A-Za-z0-9_-]+\z") || !ids.Add(operation.Id))
                    throw new DocxReviewRequiredException("invalid_operation_id");
                if (!operation.Approved) throw Review("approval_required", operation);
                if (operation.EvidenceIds is null || operation.EvidenceIds.Length == 0 || operation.EvidenceIds.Any(string.IsNullOrWhiteSpace))
                    throw Review("evidence_required", operation);
                var block = inspection.Blocks.SingleOrDefault(b => b.Id == operation.BlockId);
                if (block is null || !block.Editable) throw Review("protected_target", operation);
                if (operation.ExpectedText != block.Text) throw Review("expected_text_mismatch", operation);
                ValidateReplacement(operation);
                var paragraph = paragraphs[block.Index];
                var start = operation.Start;
                var length = operation.Length;
                var replacement = operation.NewText;
                if (operation.Kind == "insert_skill")
                {
                    if (block.Kind != "skills" || start != 0 || length != 0) throw Review("invalid_skill_target", operation);
                    try
                    {
                        var skillEdit = DocxSkillListEditor.Prepare(paragraph, replacement);
                        start = paragraph.Descendants<Text>().TakeWhile(t => t != skillEdit.Target).Sum(t => t.Text.Length);
                        length = skillEdit.Target.Text.Length;
                        replacement = skillEdit.Replacement;
                    }
                    catch (DocxReviewRequiredException error) { throw Review(error.Code, operation); }
                    catch (ArgumentException) { throw Review("invalid_skill", operation); }
                }
                else if (operation.Kind != "replace_text" || block.Kind is not ("professional_title" or "summary" or "experience"))
                    throw Review("unsupported_operation", operation);
                if (start < 0 || length <= 0 || (long)start + length > block.Text.Length ||
                    SplitsSurrogate(block.Text, start) || SplitsSurrogate(block.Text, start + length)) throw Review("invalid_range", operation);
                var touched = Intersections(paragraph, start, length).ToArray();
                var firstFormat = (touched[0].Node.Parent as Run)?.RunProperties;
                if (touched.Any(t => !DocxPackageVerifier.EquivalentProperties(firstFormat, (t.Node.Parent as Run)?.RunProperties)))
                    throw Review("mixed_formatting", operation);
                if (block.Kind == "experience" && !Numbers(block.Text.Substring(start, length)).SequenceEqual(Numbers(replacement)))
                    throw Review("historical_numbers_changed", operation);
                foreach (var touchedNode in touched) changedNodes.Add(Array.IndexOf(allTexts, touchedNode.Node));
                prepared.Add(new(operation, block.Index, start, length, replacement));
            }
            foreach (var group in prepared.GroupBy(e => e.ParagraphIndex))
            {
                var ascending = group.OrderBy(e => e.Start).ToArray();
                for (var i = 1; i < ascending.Length; i++)
                    if (ascending[i].Start < ascending[i - 1].Start + ascending[i - 1].Length)
                        throw Review("overlapping_operations", ascending[i].Operation);
                var expected = paragraphs[group.Key].InnerText;
                foreach (var edit in ascending.Reverse())
                {
                    expected = expected[..edit.Start] + edit.Replacement + expected[(edit.Start + edit.Length)..];
                    // Punctuation at a range boundary can split/join a number without containing any digits itself.
                    if (inspection.Blocks[group.Key].Kind == "experience" &&
                        !Numbers(paragraphs[group.Key].InnerText).SequenceEqual(Numbers(expected)))
                        throw Review("historical_numbers_changed", edit.Operation);
                }
                expectedParagraphs[group.Key] = expected;
            }
            // Only now, after all preconditions and collisions were checked, mutate the in-memory copy.
            foreach (var group in prepared.GroupBy(e => e.ParagraphIndex))
                foreach (var edit in group.OrderByDescending(e => e.Start))
                    Replace(paragraphs[edit.ParagraphIndex], edit.Start, edit.Length, edit.Replacement);
            xml.Save();
        }
        var result = memory.ToArray();
        var schemaErrors = DocxPackageVerifier.Verify(source, result, changedNodes, expectedParagraphs);
        return new(result, plan.Operations.Select(o => o.Id).ToArray(), "visual_review_pending", schemaErrors);
    }

    private static void ValidateReplacement(DocxEditOperationModel operation)
    {
        if (string.IsNullOrWhiteSpace(operation.NewText) || operation.NewText.Any(char.IsControl)) throw Review("invalid_replacement", operation);
        try { XmlConvert.VerifyXmlChars(operation.NewText); }
        catch (XmlException) { throw Review("invalid_replacement", operation); }
    }

    private static void Replace(Paragraph paragraph, int start, int length, string replacement)
    {
        var targets = Intersections(paragraph, start, length).ToArray();
        for (var i = 0; i < targets.Length; i++)
        {
            var (node, offset, count) = targets[i];
            node.Text = node.Text[..offset] + (i == 0 ? replacement : "") + node.Text[(offset + count)..];
            if (node.Text.Length > 0 && (char.IsWhiteSpace(node.Text[0]) || char.IsWhiteSpace(node.Text[^1])))
                node.Space = SpaceProcessingModeValues.Preserve;
        }
    }

    private static IEnumerable<(Text Node, int Offset, int Count)> Intersections(Paragraph paragraph, int start, int length)
    {
        var offset = 0;
        foreach (var text in paragraph.Descendants<Text>())
        {
            var begin = Math.Max(start, offset);
            var end = Math.Min(start + length, offset + text.Text.Length);
            if (begin < end) yield return (text, begin - offset, end - begin);
            offset += text.Text.Length;
        }
    }

    private static bool SplitsSurrogate(string text, int index) => index > 0 && index < text.Length &&
        char.IsHighSurrogate(text[index - 1]) && char.IsLowSurrogate(text[index]);
    private static IEnumerable<string> Numbers(string text) => Regex.Matches(text, @"\d+(?:[.,]\d+)*").Select(m => m.Value);
    private static DocxReviewRequiredException Review(string code, DocxEditOperationModel op) => new(code, op.Id);
    private sealed record PreparedEdit(DocxEditOperationModel Operation, int ParagraphIndex, int Start, int Length, string Replacement);
}
