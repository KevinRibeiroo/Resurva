namespace ResumeMatcher.Application;

public sealed record LayoutInspectionModel(int Version, string SourceSha256,
    IReadOnlyList<LayoutChangeModel> Changes, string ReviewStatus = "visual_review_pending");
