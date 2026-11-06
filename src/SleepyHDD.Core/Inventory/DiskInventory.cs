using SleepyHDD.Core.Devices;

namespace SleepyHDD.Core.Inventory;

/// <summary>
/// 磁盘清单：经 IDiskDeviceOps 缝隙枚举设备并判定受管机械盘。
/// </summary>
public sealed class DiskInventory
{
    /// <summary>内置机械盘可能出现的总线类型；USB 等外置总线一律排除。</summary>
    private static readonly HashSet<DiskBusType> ManagedBusTypes = new()
    {
        DiskBusType.Ata,
        DiskBusType.Sata,
        DiskBusType.Scsi,
    };

    private readonly IDiskDeviceOps _deviceOps;

    public DiskInventory(IDiskDeviceOps deviceOps)
    {
        _deviceOps = deviceOps;
    }

    /// <summary>
    /// 枚举受管磁盘（含其上的卷），按物理盘编号排序。
    /// </summary>
    public IReadOnlyList<ManagedDisk> EnumerateManagedDisks()
    {
        var volumesByDisk = MapVolumesToDisks();

        var managed = new List<ManagedDisk>();
        foreach (var disk in _deviceOps.EnumerateDisks())
        {
            var seekPenalty = _deviceOps.GetSeekPenalty(disk.DiskNumber);
            if (!ManagedBusTypes.Contains(disk.BusType)
                || seekPenalty == false
                || (seekPenalty == null && IsLikelySsd(disk.Model)))
            {
                continue;
            }

            managed.Add(new ManagedDisk
            {
                DiskNumber = disk.DiskNumber,
                BusType = disk.BusType,
                Model = disk.Model,
                SerialNumber = disk.SerialNumber,
                SizeBytes = disk.SizeBytes,
                Volumes = volumesByDisk.TryGetValue(disk.DiskNumber, out var volumes)
                    ? volumes
                    : Array.Empty<ManagedVolume>(),
            });
        }

        return managed
            .OrderBy(d => d.DiskNumber)
            .ToList();
    }

    /// <summary>
    /// 把挂载了盘符且能定位物理盘的卷按盘编号归组。
    /// </summary>
    private Dictionary<int, List<ManagedVolume>> MapVolumesToDisks()
    {
        var map = new Dictionary<int, List<ManagedVolume>>();
        foreach (var volume in _deviceOps.EnumerateVolumes())
        {
            if (volume.DiskNumber is not { } diskNumber || volume.DriveLetter is null)
            {
                continue;
            }

            if (!map.TryGetValue(diskNumber, out var volumes))
            {
                volumes = new List<ManagedVolume>();
                map[diskNumber] = volumes;
            }

            volumes.Add(new ManagedVolume
            {
                VolumePath = volume.VolumePath,
                DriveLetter = volume.DriveLetter,
                Label = volume.Label,
                TotalBytes = volume.TotalBytes,
                UsedBytes = volume.TotalBytes is { } total && volume.FreeBytes is { } free
                    ? total - free
                    : null,
            });
        }

        return map;
    }

    /// <summary>
    /// 寻道惩罚无法判定时的兜底：型号含 "SSD" 视为固态盘。
    /// </summary>
    private static bool IsLikelySsd(string model)
        => model.Contains("SSD", StringComparison.OrdinalIgnoreCase);
}
