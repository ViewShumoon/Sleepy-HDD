using SleepyHDD.Core.Devices;
using SleepyHDD.Core.Inventory;
using SleepyHDD.Core.Lifecycle;
using SleepyHDD.Core.StateMachine;
using SleepyHDD.Core.Tests.Fakes;

namespace SleepyHDD.Core.Tests.Lifecycle;

/// <summary>
/// 生命周期缝隙（ADR-0005，DESIGN.md 2.7）：经 fake IDiskDeviceOps/IStateStore 验证
/// 启动恢复序列（按序列号匹配、盘消失清理、离线清障）与退出唤醒序列（仅动在案盘），
/// 不触碰 Native 内部实现。
/// </summary>
public class DiskLifecycleTests
{
    private const string VolD = @"\\?\Volume{dd-ddata}\";
    private const string VolE = @"\\?\Volume{ee-edata}\";

    // ---------- 启动恢复 ----------

    [Fact]
    public void RecoverOnStartup_OfflineRecord_ClearsOfflineAndForgets()
    {
        var fake = new FakeDiskDeviceOps();
        fake.Disks.Add(Descriptor(1, "HDD1"));
        var store = new FakeStateStore();
        store.Entries["HDD1"] = DiskState.Offline;
        var lifecycle = new DiskLifecycle(fake, store);

        lifecycle.RecoverOnStartup();

        Assert.Equal(new[] { "clear-offline:1" }, fake.Calls);
        Assert.Equal(new[] { "forget:HDD1" }, store.Calls);
    }

    [Fact]
    public void RecoverOnStartup_MatchesBySerial_AfterDiskNumberChanged()
    {
        // 重启后同一序列号盘的盘号从 1 变为 2：恢复必须按序列号用当前盘号清离线属性。
        var fake = new FakeDiskDeviceOps();
        fake.Disks.Add(Descriptor(2, "HDD1"));
        var store = new FakeStateStore();
        store.Entries["HDD1"] = DiskState.Offline;
        var lifecycle = new DiskLifecycle(fake, store);

        lifecycle.RecoverOnStartup();

        Assert.Equal(new[] { "clear-offline:2" }, fake.Calls);
        Assert.Equal(new[] { "forget:HDD1" }, store.Calls);
    }

    [Fact]
    public void RecoverOnStartup_MissingDisk_SilentlyForgets()
    {
        // 记录中的盘已消失（拔出）：静默清理其记录，不产生任何设备调用。
        var fake = new FakeDiskDeviceOps();
        fake.Disks.Add(Descriptor(1, "HDD1"));
        var store = new FakeStateStore();
        store.Entries["HDD1"] = DiskState.Offline;
        store.Entries["GONE"] = DiskState.Offline;
        var lifecycle = new DiskLifecycle(fake, store);

        lifecycle.RecoverOnStartup();

        Assert.Equal(new[] { "clear-offline:1" }, fake.Calls);
        Assert.Contains("forget:GONE", store.Calls);
        Assert.Contains("forget:HDD1", store.Calls);
    }

    [Fact]
    public void RecoverOnStartup_SleepingRecord_ForgetsWithoutDeviceCalls()
    {
        // 休眠不跨重启存续：卷锁随旧进程句柄关闭而消失、盘访问即起转，只需清理记录。
        var fake = new FakeDiskDeviceOps();
        fake.Disks.Add(Descriptor(1, "HDD1"));
        var store = new FakeStateStore();
        store.Entries["HDD1"] = DiskState.Sleeping;
        var lifecycle = new DiskLifecycle(fake, store);

        lifecycle.RecoverOnStartup();

        Assert.Empty(fake.Calls);
        Assert.Equal(new[] { "forget:HDD1" }, store.Calls);
    }

    [Fact]
    public void RecoverOnStartup_ClearOfflineFails_KeepsRecordAndDoesNotThrow()
    {
        // 恢复失败不阻塞启动：保留记录（下次启动重试），不向上抛异常。
        var fake = new FakeDiskDeviceOps();
        fake.Disks.Add(Descriptor(1, "HDD1"));
        fake.ClearOfflineFailures.Add(1);
        var store = new FakeStateStore();
        store.Entries["HDD1"] = DiskState.Offline;
        var lifecycle = new DiskLifecycle(fake, store);

        lifecycle.RecoverOnStartup();

        Assert.Equal(new[] { "clear-offline:1" }, fake.Calls);
        Assert.Empty(store.Calls);
    }

    [Fact]
    public void RecoverOnStartup_EmptyStore_TouchesNothing()
    {
        var fake = new FakeDiskDeviceOps();
        var store = new FakeStateStore();
        var lifecycle = new DiskLifecycle(fake, store);

        lifecycle.RecoverOnStartup();

        Assert.Empty(fake.Calls);
        Assert.Empty(store.Calls);
    }

    // ---------- 退出唤醒 ----------

    [Fact]
    public void WakeAllForExit_OfflineRecord_ClearsOfflineThenUnlocksVolumesAndForgets()
    {
        var fake = new FakeDiskDeviceOps();
        var store = new FakeStateStore();
        store.Entries["HDD1"] = DiskState.Offline;
        var lifecycle = new DiskLifecycle(fake, store);

        lifecycle.WakeAllForExit(new[] { Disk(1, "HDD1", VolD, VolE) });

        // 与状态机唤醒语义一致：先清离线属性（卷自动重新挂载），再解锁全部卷。
        Assert.Equal(new[] { "clear-offline:1", $"unlock:{VolD}", $"unlock:{VolE}" }, fake.Calls);
        Assert.Equal(new[] { "forget:HDD1" }, store.Calls);
    }

    [Fact]
    public void WakeAllForExit_SleepingRecord_UnlocksVolumesWithoutOfflineChange()
    {
        var fake = new FakeDiskDeviceOps();
        var store = new FakeStateStore();
        store.Entries["HDD1"] = DiskState.Sleeping;
        var lifecycle = new DiskLifecycle(fake, store);

        lifecycle.WakeAllForExit(new[] { Disk(1, "HDD1", VolD, VolE) });

        Assert.Equal(new[] { $"unlock:{VolD}", $"unlock:{VolE}" }, fake.Calls);
        Assert.Equal(new[] { "forget:HDD1" }, store.Calls);
    }

    [Fact]
    public void WakeAllForExit_UnrecordedDisk_Untouched()
    {
        // 仅唤醒状态文件在案的盘：无记录的盘不碰设备层、不写记录。
        var fake = new FakeDiskDeviceOps();
        var store = new FakeStateStore();
        store.Entries["HDD1"] = DiskState.Sleeping;
        var lifecycle = new DiskLifecycle(fake, store);

        lifecycle.WakeAllForExit(new[]
        {
            Disk(1, "HDD1", VolD, VolE),
            Disk(2, "HDD2", VolE),
        });

        Assert.Equal(new[] { $"unlock:{VolD}", $"unlock:{VolE}" }, fake.Calls);
        Assert.Equal(new[] { "forget:HDD1" }, store.Calls);
    }

    [Fact]
    public void WakeAllForExit_RecordedDiskMissingFromSnapshot_SkippedAndRecordKept()
    {
        // 退出前盘已拔出：离线属性随盘持久，保留记录交由下次启动恢复处理。
        var fake = new FakeDiskDeviceOps();
        var store = new FakeStateStore();
        store.Entries["GONE"] = DiskState.Offline;
        var lifecycle = new DiskLifecycle(fake, store);

        lifecycle.WakeAllForExit(new[] { Disk(1, "HDD1", VolD) });

        Assert.Empty(fake.Calls);
        Assert.Empty(store.Calls);
    }

    [Fact]
    public void WakeAllForExit_ClearOfflineFails_KeepsRecordAndSkipsUnlock()
    {
        // 清离线失败：中止该盘唤醒、保留记录（下次启动恢复兜底），不解锁卷。
        var fake = new FakeDiskDeviceOps();
        fake.ClearOfflineFailures.Add(1);
        var store = new FakeStateStore();
        store.Entries["HDD1"] = DiskState.Offline;
        var lifecycle = new DiskLifecycle(fake, store);

        lifecycle.WakeAllForExit(new[] { Disk(1, "HDD1", VolD, VolE) });

        Assert.Equal(new[] { "clear-offline:1" }, fake.Calls);
        Assert.Empty(store.Calls);
    }

    [Fact]
    public void WakeAllForExit_EmptyStore_TouchesNothing()
    {
        var fake = new FakeDiskDeviceOps();
        var store = new FakeStateStore();
        var lifecycle = new DiskLifecycle(fake, store);

        lifecycle.WakeAllForExit(new[] { Disk(1, "HDD1", VolD, VolE) });

        Assert.Empty(fake.Calls);
        Assert.Empty(store.Calls);
    }

    private static DiskDescriptor Descriptor(int number, string serial) => new()
    {
        DiskNumber = number,
        BusType = DiskBusType.Sata,
        Model = "ST1000DM010",
        SerialNumber = serial,
        SizeBytes = 1_000_204_886_016,
    };

    private static ManagedDisk Disk(int number, string serial, params string[] volumePaths) => new()
    {
        DiskNumber = number,
        BusType = DiskBusType.Sata,
        Model = "ST1000DM010",
        SerialNumber = serial,
        SizeBytes = 1_000_204_886_016,
        RestrictedReasons = Array.Empty<RestrictedReason>(),
        Volumes = volumePaths
            .Select((path, index) => new ManagedVolume
            {
                VolumePath = path,
                DriveLetter = ((char)('D' + index)).ToString(),
            })
            .ToList(),
    };
}
