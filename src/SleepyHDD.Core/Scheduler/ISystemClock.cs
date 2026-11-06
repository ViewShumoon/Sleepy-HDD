namespace SleepyHDD.Core.Scheduler;

/// <summary>
/// 可注入时钟抽象：调度器以本地时间计算触发时刻，测试用固定时钟驱动。
/// </summary>
public interface ISystemClock
{
    /// <summary>当前本地时间。</summary>
    DateTime Now { get; }
}
