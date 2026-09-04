using ResumeMatcher.Application;
using Xunit;

namespace ResumeMatcher.Tests;

public sealed class SeniorityScoringTests
{
    private readonly ScoringOptions _options = new()
    {
        PlenoRequiredJuniorScore = 60.0,
        SeniorRequiredJuniorScore = 30.0,
        SeniorRequiredPlenoScore = 70.0
    };

    [Theory]
    [InlineData(SeniorityLevel.Junior, SeniorityLevel.Junior, 100.0)]
    [InlineData(SeniorityLevel.Junior, SeniorityLevel.Pleno, 100.0)]
    [InlineData(SeniorityLevel.Junior, SeniorityLevel.Senior, 100.0)]
    [InlineData(SeniorityLevel.Junior, SeniorityLevel.Lead, 100.0)]
    public void Evaluate_JuniorJob_CandidateMeetsOrExceeds_Returns100(
        SeniorityLevel jobSeniority,
        SeniorityLevel candidateSeniority,
        double expectedScore)
    {
        var score = SeniorityEvaluator.Evaluate(jobSeniority, candidateSeniority, _options);
        Assert.Equal(expectedScore, score);
    }

    [Theory]
    [InlineData(SeniorityLevel.Pleno, SeniorityLevel.Junior, 60.0)]
    [InlineData(SeniorityLevel.Pleno, SeniorityLevel.Pleno, 100.0)]
    [InlineData(SeniorityLevel.Pleno, SeniorityLevel.Senior, 100.0)]
    [InlineData(SeniorityLevel.Pleno, SeniorityLevel.Lead, 100.0)]
    public void Evaluate_PlenoJob_EvaluatesCorrectMatrix(
        SeniorityLevel jobSeniority,
        SeniorityLevel candidateSeniority,
        double expectedScore)
    {
        var score = SeniorityEvaluator.Evaluate(jobSeniority, candidateSeniority, _options);
        Assert.Equal(expectedScore, score);
    }

    [Theory]
    [InlineData(SeniorityLevel.Senior, SeniorityLevel.Junior, 30.0)]
    [InlineData(SeniorityLevel.Senior, SeniorityLevel.Pleno, 70.0)]
    [InlineData(SeniorityLevel.Senior, SeniorityLevel.Senior, 100.0)]
    [InlineData(SeniorityLevel.Senior, SeniorityLevel.Lead, 100.0)]
    public void Evaluate_SeniorJob_EvaluatesCorrectMatrix(
        SeniorityLevel jobSeniority,
        SeniorityLevel candidateSeniority,
        double expectedScore)
    {
        var score = SeniorityEvaluator.Evaluate(jobSeniority, candidateSeniority, _options);
        Assert.Equal(expectedScore, score);
    }

    [Fact]
    public void Evaluate_NotSpecifiedJob_Returns100()
    {
        var score = SeniorityEvaluator.Evaluate(SeniorityLevel.NotSpecified, SeniorityLevel.Junior, _options);
        Assert.Equal(100.0, score);
    }

    [Theory]
    [InlineData(SeniorityLevel.Junior, SeniorityLevel.Pleno, true)]
    [InlineData(SeniorityLevel.Junior, SeniorityLevel.Senior, true)]
    [InlineData(SeniorityLevel.Pleno, SeniorityLevel.Senior, true)]
    [InlineData(SeniorityLevel.Senior, SeniorityLevel.Senior, false)]
    [InlineData(SeniorityLevel.Senior, SeniorityLevel.Pleno, false)]
    [InlineData(SeniorityLevel.Senior, SeniorityLevel.Junior, false)]
    [InlineData(SeniorityLevel.NotSpecified, SeniorityLevel.Senior, false)]
    public void IsOverqualified_IdentifiesCorrectly(
        SeniorityLevel required,
        SeniorityLevel candidate,
        bool expectedOverqualified)
    {
        var result = SeniorityEvaluator.IsOverqualified(required, candidate);
        Assert.Equal(expectedOverqualified, result);
    }

    [Theory]
    [InlineData("Desenvolvedor C# Sênior", SeniorityLevel.Senior)]
    [InlineData("Senior Backend Engineer", SeniorityLevel.Senior)]
    [InlineData("Engenheiro de Software Pleno", SeniorityLevel.Pleno)]
    [InlineData("Vaga para Desenvolvedor Júnior", SeniorityLevel.Junior)]
    [InlineData("Estagiário de Desenvolvimento", SeniorityLevel.Junior)]
    [InlineData("Tech Lead / Especialista .NET", SeniorityLevel.Lead)]
    [InlineData("Analista de Sistemas", SeniorityLevel.NotSpecified)]
    [InlineData(null, SeniorityLevel.NotSpecified)]
    public void Parse_ParsesExpectedSeniorityLevel(string? input, SeniorityLevel expectedLevel)
    {
        var result = SeniorityEvaluator.Parse(input);
        Assert.Equal(expectedLevel, result);
    }
}
