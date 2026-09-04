using ResumeMatcher.Domain;

namespace ResumeMatcher.Application;

public interface IResumeRepository
{
    Task AddAsync(ResumeEntity resume, CancellationToken cancellationToken);
    Task<ResumeEntity?> GetAsync(Guid id, CancellationToken cancellationToken);
}
