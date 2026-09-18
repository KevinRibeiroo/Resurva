namespace ResumeMatcher.DocumentLayout;

public sealed record DocxInspectionModel(string SourceSha256, IReadOnlyList<DocxBlockModel> Blocks);
