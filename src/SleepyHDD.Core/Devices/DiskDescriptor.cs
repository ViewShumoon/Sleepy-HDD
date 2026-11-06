namespace SleepyHDD.Core.Devices;

/// <summary>
/// 物理磁盘的原始描述，由设备层枚举产生，不含受管判定。
/// </summary>
public sealed record DiskDescriptor
{
    /// <summary>物理盘编号（PhysicalDriveN 的 N）。</summary>
    public required int DiskNumber { get; init; }

    /// <summary>总线类型。</summary>
    public required DiskBusType BusType { get; init; }

    /// <summary>型号字符串（来自 STORAGE_DEVICE_DESCRIPTOR，已去除首尾空白）。</summary>
    public required string Model { get; init; }

    /// <summary>序列号字符串。</summary>
    public required string SerialNumber { get; init; }

    /// <summary>磁盘总字节数。</summary>
    public required long SizeBytes { get; init; }
}
