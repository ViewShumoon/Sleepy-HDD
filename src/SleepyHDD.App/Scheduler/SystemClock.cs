using SleepyHDD.Core.Scheduler;

namespace SleepyHDD.App.Scheduler;

/// <summary>
/// ISystemClock 的生产实现：直接取系统本地时间（定时策略按本地时刻触发）。
/// </summary>
public sealed class SystemClock : ISystemClock
{
    /// <inheritdoc />
    public DateTime Now => DateTime.Now;
}
