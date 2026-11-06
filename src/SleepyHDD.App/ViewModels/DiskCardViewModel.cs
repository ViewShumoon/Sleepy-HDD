using SleepyHDD.Core.Inventory;

namespace SleepyHDD.App.ViewModels;

/// <summary>
/// 受管磁盘卡片展示项。受限盘仅以红字徽章说明原因，不提供任何动作按钮（DESIGN.md 4.1）。
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
        IsRestricted = disk.IsRestricted;
        IsNotRestricted = !disk.IsRestricted;
        StatusText = disk.PrimaryRestrictedReason is { } reason
            ? $"受限 · {Describe(reason)}"
            : "在线";
        RestrictedReasonsText = disk.IsRestricted
            ? string.Join("；", disk.RestrictedReasons.Select(Describe))
            : string.Empty;
    }

    /// <summary>磁盘型号。</summary>
    public string Model { get; }

    /// <summary>容量文本，如 "931 GB"。</summary>
    public string CapacityText { get; }

    /// <summary>该盘上的卷。</summary>
    public IReadOnlyList<VolumeCardViewModel> Volumes { get; }

    /// <summary>受限展示态：徽章显示红字原因，无动作按钮。</summary>
    public bool IsRestricted { get; }

    /// <summary>不受限展示态：徽章显示绿色"在线"。</summary>
    public bool IsNotRestricted { get; }

    /// <summary>徽章文本："在线" 或 "受限 · 主因说明"。</summary>
    public string StatusText { get; }

    /// <summary>全部受限原因说明（悬停提示用）；不受限时为空。</summary>
    public string RestrictedReasonsText { get; }

    private static string Describe(RestrictedReason reason) => reason switch
    {
        RestrictedReason.BootOrSystemVolume => "承载启动/系统卷",
        RestrictedReason.Pagefile => "承载页面文件",
        RestrictedReason.DynamicOrSpacesMember => "动态盘/Storage Spaces 成员",
        RestrictedReason.AtaPassThroughUnavailable => "ATA pass-through 不可用",
        _ => reason.ToString(),
    };
}
