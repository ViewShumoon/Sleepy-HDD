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
        var bootSystemLetters = new HashSet<string>(
            _deviceOps.GetBootSystemDriveLetters(),
            StringComparer.OrdinalIgnoreCase);

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

            IReadOnlyList<ManagedVolume> volumes = volumesByDisk.TryGetValue(disk.DiskNumber, out var mapped)
                ? mapped
                : Array.Empty<ManagedVolume>();

            managed.Add(new ManagedDisk
            {
                DiskNumber = disk.DiskNumber,
                BusType = disk.BusType,
                Model = disk.Model,
                SerialNumber = disk.SerialNumber,
                SizeBytes = disk.SizeBytes,
                Volumes = volumes,
                RestrictedReasons = ClassifyRestricted(disk, volumes, bootSystemLetters),
            });
        }

        return managed
            .OrderBy(d => d.DiskNumber)
            .ToList();
    }

    /// <summary>
    /// 受限判定：按 RestrictedReason 声明顺序（即优先级）收集全部命中原因。
    /// </summary>
    private IReadOnlyList<RestrictedReason> ClassifyRestricted(
        DiskDescriptor disk,
        IReadOnlyList<ManagedVolume> volumes,
        HashSet<string> bootSystemLetters)
    {
        var reasons = new List<RestrictedReason>();

        if (volumes.Any(v => bootSystemLetters.Contains(v.DriveLetter)))
        {
            reasons.Add(RestrictedReason.BootOrSystemVolume);
        }

        return reasons;
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
