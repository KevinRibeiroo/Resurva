using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocxLayoutProbe;

/// <summary>Generates synthetic examples only, outside the application's export flow.</summary>
public static class DocxDemoGenerator
{
    public static void Generate(string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        var destination = Path.GetFullPath(outputDirectory);
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new IOException("A pasta de saída já existe. Escolha outra pasta; nada será sobrescrito.");

        var original = CreateSyntheticOriginal();
        // Confirmation belongs to this invented example, never to a real user's curriculum.
        var adapted = DocxSkillInsertionProbe.Insert(original, "HABILIDADES", "Bancos de Dados", "PostgreSQL", confirmed: true);
        Directory.CreateDirectory(destination);
        WriteNew(Path.Combine(destination, "original.docx"), original);
        WriteNew(Path.Combine(destination, "adaptado.docx"), adapted);
    }

    private static void WriteNew(string path, byte[] content)
    {
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        file.Write(content);
    }

    private static byte[] CreateSyntheticOriginal()
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            var styles = main.AddNewPart<StyleDefinitionsPart>();
            styles.Styles = new Styles(
                new Style(new StyleName { Val = "Normal" },
                    new StyleParagraphProperties(new SpacingBetweenLines { After = "100", Line = "264", LineRule = LineSpacingRuleValues.Auto }),
                    new StyleRunProperties(new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" }, new Color { Val = "000000" }, new FontSize { Val = "22" }))
                    { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true },
                new Style(new StyleName { Val = "Title" }, new BasedOn { Val = "Normal" },
                    new StyleParagraphProperties(new SpacingBetweenLines { After = "80" }, new Justification { Val = JustificationValues.Center }),
                    new StyleRunProperties(new Bold(), new FontSize { Val = "36" }))
                    { Type = StyleValues.Paragraph, StyleId = "Title" },
                new Style(new StyleName { Val = "Heading 1" }, new BasedOn { Val = "Normal" },
                    new StyleParagraphProperties(new KeepNext(), new SpacingBetweenLines { Before = "200", After = "100" }, new OutlineLevel { Val = 0 }),
                    new StyleRunProperties(new Bold(), new FontSize { Val = "24" }))
                    { Type = StyleValues.Paragraph, StyleId = "Heading1" });

            main.Document = new Document(new Body(
                Styled("Title", "CANDIDATO EXEMPLO"),
                new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                    new Run(new Text("Desenvolvimento backend | contato@example.test"))),
                Styled("Heading1", "RESUMO"),
                TextParagraph("Profissional fictício de desenvolvimento backend, com atuação em APIs e manutenção de sistemas. Interesse em soluções claras, testes automatizados e colaboração entre equipes."),
                Styled("Heading1", "HABILIDADES"),
                Category("Linguagens", "C#, JavaScript, SQL."),
                Category("Frameworks", "ASP.NET Core, Entity Framework Core."),
                Category("Bancos de Dados", "SQL Server, MySQL."),
                Category("Ferramentas", "Git, testes unitários, integração contínua."),
                Styled("Heading1", "EXPERIENCIA"),
                new Paragraph(new Run(new RunProperties(new Bold()), new Text("Desenvolvedor backend | Empresa Exemplo"))),
                TextParagraph("2023 a 2025"),
                TextParagraph("Desenvolvimento e manutenção de APIs de catálogo em C# e ASP.NET Core, com validação de entradas e acesso a dados via Entity Framework Core."),
                TextParagraph("Criação de testes unitários para regras de negócio e participação na revisão de código da equipe."),
                Styled("Heading1", "FORMACAO"),
                TextParagraph("Análise e Desenvolvimento de Sistemas | Instituição Exemplo"),
                TextParagraph("Conclusão em 2023"),
                new SectionProperties(new PageSize { Width = 12240, Height = 15840 },
                    new PageMargin { Top = 1080, Right = 1080, Bottom = 1080, Left = 1080, Header = 360, Footer = 360, Gutter = 0 })));
        }
        return stream.ToArray();
    }

    private static Paragraph Styled(string style, string text) =>
        new(new ParagraphProperties(new ParagraphStyleId { Val = style }), new Run(new Text(text)));

    private static Paragraph TextParagraph(string text) => new(new Run(new Text(text)));

    private static Paragraph Category(string label, string text) => new(
        new Run(new RunProperties(new Bold()), new Text(label + ": ") { Space = SpaceProcessingModeValues.Preserve }),
        new Run(new Text(text)));
}
