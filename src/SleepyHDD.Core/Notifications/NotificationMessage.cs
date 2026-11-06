namespace SleepyHDD.Core.Notifications;

/// <summary>
/// 一条托盘通知的文本（标题 + 正文）；由 App 层交给托盘库展示，Core 不依赖通知实现。
/// </summary>
public sealed record NotificationMessage(string Title, string Body);
