using System.IO.Compression;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocxLayoutProbe.Tests;

public class DocxAdaptationEngineTests
{
    [Fact]
    public void AppliesWholePlanPreservingOtherPartsOriginalAndHistory()
    {
        var source = DocxTestDocument.Create();
        var snapshot = source.ToArray();
        var plan = Plan(source, Edit("title", "p:1", "Desenvolvedor de software", 0, 24, "Desenvolvedor .NET"),
            Edit("summary", "p:4", "API em C#", 0, 3, "API REST"),
            Edit("experience", "p:9", "• API em C# com 2 serviços.", 2, 3, "API REST"),
            Edit("skill", "p:6", "Bancos: SQL Server, MySQL.", 0, 0, "PostgreSQL") with { Kind = "insert_skill" });
        var result = DocxAdaptationEngine.Apply(source, plan);
        Assert.Equal("visual_review_pending", result.ReviewStatus);
        Assert.Equal(0, result.ExistingSchemaErrors);
        Assert.Equal(new[] { "title", "summary", "experience", "skill" }, result.AppliedOperationIds);
        Assert.Equal(snapshot, source);
        Assert.Equal("API REST em C#", Paragraphs(result.Document)[4]);
        Assert.Equal("• API REST em C# com 2 serviços.", Paragraphs(result.Document)[9]);
        Assert.Equal("Bancos: SQL Server, MySQL, PostgreSQL.", Paragraphs(result.Document)[6]);
        Assert.Equal(Paragraphs(source)[8], Paragraphs(result.Document)[8]);
        var originalParts = Parts(source);
        var adaptedParts = Parts(result.Document);
        Assert.Equal(originalParts.Keys.Order(), adaptedParts.Keys.Order());
        foreach (var name in originalParts.Keys.Where(n => n != "word/document.xml"))
            Assert.Equal(originalParts[name], adaptedParts[name]);
    }

    [Fact]
    public void ReplacesEquivalentFragmentedRunsAndAppliesRangesInOriginalCoordinates()
    {
        var source = DocxTestDocument.Create((body, _) =>
        {
            var p = body.Elements<Paragraph>().ElementAt(4);
            p.RemoveAllChildren();
            p.Append(new Run(new Text("API ")), new Run(new Text("em C#")));
        });
        var result = DocxAdaptationEngine.Apply(source, Plan(source,
            Edit("first", "p:4", "API em C#", 0, 6, "Serviço REST em"),
            Edit("second", "p:4", "API em C#", 7, 2, ".NET")));
        Assert.Equal("Serviço REST em .NET", Paragraphs(result.Document)[4]);
    }

    [Theory]
    [InlineData("hash", "source_hash_mismatch")]
    [InlineData("approval", "approval_required")]
    [InlineData("evidence", "evidence_required")]
    [InlineData("expected", "expected_text_mismatch")]
    [InlineData("overlap", "overlapping_operations")]
    [InlineData("duplicate", "invalid_operation_id")]
    [InlineData("protected", "protected_target")]
    [InlineData("range", "invalid_range")]
    [InlineData("control", "invalid_replacement")]
    public void RejectsInvalidPlansWithoutTouchingSource(string mutation, string code)
    {
        var source = DocxTestDocument.Create();
        var before = source.ToArray();
        var op = Edit("edit", "p:4", "API em C#", 0, 3, "API REST");
        var plan = Plan(source, op);
        plan = mutation switch
        {
            "hash" => plan with { SourceSha256 = "stale" },
            "approval" => plan with { Operations = [op with { Approved = false }] },
            "evidence" => plan with { Operations = [op with { EvidenceIds = [] }] },
            "expected" => plan with { Operations = [op with { ExpectedText = "different" }] },
            "overlap" => plan with { Operations = [op, op with { Id = "edit2", Start = 2 }] },
            "duplicate" => plan with { Operations = [op, op] },
            "protected" => plan with { Operations = [op with { BlockId = "p:0", ExpectedText = "Candidato Fictício" }] },
            "range" => plan with { Operations = [op with { Length = int.MaxValue }] },
            "control" => plan with { Operations = [op with { NewText = "REST\nAPI" }] },
            _ => plan
        };
        Assert.Equal(code, Assert.Throws<DocxReviewRequiredException>(() => DocxAdaptationEngine.Apply(source, plan)).Code);
        Assert.Equal(before, source);
    }

    [Fact]
    public void RejectsMixedFormattingAndChangedExperienceNumbers()
    {
        var source = DocxTestDocument.Create((body, _) =>
        {
            var p = body.Elements<Paragraph>().ElementAt(4);
            p.RemoveAllChildren();
            p.Append(new Run(new RunProperties(new Bold()), new Text("API ")), new Run(new Text("em C#")));
        });
        Assert.Equal("mixed_formatting", Assert.Throws<DocxReviewRequiredException>(() => DocxAdaptationEngine.Apply(source,
            Plan(source, Edit("edit", "p:4", "API em C#", 0, 6, "API REST em")))).Code);
        Assert.Equal("historical_numbers_changed", Assert.Throws<DocxReviewRequiredException>(() => DocxAdaptationEngine.Apply(source,
            Plan(source, Edit("edit", "p:9", "• API em C# com 2 serviços.", 15, 1, "4")))).Code);
    }

    [Fact]
    public void RejectsDuplicateSkill()
    {
        var source = DocxTestDocument.Create();
        Assert.Equal("duplicate_skill", Assert.Throws<DocxReviewRequiredException>(() => DocxAdaptationEngine.Apply(source,
            Plan(source, Edit("skill", "p:6", "Bancos: SQL Server, MySQL.", 0, 0, "mysql") with { Kind = "insert_skill" }))).Code);
    }

    [Fact]
    public void RejectsNumericChangesCausedByPunctuationInsideNumber()
    {
        const string originalText = "• Reduzi o tempo em 2.5 horas.";
        var source = DocxTestDocument.Create((body, _) =>
        {
            var paragraph = body.Elements<Paragraph>().ElementAt(9);
            paragraph.RemoveAllChildren();
            paragraph.Append(new Run(new Text(originalText)));
        });
        Assert.Equal("historical_numbers_changed", Assert.Throws<DocxReviewRequiredException>(() => DocxAdaptationEngine.Apply(source,
            Plan(source, Edit("punctuation", "p:9", originalText, originalText.IndexOf('.'), 1, "-")))).Code);
    }

    internal static DocxEditOperationModel Edit(string id, string block, string expected, int start, int length, string replacement) => new()
    {
        Id = id, BlockId = block, ExpectedText = expected, Start = start, Length = length, NewText = replacement,
        Approved = true, EvidenceIds = ["synthetic-confirmation"]
    };
    internal static DocxAdaptationPlanModel Plan(byte[] source, params DocxEditOperationModel[] operations) => new()
    {
        SourceSha256 = DocxDocumentInspector.Inspect(source, DocxTestDocument.Profile).SourceSha256,
        Profile = DocxTestDocument.Profile, Operations = operations
    };
    internal static string[] Paragraphs(byte[] bytes)
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        return doc.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().Select(p => p.InnerText).ToArray();
    }
    private static Dictionary<string, byte[]> Parts(byte[] bytes)
    {
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        return archive.Entries.ToDictionary(e => e.FullName, e =>
        {
            using var part = e.Open();
            using var copy = new MemoryStream();
            part.CopyTo(copy);
            return copy.ToArray();
        });
    }
}
