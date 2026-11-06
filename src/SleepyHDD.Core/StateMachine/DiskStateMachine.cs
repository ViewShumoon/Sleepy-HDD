using SleepyHDD.Core.Devices;
using SleepyHDD.Core.Inventory;
using SleepyHDD.Core.Store;

namespace SleepyHDD.Core.StateMachine;

/// <summary>
/// 转休眠/转离线/唤醒编排（保护模式）：逐卷锁定→卸载，再 CACHE FLUSH (0xE7) + STANDBY IMMEDIATE (0xE0)；
/// 绝不发送 SLEEP (0xE6)。离线在休眠全部步骤之上追加置磁盘离线属性（卷从系统消失），
/// 休眠↔离线互转保持卷锁、只增减离线属性；离线唤醒先清属性（卷自动重新挂载）再解锁。
/// 锁定失败则查询占用进程并回滚已锁卷。同一磁盘的操作经信号量串行，不同磁盘的操作并行执行。
/// 全局操作（全部休眠/离线/唤醒）跳过受限盘、逐盘并行发起，单盘失败不影响其余盘。
/// 软件视角状态以磁盘序列号为键（与状态文件同键，DESIGN.md 2.7）：热插拔后盘号可能重排，
/// 序列号保证状态仍归属同一块盘。
/// </summary>
public sealed class DiskStateMachine
{
    private readonly IDiskDeviceOps _ops;
    private readonly IStateStore _store;
    private readonly object _gate = new();
    private readonly Dictionary<int, SemaphoreSlim> _perDiskLocks = new();
    private readonly Dictionary<string, DiskState> _states = new(StringComparer.Ordinal);

    public DiskStateMachine(IDiskDeviceOps ops, IStateStore store)
    {
        _ops = ops ?? throw new ArgumentNullException(nameof(ops));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>磁盘状态变更事件（磁盘编号, 新状态）；可能在后台线程触发，订阅方需自行编组到 UI 线程。</summary>
    public event Action<int, DiskState>? StateChanged;

    /// <summary>查询磁盘的软件视角状态；未在管理中记录过的默认在线。键与状态文件一致（序列号）。</summary>
    public DiskState GetState(string serialNumber)
    {
        lock (_gate)
        {
            return _states.TryGetValue(serialNumber, out var state) ? state : DiskState.Online;
        }
    }

    /// <summary>转休眠：在线时锁定并卸载全部卷后停转；离线时仅清离线属性、保持卷锁。</summary>
    public Task<DiskOperationResult> SleepAsync(ManagedDisk disk) =>
        RunExclusiveAsync(disk.DiskNumber, () => SleepCoreAsync(disk));

    /// <summary>转离线：在线时执行完整休眠步骤后置离线属性；休眠时保持卷锁、仅置离线属性。</summary>
    public Task<DiskOperationResult> OfflineAsync(ManagedDisk disk) =>
        RunExclusiveAsync(disk.DiskNumber, () => OfflineCoreAsync(disk));

    /// <summary>唤醒：离线时先清离线属性使卷重新挂载，再解锁全部卷（对未锁定的卷为无害空操作），移除状态文件记录。</summary>
    public Task<DiskOperationResult> WakeAsync(ManagedDisk disk) =>
        RunExclusiveAsync(disk.DiskNumber, () => WakeCoreAsync(disk));

    /// <summary>全局休眠：跳过受限盘，其余各盘并行发起、按盘串行排队；单盘失败不影响其余盘。</summary>
    public Task<IReadOnlyList<DiskOperationOutcome>> SleepAllAsync(IReadOnlyList<ManagedDisk> disks) =>
        RunOnAllAsync(disks, SleepAsync);

    /// <summary>全局离线：跳过受限盘，其余各盘并行发起、按盘串行排队；单盘失败不影响其余盘。</summary>
    public Task<IReadOnlyList<DiskOperationOutcome>> OfflineAllAsync(IReadOnlyList<ManagedDisk> disks) =>
        RunOnAllAsync(disks, OfflineAsync);

    /// <summary>全局唤醒：跳过受限盘，其余各盘并行发起、按盘串行排队；单盘失败不影响其余盘。</summary>
    public Task<IReadOnlyList<DiskOperationOutcome>> WakeAllAsync(IReadOnlyList<ManagedDisk> disks) =>
        RunOnAllAsync(disks, WakeAsync);

    /// <summary>
    /// 全局编排：对全部非受限盘并行发起指定转换（跨盘并行、同盘经既有信号量串行），
    /// 汇总每盘结果；任一盘的失败不会中断其余盘的操作。
    /// </summary>
    private static async Task<IReadOnlyList<DiskOperationOutcome>> RunOnAllAsync(
        IReadOnlyList<ManagedDisk> disks,
        Func<ManagedDisk, Task<DiskOperationResult>> operation)
    {
        var tasks = disks
            .Where(disk => !disk.IsRestricted)
            .Select(async disk => new DiskOperationOutcome
            {
                DiskNumber = disk.DiskNumber,
                Result = await operation(disk).ConfigureAwait(false),
            })
            .ToArray();

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

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
        DiskState current = GetState(disk.SerialNumber);
        if (current == DiskState.Offline)
        {
            // 离线→休眠：仅清离线属性、保持卷锁；盘已停转，无需重新停转。
            if (!_ops.ClearDiskOffline(disk.DiskNumber))
            {
                return Task.FromResult(new DiskOperationResult
                {
                    Success = false,
                    Message = "清除离线属性失败，磁盘保持离线。",
                });
            }

            _store.Record(disk.SerialNumber, DiskState.Sleeping);
            SetState(disk, DiskState.Sleeping);
            return Task.FromResult(new DiskOperationResult { Success = true });
        }

        if (current == DiskState.Sleeping)
        {
            return Task.FromResult(new DiskOperationResult { Success = true }); // 幂等：已在休眠。
        }

        var lockedVolumes = new List<string>();
        var spinDownFailure = SpinDown(disk, lockedVolumes);
        if (spinDownFailure is not null)
        {
            return Task.FromResult(spinDownFailure);
        }

        _store.Record(disk.SerialNumber, DiskState.Sleeping);
        SetState(disk, DiskState.Sleeping);
        return Task.FromResult(new DiskOperationResult { Success = true });
    }

    private Task<DiskOperationResult> OfflineCoreAsync(ManagedDisk disk)
    {
        DiskState current = GetState(disk.SerialNumber);
        if (current == DiskState.Offline)
        {
            return Task.FromResult(new DiskOperationResult { Success = true }); // 幂等：已在离线。
        }

        var lockedVolumes = new List<string>();
        if (current == DiskState.Online)
        {
            // 在线→离线 = 完整休眠步骤 + 置离线属性。
            var spinDownFailure = SpinDown(disk, lockedVolumes);
            if (spinDownFailure is not null)
            {
                return Task.FromResult(spinDownFailure);
            }
        }
        // 休眠→离线：保持卷锁、不重复锁定，只追加置离线属性。

        if (!_ops.SetDiskOffline(disk.DiskNumber))
        {
            if (current == DiskState.Online)
            {
                // 刚停转但未成功离线：回滚卷锁，回到在线（下次访问自然起转）。
                Rollback(lockedVolumes);
                return Task.FromResult(new DiskOperationResult
                {
                    Success = false,
                    Message = "置离线属性失败，已回滚卷锁。",
                });
            }

            return Task.FromResult(new DiskOperationResult
            {
                Success = false,
                Message = "置离线属性失败，磁盘保持休眠。",
            });
        }

        _store.Record(disk.SerialNumber, DiskState.Offline);
        SetState(disk, DiskState.Offline);
        return Task.FromResult(new DiskOperationResult { Success = true });
    }

    private Task<DiskOperationResult> WakeCoreAsync(ManagedDisk disk)
    {
        if (GetState(disk.SerialNumber) == DiskState.Offline)
        {
            // 离线→唤醒：先清离线属性，卷由系统自动重新挂载、盘符回来。
            if (!_ops.ClearDiskOffline(disk.DiskNumber))
            {
                return Task.FromResult(new DiskOperationResult
                {
                    Success = false,
                    Message = "清除离线属性失败，磁盘保持离线。",
                });
            }
        }

        foreach (var volume in disk.Volumes)
        {
            _ops.UnlockVolume(volume.VolumePath);
        }

        _store.Forget(disk.SerialNumber);
        SetState(disk, DiskState.Online);
        return Task.FromResult(new DiskOperationResult { Success = true });
    }

    /// <summary>
    /// 停转编排：逐卷锁定→卸载，再 CACHE FLUSH + STANDBY IMMEDIATE；
    /// 失败时回滚已锁定的卷并返回失败结果，成功返回 null。
    /// 已成功锁定的卷记入 <paramref name="lockedVolumes"/>，供调用方在后续步骤失败时回滚。
    /// </summary>
    private DiskOperationResult? SpinDown(ManagedDisk disk, List<string> lockedVolumes)
    {
        foreach (var volume in disk.Volumes)
        {
            if (!_ops.LockVolume(volume.VolumePath))
            {
                // 锁定失败：先列出占用者供应用内报告，再回滚此前已锁定的卷。
                var occupants = _ops.GetOccupyingProcesses(volume.VolumePath);
                Rollback(lockedVolumes);
                return new DiskOperationResult
                {
                    Success = false,
                    Message = $"卷 {volume.DriveLetter}: 正被其他程序占用，无法锁定。",
                    OccupyingProcesses = occupants,
                };
            }

            lockedVolumes.Add(volume.VolumePath);

            if (!_ops.DismountVolume(volume.VolumePath))
            {
                Rollback(lockedVolumes);
                return new DiskOperationResult
                {
                    Success = false,
                    Message = $"卷 {volume.DriveLetter}: 卸载失败。",
                };
            }
        }

        if (!_ops.FlushWriteCache(disk.DiskNumber))
        {
            Rollback(lockedVolumes);
            return new DiskOperationResult
            {
                Success = false,
                Message = "写入缓存刷新失败，已取消停转并回滚卷锁。",
            };
        }

        if (!_ops.StandbyImmediate(disk.DiskNumber))
        {
            Rollback(lockedVolumes);
            return new DiskOperationResult
            {
                Success = false,
                Message = "停转命令失败，已回滚卷锁。",
            };
        }

        return null;
    }

    /// <summary>回滚：解锁此前成功锁定的卷（逆序）。</summary>
    private void Rollback(List<string> lockedVolumes)
    {
        for (var i = lockedVolumes.Count - 1; i >= 0; i--)
        {
            _ops.UnlockVolume(lockedVolumes[i]);
        }
    }

    private void SetState(ManagedDisk disk, DiskState state)
    {
        lock (_gate)
        {
            _states[disk.SerialNumber] = state;
        }

        StateChanged?.Invoke(disk.DiskNumber, state);
    }
}
