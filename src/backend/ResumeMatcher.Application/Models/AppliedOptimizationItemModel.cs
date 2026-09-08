namespace ResumeMatcher.Application;

public sealed record AppliedOptimizationItemModel(
    Guid SuggestionId,
    OptimizationSafetyLevel Level,
    string OriginalText,
    string ProposedText,
    string Reason,
    bool WasConfirmed,
    string InformationOrigin,
    string? UserDeclaration = null);
