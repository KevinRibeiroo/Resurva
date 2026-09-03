namespace ResumeMatcher.Domain;

public sealed class Resume
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string FileName
    {
        get; init;
    }
    public required string ContentType
    {
        get; init;
    }
    public required string ExtractedText
    {
        get; init;
    }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class Analysis
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ResumeId
    {
        get; init;
    }
    public required string JobDescription
    {
        get; init;
    }
    public double OverallScore
    {
        get; init;
    }
    public double SkillsScore
    {
        get; init;
    }
    public double ExperienceScore
    {
        get; init;
    }
    public double SeniorityScore
    {
        get; init;
    }
    public double RequirementsScore
    {
        get; init;
    }
    public double EducationScore
    {
        get; init;
    }
    public required string ResultJson
    {
        get; init;
    }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record EvidenceItem(string Text, string? Evidence = null);

public sealed record AnalysisResult(
    Guid Id,
    Guid ResumeId,
    double OverallScore,
    double SkillsScore,
    double ExperienceScore,
    double SeniorityScore,
    double RequirementsScore,
    double EducationScore,
    IReadOnlyList<EvidenceItem> MatchedSkills,
    IReadOnlyList<EvidenceItem> MissingSkills,
    IReadOnlyList<EvidenceItem> RequirementsMet,
    IReadOnlyList<EvidenceItem> RequirementsMissing,
    IReadOnlyList<EvidenceItem> Strengths,
    IReadOnlyList<EvidenceItem> PointsOfAttention,
    IReadOnlyList<EvidenceItem> Recommendations);
