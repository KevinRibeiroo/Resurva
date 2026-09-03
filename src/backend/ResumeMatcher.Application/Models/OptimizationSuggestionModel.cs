namespace ResumeMatcher.Application;

public sealed record OptimizationSuggestionModel(
    Guid Id,
    OptimizationSafetyLevel Level,
    bool Confirmed = false);
