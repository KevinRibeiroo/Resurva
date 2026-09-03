namespace ResumeMatcher.Application;

public interface IResumeTextExtractor
{
    bool CanExtract(string extension, string contentType);
    Task<string> ExtractAsync(Stream stream, CancellationToken cancellationToken);
}
