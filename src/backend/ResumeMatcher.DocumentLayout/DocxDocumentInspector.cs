using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ResumeMatcher.DocumentLayout;

public static class DocxDocumentInspector
{
    public static DocxInspectionModel Inspect(byte[] source, DocxInspectionProfileModel? profile = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        profile ??= new();
        if (profile.HeadingStyleIds is null || profile.BlockRoles is null ||
            profile.HeadingStyleIds.Any(string.IsNullOrWhiteSpace)) throw Review("invalid_profile");
        try
        {
            CheckPackage(source);
            using var stream = new MemoryStream(source, writable: false);
            using var package = WordprocessingDocument.Open(stream, false);
            var main = package.MainDocumentPart ?? throw Review("missing_document");
            var body = main.Document?.Body ?? throw Review("missing_body");
            CheckRelatedParts(package);
            if (package.DocumentType != WordprocessingDocumentType.Document) throw Review("unsupported_document_type");
            if (main.DocumentSettingsPart?.Settings is { } settings &&
                (settings.Descendants<DocumentProtection>().Any() || settings.Descendants<WriteProtection>().Any() ||
                 settings.Descendants<AttachedTemplate>().Any())) throw Review("protected_document");
            if (body.ChildElements.Any(e => e is not Paragraph and not SectionProperties)) throw Review("unsupported_body");
            if (body.Descendants<Columns>().Any(c => (c.ColumnCount?.Value ?? 1) != 1)) throw Review("unsupported_columns");
            var styles = (main.StyleDefinitionsPart?.Styles?.Elements<Style>() ?? [])
                .Where(s => s.StyleId?.Value is not null).ToArray();
            if (styles.GroupBy(s => s.StyleId!.Value!).Any(g => g.Count() != 1)) throw Review("duplicate_style");
            var styleMap = styles.ToDictionary(s => s.StyleId!.Value!, StringComparer.Ordinal);
            var paragraphs = body.Elements<Paragraph>().ToArray();
            var firstContent = Array.FindIndex(paragraphs, p => !string.IsNullOrWhiteSpace(p.InnerText));
            var blocks = new List<DocxBlockModel>();
            string? section = null;
            for (var index = 0; index < paragraphs.Length; index++)
            {
                var paragraph = paragraphs[index];
                var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
                var chain = ResolveStyles(styleId, styleMap);
                // Traverse the complete chain even when direct formatting or the profile declares a heading.
                var outline = paragraph.ParagraphProperties?.OutlineLevel?.Val?.Value ??
                    chain.Select(s => s.StyleParagraphProperties?.OutlineLevel?.Val?.Value).FirstOrDefault(v => v.HasValue);
                var heading = outline is >= 0 and <= 8 ||
                    chain.Any(s => s.StyleId == "Heading1" || profile.HeadingStyleIds.Contains(s.StyleId!.Value!, StringComparer.Ordinal));
                var text = paragraph.InnerText;
                if (heading) section = text.Trim();
                var id = $"p:{index}";
                var protectedContent = index == firstContent || IsIdentityOrDate(text);
                var kind = heading ? "section_heading" : section is null ? "protected" : "other";
                var simple = IsSimple(paragraph) && !string.IsNullOrWhiteSpace(text);
                if (profile.DetectProfessionalTitle && !heading && !protectedContent && simple && section is null &&
                    IsProfessionalTitle(text)) kind = "professional_title";
                if (!heading && !protectedContent && section is not null)
                {
                    kind = SectionKind(section);
                    var numberingId = paragraph.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value ??
                        chain.Select(s => s.StyleParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value)
                            .FirstOrDefault(v => v.HasValue);
                    if (kind == "experience" && !(numberingId > 0) &&
                        !text.TrimStart().StartsWith('•')) kind = "protected";
                }
                if (protectedContent && !heading) kind = "protected";
                if (profile.BlockRoles.TryGetValue(id, out var role))
                {
                    if (role == "protected") kind = role;
                    else if (protectedContent || heading || !simple ||
                        !(role == kind && role is "summary" or "experience" or "skills" ||
                          role == "professional_title" && section is null)) throw Review("protected_role_override");
                    else kind = role;
                }
                blocks.Add(new(id, index, text, styleId, section, kind,
                    simple && kind is "professional_title" or "summary" or "experience" or "skills"));
            }
            if (profile.BlockRoles.Keys.Any(id => !blocks.Any(b => b.Id == id))) throw Review("unknown_profile_block");
            return new(Convert.ToHexString(SHA256.HashData(source)), blocks);
        }
        catch (Exception error) when (error is InvalidDataException or OpenXmlPackageException or System.Xml.XmlException or ArgumentException)
        {
            throw Review("invalid_document");
        }
    }

    internal static bool IsSimple(Paragraph paragraph) =>
        paragraph.ChildElements.All(e => e is ParagraphProperties or Run) &&
        paragraph.Elements<Run>().All(r => r.ChildElements.All(e => e is RunProperties or Text)) &&
        !paragraph.Descendants().Any(e => e.LocalName is "rPrChange" or "pPrChange" or "sectPrChange" or "numberingChange");

    private static List<Style> ResolveStyles(string? id, Dictionary<string, Style> styles)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<Style>();
        while (id is not null)
        {
            if (!visited.Add(id)) throw Review("style_cycle");
            if (!styles.TryGetValue(id, out var style)) throw Review("missing_style");
            result.Add(style);
            id = style.BasedOn?.Val?.Value;
        }
        return result;
    }

    private static string SectionKind(string value)
    {
        var normalized = string.Concat(value.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToUpperInvariant();
        return normalized.Trim().TrimEnd(':') switch
        {
            "RESUMO" or "RESUMO PROFISSIONAL" or "SUMMARY" or "PROFESSIONAL SUMMARY" or "PERFIL PROFISSIONAL" => "summary",
            "HABILIDADES" or "HABILIDADES TECNICAS" or "COMPETENCIAS" or "COMPETENCIAS TECNICAS" or "SKILLS" or "TECHNICAL SKILLS" => "skills",
            "EXPERIENCIA" or "EXPERIENCIA PROFISSIONAL" or "EXPERIENCIAS PROFISSIONAIS" or "EXPERIENCE" or "WORK EXPERIENCE" => "experience",
            _ => "other"
        };
    }

    private static bool IsIdentityOrDate(string text) =>
        text.Contains('@') || Regex.IsMatch(text, @"(?i)(https?://|www\.|linkedin\.com|github\.com)") ||
        Regex.IsMatch(text, @"\+?\d[\d ()-]{7,}\d") ||
        Regex.IsMatch(text, @"\b(?:19|20)\d{2}\s*[-–—/]\s*(?:(?:19|20)\d{2}|[Pp]resente|[Aa]tual)");

    // Opt-in and limited to the preamble. A role label never releases identity,
    // contact details, dated employment headers, section headings or complex runs.
    private static bool IsProfessionalTitle(string text) => text.Length <= 240 &&
        !Regex.IsMatch(text, @"\b(?:19|20)\d{2}\b") &&
        Regex.IsMatch(text.Trim(),
            @"\A(?:(?:senior|sênior|junior|júnior|lead|principal|staff)\s+)*(?:software\s+(?:engineer|developer)|(?:backend|frontend|full[ -]?stack)\s+(?:engineer|developer)|engenheir[oa]\s+de\s+software|desenvolvedor(?:a)?)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static void CheckPackage(byte[] source)
    {
        using var archive = new ZipArchive(new MemoryStream(source, writable: false), ZipArchiveMode.Read);
        var entries = archive.Entries.Select(e => e.FullName).ToArray();
        if (entries.Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Length) throw Review("duplicate_package_part");
        if (entries.Any(name => name.Contains("vba", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("/activeX/", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("/embeddings/", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("_xmlsignatures/", StringComparison.OrdinalIgnoreCase))) throw Review("active_or_signed_document");
    }

    private static void CheckRelatedParts(WordprocessingDocument document)
    {
        var remaining = new Stack<OpenXmlPart>(document.Parts.Select(p => p.OpenXmlPart));
        var seen = new HashSet<Uri>();
        while (remaining.TryPop(out var part))
        {
            if (!seen.Add(part.Uri)) continue;
            if (new[] { "vba", "macroEnabled", "activeX", "oleObject", "digital-signature" }
                .Any(fragment => part.ContentType.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
                throw Review("active_or_signed_document");
            if (part.ExternalRelationships.Any(r => !r.RelationshipType.EndsWith("/hyperlink", StringComparison.Ordinal)))
                throw Review("unsupported_external_relationship");
            if (part.RootElement?.Descendants().Any(e => e.LocalName is "sdt" or "object" or "control" or "OLEObject" or "altChunk") == true)
                throw Review("active_or_controlled_content");
            foreach (var child in part.Parts) remaining.Push(child.OpenXmlPart);
        }
    }

    private static DocxReviewRequiredException Review(string code) => new(code);
}
