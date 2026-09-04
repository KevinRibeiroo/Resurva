using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ResumeMatcher.Application;
using ResumeMatcher.Infrastructure;
using System.Text;

namespace ResumeMatcher.Tests;

public sealed class ResumeTextExtractorTests
{
    [Fact]
    public async Task Valid_docx_is_extracted()
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document, true))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new Document(new Body(new Paragraph(new Run(new Text("C# e React")))));
        }
        stream.Position = 0;
        var text = await new DocxResumeTextExtractor().ExtractAsync(stream, CancellationToken.None);
        Assert.Contains("C# e React", text);
    }

    [Fact]
    public async Task Valid_pdf_is_extracted()
    {
        using var stream = new MemoryStream(BuildMinimalPdf("C# and React"));
        var text = await new PdfResumeTextExtractor().ExtractAsync(stream, CancellationToken.None);
        Assert.Contains("C# and React", text);
    }

    [Theory]
    [InlineData("pdf")]
    [InlineData("docx")]
    public async Task Invalid_file_is_rejected(string type)
    {
        using var stream = new MemoryStream("not a document"u8.ToArray());
        IResumeTextExtractor extractor = type == "pdf" ? new PdfResumeTextExtractor() : new DocxResumeTextExtractor();
        await Assert.ThrowsAsync<InvalidResumeException>(() => extractor.ExtractAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task Empty_docx_text_is_rejected_by_service()
    {
        var extractor = new StubExtractor();
        var service = new ResumeService([extractor], new StubRepository());
        await Assert.ThrowsAsync<InvalidResumeException>(() => service.UploadAsync(new("empty.pdf", "application/pdf", new MemoryStream([1])), CancellationToken.None));
    }

    private sealed class StubExtractor : IResumeTextExtractor
    {
        public bool CanExtract(string extension, string contentType)
        {
            return true;
        }

        public Task<string> ExtractAsync(Stream stream, CancellationToken cancellationToken)
        {
            return Task.FromResult(string.Empty);
        }
    }

    private sealed class StubRepository : IResumeRepository
    {
        public Task AddAsync(Domain.ResumeEntity resume, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<Domain.ResumeEntity?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult<Domain.ResumeEntity?>(null);
        }
    }

    private static byte[] BuildMinimalPdf(string text)
    {
        var content = $"BT /F1 12 Tf 25 700 Td ({text}) Tj ET";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        ];
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(pdf.Length);
            pdf.Append($"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }
        var xref = pdf.Length;
        pdf.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            pdf.Append($"{offset:0000000000} 00000 n \n");
        pdf.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
