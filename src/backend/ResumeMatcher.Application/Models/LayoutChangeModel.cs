namespace ResumeMatcher.Application;

public sealed record LayoutChangeModel(Guid SuggestionId, string ProposedText, string Kind,
    IReadOnlyList<LayoutBlockModel> Candidates, string? BlockedReason);
