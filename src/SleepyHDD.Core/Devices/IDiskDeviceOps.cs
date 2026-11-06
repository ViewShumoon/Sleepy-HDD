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
    /// 锁定卷（FSCTL_LOCK_VOLUME）：实现内部同步重试约 3 秒；成功后须保持锁直至 <see cref="UnlockVolume"/>。
    /// </summary>
    bool LockVolume(string volumePath);

    /// <summary>卸载已锁定的卷（FSCTL_DISMOUNT_VOLUME）；卷必须已被 <see cref="LockVolume"/> 锁定。</summary>
    bool DismountVolume(string volumePath);

    /// <summary>解锁卷（FSCTL_UNLOCK_VOLUME）并释放锁句柄；对未锁定的卷为无害空操作。</summary>
    bool UnlockVolume(string volumePath);

    /// <summary>经 ATA pass-through 向物理盘发送 CACHE FLUSH (0xE7)，同步等待完成。</summary>
    bool FlushWriteCache(int diskNumber);

    /// <summary>经 ATA pass-through 向物理盘发送 STANDBY IMMEDIATE (0xE0) 使其停转；绝不发送 SLEEP (0xE6)。</summary>
    bool StandbyImmediate(int diskNumber);

    /// <summary>用 Restart Manager 列出正占用该卷的进程名；无法获取时返回空列表。</summary>
    IReadOnlyList<string> GetOccupyingProcesses(string volumePath);
}
