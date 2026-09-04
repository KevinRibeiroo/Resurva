using ResumeMatcher.Domain;

namespace ResumeMatcher.Application;

public interface IAnalysisRepository
{
    Task AddAsync(AnalysisEntity analysis, CancellationToken cancellationToken);
    Task<AnalysisEntity?> GetAsync(Guid id, CancellationToken cancellationToken);
}
