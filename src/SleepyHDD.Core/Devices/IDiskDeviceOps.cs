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

    /// <summary>
    /// 承载启动/系统目录的卷所在盘符（如 ["C"]），用于受限判定。
    /// </summary>
    IReadOnlyList<string> GetBootSystemDriveLetters();

    /// <summary>
    /// 系统配置的页面文件路径（已展开环境变量，如 ["C:\\pagefile.sys"]），用于受限判定。
    /// </summary>
    IReadOnlyList<string> GetPagefilePaths();

    /// <summary>
    /// 指定物理盘是否为动态盘或 Storage Spaces 成员，用于受限判定。
    /// </summary>
    bool IsDynamicOrSpacesMember(int diskNumber);

    /// <summary>
    /// 对指定物理盘试探 ATA pass-through（IDENTIFY DEVICE）：true=可用，
    /// false=不可用（含 Intel RST/RAID 模式），失败的盘不可安全停转。
    /// </summary>
    bool ProbeAtaPassThrough(int diskNumber);
}
