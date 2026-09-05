using ResumeMatcher.Application;

namespace ResumeMatcher.Tests;

public sealed class AnalysisInputHasherTests
{
    private const string ResumeText = "Desenvolvedor .NET com experiência em C#.";
    private const string JobDescription = "Vaga para desenvolvimento de APIs em C#.";

    [Fact]
    public void SameInputProducesSameHash()
    {
        var first = Generate();
        var second = Generate();

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void DifferentJobDescriptionProducesDifferentHash()
    {
        Assert.NotEqual(Generate(), Generate(jobDescription: "Vaga para desenvolvimento em Java."));
    }

    [Fact]
    public void DifferentResumeProducesDifferentHash()
    {
        Assert.NotEqual(Generate(), Generate(resumeText: "Desenvolvedor Java com experiência em Spring."));
    }

    [Fact]
    public void IrrelevantWhitespaceProducesSameHash()
    {
        var normalized = Generate();
        var withWhitespace = Generate(
            resumeText: "  Desenvolvedor   .NET\r\ncom experiência em C#.  ",
            jobDescription: "Vaga para\r\n desenvolvimento   de APIs em C#. ");

        Assert.Equal(normalized, withWhitespace);
    }

    [Fact]
    public void DifferentPromptVersionProducesDifferentHash()
    {
        Assert.NotEqual(Generate(), Generate(promptVersion: "v2"));
    }

    [Fact]
    public void DifferentModelProducesDifferentHash()
    {
        Assert.NotEqual(Generate(), Generate(model: "gemini-3.8-flash"));
    }

    [Fact]
    public void DifferentLlmConfigurationProducesDifferentHash()
    {
        Assert.NotEqual(Generate(), Generate(configurationFingerprint: "temperature:0.2"));
    }

    [Fact]
    public void DifferentAnalysisRulesVersionProducesDifferentHash()
    {
        Assert.NotEqual(Generate(), Generate(rulesVersion: "v2"));
    }

    private static string Generate(
        string resumeText = ResumeText,
        string jobDescription = JobDescription,
        string model = "gemini-3.5-flash",
        string configurationFingerprint = "temperature:0.1",
        string promptVersion = "v1",
        string rulesVersion = "v1")
    {
        return AnalysisInputHasher.Generate(
            resumeText,
            jobDescription,
            model,
            configurationFingerprint,
            promptVersion,
            rulesVersion);
    }
}
