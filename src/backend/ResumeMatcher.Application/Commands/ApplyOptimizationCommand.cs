namespace ResumeMatcher.Application;

public sealed record ApplyOptimizationCommand(
    int Version,
    IReadOnlyList<OptimizationDecisionModel> Decisions);
