using ResumeMatcher.Domain;

namespace ResumeMatcher.Application;

public interface IAnalysisService
{
    Task<AnalysisResultModel> CompareAsync(CompareCommand command, CancellationToken cancellationToken);
    Task<AnalysisResultModel?> GetAsync(Guid id, CancellationToken cancellationToken);
}
