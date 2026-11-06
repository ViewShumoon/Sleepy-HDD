using SleepyHDD.Core.Inventory;
using SleepyHDD.Core.Notifications;
using SleepyHDD.Core.StateMachine;
using SleepyHDD.Core.Store;

namespace SleepyHDD.Core.Scheduler;

/// <summary>
/// 进程内定时调度器（票 #10，DESIGN.md 2.6）：只维护"最近一次触发"的单个定时器，
/// 到期后执行全部到点策略——按序列号定位磁盘，复用 DiskStateMachine 与手动完全相同的转换流程；
/// 磁盘消失/受限/锁定失败均跳过本次并通知，不重试，仅按策略重排下一次触发。
/// 策略变更后调用 <see cref="Reload"/> 重算。
/// </summary>
public sealed class DiskScheduler : IDisposable
{
    /// <summary>到期判定回看窗口：容忍定时器回调相对计划时刻的轻微迟到（线程调度抖动）。</summary>
    private static readonly TimeSpan MissLookback = TimeSpan.FromMinutes(1);

    private readonly ISettingsStore _settings;
    private readonly DiskInventory _inventory;
    private readonly DiskStateMachine _machine;
    private readonly ISystemClock _clock;
    private readonly IScheduleTimer _timer;
    private bool _disposed;

    public DiskScheduler(
        ISettingsStore settings,
        DiskInventory inventory,
        DiskStateMachine machine,
        ISystemClock clock,
        IScheduleTimer timer)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _machine = machine ?? throw new ArgumentNullException(nameof(machine));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _timer = timer ?? throw new ArgumentNullException(nameof(timer));
    }

    /// <summary>托盘通知事件；可能在定时器后台线程触发，订阅方需自行编组。</summary>
    public event Action<NotificationMessage>? Notified;

    /// <summary>启动调度：读取策略并安排最近一次触发。</summary>
    public void Start() => Reschedule();

    /// <summary>策略集合变更后的重排：取消当前安排，按最新策略重新计算。</summary>
    public void Reload() => Reschedule();

    public void Dispose()
    {
        _disposed = true;
        _timer.Cancel();
    }

    private void Reschedule()
    {
        if (_disposed)
        {
            return;
        }

        var now = _clock.Now;
        DateTime? earliest = null;
        foreach (var schedule in _settings.Load().Schedules)
        {
            var next = ScheduleCalculator.NextOccurrence(schedule, now);
            if (next is { } candidate && (earliest is null || candidate < earliest))
            {
                earliest = candidate;
            }
        }

        if (earliest is null)
        {
            _timer.Cancel();
            return;
        }

        _timer.ScheduleOnce(earliest.Value - now, OnTimer);
    }

    private void OnTimer()
    {
        if (_disposed)
        {
            return;
        }

        var now = _clock.Now;
        foreach (var schedule in _settings.Load().Schedules)
        {
            // 回看一个窗口：把"本应在刚刚到期"的策略判为到期。
            var next = ScheduleCalculator.NextOccurrence(schedule, now - MissLookback);
            if (next is { } due && due <= now)
            {
                Fire(schedule);
            }
        }

        Reschedule();
    }

    /// <summary>执行单条策略：与手动完全相同的状态机流程；任何失败只通知、不重试。</summary>
    private void Fire(DiskSchedule schedule)
    {
        var disk = _inventory.EnumerateManagedDisks()
            .FirstOrDefault(d => string.Equals(d.SerialNumber, schedule.SerialNumber, StringComparison.Ordinal));

        if (disk is null)
        {
            Notified?.Invoke(NotificationMessages.ScheduleSkipped(
                schedule.SerialNumber, "磁盘未接入或不可管理"));
            return;
        }

        if (disk.IsRestricted)
        {
            Notified?.Invoke(NotificationMessages.ScheduleSkipped(disk.Model, "磁盘受限，无法操作"));
            return;
        }

        // 状态机经信号量保证同盘串行：与用户手动操作并发时自动排队，流程一致。
        var result = schedule.Target.RunAsync(_machine, disk).GetAwaiter().GetResult();

        Notified?.Invoke(result.Success
            ? NotificationMessages.ScheduleSucceeded(disk.Model, schedule.Target)
            : NotificationMessages.ScheduleLockFailed(disk.Model, result));
    }
}
