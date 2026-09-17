using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocxLayoutProbe.Tests;

internal static class DocxTestDocument
{
    internal static byte[] Create(Action<Body, Styles>? configure = null)
    {
        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            var styles = main.AddNewPart<StyleDefinitionsPart>();
            styles.Styles = new Styles(
                new Style(new StyleName { Val = "Normal" }) { StyleId = "Normal", Type = StyleValues.Paragraph },
                new Style(new StyleName { Val = "SectionHeader" }, new StyleRunProperties(new Bold()))
                { StyleId = "SectionHeader", Type = StyleValues.Paragraph });
            var settings = main.AddNewPart<DocumentSettingsPart>();
            settings.FeedData(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(
                "<w:settings xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:zoom w:percent=\"100\"/></w:settings>")));
            var body = new Body(
                Text("Candidato Fictício"),
                Text("Desenvolvedor de software"),
                Text("pessoa@example.invalid"),
                Heading("RESUMO"),
                Text("API em C#"),
                Heading("HABILIDADES TÉCNICAS"),
                new Paragraph(new Run(new RunProperties(new Bold()), new Text("Bancos: ")),
                    new Run(new Text("SQL Server, MySQL."))),
                Heading("EXPERIÊNCIA"),
                Text("Empresa Fictícia — Desenvolvedor — 2022–2024"),
                Text("• API em C# com 2 serviços."),
                new SectionProperties(new PageSize { Width = 11906U, Height = 16838U }));
            main.Document = new Document(body);
            configure?.Invoke(body, styles.Styles);
            main.Document.Save();
            styles.Styles.Save();
        }
        return stream.ToArray();
    }

    internal static Paragraph Text(string text) => new(new Run(new Text(text)));
    internal static Paragraph Heading(string text) => new(
        new ParagraphProperties(new ParagraphStyleId { Val = "SectionHeader" }), new Run(new Text(text)));
    internal static DocxInspectionProfileModel Profile => new()
    {
        HeadingStyleIds = ["SectionHeader"],
        BlockRoles = new() { ["p:1"] = "professional_title" }
    };
}
