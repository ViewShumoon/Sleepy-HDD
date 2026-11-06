using SleepyHDD.Core.Inventory;

namespace SleepyHDD.Core.Notifications;

/// <summary>
/// 磁盘插拔差异计算：比较两次受管磁盘枚举结果，按序列号匹配得出新增/移除，
/// 供托盘生成"磁盘已接入/已移除"通知（票 #7；序列号匹配不受盘符/盘号变化影响，同 ADR-0005）。
/// </summary>
public static class DiskChangeDiff
{
    public static (IReadOnlyList<ManagedDisk> Added, IReadOnlyList<ManagedDisk> Removed) Compute(
        IReadOnlyList<ManagedDisk> previous,
        IReadOnlyList<ManagedDisk> current)
    {
        var previousSerials = new HashSet<string>(previous.Select(disk => disk.SerialNumber), StringComparer.Ordinal);
        var currentSerials = new HashSet<string>(current.Select(disk => disk.SerialNumber), StringComparer.Ordinal);

        var added = current.Where(disk => !previousSerials.Contains(disk.SerialNumber)).ToList();
        var removed = previous.Where(disk => !currentSerials.Contains(disk.SerialNumber)).ToList();
        return (added, removed);
    }
}
