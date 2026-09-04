using ResumeMatcher.Domain;

namespace ResumeMatcher.Application;

public interface IAnalysisRepository
{
    Task<bool> TryAddAsync(AnalysisEntity analysis, CancellationToken cancellationToken);
    Task<AnalysisEntity?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<AnalysisEntity?> GetByInputHashAsync(string analysisInputHash, CancellationToken cancellationToken);
}
