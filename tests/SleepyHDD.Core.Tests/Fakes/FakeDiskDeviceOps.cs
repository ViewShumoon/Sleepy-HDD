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

    public IReadOnlyList<DiskDescriptor> EnumerateDisks() => Disks;

    public bool? GetSeekPenalty(int diskNumber)
        => Penalties.TryGetValue(diskNumber, out var penalty) ? penalty : null;

    public IReadOnlyList<VolumeDescriptor> EnumerateVolumes() => Volumes;
}
