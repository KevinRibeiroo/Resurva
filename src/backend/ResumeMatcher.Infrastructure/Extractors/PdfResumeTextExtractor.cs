using ResumeMatcher.Application;
using UglyToad.PdfPig;

namespace ResumeMatcher.Infrastructure;

public sealed class PdfResumeTextExtractor : IResumeTextExtractor
{
    public bool CanExtract(string extension, string contentType)
    {
        return extension == ".pdf" && (string.IsNullOrWhiteSpace(contentType) || contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<string> ExtractAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            using var document = PdfDocument.Open(buffer);
            var pages = document.GetPages().Select(page =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return page.Text;
            });
            return string.Join(Environment.NewLine, pages);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidResumeException($"Invalid PDF file: {exception.Message}");
        }
    }
}
