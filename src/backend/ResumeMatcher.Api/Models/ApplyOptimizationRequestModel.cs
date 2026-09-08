using ResumeMatcher.Application;

namespace ResumeMatcher.Api.Models;

public sealed record ApplyOptimizationRequestModel(
    int Version,
    IReadOnlyList<OptimizationDecisionModel> Decisions);
