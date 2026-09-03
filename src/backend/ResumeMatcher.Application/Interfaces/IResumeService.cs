namespace ResumeMatcher.Application;

public interface IResumeService
{
    Task<UploadResumeResultModel> UploadAsync(
        UploadResumeCommand command,
        CancellationToken cancellationToken);
}
