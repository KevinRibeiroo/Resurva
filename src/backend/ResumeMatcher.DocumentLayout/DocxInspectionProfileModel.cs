namespace ResumeMatcher.DocumentLayout;

public sealed record DocxInspectionProfileModel
{
    public string[] HeadingStyleIds { get; init; } = [];
    public Dictionary<string, string> BlockRoles { get; init; } = [];
}
