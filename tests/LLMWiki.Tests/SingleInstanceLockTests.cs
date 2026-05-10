using LLMWiki.Core.Infrastructure;

namespace LLMWiki.Tests;

[TestFixture]
public class SingleInstanceLockTests
{
    private string _dir = null!;

    [SetUp]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), "llmwiki-lock-" + Guid.NewGuid());
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void Cleanup() { try { Directory.Delete(_dir, true); } catch { } }

    [Test]
    public void Acquire_OnFreshDirectory_Succeeds()
    {
        var lockFile = Path.Combine(_dir, "app.lock");
        using var l = new SingleInstanceLock(lockFile);
        l.TryAcquire().Should().BeTrue();
        File.Exists(lockFile).Should().BeTrue();
    }

    [Test]
    public void Acquire_RemovesLockOnDispose()
    {
        var lockFile = Path.Combine(_dir, "app.lock");
        var l = new SingleInstanceLock(lockFile);
        l.TryAcquire().Should().BeTrue();
        l.Dispose();
        File.Exists(lockFile).Should().BeFalse();
    }

    [Test]
    public void Acquire_StaleLockWithDeadPid_IsCleanedAndAcquired()
    {
        var lockFile = Path.Combine(_dir, "app.lock");
        File.WriteAllText(lockFile, "999999999");

        using var l = new SingleInstanceLock(lockFile);
        l.TryAcquire().Should().BeTrue();
    }

    [Test]
    public void Acquire_LiveOtherProcess_Refuses()
    {
        var lockFile = Path.Combine(_dir, "app.lock");
        File.WriteAllText(lockFile, Environment.ProcessId.ToString());

        using var l = new SingleInstanceLock(lockFile);
        l.TryAcquire().Should().BeFalse();
    }

    [Test]
    public void Acquire_GarbageInLock_TreatedAsStale()
    {
        var lockFile = Path.Combine(_dir, "app.lock");
        File.WriteAllText(lockFile, "not-a-pid");

        using var l = new SingleInstanceLock(lockFile);
        l.TryAcquire().Should().BeTrue();
    }
}
