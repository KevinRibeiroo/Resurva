using System.IO.Compression;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocxLayoutProbe.Tests;

public class DocxPackageVerifierTests
{
    [Fact]
    public void ExistingUnchangedSchemaErrorsAreReportedNotHidden()
    {
        var original = DocxTestDocument.Create((body, _) => body.Elements<Paragraph>().First()
            .Elements<Run>().First().RunProperties = new RunProperties(new FontSize { Val = "invalid" }));
        var result = DocxAdaptationEngine.Apply(original, DocxAdaptationEngineTests.Plan(original,
            DocxAdaptationEngineTests.Edit("summary", "p:4", "API em C#", 0, 3, "API REST")));
        Assert.True(result.ExistingSchemaErrors > 0);
    }

    [Fact]
    public void NewSchemaErrorsAreRejected()
    {
        var original = DocxTestDocument.Create();
        var invalid = Change(original, document => document.MainDocumentPart!.Document!.Body!.Elements<Paragraph>()
            .First().Elements<Run>().First().RunProperties = new RunProperties(new FontSize { Val = "invalid" }));
        Assert.Equal("new_schema_errors", Assert.Throws<DocxReviewRequiredException>(() =>
            DocxPackageVerifier.Verify(original, invalid, [], [])).Code);
    }

    [Fact]
    public void ChangesToUnrelatedParagraphPropertiesAreRejected()
    {
        var original = DocxTestDocument.Create();
        var invalid = Change(original, document => document.MainDocumentPart!.Document!.Body!.Elements<Paragraph>()
            .First().ParagraphProperties = new ParagraphProperties(new Justification { Val = JustificationValues.Center }));
        Assert.Equal("unexpected_document_change", Assert.Throws<DocxReviewRequiredException>(() =>
            DocxPackageVerifier.Verify(original, invalid, [], [])).Code);
    }

    [Fact]
    public void SettingsBytesMustNotChangeEvenIfSemanticsEqual()
    {
        var original = DocxTestDocument.Create();
        using var memory = new MemoryStream();
        memory.Write(original);
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Update, leaveOpen: true))
        {
            var settings = zip.GetEntry("word/settings.xml")!;
            string xml;
            using (var reader = new StreamReader(settings.Open())) xml = reader.ReadToEnd();
            settings.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("word/settings.xml").Open());
            writer.Write(xml + "\n");
        }
        Assert.Equal("unrelated_part_changed", Assert.Throws<DocxReviewRequiredException>(() =>
            DocxPackageVerifier.Verify(original, memory.ToArray(), [], [])).Code);
    }

    [Fact]
    public void TextOutsideAllowedNodesIsRejected()
    {
        var original = DocxTestDocument.Create();
        var invalid = Change(original, document => document.MainDocumentPart!.Document!.Descendants<Text>().First().Text = "Changed");
        Assert.Equal("unexpected_document_change", Assert.Throws<DocxReviewRequiredException>(() =>
            DocxPackageVerifier.Verify(original, invalid, [], [])).Code);
    }

    [Fact]
    public void WhitespaceOnlyTextOutsideApprovedNodesIsNotIgnored()
    {
        var original = DocxTestDocument.Create((body, _) => body.Elements<Paragraph>().First().Append(new Run(new Text(" "))));
        var invalid = Change(original, document => document.MainDocumentPart!.Document!.Body!.Elements<Paragraph>()
            .First().Descendants<Text>().Last().Text = "  ");
        Assert.Equal("unexpected_document_change", Assert.Throws<DocxReviewRequiredException>(() =>
            DocxPackageVerifier.Verify(original, invalid, [], [])).Code);
    }

    private static byte[] Change(byte[] original, Action<WordprocessingDocument> change)
    {
        using var memory = new MemoryStream();
        memory.Write(original);
        memory.Position = 0;
        using (var doc = WordprocessingDocument.Open(memory, true, new OpenSettings { AutoSave = false }))
        {
            change(doc);
            doc.MainDocumentPart!.Document!.Save();
        }
        return memory.ToArray();
    }
}
