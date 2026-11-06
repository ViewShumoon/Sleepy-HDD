using SleepyHDD.Core.Scheduler;

namespace SleepyHDD.Core.Tests.Fakes;

/// <summary>
/// IScheduleTimer 的测试替身：记录最近一次安排的到期时长与回调，由测试显式 Fire 触发。
/// </summary>
internal sealed class FakeTimer : IScheduleTimer
{
    public TimeSpan? LastDueTime { get; private set; }

    public int CancelCount { get; private set; }

    private Action? _pending;

    public void ScheduleOnce(TimeSpan dueTime, Action callback)
    {
        LastDueTime = dueTime;
        _pending = callback;
    }

    public void Cancel()
    {
        LastDueTime = null;
        _pending = null;
        CancelCount++;
    }

    /// <summary>模拟定时器到期：执行待决回调（无待决安排时为空操作）。</summary>
    public void Fire()
    {
        var pending = _pending;
        _pending = null;
        LastDueTime = null;
        pending?.Invoke();
    }
}
