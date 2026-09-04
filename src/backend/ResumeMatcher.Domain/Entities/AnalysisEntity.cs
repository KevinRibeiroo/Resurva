namespace ResumeMatcher.Domain;

public sealed class AnalysisEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ResumeId { get; init; }
    public string? AnalysisInputHash { get; init; }
    public required string JobDescription { get; init; }
    public double OverallScore { get; init; }
    public double SkillsScore { get; init; }
    public double ExperienceScore { get; init; }
    public double SeniorityScore { get; init; }
    public double RequirementsScore { get; init; }
    public double EducationScore { get; init; }
    public required string ResultJson { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
