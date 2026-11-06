using SleepyHDD.Core.Scheduler;
using SleepyHDD.Core.StateMachine;

namespace SleepyHDD.Core.Notifications;

/// <summary>
/// 托盘通知文案生成（票 #7，DESIGN.md 4.3）：操作失败附占用进程列表、磁盘插拔提示、
/// 定时触发结果（票 #10）。运行时缺失引导的通知属票 #8 范畴，接入时复用本类的构造方式。
/// </summary>
public static class NotificationMessages
{
    /// <summary>磁盘操作失败通知：正文含失败原因；锁定失败时附占用进程列表（用户故事 9）。</summary>
    public static NotificationMessage OperationFailed(string model, DiskOperationResult result)
    {
        return new NotificationMessage($"磁盘操作失败 · {model}", $"{result.Message ?? "未知错误。"}{Occupants(result)}")
        {
            Severity = NotificationSeverity.Warning,
        };
    }

    /// <summary>受管磁盘接入通知。</summary>
    public static NotificationMessage DiskArrived(string model) =>
        new("磁盘已接入", $"{model} 已加入受管磁盘列表。");

    /// <summary>受管磁盘移除通知。</summary>
    public static NotificationMessage DiskRemoved(string model) =>
        new("磁盘已移除", $"{model} 已从受管磁盘列表移除。");

    /// <summary>定时任务完成通知（票 #10，用户故事 16）。</summary>
    public static NotificationMessage ScheduleSucceeded(string model, ScheduleTargetState target) =>
        new(
            "定时任务完成",
            $"{model} 已按计划转入{target.ToTargetLabel()}。");

    /// <summary>定时任务跳过通知：磁盘消失/受限等原因导致本次不执行，不重试。</summary>
    public static NotificationMessage ScheduleSkipped(string diskLabel, string reason) =>
        new($"定时任务已跳过 · {diskLabel}", $"{diskLabel}：{reason}，本次跳过，将于下次计划时间再执行。")
        {
            Severity = NotificationSeverity.Warning,
        };

    /// <summary>定时触发时卷锁定失败：跳过本次并附占用进程列表，不自动重试（票 #10）。</summary>
    public static NotificationMessage ScheduleLockFailed(string model, DiskOperationResult result) =>
        new($"定时任务已跳过 · {model}", $"{result.Message ?? "未知错误。"}{Occupants(result)}")
        {
            Severity = NotificationSeverity.Warning,
        };

    /// <summary>占用进程列表文本；无占用者时为空串。</summary>
    private static string Occupants(DiskOperationResult result) =>
        result.OccupyingProcesses.Count > 0
            ? $"\n占用进程：{string.Join("、", result.OccupyingProcesses)}"
            : string.Empty;
}
