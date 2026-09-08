namespace ResumeMatcher.Application;

public sealed record OptimizationSuggestionModel(
    Guid Id,
    OptimizationSafetyLevel Level,
    bool Confirmed = false,
    string OriginalText = "",
    string ProposedText = "",
    string Reason = "",
    string? Evidence = null,
    string? ConfirmationQuestion = null);
