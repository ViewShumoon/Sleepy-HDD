using SleepyHDD.Core.StateMachine;

namespace SleepyHDD.Core.Notifications;

/// <summary>
/// 托盘通知文案生成（票 #7，DESIGN.md 4.3）：操作失败附占用进程列表、磁盘插拔提示。
/// 定时触发结果与运行时缺失引导的通知属票 #10/#8 范畴，接入时复用本类的构造方式。
/// </summary>
public static class NotificationMessages
{
    /// <summary>磁盘操作失败通知：正文含失败原因；锁定失败时附占用进程列表（用户故事 9）。</summary>
    public static NotificationMessage OperationFailed(string model, DiskOperationResult result)
    {
        string occupants = result.OccupyingProcesses.Count > 0
            ? $"\n占用进程：{string.Join("、", result.OccupyingProcesses)}"
            : string.Empty;
        return new NotificationMessage($"磁盘操作失败 · {model}", $"{result.Message ?? "未知错误。"}{occupants}");
    }

    /// <summary>受管磁盘接入通知。</summary>
    public static NotificationMessage DiskArrived(string model) =>
        new("磁盘已接入", $"{model} 已加入受管磁盘列表。");

    /// <summary>受管磁盘移除通知。</summary>
    public static NotificationMessage DiskRemoved(string model) =>
        new("磁盘已移除", $"{model} 已从受管磁盘列表移除。");
}
