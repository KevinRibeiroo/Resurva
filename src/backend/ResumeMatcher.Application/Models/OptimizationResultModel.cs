namespace ResumeMatcher.Application;

public sealed record OptimizationResultModel(
    Guid Id,
    Guid AnalysisId,
    Guid ResumeId,
    string OriginalText,
    string AdaptedText,
    IReadOnlyList<AppliedOptimizationItemModel> AppliedChanges,
    DateTimeOffset UpdatedAt);
