using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using ResumeMatcher.Application;

namespace ResumeMatcher.Infrastructure;

public sealed class DocxResumeDocumentExporter : IResumeDocumentExporter
{
    public string Format => "docx";
    public string ContentType => "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public string FileExtension => ".docx";

    public Task<byte[]> ExportAsync(string adaptedText, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var stream = new MemoryStream();
        using (var wordDoc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            // Section Properties: A4 Page & 1-inch margins (1440 dxa)
            var sectionProps = new SectionProperties();
            sectionProps.AppendChild(new PageSize { Width = 11906U, Height = 16838U }); // A4 in dxa
            sectionProps.AppendChild(new PageMargin
            {
                Top = 1440,
                Right = 1440,
                Bottom = 1440,
                Left = 1440,
                Header = 720U,
                Footer = 720U,
                Gutter = 0U
            });

            var lines = adaptedText.Replace("\r\n", "\n").Split('\n');
            var isFirstLine = true;

            foreach (var rawLine in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = rawLine.Trim();

                if (string.IsNullOrEmpty(line))
                {
                    // Empty spacing paragraph
                    body.AppendChild(new Paragraph(new ParagraphProperties(new SpacingBetweenLines { After = "120" })));
                    continue;
                }

                var p = new Paragraph();
                var pProps = new ParagraphProperties();

                if (isFirstLine && !line.StartsWith("•") && !line.StartsWith("-"))
                {
                    // Candidate Name Header
                    pProps.SpacingBetweenLines = new SpacingBetweenLines { After = "120" };
                    p.AppendChild(pProps);

                    var r = new Run();
                    r.AppendChild(new RunProperties(
                        new Bold(),
                        new FontSize { Val = "32" }, // 16pt
                        new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" },
                        new Color { Val = "1A365D" } // Deep Navy
                    ));
                    r.AppendChild(new Text(line) { Space = SpaceProcessingModeValues.Preserve });
                    p.AppendChild(r);
                    isFirstLine = false;
                }
                else if (IsSectionHeading(line))
                {
                    // Section Header (e.g. RESUMO, EXPERIÊNCIA PROFISSIONAL)
                    pProps.SpacingBetweenLines = new SpacingBetweenLines { Before = "240", After = "80" };
                    p.AppendChild(pProps);

                    var r = new Run();
                    r.AppendChild(new RunProperties(
                        new Bold(),
                        new FontSize { Val = "24" }, // 12pt
                        new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" },
                        new Color { Val = "2C5282" }
                    ));
                    r.AppendChild(new Text(line) { Space = SpaceProcessingModeValues.Preserve });
                    p.AppendChild(r);
                }
                else if (line.StartsWith("•") || line.StartsWith("- "))
                {
                    // Bullet list item
                    pProps.Indentation = new Indentation { Left = "360", Hanging = "240" };
                    pProps.SpacingBetweenLines = new SpacingBetweenLines { After = "60", Line = "260", LineRule = LineSpacingRuleValues.Auto };
                    p.AppendChild(pProps);

                    var bulletRun = new Run(new RunProperties(new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" }, new FontSize { Val = "21" }));
                    bulletRun.AppendChild(new Text("• ") { Space = SpaceProcessingModeValues.Preserve });
                    p.AppendChild(bulletRun);

                    var contentText = line.StartsWith("•") ? line[1..].Trim() : line[2..].Trim();
                    var textRun = new Run(new RunProperties(new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" }, new FontSize { Val = "21" })); // 10.5pt
                    textRun.AppendChild(new Text(contentText) { Space = SpaceProcessingModeValues.Preserve });
                    p.AppendChild(textRun);
                }
                else
                {
                    // Regular Body text
                    pProps.SpacingBetweenLines = new SpacingBetweenLines { After = "80", Line = "260", LineRule = LineSpacingRuleValues.Auto };
                    p.AppendChild(pProps);

                    var r = new Run();
                    r.AppendChild(new RunProperties(
                        new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" },
                        new FontSize { Val = "21" } // 10.5pt
                    ));
                    r.AppendChild(new Text(line) { Space = SpaceProcessingModeValues.Preserve });
                    p.AppendChild(r);
                }

                body.AppendChild(p);
            }

            body.AppendChild(sectionProps);
            mainPart.Document.Save();
        }

        return Task.FromResult(stream.ToArray());
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
}
