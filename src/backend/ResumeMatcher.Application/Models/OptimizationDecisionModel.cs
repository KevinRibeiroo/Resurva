namespace ResumeMatcher.Application;

public sealed record OptimizationDecisionModel(
    Guid SuggestionId,
    bool Accepted,
    bool Confirmed = false,
    string? UserDeclaration = null);
