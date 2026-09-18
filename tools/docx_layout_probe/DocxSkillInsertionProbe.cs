using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocxLayoutProbe;

/// <summary>Local experiment only; not a production exporter or upload validator.</summary>
public static class DocxSkillInsertionProbe
{
    public static byte[] Insert(byte[] original, string section, string category, string skill, bool confirmed)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        skill = DocxSkillListEditor.ValidateSkill(skill);
        if (!confirmed) throw Review();

        using var output = new MemoryStream();
        output.Write(original);
        output.Position = 0;
        using (var document = WordprocessingDocument.Open(output, true, new OpenSettings { AutoSave = false }))
        {
            if (document.DocumentType != WordprocessingDocumentType.Document) throw Review();
            var main = document.MainDocumentPart ?? throw Review();
            var xml = main.Document ?? throw Review();
            var body = xml.Body ?? throw Review();
            // Deliberately narrow profile. Unsupported layouts must never become a generic template.
            if (main.VbaProjectPart is not null ||
                main.DocumentSettingsPart?.Settings?.Descendants<DocumentProtection>().Any() == true ||
                body.ChildElements.Any(e => e is not Paragraph and not SectionProperties) ||
                body.Descendants<Columns>().Any(c => (c.ColumnCount?.Value ?? 1) != 1))
                throw Review();

            var paragraphs = body.Elements<Paragraph>().ToList();
            bool Heading(Paragraph p) => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value == "Heading1";
            var starts = paragraphs.Select((p, i) => (p, i))
                .Where(x => Heading(x.p) && x.p.InnerText.Trim() == section.Trim()).ToArray();
            if (starts.Length != 1) throw Review();
            var candidates = paragraphs.Skip(starts[0].i + 1).TakeWhile(p => !Heading(p))
                .Where(p => p.InnerText.StartsWith(category.Trim() + ":", StringComparison.Ordinal)).ToArray();
            if (candidates.Length != 1) throw Review();
            var target = candidates[0];
            if (target.ChildElements.Any(e => e is not ParagraphProperties and not Run) ||
                target.Elements<Run>().Any(r => r.ChildElements.Any(e => e is not RunProperties and not Text)))
                throw Review();
            try
            {
                var edit = DocxSkillListEditor.Prepare(target, skill);
                edit.Target.Text = edit.Replacement;
            }
            catch (DocxReviewRequiredException) { throw Review(); }
            xml.Save();
        }
        // Structural preservation is not proof of pagination; every result still needs rendering.
        return output.ToArray();
    }

    private static InvalidOperationException Review() => new("review_required: unsupported or unconfirmed insertion.");
}
