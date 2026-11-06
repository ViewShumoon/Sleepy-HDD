using SleepyHDD.Core.Devices;

namespace SleepyHDD.Core.Inventory;

/// <summary>
/// 判定为受管的机械硬盘及其上的卷。
/// </summary>
public sealed record ManagedDisk
{
    /// <summary>物理盘编号。</summary>
    public required int DiskNumber { get; init; }

    /// <summary>总线类型。</summary>
    public required DiskBusType BusType { get; init; }

    /// <summary>型号。</summary>
    public required string Model { get; init; }

    /// <summary>序列号。</summary>
    public required string SerialNumber { get; init; }

    /// <summary>磁盘总字节数。</summary>
    public required long SizeBytes { get; init; }

    /// <summary>该盘上挂载盘符的卷。</summary>
    public required IReadOnlyList<ManagedVolume> Volumes { get; init; }
}
