namespace ResumeMatcher.DocumentLayout;

public sealed record DocxEditOperationModel
{
    public string Id { get; init; } = "";
    public string BlockId { get; init; } = "";
    public string ExpectedText { get; init; } = "";
    public string Kind { get; init; } = "replace_text";
    public int Start { get; init; }
    public int Length { get; init; }
    public string NewText { get; init; } = "";
    public bool Approved { get; init; }
    public string[] EvidenceIds { get; init; } = [];
}
