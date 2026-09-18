using System.Text.Json;

namespace DocxLayoutProbe.Tests;

public class DocxLocalCommandsTests
{
    [Fact]
    public async Task InspectWritesPrivateMapAndEmptyPlanWithoutLoggingText()
    {
        var root = Directory.CreateTempSubdirectory("docx-local-tests-");
        try
        {
            var input = Path.Combine(root.FullName, "input.docx");
            var profile = Path.Combine(root.FullName, "profile.json");
            var destination = Path.Combine(root.FullName, "inspection");
            await File.WriteAllBytesAsync(input, DocxTestDocument.Create());
            await File.WriteAllTextAsync(profile, JsonSerializer.Serialize(DocxTestDocument.Profile));
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            Assert.Equal(0, await DocxLocalCommands.RunAsync(["inspect", input, profile, destination], stdout, stderr));
            Assert.Equal(new[] { "inspection.json", "plan.json" }, Directory.GetFiles(destination).Select(Path.GetFileName).Order());
            var plan = JsonSerializer.Deserialize<DocxAdaptationPlanModel>(await File.ReadAllTextAsync(Path.Combine(destination, "plan.json")),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.Empty(plan!.Operations);
            Assert.Equal(64, plan.SourceSha256.Length);
            Assert.DoesNotContain("Candidato", stdout.ToString());
            Assert.DoesNotContain("example.invalid", stdout.ToString());
            Assert.Equal("", stderr.ToString());
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task ApplyPublishesBothFilesAndNeverOverwritesOrLeaksText()
    {
        var root = Directory.CreateTempSubdirectory("docx-local-tests-");
        try
        {
            var source = DocxTestDocument.Create();
            var input = Path.Combine(root.FullName, "input.docx");
            var planPath = Path.Combine(root.FullName, "plan.json");
            var destination = Path.Combine(root.FullName, "output");
            await File.WriteAllBytesAsync(input, source);
            var plan = DocxAdaptationEngineTests.Plan(source,
                DocxAdaptationEngineTests.Edit("summary", "p:4", "API em C#", 0, 3, "API REST"));
            await File.WriteAllTextAsync(planPath, JsonSerializer.Serialize(plan));
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            Assert.Equal(0, await DocxLocalCommands.RunAsync(["apply", input, planPath, destination], stdout, stderr));
            var generated = await File.ReadAllBytesAsync(Path.Combine(destination, "adaptado.docx"));
            Assert.Equal("API REST em C#", DocxAdaptationEngineTests.Paragraphs(generated)[4]);
            var report = await File.ReadAllTextAsync(Path.Combine(destination, "report.json"));
            Assert.Contains("visual_review_pending", report);
            Assert.DoesNotContain("API em C#", report);
            Assert.DoesNotContain("Candidato", report);
            Assert.Equal(2, await DocxLocalCommands.RunAsync(["apply", input, planPath, destination], stdout, stderr));
            Assert.Equal(generated, await File.ReadAllBytesAsync(Path.Combine(destination, "adaptado.docx")));
            Assert.Equal(source, await File.ReadAllBytesAsync(input));
            Assert.DoesNotContain("Candidato", stdout.ToString() + stderr);
            Assert.Empty(Directory.GetDirectories(root.FullName, ".docx-stage-*"));
        }
        finally { root.Delete(true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidOrUnapprovedPlanProducesNoPartialDirectory(bool malformed)
    {
        var root = Directory.CreateTempSubdirectory("docx-local-tests-");
        try
        {
            var source = DocxTestDocument.Create();
            var input = Path.Combine(root.FullName, "input.docx");
            var planPath = Path.Combine(root.FullName, "plan.json");
            var destination = Path.Combine(root.FullName, "output");
            await File.WriteAllBytesAsync(input, source);
            var plan = DocxAdaptationEngineTests.Plan(source,
                DocxAdaptationEngineTests.Edit("summary", "p:4", "API em C#", 0, 3, "API REST") with { Approved = false });
            await File.WriteAllTextAsync(planPath, malformed ? "{private-document-text" : JsonSerializer.Serialize(plan));
            var stderr = new StringWriter();
            Assert.Equal(2, await DocxLocalCommands.RunAsync(["apply", input, planPath, destination], new StringWriter(), stderr));
            Assert.False(Directory.Exists(destination));
            Assert.Empty(Directory.GetDirectories(root.FullName, ".docx-stage-*"));
            Assert.DoesNotContain("private-document-text", stderr.ToString());
            if (!malformed) Assert.Contains("summary", stderr.ToString());
        }
        finally { root.Delete(true); }
    }
}
