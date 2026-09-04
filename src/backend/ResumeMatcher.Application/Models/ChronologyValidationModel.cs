namespace ResumeMatcher.Application;

public sealed record ChronologyValidationModel(
    bool IsValid,
    bool IsFuture,
    bool IsIndeterminate,
    string? Message = null);
