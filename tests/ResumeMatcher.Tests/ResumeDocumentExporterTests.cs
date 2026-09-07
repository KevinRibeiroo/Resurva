using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ResumeMatcher.Infrastructure;
using UglyToad.PdfPig;
using Xunit;

namespace ResumeMatcher.Tests;

public sealed class ResumeDocumentExporterTests
{
    private const string SampleResumeText = """
        CANDIDATO FICTÍCIO
        Cidade Exemplo, BR | candidato@example.test | https://example.test/perfil

        RESUMO
        Engenheiro de Software com experiência em .NET 10, C# e microsserviços em nuvem.

        EXPERIÊNCIA PROFISSIONAL
        • Desenvolveu soluções de integração de alto desempenho com APIs REST e mensageria.
        • Liderou iniciativas de observabilidade e modernização de arquitetura legada.
        - Mentorou desenvolvedores juniores em boas práticas de Clean Architecture e testes unitários.

        HABILIDADES TÉCNICAS
        • Backend: C#, .NET Core, ASP.NET, Entity Framework, PostgreSQL, Docker.
        """;

    [Fact]
    public async Task DocxExporter_GeneratesValidDocumentWithAccentsAndStructure()
    {
        var exporter = new DocxResumeDocumentExporter();
        Assert.Equal("docx", exporter.Format);
        Assert.Equal(".docx", exporter.FileExtension);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", exporter.ContentType);

        var bytes = await exporter.ExportAsync(SampleResumeText, CancellationToken.None);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);

        using var stream = new MemoryStream(bytes);
        using var wordDoc = WordprocessingDocument.Open(stream, false);
        var bodyText = wordDoc.MainDocumentPart?.Document?.Body?.InnerText ?? "";

        Assert.Contains("CANDIDATO FICTÍCIO", bodyText);
        Assert.Contains("EXPERIÊNCIA PROFISSIONAL", bodyText);
        Assert.Contains("observabilidade", bodyText);
        Assert.Contains("HABILIDADES TÉCNICAS", bodyText);
    }

    [Fact]
    public async Task PdfExporter_GeneratesValidDocumentWithAccentsAndStructure()
    {
        var exporter = new PdfResumeDocumentExporter();
        Assert.Equal("pdf", exporter.Format);
        Assert.Equal(".pdf", exporter.FileExtension);
        Assert.Equal("application/pdf", exporter.ContentType);

        var bytes = await exporter.ExportAsync(SampleResumeText, CancellationToken.None);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);

        using var pdfDoc = PdfDocument.Open(bytes);
        Assert.True(pdfDoc.NumberOfPages >= 1);

        var pageText = pdfDoc.GetPage(1).Text;
        Assert.Contains("CANDIDATO FICTÍCIO", pageText);
        Assert.Contains("EXPERIÊNCIA PROFISSIONAL", pageText);
        Assert.Contains("observabilidade", pageText);
        Assert.Contains("HABILIDADES TÉCNICAS", pageText);
    }

    [Fact]
    public async Task PdfExporter_PaginatesMultiplePagesWhenContentExceedsSinglePage()
    {
        var exporter = new PdfResumeDocumentExporter();
        var longTextBuilder = new System.Text.StringBuilder();
        longTextBuilder.AppendLine("CANDIDATO MULTIPÁGINA TESTE");
        longTextBuilder.AppendLine("RESUMO");

        for (var i = 1; i <= 70; i++)
        {
            longTextBuilder.AppendLine($"• Linha de experiência {i} com texto suficientemente detalhado demonstrando quebras de página automáticas na exportação para PDF.");
        }

        var bytes = await exporter.ExportAsync(longTextBuilder.ToString(), CancellationToken.None);
        using var pdfDoc = PdfDocument.Open(bytes);

        Assert.True(pdfDoc.NumberOfPages > 1, $"Expected > 1 pages, got {pdfDoc.NumberOfPages}");
    }
}
