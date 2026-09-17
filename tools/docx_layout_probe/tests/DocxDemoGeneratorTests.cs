using System.IO.Compression;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

namespace DocxLayoutProbe.Tests;

public class DocxDemoGeneratorTests
{
    [Fact]
    public void WritesTwoValidDocumentsWithOnlyTheConfirmedSkillChanged()
    {
        var root = Directory.CreateTempSubdirectory("resumematcher-docx-demo-test-");
        try
        {
            var output = Path.Combine(root.FullName, "demo");
            DocxDemoGenerator.Generate(output);
            Assert.Equal(new[] { "adaptado.docx", "original.docx" }, Directory.GetFiles(output).Select(Path.GetFileName).Order());
            var original = Path.Combine(output, "original.docx");
            var adapted = Path.Combine(output, "adaptado.docx");
            foreach (var path in new[] { original, adapted })
            {
                using var document = WordprocessingDocument.Open(path, false);
                Assert.Empty(new OpenXmlValidator().Validate(document));
            }
            var before = ReadParts(original);
            var after = ReadParts(adapted);
            Assert.Equal(before.Keys.Order(), after.Keys.Order());
            foreach (var part in before.Keys.Where(p => p != "word/document.xml"))
                Assert.Equal(before[part], after[part]);
            var expected = XDocument.Parse(System.Text.Encoding.UTF8.GetString(before["word/document.xml"]));
            var actual = XDocument.Parse(System.Text.Encoding.UTF8.GetString(after["word/document.xml"]));
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            expected.Descendants(w + "t").Single(t => t.Value == "SQL Server, MySQL.").Value = "SQL Server, MySQL, PostgreSQL.";
            foreach (var xml in new[] { expected, actual })
                xml.Descendants().Attributes().Where(a => a.IsNamespaceDeclaration).Remove();
            Assert.True(XNode.DeepEquals(expected, actual));
        }
        finally { root.Delete(recursive: true); }
    }

    [Fact]
    public void RefusesExistingDirectoryWithoutOverwritingAnyFile()
    {
        var root = Directory.CreateTempSubdirectory("resumematcher-docx-demo-test-");
        try
        {
            var existing = Path.Combine(root.FullName, "adaptado.docx");
            File.WriteAllBytes(existing, [1, 2, 3]);
            Assert.Throws<IOException>(() => DocxDemoGenerator.Generate(root.FullName));
            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(existing));
            Assert.Single(Directory.GetFiles(root.FullName));
        }
        finally { root.Delete(recursive: true); }
    }

    private static Dictionary<string, byte[]> ReadParts(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        return archive.Entries.ToDictionary(e => e.FullName, e =>
        {
            using var source = e.Open();
            using var target = new MemoryStream();
            source.CopyTo(target);
            return target.ToArray();
        });
    }
}
