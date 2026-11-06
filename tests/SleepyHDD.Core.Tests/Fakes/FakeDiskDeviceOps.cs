using SleepyHDD.Core.Devices;

namespace SleepyHDD.Core.Tests.Fakes;

/// <summary>
/// IDiskDeviceOps 的测试替身：以数据驱动返回磁盘、寻道惩罚与卷。
/// </summary>
internal sealed class FakeDiskDeviceOps : IDiskDeviceOps
{
    public List<DiskDescriptor> Disks { get; set; } = new();

    public Dictionary<int, bool?> Penalties { get; } = new();

    public List<VolumeDescriptor> Volumes { get; set; } = new();

    /// <summary>启动/系统卷所在盘符。</summary>
    public List<string> BootSystemDriveLetters { get; set; } = new();

    /// <summary>页面文件路径。</summary>
    public List<string> PagefilePaths { get; set; } = new();

    /// <summary>被视为动态盘/Storage Spaces 成员的盘编号集合。</summary>
    public HashSet<int> DynamicOrSpacesDisks { get; } = new();

    /// <summary>ATA pass-through 试探结果；未配置的盘默认可用（不受限）。</summary>
    public Dictionary<int, bool> AtaProbes { get; } = new();

    public IReadOnlyList<DiskDescriptor> EnumerateDisks() => Disks;

    public bool? GetSeekPenalty(int diskNumber)
        => Penalties.TryGetValue(diskNumber, out var penalty) ? penalty : null;

    public IReadOnlyList<VolumeDescriptor> EnumerateVolumes() => Volumes;

    public IReadOnlyList<string> GetBootSystemDriveLetters() => BootSystemDriveLetters;

    public IReadOnlyList<string> GetPagefilePaths() => PagefilePaths;

    public bool IsDynamicOrSpacesMember(int diskNumber) => DynamicOrSpacesDisks.Contains(diskNumber);

    public bool ProbeAtaPassThrough(int diskNumber)
        => AtaProbes.TryGetValue(diskNumber, out var available) ? available : true;
}
