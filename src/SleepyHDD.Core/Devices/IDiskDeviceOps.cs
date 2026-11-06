namespace SleepyHDD.Core.Devices;

/// <summary>
/// 设备访问缝隙：Core 经此接口获取磁盘与卷信息，由 Native 层实现、测试以 fake 替换。
/// </summary>
public interface IDiskDeviceOps
{
    /// <summary>枚举系统中当前存在的物理磁盘。</summary>
    IReadOnlyList<DiskDescriptor> EnumerateDisks();

    /// <summary>
    /// 查询指定物理盘是否产生寻道惩罚：true=机械盘，false=固态，null=无法判定。
    /// </summary>
    bool? GetSeekPenalty(int diskNumber);

    /// <summary>枚举系统中当前存在的卷。</summary>
    IReadOnlyList<VolumeDescriptor> EnumerateVolumes();
}
