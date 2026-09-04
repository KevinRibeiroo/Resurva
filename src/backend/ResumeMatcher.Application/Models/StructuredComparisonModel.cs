using ResumeMatcher.Domain;

namespace ResumeMatcher.Application;

public sealed record StructuredComparisonModel(
    IReadOnlyList<EvidenceItemModel> MatchedSkills,
    IReadOnlyList<EvidenceItemModel> MissingSkills,
    IReadOnlyList<EvidenceItemModel> RequirementsMet,
    IReadOnlyList<EvidenceItemModel> RequirementsMissing,
    IReadOnlyList<EvidenceItemModel> Strengths,
    IReadOnlyList<EvidenceItemModel> PointsOfAttention,
    IReadOnlyList<EvidenceItemModel> Recommendations,
    double ExperienceMatch,
    double SeniorityMatch,
    double EducationMatch,
    string? CandidateSeniority = null,
    string? RequiredSeniority = null,
    double? CandidateExperienceYears = null,
    double? RequiredExperienceYears = null);
