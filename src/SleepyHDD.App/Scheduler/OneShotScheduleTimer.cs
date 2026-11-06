using SleepyHDD.Core.Scheduler;

namespace SleepyHDD.App.Scheduler;

/// <summary>
/// IScheduleTimer 的生产实现：包装 System.Threading.Timer 的一次性定时器，
/// 到期回调在线程池线程执行；重复 ScheduleOnce 覆盖先前未决的安排。
/// </summary>
public sealed class OneShotScheduleTimer : IScheduleTimer, IDisposable
{
    private readonly object _gate = new();
    private Timer? _timer;

    /// <inheritdoc />
    public void ScheduleOnce(TimeSpan dueTime, Action callback)
    {
        lock (_gate)
        {
            CancelCore();
            _timer = new Timer(
                _ => callback(),
                null,
                dueTime < TimeSpan.Zero ? TimeSpan.Zero : dueTime,
                Timeout.InfiniteTimeSpan);
        }
    }

    /// <inheritdoc />
    public void Cancel()
    {
        lock (_gate)
        {
            CancelCore();
        }
    }

    public void Dispose()
    {
        Cancel();
    }

    private void CancelCore()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
