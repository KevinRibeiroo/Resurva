using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ResumeMatcher.Application;
using UglyToad.PdfPig;

namespace ResumeMatcher.Infrastructure;

public sealed class PdfResumeTextExtractor : IResumeTextExtractor
{
    public bool CanExtract(string extension, string contentType)
    {
        return extension == ".pdf" && (string.IsNullOrWhiteSpace(contentType) || contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase));
    }

    public Task<string> ExtractAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            buffer.Position = 0;
            using var document = PdfDocument.Open(buffer);
            var pages = document.GetPages().Select(page =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return page.Text;
            });
            return Task.FromResult(string.Join(Environment.NewLine, pages));
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

public sealed class DocxResumeTextExtractor : IResumeTextExtractor
{
    private const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public bool CanExtract(string extension, string contentType)
    {
        return extension == ".docx" && (string.IsNullOrWhiteSpace(contentType) || contentType.Equals(DocxContentType, StringComparison.OrdinalIgnoreCase));
    }

    public Task<string> ExtractAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            buffer.Position = 0;
            using var document = WordprocessingDocument.Open(buffer, false);
            var body = document.MainDocumentPart?.Document.Body
                ?? throw new InvalidResumeException("The DOCX document has no body.");
            var paragraphs = body.Descendants<Paragraph>().Select(paragraph => string.Concat(paragraph.Descendants<Text>().Select(text => text.Text)));
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(string.Join(Environment.NewLine, paragraphs));
        }
        catch (InvalidResumeException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidResumeException($"Invalid DOCX file: {exception.Message}");
        }
    }
}
