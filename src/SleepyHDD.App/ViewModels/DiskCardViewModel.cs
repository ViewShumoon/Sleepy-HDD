using SleepyHDD.Core.Inventory;

namespace SleepyHDD.App.ViewModels;

/// <summary>
/// 受管磁盘卡片展示项。
/// </summary>
public sealed class DiskCardViewModel
{
    public DiskCardViewModel(ManagedDisk disk)
    {
        Model = disk.Model.Length > 0 ? disk.Model : $"磁盘 #{disk.DiskNumber}";
        CapacityText = ByteFormat.Format(disk.SizeBytes);
        Volumes = disk.Volumes
            .Select(volume => new VolumeCardViewModel(volume))
            .ToList();
    }

    /// <summary>磁盘型号。</summary>
    public string Model { get; }

    /// <summary>容量文本，如 "931 GB"。</summary>
    public string CapacityText { get; }

    /// <summary>该盘上的卷。</summary>
    public IReadOnlyList<VolumeCardViewModel> Volumes { get; }
}
