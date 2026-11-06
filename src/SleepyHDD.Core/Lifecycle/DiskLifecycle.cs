using SleepyHDD.Core.Devices;
using SleepyHDD.Core.Inventory;
using SleepyHDD.Core.StateMachine;
using SleepyHDD.Core.Store;

namespace SleepyHDD.Core.Lifecycle;

/// <summary>
/// 磁盘状态与软件生命周期绑定（ADR-0005，DESIGN.md 2.7）：
/// 启动恢复——按序列号匹配状态文件记录（盘号重启后可能变化），为仍存在的离线盘清离线属性
/// （卷由系统自动重新挂载），盘已消失或休眠记录（不跨重启存续）一律静默清理；
/// 清属性失败保留记录待下次启动重试，不向上抛异常、不阻塞应用启动。
/// 退出唤醒——唤醒状态文件在案、由本软件置为休眠/离线的全部磁盘（离线先清属性再解锁卷，
/// 休眠仅解锁卷），未记录的盘一律不碰；在案盘已不在快照中则跳过、保留记录交下次启动恢复。
/// 两条路径都只以状态文件为准，崩溃/被强杀场景由下次启动的启动恢复兜底。
/// </summary>
public sealed class DiskLifecycle
{
    private readonly IDiskDeviceOps _ops;
    private readonly IStateStore _store;

    public DiskLifecycle(IDiskDeviceOps ops, IStateStore store)
    {
        _ops = ops ?? throw new ArgumentNullException(nameof(ops));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// 启动恢复：清障状态文件在案且仍离线的盘，清理消失盘与休眠记录。
    /// 单盘清离线属性失败仅保留记录，不影响其余盘，也不抛异常。
    /// </summary>
    public void RecoverOnStartup()
    {
        var records = _store.Load();
        if (records.Count == 0)
        {
            return;
        }

        // 按序列号匹配当前设备：重启后盘号可能变化，记录里的盘号不可信。
        var diskNumbersBySerial = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var disk in _ops.EnumerateDisks())
        {
            diskNumbersBySerial[disk.SerialNumber] = disk.DiskNumber;
        }

        foreach (var (serialNumber, state) in records)
        {
            if (!diskNumbersBySerial.TryGetValue(serialNumber, out var diskNumber))
            {
                // 盘已消失（拔出等）：静默清理记录。
                _store.Forget(serialNumber);
                continue;
            }

            if (state == DiskState.Sleeping)
            {
                // 休眠不跨重启存续：卷锁随旧进程句柄关闭而消失、停转在重启后自然解除，仅清理记录。
                _store.Forget(serialNumber);
                continue;
            }

            // 离线属性持久：清除后卷由系统自动重新挂载、盘符回来；失败则保留记录，下次启动重试。
            if (_ops.ClearDiskOffline(diskNumber))
            {
                _store.Forget(serialNumber);
            }
        }
    }

    /// <summary>
    /// 退出唤醒：对 <paramref name="disks"/> 快照中状态文件在案的盘执行唤醒
    /// （离线先清离线属性再解锁全部卷，休眠仅解锁全部卷）并移除记录；
    /// 无记录的盘不产生任何设备调用。快照中缺席的在案盘跳过、保留记录。
    /// </summary>
    public void WakeAllForExit(IReadOnlyList<ManagedDisk> disks)
    {
        var records = _store.Load();
        if (records.Count == 0)
        {
            return;
        }

        foreach (var disk in disks)
        {
            if (!records.TryGetValue(disk.SerialNumber, out var state))
            {
                continue; // 非本软件操作的盘：不碰。
            }

            if (state == DiskState.Offline && !_ops.ClearDiskOffline(disk.DiskNumber))
            {
                // 清离线失败：中止该盘唤醒并保留记录，由下次启动恢复兜底。
                continue;
            }

            // 解锁全部卷（对未锁定的卷为无害空操作）；即使遗漏，进程退出关闭句柄亦隐式解锁。
            foreach (var volume in disk.Volumes)
            {
                _ops.UnlockVolume(volume.VolumePath);
            }

            _store.Forget(disk.SerialNumber);
        }
    }
}
