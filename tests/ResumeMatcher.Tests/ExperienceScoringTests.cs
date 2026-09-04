using ResumeMatcher.Application;
using Xunit;

namespace ResumeMatcher.Tests;

public sealed class ExperienceScoringTests
{
    [Fact]
    public void Evaluate_CandidateYearsEqualRequired_Returns100()
    {
        var score = ExperienceEvaluator.Evaluate(2.0, 2.0);
        Assert.Equal(100.0, score);
    }

    [Fact]
    public void Evaluate_CandidateYearsExceedsRequired_Returns100WithoutPenalty()
    {
        var score = ExperienceEvaluator.Evaluate(2.0, 5.0);
        Assert.Equal(100.0, score);
    }

    [Fact]
    public void Evaluate_CandidateYearsLowerThanRequired_ReturnsProportionalScore()
    {
        var score = ExperienceEvaluator.Evaluate(5.0, 3.0);
        Assert.Equal(60.0, score);
    }

    [Theory]
    [InlineData(null, 3.0, 100.0)]
    [InlineData(0.0, 3.0, 100.0)]
    [InlineData(-1.0, 3.0, 100.0)]
    public void Evaluate_NoRequiredExperience_Returns100(double? required, double? candidate, double expected)
    {
        var score = ExperienceEvaluator.Evaluate(required, candidate);
        Assert.Equal(expected, score);
    }

    [Fact]
    public void Evaluate_RequiredSpecifiedCandidateNull_Returns0()
    {
        var score = ExperienceEvaluator.Evaluate(3.0, null);
        Assert.Equal(0.0, score);
    }
}
