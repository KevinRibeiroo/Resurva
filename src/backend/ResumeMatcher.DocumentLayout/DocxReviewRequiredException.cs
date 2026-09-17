namespace ResumeMatcher.DocumentLayout;

public sealed class DocxReviewRequiredException : InvalidOperationException
{
    public string Code { get; }
    public string? OperationId { get; }

    public DocxReviewRequiredException(string code, string? operationId = null)
        : base("review_required")
    {
        Code = code;
        OperationId = operationId;
    }
}
