using System.Diagnostics;

namespace LLMWiki.Core.Infrastructure;

public sealed class SingleInstanceLock : IDisposable
{
    private readonly string _lockFile;
    private bool _ownedByThisInstance;

    public SingleInstanceLock(string? lockFile = null)
    {
        _lockFile = lockFile ?? LLMWikiPaths.AppLockFile;
    }

    public bool TryAcquire()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_lockFile)!);

        if (File.Exists(_lockFile))
        {
            var existingPid = ReadPid(_lockFile);
            if (existingPid.HasValue && IsProcessAlive(existingPid.Value))
                return false;
            try { File.Delete(_lockFile); }
            catch { return false; }
        }

        try
        {
            File.WriteAllText(_lockFile, Environment.ProcessId.ToString());
            _ownedByThisInstance = true;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public int? CurrentHolderPid() => ReadPid(_lockFile);

    public void Dispose()
    {
        if (!_ownedByThisInstance) return;

        try
        {
            if (File.Exists(_lockFile))
            {
                var pid = ReadPid(_lockFile);
                if (pid == Environment.ProcessId)
                    File.Delete(_lockFile);
            }
        }
        catch { /* best-effort */ }

        _ownedByThisInstance = false;
    }

    private static int? ReadPid(string path)
    {
        try
        {
            var raw = File.ReadAllText(path).Trim();
            return int.TryParse(raw, out var pid) ? pid : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            var proc = Process.GetProcessById(pid);
            return !proc.HasExited;
        }
        catch
        {
            return false;
        }
    }
}
