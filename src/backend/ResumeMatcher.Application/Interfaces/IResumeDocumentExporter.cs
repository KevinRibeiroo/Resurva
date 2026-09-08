namespace ResumeMatcher.Application;

public interface IResumeDocumentExporter
{
    string Format { get; }
    string ContentType { get; }
    string FileExtension { get; }
    Task<byte[]> ExportAsync(string adaptedText, CancellationToken cancellationToken);
}
