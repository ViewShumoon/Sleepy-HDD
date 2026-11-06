using SleepyHDD.Core.Scheduler;
using SleepyHDD.Core.Store;

namespace SleepyHDD.Core.Tests.Store;

/// <summary>
/// 设置文件持久化：默认动作、开机自启开关与定时策略写入 settings.json，跨实例可读回；
/// 文件缺失或损坏时回退到默认值（休眠、自启关、空策略列表）。
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

    [Fact]
    public void AutoStartEnabled_Default_IsOff()
        => Assert.False(new FileSettingsStore(_path).Load().AutoStartEnabled);

    [Fact]
    public void AutoStartEnabled_SurvivesReload()
    {
        new FileSettingsStore(_path).Save(new Settings { AutoStartEnabled = true });

        Assert.True(new FileSettingsStore(_path).Load().AutoStartEnabled);
    }

    [Fact]
    public void Load_CorruptFile_AutoStartDefaultsToOff()
    {
        File.WriteAllText(_path, "{ not json");

        Assert.False(new FileSettingsStore(_path).Load().AutoStartEnabled);
    }

    [Fact]
    public void Save_WithSchedules_SurvivesReload()
    {
        var schedule = new DiskSchedule
        {
            Id = "abc",
            SerialNumber = "HDD1",
            At = new TimeOnly(23, 30),
            Days = DayOfWeekMask.Monday | DayOfWeekMask.Friday,
            Target = ScheduleTargetState.Offline,
        };

        new FileSettingsStore(_path).Save(new Settings { Schedules = new List<DiskSchedule> { schedule } });

        var reloaded = new FileSettingsStore(_path).Load();
        var restored = Assert.Single(reloaded.Schedules);
        Assert.Equal(schedule, restored);
    }

    [Fact]
    public void Load_LegacyFileWithoutSchedules_ReturnsEmptyList()
    {
        File.WriteAllText(_path, "{ \"DefaultAction\": \"Offline\" }");

        var loaded = new FileSettingsStore(_path).Load();
        Assert.Equal(DefaultAction.Offline, loaded.DefaultAction);
        Assert.Empty(loaded.Schedules);
    }

    [Fact]
    public void FileContent_SchedulesUseReadableEnumNames()
    {
        new FileSettingsStore(_path).Save(new Settings
        {
            Schedules = new List<DiskSchedule>
            {
                new()
                {
                    Id = "abc",
                    SerialNumber = "HDD1",
                    At = new TimeOnly(1, 30),
                    Days = DayOfWeekMask.Sunday,
                    Target = ScheduleTargetState.Sleep,
                },
            },
        });

        string json = File.ReadAllText(_path);
        Assert.Contains("Sunday", json);
        Assert.Contains("Sleep", json);
        Assert.Contains("HDD1", json);
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
