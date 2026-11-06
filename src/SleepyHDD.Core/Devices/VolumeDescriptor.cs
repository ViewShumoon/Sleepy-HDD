namespace SleepyHDD.Core.Devices;

/// <summary>
/// 卷的原始描述，由设备层枚举产生。
/// </summary>
public sealed record VolumeDescriptor
{
    /// <summary>卷路径，形如 \\?\Volume{guid}\。</summary>
    public required string VolumePath { get; init; }

    /// <summary>所在物理盘编号；无法确定时为 null。</summary>
    public int? DiskNumber { get; init; }

    /// <summary>挂载的盘符（如 "D"）；未挂载盘符时为 null。</summary>
    public string? DriveLetter { get; init; }

    /// <summary>卷标。</summary>
    public string? Label { get; init; }

    /// <summary>卷总字节数。</summary>
    public long? TotalBytes { get; init; }

    /// <summary>卷可用字节数。</summary>
    public long? FreeBytes { get; init; }
}
