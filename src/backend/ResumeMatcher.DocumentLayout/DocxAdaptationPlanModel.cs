namespace ResumeMatcher.DocumentLayout;

public sealed record DocxAdaptationPlanModel
{
    public int Version { get; init; } = 1;
    public string SourceSha256 { get; init; } = "";
    public DocxInspectionProfileModel Profile { get; init; } = new();
    public DocxEditOperationModel[] Operations { get; init; } = [];
}
