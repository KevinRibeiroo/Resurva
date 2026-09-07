namespace ResumeMatcher.Domain;

public sealed class ResumeOptimizationEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string OwnerUserId { get; init; } = "";
    public Guid ResumeId { get; init; }
    public Guid AnalysisId { get; init; }
    public required string OriginalText { get; init; }
    public int Version { get; set; } = 1;
    public string Status { get; set; } = "Pending";
    public required string SuggestionsJson { get; set; }
    public string? DecisionsJson { get; set; }
    public string? AdaptedText { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
