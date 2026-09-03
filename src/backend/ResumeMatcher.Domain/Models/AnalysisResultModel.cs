namespace ResumeMatcher.Domain;

public sealed record AnalysisResultModel(
    Guid Id,
    Guid ResumeId,
    double OverallScore,
    double SkillsScore,
    double ExperienceScore,
    double SeniorityScore,
    double RequirementsScore,
    double EducationScore,
    IReadOnlyList<EvidenceItemModel> MatchedSkills,
    IReadOnlyList<EvidenceItemModel> MissingSkills,
    IReadOnlyList<EvidenceItemModel> RequirementsMet,
    IReadOnlyList<EvidenceItemModel> RequirementsMissing,
    IReadOnlyList<EvidenceItemModel> Strengths,
    IReadOnlyList<EvidenceItemModel> PointsOfAttention,
    IReadOnlyList<EvidenceItemModel> Recommendations);
