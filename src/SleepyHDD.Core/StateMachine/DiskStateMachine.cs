using SleepyHDD.Core.Devices;
using SleepyHDD.Core.Inventory;
using SleepyHDD.Core.Store;

namespace SleepyHDD.Core.StateMachine;

/// <summary>
/// 转休眠/唤醒编排（保护模式）：逐卷锁定→卸载，再 CACHE FLUSH (0xE7) + STANDBY IMMEDIATE (0xE0)；
/// 绝不发送 SLEEP (0xE6)。锁定失败则查询占用进程并回滚已锁卷。
/// 同一磁盘的操作经信号量串行，不同磁盘的操作并行执行。
/// </summary>
public sealed class DiskStateMachine
{
    private readonly IDiskDeviceOps _ops;
    private readonly IStateStore _store;
    private readonly object _gate = new();
    private readonly Dictionary<int, SemaphoreSlim> _perDiskLocks = new();
    private readonly Dictionary<int, DiskState> _states = new();

    public DiskStateMachine(IDiskDeviceOps ops, IStateStore store)
    {
        _ops = ops ?? throw new ArgumentNullException(nameof(ops));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>磁盘状态变更事件（磁盘编号, 新状态）；可能在后台线程触发，订阅方需自行编组到 UI 线程。</summary>
    public event Action<int, DiskState>? StateChanged;

    /// <summary>查询磁盘的软件视角状态；未在管理中记录过的默认在线。</summary>
    public DiskState GetState(int diskNumber)
    {
        lock (_gate)
        {
            return _states.TryGetValue(diskNumber, out var state) ? state : DiskState.Online;
        }
    }

    /// <summary>转休眠：锁定并卸载全部卷后停转磁盘；成功后按序列号写入状态文件。</summary>
    public Task<DiskOperationResult> SleepAsync(ManagedDisk disk) =>
        RunExclusiveAsync(disk.DiskNumber, () => SleepCoreAsync(disk));

    /// <summary>唤醒：解锁全部卷（对未锁定的卷为无害空操作），移除状态文件记录。</summary>
    public Task<DiskOperationResult> WakeAsync(ManagedDisk disk) =>
        RunExclusiveAsync(disk.DiskNumber, () => WakeCoreAsync(disk));

    private async Task<DiskOperationResult> RunExclusiveAsync(
        int diskNumber,
        Func<Task<DiskOperationResult>> core)
    {
        SemaphoreSlim? semaphore;
        lock (_gate)
        {
            if (!_perDiskLocks.TryGetValue(diskNumber, out semaphore))
            {
                semaphore = new SemaphoreSlim(1, 1);
                _perDiskLocks[diskNumber] = semaphore;
            }
        }

        // 经线程池调度，保证调用方立即拿到 Task；同盘操作在信号量上排队，跨盘互不阻塞。
        return await Task.Run(async () =>
        {
            await semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                return await core().ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        }).ConfigureAwait(false);
    }

    private Task<DiskOperationResult> SleepCoreAsync(ManagedDisk disk)
    {
        var lockedVolumes = new List<string>();
        foreach (var volume in disk.Volumes)
        {
            if (!_ops.LockVolume(volume.VolumePath))
            {
                // 锁定失败：先列出占用者供应用内报告，再回滚此前已锁定的卷。
                var occupants = _ops.GetOccupyingProcesses(volume.VolumePath);
                Rollback(lockedVolumes);
                return Task.FromResult(new DiskOperationResult
                {
                    Success = false,
                    Message = $"卷 {volume.DriveLetter}: 正被其他程序占用，无法锁定。",
                    OccupyingProcesses = occupants,
                });
            }

            lockedVolumes.Add(volume.VolumePath);

            if (!_ops.DismountVolume(volume.VolumePath))
            {
                Rollback(lockedVolumes);
                return Task.FromResult(new DiskOperationResult
                {
                    Success = false,
                    Message = $"卷 {volume.DriveLetter}: 卸载失败。",
                });
            }
        }

        if (!_ops.FlushWriteCache(disk.DiskNumber))
        {
            Rollback(lockedVolumes);
            return Task.FromResult(new DiskOperationResult
            {
                Success = false,
                Message = "写入缓存刷新失败，已取消停转并回滚卷锁。",
            });
        }

        if (!_ops.StandbyImmediate(disk.DiskNumber))
        {
            Rollback(lockedVolumes);
            return Task.FromResult(new DiskOperationResult
            {
                Success = false,
                Message = "停转命令失败，已回滚卷锁。",
            });
        }

        _store.Record(disk.SerialNumber, DiskState.Sleeping);
        SetState(disk.DiskNumber, DiskState.Sleeping);
        return Task.FromResult(new DiskOperationResult { Success = true });
    }

    private Task<DiskOperationResult> WakeCoreAsync(ManagedDisk disk)
    {
        foreach (var volume in disk.Volumes)
        {
            _ops.UnlockVolume(volume.VolumePath);
        }

        _store.Forget(disk.SerialNumber);
        SetState(disk.DiskNumber, DiskState.Online);
        return Task.FromResult(new DiskOperationResult { Success = true });
    }

    /// <summary>回滚：解锁此前成功锁定的卷（逆序）。</summary>
    private void Rollback(List<string> lockedVolumes)
    {
        for (var i = lockedVolumes.Count - 1; i >= 0; i--)
        {
            _ops.UnlockVolume(lockedVolumes[i]);
        }
    }

    private void SetState(int diskNumber, DiskState state)
    {
        lock (_gate)
        {
            _states[diskNumber] = state;
        }

        StateChanged?.Invoke(diskNumber, state);
    }
}
