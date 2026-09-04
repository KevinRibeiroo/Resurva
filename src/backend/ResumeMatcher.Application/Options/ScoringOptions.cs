namespace ResumeMatcher.Application;

public sealed class ScoringOptions
{
    public const string SectionName = "Scoring";
    public double SkillsWeight { get; set; } = 0.40;
    public double ExperienceWeight { get; set; } = 0.30;
    public double SeniorityWeight { get; set; } = 0.15;
    public double RequirementsWeight { get; set; } = 0.10;
    public double EducationWeight { get; set; } = 0.05;
}
