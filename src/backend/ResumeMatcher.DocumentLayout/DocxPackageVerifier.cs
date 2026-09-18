using System.IO.Compression;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;

namespace ResumeMatcher.DocumentLayout;

internal static class DocxPackageVerifier
{
    internal static int Verify(byte[] original, byte[] adapted, HashSet<int> allowedTexts, Dictionary<int, string> expectedParagraphs)
    {
        using var before = WordprocessingDocument.Open(new MemoryStream(original, writable: false), false);
        using var after = WordprocessingDocument.Open(new MemoryStream(adapted, writable: false), false);
        var beforeErrors = SchemaErrors(before);
        var afterErrors = SchemaErrors(after);
        if (afterErrors.Any(e => !beforeErrors.TryGetValue(e.Key, out var count) || e.Value > count))
            throw new DocxReviewRequiredException("new_schema_errors");
        var first = Parts(original);
        var second = Parts(adapted);
        if (!first.Keys.Order().SequenceEqual(second.Keys.Order())) throw new DocxReviewRequiredException("package_parts_changed");
        var documentPath = before.MainDocumentPart!.Uri.OriginalString.TrimStart('/');
        foreach (var part in first.Keys.Where(p => p != documentPath))
            if (!first[part].SequenceEqual(second[part])) throw new DocxReviewRequiredException("unrelated_part_changed");
        var originalXml = ReadXml(first[documentPath]);
        var adaptedXml = ReadXml(second[documentPath]);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var originalTexts = originalXml.Descendants(w + "t").ToArray();
        var adaptedTexts = adaptedXml.Descendants(w + "t").ToArray();
        if (originalTexts.Length != adaptedTexts.Length) throw new DocxReviewRequiredException("document_structure_changed");
        foreach (var index in allowedTexts)
        {
            originalTexts[index].Value = adaptedTexts[index].Value;
            // xml:space=preserve is the only permitted attribute change, on a changed text with edge whitespace.
            var value = adaptedTexts[index].Value;
            if (value.Length > 0 && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1])))
                originalTexts[index].SetAttributeValue(XNamespace.Xml + "space", "preserve");
        }
        if (!XNode.DeepEquals(originalXml, adaptedXml)) throw new DocxReviewRequiredException("unexpected_document_change");
        var paragraphs = after.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().ToArray();
        foreach (var expected in expectedParagraphs)
            if (paragraphs[expected.Key].InnerText != expected.Value) throw new DocxReviewRequiredException("result_text_mismatch");
        return beforeErrors.Values.Sum();
    }

    internal static bool EquivalentProperties(OpenXmlElement? left, OpenXmlElement? right)
    {
        if (left is null || left.ChildElements.Count == 0 && !left.HasAttributes)
            return right is null || right.ChildElements.Count == 0 && !right.HasAttributes;
        return right is not null && XNode.DeepEquals(Normalize(XElement.Parse(left.OuterXml)), Normalize(XElement.Parse(right.OuterXml)));
    }

    private static Dictionary<string, int> SchemaErrors(WordprocessingDocument document) =>
        new OpenXmlValidator { MaxNumberOfErrors = 0 }.Validate(document)
            .GroupBy(e => $"{e.Id}|{e.Part?.Uri}|{e.Path?.XPath}|{e.Description}")
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

    private static Dictionary<string, byte[]> Parts(byte[] data)
    {
        using var archive = new ZipArchive(new MemoryStream(data, writable: false), ZipArchiveMode.Read);
        return archive.Entries.ToDictionary(e => e.FullName, e =>
        {
            using var input = e.Open();
            using var memory = new MemoryStream();
            input.CopyTo(memory);
            return memory.ToArray();
        }, StringComparer.Ordinal);
    }

    private static XElement ReadXml(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return Normalize(XElement.Load(stream, LoadOptions.PreserveWhitespace));
    }

    private static XElement Normalize(XElement element) => new(element.Name,
        element.Attributes().Where(a => !a.IsNamespaceDeclaration).OrderBy(a => a.Name.ToString()).Select(a => new XAttribute(a)),
        element.Nodes().Where(n => n is not XText text || !string.IsNullOrWhiteSpace(text.Value) ||
            element.Name == XName.Get("t", "http://schemas.openxmlformats.org/wordprocessingml/2006/main"))
            .Select(n => n is XElement child ? Normalize(child) : n));
}
