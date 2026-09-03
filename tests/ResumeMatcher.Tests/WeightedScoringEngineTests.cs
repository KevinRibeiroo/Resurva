using Microsoft.Extensions.Options;
using ResumeMatcher.Application;

namespace ResumeMatcher.Tests;

public sealed class WeightedScoringEngineTests
{
    private static WeightedScoringEngine Create(ScoringOptions? options = null)
    {
        return new(Options.Create(options ?? new()));
    }

    [Fact]
    public void Full_match_returns_100()
    {
        var result = Create().Calculate(new(100, 100, 100, 100, 100));
        Assert.Equal(100, result.Overall);
    }

    [Fact]
    public void No_match_returns_zero()
    {
        var result = Create().Calculate(new(0, 0, 0, 0, 0));
        Assert.Equal(0, result.Overall);
    }

    [Fact]
    public void Partial_match_uses_configured_weights()
    {
        var result = Create().Calculate(new(100, 50, 0, 100, 0));
        Assert.Equal(65, result.Overall);
    }

    [Fact]
    public void Custom_weights_are_used()
    {
        var options = new ScoringOptions { SkillsWeight = 1, ExperienceWeight = 0, SeniorityWeight = 0, RequirementsWeight = 0, EducationWeight = 0 };
        var result = Create(options).Calculate(new(42, 100, 100, 100, 100));
        Assert.Equal(42, result.Overall);
    }

    [Theory]
    [InlineData(-50, 0)]
    [InlineData(180, 100)]
    public void Result_is_clamped(double component, double expected)
    {
        var result = Create().Calculate(new(component, component, component, component, component));
        Assert.Equal(expected, result.Overall);
    }
}
