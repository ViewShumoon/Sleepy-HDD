using SleepyHDD.Core.Devices;

namespace SleepyHDD.Core.Tests.Fakes;

/// <summary>
/// IDiskDeviceOps 的测试替身：以数据驱动返回磁盘、寻道惩罚与卷，
/// 并记录全部设备调用序列（lock:/dismount:/unlock:/flush:/standby:/occupants: 前缀）供断言。
/// </summary>
internal sealed class FakeDiskDeviceOps : IDiskDeviceOps
{
    public List<DiskDescriptor> Disks { get; set; } = new();

    public Dictionary<int, bool?> Penalties { get; } = new();

    public List<VolumeDescriptor> Volumes { get; set; } = new();

    /// <summary>按发生顺序记录的设备调用，如 "lock:\\?\Volume{d}\"。</summary>
    public List<string> Calls { get; } = new();

    /// <summary>LockVolume 将失败的卷路径集合。</summary>
    public HashSet<string> LockFailures { get; } = new(StringComparer.Ordinal);

    /// <summary>GetOccupyingProcesses 按卷路径返回的占用进程名。</summary>
    public Dictionary<string, List<string>> OccupantsByVolume { get; } = new(StringComparer.Ordinal);

    /// <summary>StandbyImmediate 按盘号阻塞等待的事件（模拟停转耗时，用于并发/串行断言）。</summary>
    public Dictionary<int, ManualResetEventSlim> StandbyGates { get; } = new();

    /// <summary>StandbyImmediate 已进入阻塞的信号（按盘号）。</summary>
    public Dictionary<int, ManualResetEventSlim> StandbyEntered { get; } = new();

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

    public bool LockVolume(string volumePath)
    {
        Record($"lock:{volumePath}");
        return !LockFailures.Contains(volumePath);
    }

    public bool DismountVolume(string volumePath)
    {
        Record($"dismount:{volumePath}");
        return true;
    }

    public bool UnlockVolume(string volumePath)
    {
        Record($"unlock:{volumePath}");
        return true;
    }

    public bool FlushWriteCache(int diskNumber)
    {
        Record($"flush:{diskNumber}");
        return true;
    }

    public bool StandbyImmediate(int diskNumber)
    {
        Record($"standby:{diskNumber}");
        if (StandbyGates.TryGetValue(diskNumber, out var gate))
        {
            if (StandbyEntered.TryGetValue(diskNumber, out var entered))
            {
                entered.Set();
            }

            gate.Wait(TimeSpan.FromSeconds(30));
        }

        return true;
    }

    public IReadOnlyList<string> GetOccupyingProcesses(string volumePath)
    {
        Record($"occupants:{volumePath}");
        return OccupantsByVolume.TryGetValue(volumePath, out var occupants)
            ? occupants
            : Array.Empty<string>();
    }

    public IReadOnlyList<string> GetBootSystemDriveLetters() => BootSystemDriveLetters;

    public IReadOnlyList<string> GetPagefilePaths() => PagefilePaths;

    public bool IsDynamicOrSpacesMember(int diskNumber) => DynamicOrSpacesDisks.Contains(diskNumber);

    public bool ProbeAtaPassThrough(int diskNumber)
        => AtaProbes.TryGetValue(diskNumber, out var available) ? available : true;

    /// <summary>Calls 中是否包含以指定前缀开头的记录。</summary>
    public bool HasCall(string prefix)
    {
        lock (Calls)
        {
            return Calls.Any(call => call.StartsWith(prefix, StringComparison.Ordinal));
        }
    }

    /// <summary>指定前缀调用在 Calls 序列中的位置（不存在返回 -1）。</summary>
    public int IndexOfCall(string prefix)
    {
        lock (Calls)
        {
            return Calls.FindIndex(call => call.StartsWith(prefix, StringComparison.Ordinal));
        }
    }

    private void Record(string call)
    {
        lock (Calls)
        {
            Calls.Add(call);
        }
    }
}
