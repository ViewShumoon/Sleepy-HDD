using SleepyHDD.Core.Devices;
using SleepyHDD.Core.Inventory;
using SleepyHDD.Core.Notifications;

namespace SleepyHDD.Core.Tests.Notifications;

/// <summary>
/// 磁盘插拔差异计算（票 #7 通知用）：两次枚举结果按序列号匹配得出新增/移除。
/// </summary>
public class DiskChangeDiffTests
{
    [Fact]
    public void Compute_NewDisk_IsAdded()
    {
        var current = new List<ManagedDisk> { Disk(1, "SN1"), Disk(2, "SN2") };

        var (added, removed) = DiskChangeDiff.Compute(new List<ManagedDisk> { Disk(1, "SN1") }, current);

        Assert.Equal(new[] { "SN2" }, added.Select(disk => disk.SerialNumber));
        Assert.Empty(removed);
    }

    [Fact]
    public void Compute_GoneDisk_IsRemoved()
    {
        var previous = new List<ManagedDisk> { Disk(1, "SN1"), Disk(2, "SN2") };

        var (added, removed) = DiskChangeDiff.Compute(previous, new List<ManagedDisk> { Disk(1, "SN1") });

        Assert.Empty(added);
        Assert.Equal(new[] { "SN2" }, removed.Select(disk => disk.SerialNumber));
    }

    [Fact]
    public void Compute_UnchangedLists_ProduceNoDiff()
    {
        var disks = new List<ManagedDisk> { Disk(1, "SN1"), Disk(2, "SN2") };

        var (added, removed) = DiskChangeDiff.Compute(disks, disks);

        Assert.Empty(added);
        Assert.Empty(removed);
    }

    [Fact]
    public void Compute_DiskReplaced_ReportsBothSides()
    {
        var (added, removed) = DiskChangeDiff.Compute(
            new List<ManagedDisk> { Disk(1, "SN1") },
            new List<ManagedDisk> { Disk(2, "SN9") });

        Assert.Equal(new[] { "SN9" }, added.Select(disk => disk.SerialNumber));
        Assert.Equal(new[] { "SN1" }, removed.Select(disk => disk.SerialNumber));
    }

    private static ManagedDisk Disk(int number, string serial) => new()
    {
        DiskNumber = number,
        BusType = DiskBusType.Sata,
        Model = $"ST1000DM010-{serial}",
        SerialNumber = serial,
        SizeBytes = 1_000_204_886_016,
        RestrictedReasons = Array.Empty<RestrictedReason>(),
        Volumes = Array.Empty<ManagedVolume>(),
    };
}
