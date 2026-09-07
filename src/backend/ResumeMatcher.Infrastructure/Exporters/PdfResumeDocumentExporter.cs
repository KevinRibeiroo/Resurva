using System.Reflection;
using System.Text;
using ResumeMatcher.Application;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

namespace ResumeMatcher.Infrastructure;

public sealed class PdfResumeDocumentExporter : IResumeDocumentExporter
{
    private static readonly byte[] RegularFontBytes = LoadFontResource("Roboto-Regular.ttf");
    private static readonly byte[] BoldFontBytes = LoadFontResource("Roboto-Bold.ttf");

    public string Format => "pdf";
    public string ContentType => "application/pdf";
    public string FileExtension => ".pdf";

    public Task<byte[]> ExportAsync(string adaptedText, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var builder = new PdfDocumentBuilder();
        var regularFont = builder.AddTrueTypeFont(RegularFontBytes);
        var boldFont = builder.AddTrueTypeFont(BoldFontBytes);

        const double pageHeight = 841.89;
        const double marginX = 50.0;
        const double marginTop = 50.0;
        const double marginBottom = 50.0;

        var currentPage = builder.AddPage(PageSize.A4);
        var currentY = pageHeight - marginTop;

        void EnsureSpace(double neededHeight)
        {
            if (currentY - neededHeight < marginBottom)
            {
                currentPage = builder.AddPage(PageSize.A4);
                currentY = pageHeight - marginTop;
            }
        }

        var lines = adaptedText.Replace("\r\n", "\n").Split('\n');
        var isFirstLine = true;

        foreach (var rawLine in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = rawLine.Trim();

            if (string.IsNullOrEmpty(line))
            {
                currentY -= 8;
                continue;
            }

            if (isFirstLine && !line.StartsWith("•") && !line.StartsWith("-"))
            {
                // Candidate Name Header
                const int fontSize = 16;
                const double lineHeight = 20.0;
                EnsureSpace(lineHeight + 6);

                var wrapped = WrapText(line, 55);
                foreach (var wl in wrapped)
                {
                    EnsureSpace(lineHeight);
                    currentPage.AddText(wl, fontSize, new PdfPoint(marginX, currentY), boldFont);
                    currentY -= lineHeight;
                }

                currentY -= 6;
                isFirstLine = false;
            }
            else if (IsSectionHeading(line))
            {
                // Section Heading
                const int fontSize = 12;
                const double lineHeight = 16.0;
                currentY -= 10;
                EnsureSpace(lineHeight + 4);

                var headingText = line.TrimStart('-', ' ', '[').TrimEnd(']');
                var wrapped = WrapText(headingText, 70);
                foreach (var wl in wrapped)
                {
                    EnsureSpace(lineHeight);
                    currentPage.AddText(wl, fontSize, new PdfPoint(marginX, currentY), boldFont);
                    currentY -= lineHeight;
                }

                currentY -= 4;
            }
            else if (line.StartsWith("•") || line.StartsWith("- "))
            {
                // Bullet item
                const int fontSize = 10;
                const double lineHeight = 13.5;
                var bulletContent = line.StartsWith("•") ? line[1..].Trim() : line[2..].Trim();

                var wrapped = WrapText(bulletContent, 85);
                for (var i = 0; i < wrapped.Count; i++)
                {
                    EnsureSpace(lineHeight);
                    if (i == 0)
                    {
                        currentPage.AddText("•", fontSize, new PdfPoint(marginX, currentY), regularFont);
                    }
                    currentPage.AddText(wrapped[i], fontSize, new PdfPoint(marginX + 15, currentY), regularFont);
                    currentY -= lineHeight;
                }

                currentY -= 2;
            }
            else
            {
                // Normal paragraph text
                const int fontSize = 10;
                const double lineHeight = 13.5;
                var wrapped = WrapText(line, 90);
                foreach (var wl in wrapped)
                {
                    EnsureSpace(lineHeight);
                    currentPage.AddText(wl, fontSize, new PdfPoint(marginX, currentY), regularFont);
                    currentY -= lineHeight;
                }

                currentY -= 4;
            }
        }

        var pdfBytes = builder.Build();
        return Task.FromResult(pdfBytes);
    }

    private static bool IsSectionHeading(string line)
    {
        if (line.StartsWith("---") || line.StartsWith("["))
            return true;

        var upper = line.ToUpperInvariant();
        return upper is "RESUMO" or "HABILIDADES TÉCNICAS" or "EXPERIÊNCIA PROFISSIONAL"
            or "FORMAÇÃO ACADÊMICA" or "CURSOS E CERTIFICAÇÕES" or "HISTÓRICO PROFISSIONAL"
            or "EDUCAÇÃO" or "COMPETÊNCIAS";
    }

    private static List<string> WrapText(string text, int maxCharsPerLine)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            result.Add(string.Empty);
            return result;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var currentLine = new StringBuilder();

        foreach (var word in words)
        {
            if (currentLine.Length == 0)
            {
                currentLine.Append(word);
            }
            else if (currentLine.Length + 1 + word.Length <= maxCharsPerLine)
            {
                currentLine.Append(' ').Append(word);
            }
            else
            {
                result.Add(currentLine.ToString());
                currentLine.Clear();
                currentLine.Append(word);
            }
        }

        if (currentLine.Length > 0)
        {
            result.Add(currentLine.ToString());
        }

        return result;
    }

    private static byte[] LoadFontResource(string fileName)
    {
        var assembly = typeof(PdfResumeDocumentExporter).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Font resource '{fileName}' was not found in assembly {assembly.FullName}. Available: {string.Join(", ", assembly.GetManifestResourceNames())}");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Could not open manifest stream for '{resourceName}'.");

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
