using System.Security;
using System.Text;

namespace LLMWiki.Core.Infrastructure;

public static class PathValidator
{
    public static string NormalizeRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is empty", nameof(path));

        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            throw new ArgumentException("Path contains invalid characters", nameof(path));

        var full = Path.GetFullPath(path);
        return TrimTrailingSeparator(full).Normalize(NormalizationForm.FormC);
    }

    public static string EnsureWithin(string root, string candidate)
    {
        var normalizedRoot = NormalizeRoot(root);
        var normalizedCandidate = NormalizeRoot(candidate);

        if (!IsWithin(normalizedRoot, normalizedCandidate))
            throw new SecurityException(
                $"Path traversal attempt: '{candidate}' is outside '{root}'");

        return normalizedCandidate;
    }

    public static bool IsWithin(string root, string candidate)
    {
        var normalizedRoot = NormalizeRoot(root);
        var normalizedCandidate = NormalizeRoot(candidate);

        if (string.Equals(normalizedRoot, normalizedCandidate, StringComparison.OrdinalIgnoreCase))
            return true;

        var prefix = normalizedRoot + Path.DirectorySeparatorChar;
        return normalizedCandidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    public static string ToGitPath(string relativePath)
    {
        return relativePath.Replace('\\', '/');
    }

    public static bool IsValidFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (name.Length > 255) return false;
        return name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }

    private static string TrimTrailingSeparator(string path)
    {
        if (path.Length <= 1) return path;

        var lastChar = path[^1];
        if (lastChar == Path.DirectorySeparatorChar || lastChar == Path.AltDirectorySeparatorChar)
        {
            if (path.Length == 3 && path[1] == ':') return path;
            return path[..^1];
        }

        return path;
    }
}
