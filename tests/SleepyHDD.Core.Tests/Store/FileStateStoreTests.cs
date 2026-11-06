using SleepyHDD.Core.StateMachine;
using SleepyHDD.Core.Store;

namespace SleepyHDD.Core.Tests.Store;

/// <summary>
/// 状态文件持久化：按序列号记录本软件置休眠/离线的磁盘，跨实例可读回。
/// </summary>
public class FileStateStoreTests : IDisposable
{
    private readonly string _path;

    public FileStateStoreTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"sleepy-hdd-state-{Guid.NewGuid():N}.json");
    }

    [Fact]
    public void Record_PersistsBySerialNumberAndSurvivesReload()
    {
        var store = new FileStateStore(_path);

        store.Record("ZAVR1ABC", DiskState.Sleeping);

        var reloaded = new FileStateStore(_path);
        Assert.Equal(DiskState.Sleeping, Assert.Contains("ZAVR1ABC", reloaded.Load()));
    }

    [Fact]
    public void Record_SameSerialTwice_KeepsSingleLatestEntry()
    {
        var store = new FileStateStore(_path);

        store.Record("ZAVR1ABC", DiskState.Sleeping);
        store.Record("ZAVR1ABC", DiskState.Offline);

        var entries = new FileStateStore(_path).Load();
        var entry = Assert.Single(entries);
        Assert.Equal("ZAVR1ABC", entry.Key);
        Assert.Equal(DiskState.Offline, entry.Value);
    }

    [Fact]
    public void Record_DistinguishesSleepingAndOfflinePerSerialAcrossReload()
    {
        var store = new FileStateStore(_path);

        store.Record("SLEEPED1", DiskState.Sleeping);
        store.Record("OFFLINE1", DiskState.Offline);

        var entries = new FileStateStore(_path).Load();
        Assert.Equal(DiskState.Sleeping, Assert.Contains("SLEEPED1", entries));
        Assert.Equal(DiskState.Offline, Assert.Contains("OFFLINE1", entries));
        string json = File.ReadAllText(_path);
        Assert.Contains("Sleeping", json);
        Assert.Contains("Offline", json);
    }

    [Fact]
    public void Forget_RemovesEntry()
    {
        var store = new FileStateStore(_path);
        store.Record("ZAVR1ABC", DiskState.Sleeping);

        store.Forget("ZAVR1ABC");

        Assert.Empty(new FileStateStore(_path).Load());
    }

    [Fact]
    public void Load_NoFile_ReturnsEmpty()
        => Assert.Empty(new FileStateStore(_path).Load());

    [Fact]
    public void FileContent_UsesSerialNumberNotDiskNumber()
    {
        new FileStateStore(_path).Record("ZAVR1ABC", DiskState.Sleeping);

        string json = File.ReadAllText(_path);
        Assert.Contains("ZAVR1ABC", json);
        Assert.Contains("Sleeping", json);
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
