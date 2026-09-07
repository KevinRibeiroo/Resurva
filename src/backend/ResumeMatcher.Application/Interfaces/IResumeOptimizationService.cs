namespace ResumeMatcher.Application;

public interface IResumeOptimizationService
{
    Task<OptimizationPlanModel> CreatePlanAsync(Guid analysisId, CancellationToken cancellationToken);
    Task<OptimizationPlanModel?> GetPlanAsync(Guid optimizationId, CancellationToken cancellationToken);
    Task<OptimizationResultModel> ApplyDecisionsAsync(Guid optimizationId, ApplyOptimizationCommand command, CancellationToken cancellationToken);
}
