namespace ResumeMatcher.DocumentLayout;

public sealed record DocxAdaptationResultModel(byte[] Document,
    IReadOnlyList<string> AppliedOperationIds, string ReviewStatus, int ExistingSchemaErrors);
