using SleepyHDD.Core.Store;

namespace SleepyHDD.Core.Tests.Store;

/// <summary>
/// 设置文件持久化：默认动作等设置写入 settings.json，跨实例可读回；
/// 文件缺失或损坏时回退到默认值（休眠）。
/// </summary>
public class FileSettingsStoreTests : IDisposable
{
    private readonly string _path;

    public FileSettingsStoreTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"sleepy-hdd-settings-{Guid.NewGuid():N}.json");
    }

    [Fact]
    public void Load_NoFile_ReturnsDefaultSleep()
        => Assert.Equal(DefaultAction.Sleep, new FileSettingsStore(_path).Load().DefaultAction);

    [Fact]
    public void Save_Offline_SurvivesReload()
    {
        new FileSettingsStore(_path).Save(new Settings { DefaultAction = DefaultAction.Offline });

        var reloaded = new FileSettingsStore(_path).Load();
        Assert.Equal(DefaultAction.Offline, reloaded.DefaultAction);
    }

    [Fact]
    public void Save_Twice_KeepsLatestValue()
    {
        var store = new FileSettingsStore(_path);
        store.Save(new Settings { DefaultAction = DefaultAction.Offline });
        store.Save(new Settings { DefaultAction = DefaultAction.Sleep });

        Assert.Equal(DefaultAction.Sleep, new FileSettingsStore(_path).Load().DefaultAction);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefault()
    {
        File.WriteAllText(_path, "{ not json");

        Assert.Equal(DefaultAction.Sleep, new FileSettingsStore(_path).Load().DefaultAction);
    }

    [Fact]
    public void FileContent_UsesEnumNames()
    {
        new FileSettingsStore(_path).Save(new Settings { DefaultAction = DefaultAction.Offline });

        string json = File.ReadAllText(_path);
        Assert.Contains("DefaultAction", json);
        Assert.Contains("Offline", json);
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
