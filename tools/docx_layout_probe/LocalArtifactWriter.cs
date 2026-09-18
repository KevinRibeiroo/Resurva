namespace DocxLayoutProbe;

internal static class LocalArtifactWriter
{
    internal static async Task PublishAsync(string destination, IReadOnlyDictionary<string, byte[]> files, CancellationToken cancellationToken)
    {
        var target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
        var parent = Path.GetDirectoryName(target);
        if (parent is null || !Directory.Exists(parent) || Directory.Exists(target) || File.Exists(target))
            throw new IOException("Destination must be new and its parent must exist.");
        // A sibling staging directory keeps final Directory.Move on the same volume.
        var staging = Path.Combine(parent, ".docx-stage-" + Guid.NewGuid().ToString("N"));
        if (Directory.Exists(staging) || File.Exists(staging)) throw new IOException("Staging collision.");
        Directory.CreateDirectory(staging);
        try
        {
            foreach (var file in files)
            {
                if (Path.GetFileName(file.Key) != file.Key || file.Key is "." or "..") throw new ArgumentException("Invalid output name.");
                await using var stream = new FileStream(Path.Combine(staging, file.Key), FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
                await stream.WriteAsync(file.Value, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(staging, target); // Throws if target appeared in the meantime; never merges/overwrites.
        }
        finally
        {
            // This exact randomly-named directory was created above, not a caller-supplied recursive-delete target.
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }
}
