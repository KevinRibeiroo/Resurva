namespace ResumeMatcher.Application;

public sealed record OptimizationPlanModel(
    Guid Id,
    Guid AnalysisId,
    Guid ResumeId,
    int Version,
    string Status,
    string OriginalText,
    IReadOnlyList<OptimizationSuggestionModel> Suggestions,
    DateTimeOffset CreatedAt,
    string? AdaptedText = null,
    IReadOnlyList<OptimizationDecisionModel>? AppliedDecisions = null,
    IReadOnlyList<AppliedOptimizationItemModel>? AppliedChanges = null);
