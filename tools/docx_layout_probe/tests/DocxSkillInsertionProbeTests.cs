using System.IO.Compression;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocxLayoutProbe.Tests;

public class DocxSkillInsertionProbeTests
{
    [Fact]
    public void InsertsInExistingRunWithoutRebuildingOtherContentOrParts()
    {
        var original = Fixture();
        var snapshot = original.ToArray();
        var result = DocxSkillInsertionProbe.Insert(original, "HABILIDADES", "Bancos de Dados", "PostgreSQL", true);
        Assert.Equal(snapshot, original);
        var before = Parts(original);
        var after = Parts(result);
        Assert.Equal(before.Keys.Order(), after.Keys.Order());
        foreach (var name in before.Keys.Where(n => n != "word/document.xml"))
            Assert.Equal(before[name], after[name]);
        var expected = XDocument.Parse(System.Text.Encoding.UTF8.GetString(before["word/document.xml"]));
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        expected.Descendants(w + "t").Single(t => t.Value == "MySQL.").Value = "MySQL, PostgreSQL.";
        var actual = XDocument.Parse(System.Text.Encoding.UTF8.GetString(after["word/document.xml"]));
        // The SDK may move namespace declarations. Expanded element/attribute names remain identical.
        foreach (var xml in new[] { expected, actual })
            xml.Descendants().Attributes().Where(a => a.IsNamespaceDeclaration).Remove();
        Assert.True(XNode.DeepEquals(expected, actual), "Only the existing final text node may change semantically.");
        using var stream = new MemoryStream(result);
        using var document = WordprocessingDocument.Open(stream, false);
        Assert.Empty(new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(document));
    }

    [Fact]
    public void RequiresExplicitConfirmation() => Assert.Throws<InvalidOperationException>(() =>
        DocxSkillInsertionProbe.Insert(Fixture(), "HABILIDADES", "Bancos de Dados", "PostgreSQL", false));

    [Fact]
    public void RejectsExistingSkillAcrossRuns() => Assert.Throws<InvalidOperationException>(() =>
        DocxSkillInsertionProbe.Insert(Fixture(), "HABILIDADES", "Bancos de Dados", "mysql", true));

    [Fact]
    public void RejectsAmbiguousCategory() => Assert.Throws<InvalidOperationException>(() =>
        DocxSkillInsertionProbe.Insert(Fixture(duplicate: true), "HABILIDADES", "Bancos de Dados", "PostgreSQL", true));

    [Fact]
    public void DoesNotSearchPastNextSection() => Assert.Throws<InvalidOperationException>(() =>
        DocxSkillInsertionProbe.Insert(Fixture(), "RESUMO", "Bancos de Dados", "PostgreSQL", true));

    [Fact]
    public void RequiresRecognizedSectionStructure() => Assert.Throws<InvalidOperationException>(() =>
        DocxSkillInsertionProbe.Insert(Fixture(customHeading: true), "HABILIDADES", "Bancos de Dados", "PostgreSQL", true));

    [Fact]
    public void RejectsTrackedContentInsteadOfFlatteningIt() => Assert.Throws<InvalidOperationException>(() =>
        DocxSkillInsertionProbe.Insert(Fixture(tracked: true), "HABILIDADES", "Bancos de Dados", "PostgreSQL", true));

    [Theory]
    [InlineData("")]
    [InlineData("SQL\nOutra seção")]
    [InlineData("SQL, Docker")]
    [InlineData("MySQL. ")]
    [InlineData("SQL\n")]
    public void RejectsNonAtomicInput(string skill) => Assert.Throws<ArgumentException>(() =>
        DocxSkillInsertionProbe.Insert(Fixture(), "HABILIDADES", "Bancos de Dados", skill, true));

    [Fact]
    public void RejectsTablesInsteadOfGuessingLayout() => Assert.Throws<InvalidOperationException>(() =>
        DocxSkillInsertionProbe.Insert(Fixture(table: true), "HABILIDADES", "Bancos de Dados", "PostgreSQL", true));

    [Fact]
    public void PreservesSemicolonConventionAndAcceptsUnicode()
    {
        var result = DocxSkillInsertionProbe.Insert(Fixture(semicolon: true), "HABILIDADES", "Bancos de Dados", "Análise de dados", true);
        using var document = WordprocessingDocument.Open(new MemoryStream(result), false);
        Assert.Contains("Bancos de Dados: SQL Server; MySQL; Análise de dados.", document.MainDocumentPart!.Document!.InnerText);
    }

    private static byte[] Fixture(bool duplicate = false, bool table = false, bool semicolon = false, bool customHeading = false, bool tracked = false)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            var settings = main.AddNewPart<DocumentSettingsPart>();
            using (var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(
                "<w:settings xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:zoom w:percent=\"100\" /></w:settings>")))
                settings.FeedData(input);
            var styles = main.AddNewPart<StyleDefinitionsPart>();
            styles.Styles = new Styles(new Style(new StyleName { Val = "Heading 1" })
                { Type = StyleValues.Paragraph, StyleId = "Heading1" });
            var header = main.AddNewPart<HeaderPart>();
            header.Header = new Header(new Paragraph(new Run(new Text("EXEMPLO SINTETICO"))));
            Paragraph Heading(string text) => new(new ParagraphProperties(new ParagraphStyleId { Val = customHeading ? "CustomHeading" : "Heading1" }), new Run(new Text(text)));
            Paragraph Category() => new(new ParagraphProperties(new SpacingBetweenLines { After = "60" }),
                new Run(new RunProperties(new Bold(), new Color { Val = "123456" }), new Text("Bancos de Dados: ") { Space = SpaceProcessingModeValues.Preserve }),
                new Run(new RunProperties(new RunFonts { Ascii = "Calibri" }, new FontSize { Val = "21" }), new Text(semicolon ? "SQL Server; " : "SQL Server, ") { Space = SpaceProcessingModeValues.Preserve }),
                new Run(new RunProperties(new RunFonts { Ascii = "Calibri" }, new FontSize { Val = "21" }), new Text("MySQL.")));
            var category = Category();
            if (tracked) category.Append(new InsertedRun(new Run(new Text(" revisão"))) { Id = "1", Author = "Synthetic" });
            var body = new Body(Heading("RESUMO"), new Paragraph(new Run(new Text("Exemplo."))), Heading("HABILIDADES"), category);
            if (duplicate) body.Append(Category());
            body.Append(Heading("EXPERIENCIA"), new Paragraph(new Run(new Text("Texto demonstrativo."))));
            if (table) body.Append(new Table(new TableRow(new TableCell(new Paragraph(new Run(new Text("Tabela")))))));
            body.Append(new SectionProperties(new HeaderReference { Type = HeaderFooterValues.Default, Id = main.GetIdOfPart(header) },
                new PageSize { Width = 11906, Height = 16838 }, new PageMargin { Top = 720, Right = 720, Bottom = 720, Left = 720, Header = 360, Footer = 360, Gutter = 0 }));
            main.Document = new Document(body);
        }
        return stream.ToArray();
    }

    private static Dictionary<string, byte[]> Parts(byte[] bytes)
    {
        using var archive = new ZipArchive(new MemoryStream(bytes));
        return archive.Entries.ToDictionary(e => e.FullName, e =>
        {
            using var input = e.Open();
            using var output = new MemoryStream();
            input.CopyTo(output);
            return output.ToArray();
        });
    }
}
