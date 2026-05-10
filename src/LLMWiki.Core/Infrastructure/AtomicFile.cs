namespace LLMWiki.Core.Infrastructure;

public static class AtomicFile
{
    public static void WriteAllText(string targetPath, string content)
    {
        var dir = Path.GetDirectoryName(targetPath)
            ?? throw new ArgumentException($"Path has no directory: {targetPath}");
        Directory.CreateDirectory(dir);

        var tempPath = targetPath + ".tmp";

        using (var fs = new FileStream(
                   tempPath,
                   FileMode.Create,
                   FileAccess.Write,
                   FileShare.None))
        using (var writer = new StreamWriter(fs))
        {
            writer.Write(content);
            writer.Flush();
            fs.Flush(flushToDisk: true);
        }

        File.Move(tempPath, targetPath, overwrite: true);
    }

    public static async Task WriteAllTextAsync(
        string targetPath,
        string content,
        CancellationToken cancellationToken = default)
    {
        var dir = Path.GetDirectoryName(targetPath)
            ?? throw new ArgumentException($"Path has no directory: {targetPath}");
        Directory.CreateDirectory(dir);

        var tempPath = targetPath + ".tmp";

        await using (var fs = new FileStream(
                         tempPath,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None))
        await using (var writer = new StreamWriter(fs))
        {
            await writer.WriteAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            fs.Flush(flushToDisk: true);
        }

        File.Move(tempPath, targetPath, overwrite: true);
    }
}
