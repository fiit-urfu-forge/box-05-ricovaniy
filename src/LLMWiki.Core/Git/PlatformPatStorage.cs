using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using LLMWiki.Core.Infrastructure;
using Meziantou.Framework.Win32;

namespace LLMWiki.Core.Git;

public sealed class PlatformPatStorage : IPatStorage
{
    private const string ApplicationName = "LLMWiki";

    private readonly IPatStorage _impl;

    public PlatformPatStorage()
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600))
        {
            _impl = new WindowsPatStorage();
        }
        else
        {
            _impl = new FilePatStorage(
                Path.Combine(LLMWikiPaths.AppData, "secrets"));
        }
    }

    public string? Read(string key) => _impl.Read(key);
    public void Write(string key, string value) => _impl.Write(key, value);
    public void Delete(string key) => _impl.Delete(key);

    [SupportedOSPlatform("windows5.1.2600")]
    private sealed class WindowsPatStorage : IPatStorage
    {
        public string? Read(string key)
        {
            var cred = CredentialManager.ReadCredential($"{ApplicationName}:{key}");
            return cred?.Password;
        }

        public void Write(string key, string value)
        {
            CredentialManager.WriteCredential(
                $"{ApplicationName}:{key}",
                ApplicationName,
                value,
                CredentialPersistence.LocalMachine);
        }

        public void Delete(string key)
        {
            try { CredentialManager.DeleteCredential($"{ApplicationName}:{key}"); }
            catch { /* missing — ignore */ }
        }
    }

    private sealed class FilePatStorage : IPatStorage
    {
        private readonly string _root;

        public FilePatStorage(string root)
        {
            _root = root;
            Directory.CreateDirectory(_root);
        }

        public string? Read(string key)
        {
            var path = PathFor(key);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        public void Write(string key, string value)
        {
            var path = PathFor(key);
            AtomicFile.WriteAllText(path, value);
            TrySetUserOnlyPermissions(path);
        }

        public void Delete(string key)
        {
            var path = PathFor(key);
            if (File.Exists(path)) File.Delete(path);
        }

        private string PathFor(string key)
        {
            var safeKey = string.Concat(key.Where(c =>
                char.IsLetterOrDigit(c) || c is '-' or '_' or '.'));
            if (safeKey.Length == 0) safeKey = "default";
            return Path.Combine(_root, safeKey);
        }

        private static void TrySetUserOnlyPermissions(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
            try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
            catch { }
        }
    }
}
