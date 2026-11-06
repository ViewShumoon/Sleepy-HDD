namespace SleepyHDD.Core.Notifications;

/// <summary>
/// 一条托盘通知的文本（标题 + 正文 + 级别）；由 App 层交给托盘库展示，Core 不依赖通知实现。
/// </summary>
public sealed record NotificationMessage(string Title, string Body)
{
    /// <summary>通知级别，托盘据此选择图标；默认信息级。</summary>
    public NotificationSeverity Severity { get; init; } = NotificationSeverity.Info;
}

/// <summary>托盘通知级别。</summary>
public enum NotificationSeverity
{
    /// <summary>信息（如插拔提示、定时任务完成）。</summary>
    Info,

    /// <summary>警告（如操作失败、定时任务被跳过）。</summary>
    Warning,
}
