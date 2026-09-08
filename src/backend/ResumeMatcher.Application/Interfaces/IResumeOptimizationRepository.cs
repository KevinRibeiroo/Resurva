using ResumeMatcher.Domain;

namespace ResumeMatcher.Application;

public interface IResumeOptimizationRepository
{
    Task AddAsync(ResumeOptimizationEntity entity, CancellationToken cancellationToken);
    Task<ResumeOptimizationEntity?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task UpdateAsync(ResumeOptimizationEntity entity, CancellationToken cancellationToken);
}
