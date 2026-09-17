using DocumentFormat.OpenXml.Wordprocessing;

namespace DocxLayoutProbe.Tests;

public class DocxDocumentInspectorTests
{
    [Fact]
    public void ExplicitCustomHeadingMapsSectionsAndPreservesInput()
    {
        var source = DocxTestDocument.Create();
        var snapshot = source.ToArray();
        var result = DocxDocumentInspector.Inspect(source, DocxTestDocument.Profile);
        Assert.Equal("summary", result.Blocks[4].Kind);
        Assert.Equal("skills", result.Blocks[6].Kind);
        Assert.Equal("experience", result.Blocks[9].Kind);
        Assert.Equal("professional_title", result.Blocks[1].Kind);
        Assert.False(result.Blocks[0].Editable);
        Assert.False(result.Blocks[2].Editable);
        Assert.False(result.Blocks[8].Editable);
        Assert.Equal(snapshot, source);
        Assert.Equal(64, result.SourceSha256.Length);
    }

    [Fact]
    public void OutlineCanBeInheritedAndDirectOutlineEndsSection()
    {
        var source = DocxTestDocument.Create((body, styles) =>
        {
            styles.Append(new Style(new StyleParagraphProperties(new OutlineLevel { Val = 0 }))
                { StyleId = "BaseHeading", Type = StyleValues.Paragraph });
            styles.Elements<Style>().Single(s => s.StyleId == "SectionHeader").BasedOn = new BasedOn { Val = "BaseHeading" };
            body.Elements<Paragraph>().ElementAt(7).ParagraphProperties = new ParagraphProperties(new OutlineLevel { Val = 0 });
        });
        var blocks = DocxDocumentInspector.Inspect(source).Blocks;
        Assert.Equal("summary", blocks[4].Kind);
        Assert.Equal("section_heading", blocks[7].Kind);
        Assert.Equal("EXPERIÊNCIA", blocks[9].Section);
    }

    [Fact]
    public void UnmappedVisualStyleDoesNotGuessSections()
    {
        var blocks = DocxDocumentInspector.Inspect(DocxTestDocument.Create()).Blocks;
        Assert.All(blocks, b => Assert.False(b.Editable));
    }

    [Theory]
    [InlineData("missing_style", "NotDefined")]
    [InlineData("style_cycle", "SectionHeader")]
    public void InvalidStyleInheritanceRequiresReview(string code, string parent)
    {
        var bytes = DocxTestDocument.Create((_, styles) =>
            styles.Elements<Style>().Single(s => s.StyleId == "SectionHeader").BasedOn = new BasedOn { Val = parent });
        Assert.Equal(code, Assert.Throws<DocxReviewRequiredException>(() =>
            DocxDocumentInspector.Inspect(bytes, DocxTestDocument.Profile)).Code);
    }

    [Fact]
    public void ExplicitRolesCannotReleaseIdentityOrHistoricalHeading()
    {
        foreach (var target in new[] { "p:0", "p:2", "p:3", "p:8" })
        {
            var profile = DocxTestDocument.Profile with { BlockRoles = new() { [target] = "professional_title" } };
            Assert.Throws<DocxReviewRequiredException>(() => DocxDocumentInspector.Inspect(DocxTestDocument.Create(), profile));
        }
    }

    [Fact]
    public void MixedContentIsIdentifiedButNotEditable()
    {
        var source = DocxTestDocument.Create((body, _) => body.Elements<Paragraph>().ElementAt(4)
            .Append(new Hyperlink(new Run(new Text("website")))));
        Assert.False(DocxDocumentInspector.Inspect(source, DocxTestDocument.Profile).Blocks[4].Editable);
    }

    [Fact]
    public void TablesRequireReview()
    {
        var source = DocxTestDocument.Create((body, _) => body.PrependChild(new Table()));
        Assert.Equal("unsupported_body", Assert.Throws<DocxReviewRequiredException>(() =>
            DocxDocumentInspector.Inspect(source)).Code);
    }

    [Fact]
    public void ContentControlsRequireReviewEvenOutsideEditedBlock()
    {
        var source = DocxTestDocument.Create((body, _) => body.Elements<Paragraph>().ElementAt(2)
            .Append(new SdtRun(new SdtContentRun(new Run(new Text("controlled"))))));
        Assert.Equal("active_or_controlled_content", Assert.Throws<DocxReviewRequiredException>(() =>
            DocxDocumentInspector.Inspect(source, DocxTestDocument.Profile)).Code);
    }

    [Fact]
    public void FormattingRevisionsAreNotEditable()
    {
        var bytes = DocxTestDocument.Create((body, _) => body.Elements<Paragraph>().ElementAt(4)
            .Elements<Run>().First().RunProperties = new RunProperties(new RunPropertiesChange { Id = "1", Author = "Synthetic" }));
        Assert.False(DocxDocumentInspector.Inspect(bytes, DocxTestDocument.Profile).Blocks[4].Editable);
    }

    [Fact]
    public void DisabledNumberingDoesNotReleaseEmploymentHeading()
    {
        var bytes = DocxTestDocument.Create((body, _) =>
        {
            var paragraph = body.Elements<Paragraph>().ElementAt(8);
            paragraph.RemoveAllChildren();
            paragraph.Append(new ParagraphProperties(new NumberingProperties(new NumberingId { Val = 0 })),
                new Run(new Text("Empresa Fictícia — Desenvolvedor")));
        });
        Assert.False(DocxDocumentInspector.Inspect(bytes, DocxTestDocument.Profile).Blocks[8].Editable);
    }
}
