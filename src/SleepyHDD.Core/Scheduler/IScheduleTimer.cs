namespace SleepyHDD.Core.Scheduler;

/// <summary>
/// 一次性定时器抽象（命名避开 BCL 的 System.Threading.ITimer）：
/// 调度器只安排"最近一次触发"，到期回调在后台线程执行；测试用 fake 手动触发。
/// </summary>
public interface IScheduleTimer
{
    /// <summary>安排一次到期回调；重复调用覆盖先前未决的安排。</summary>
    void ScheduleOnce(TimeSpan dueTime, Action callback);

    /// <summary>取消未决的安排。</summary>
    void Cancel();
}
