using LLMWiki.Core.Domain;

namespace LLMWiki.Core.Files;

public static class FileTypeClassifier
{
    public static readonly IReadOnlySet<string> TextExtensions = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".txt", ".csv", ".json", ".xml", ".yaml", ".yml", ".html"
    };

    public static readonly IReadOnlySet<string> PdfExtensions = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        ".pdf"
    };

    public static readonly IReadOnlySet<string> ImageExtensions = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp"
    };

    public static FileType Classify(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (TextExtensions.Contains(ext)) return FileType.Text;
        if (PdfExtensions.Contains(ext)) return FileType.Pdf;
        if (ImageExtensions.Contains(ext)) return FileType.Image;
        return FileType.Other;
    }

    public static bool IsSupported(string fileName) =>
        Classify(fileName) is FileType.Text or FileType.Pdf or FileType.Image;
}
