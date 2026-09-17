namespace DocxLayoutProbe.Tests;

public class LocalArtifactWriterTests
{
    [Fact]
    public async Task FailureAfterFirstFileDoesNotPublishAndRemovesOnlyOwnedStaging()
    {
        var root = Directory.CreateTempSubdirectory("docx-atomic-test-");
        try
        {
            var unrelated = Path.Combine(root.FullName, "keep.txt");
            await File.WriteAllTextAsync(unrelated, "unrelated");
            var target = Path.Combine(root.FullName, "output");
            await Assert.ThrowsAsync<ArgumentException>(() => LocalArtifactWriter.PublishAsync(target,
                new Dictionary<string, byte[]> { ["first.docx"] = [1, 2], ["../invalid"] = [3] }, CancellationToken.None));
            Assert.False(Directory.Exists(target));
            Assert.Empty(Directory.GetDirectories(root.FullName));
            Assert.Equal("unrelated", await File.ReadAllTextAsync(unrelated));
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task CancellationDoesNotPublish()
    {
        var root = Directory.CreateTempSubdirectory("docx-atomic-test-");
        try
        {
            var target = Path.Combine(root.FullName, "output");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => LocalArtifactWriter.PublishAsync(target,
                new Dictionary<string, byte[]> { ["first.docx"] = [1, 2] }, cancellation.Token));
            Assert.False(Directory.Exists(target));
            Assert.Empty(Directory.GetDirectories(root.FullName));
        }
        finally { root.Delete(true); }
    }
}
