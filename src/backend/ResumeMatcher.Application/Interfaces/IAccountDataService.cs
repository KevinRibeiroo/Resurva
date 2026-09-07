namespace ResumeMatcher.Application;

public interface IAccountDataService
{
    Task DeleteAllAsync(CancellationToken cancellationToken);
}
