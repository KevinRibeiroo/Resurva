using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ResumeMatcher.Domain;

namespace ResumeMatcher.Application;

public sealed class WeightedScoringEngine : IScoringEngine
{
    private readonly ScoringOptions _options;

    public WeightedScoringEngine(IOptions<ScoringOptions> options)
    {
        _options = options.Value;
        var total = _options.SkillsWeight + _options.ExperienceWeight + _options.SeniorityWeight
            + _options.RequirementsWeight + _options.EducationWeight;
        if (Math.Abs(total - 1) > 0.0001)
            throw new InvalidOperationException("Scoring weights must add up to 1.");
    }

    public ScoreBreakdownModel Calculate(ScoreComponentsModel components)
    {
        static double Normalize(double value)
        {
            return Math.Round(Math.Clamp(value, 0, 100), 2);
        }

        var skills = Normalize(components.Skills);
        var experience = Normalize(components.Experience);
        var seniority = Normalize(components.Seniority);
        var requirements = Normalize(components.Requirements);
        var education = Normalize(components.Education);
        var overall = skills * _options.SkillsWeight + experience * _options.ExperienceWeight
            + seniority * _options.SeniorityWeight + requirements * _options.RequirementsWeight
            + education * _options.EducationWeight;
        return new(Normalize(overall), skills, experience, seniority, requirements, education);
    }
}
