using Microsoft.Extensions.Options;
using ResumeMatcher.Application;
using Xunit;

namespace ResumeMatcher.Tests;

public sealed class ExpectedScenarioScoringTest
{
    [Fact]
    public void Calculate_ScenarioWith80SkillsAnd100Others_Yields92OverallScore()
    {
        // Scenario from user specification:
        // Skills: 80% (weight 0.40 = 32.0)
        // Experience: 100% (weight 0.30 = 30.0)
        // Seniority: 100% (weight 0.15 = 15.0)
        // Requirements: 100% (weight 0.10 = 10.0)
        // Education: 100% (weight 0.05 = 5.0)
        // Expected overall: 32 + 30 + 15 + 10 + 5 = 92.0
        var options = Options.Create(new ScoringOptions
        {
            SkillsWeight = 0.40,
            ExperienceWeight = 0.30,
            SeniorityWeight = 0.15,
            RequirementsWeight = 0.10,
            EducationWeight = 0.05
        });

        var engine = new WeightedScoringEngine(options);
        var components = new ScoreComponentsModel(
            Skills: 80.0,
            Experience: 100.0,
            Seniority: 100.0,
            Requirements: 100.0,
            Education: 100.0);

        var result = engine.Calculate(components);

        Assert.Equal(92.0, result.Overall);
        Assert.Equal(80.0, result.Skills);
        Assert.Equal(100.0, result.Experience);
        Assert.Equal(100.0, result.Seniority);
        Assert.Equal(100.0, result.Requirements);
        Assert.Equal(100.0, result.Education);
    }
}
