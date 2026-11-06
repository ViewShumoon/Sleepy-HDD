using SleepyHDD.Core.Devices;
using SleepyHDD.Core.Inventory;
using SleepyHDD.Core.Notifications;
using SleepyHDD.Core.Scheduler;
using SleepyHDD.Core.StateMachine;
using SleepyHDD.Core.Store;
using SleepyHDD.Core.Tests.Fakes;

namespace SleepyHDD.Core.Tests.Scheduler;

/// <summary>
/// 定时调度编排（票 #10，DESIGN.md 2.6）：经 fake 时钟/定时器/设备驱动触发判定——
/// 触发执行与手动一致的 DiskStateMachine 流程；锁定失败或磁盘消失/受限时跳过本次并通知、
/// 不自动重试，仅按策略重排下一次触发。
/// </summary>
public class DiskSchedulerTests
{
    private const string VolD = @"\\?\Volume{dd-ddata}\";

    // 2026-11-06 为周五。
    private static readonly DateTime FridayMorning = new(2026, 11, 6, 10, 0, 0);
    private static readonly DateTime FridayNight = new(2026, 11, 6, 23, 30, 0);

    [Fact]
    public void Start_EverydaySchedule_SchedulesRemainingTimeToday()
    {
        var (scheduler, _, _, clock, timer, _) = Create(
            Schedule(DayOfWeekMask.EveryDay, new TimeOnly(23, 30)));

        scheduler.Start();

        Assert.Equal(FridayNight - clock.Now, timer.LastDueTime);
    }

    [Fact]
    public void Start_NoSchedules_CancelsTimer()
    {
        var (scheduler, _, _, _, timer, _) = Create();

        scheduler.Start();

        Assert.Null(timer.LastDueTime);
        Assert.Equal(1, timer.CancelCount);
    }

    [Fact]
    public void Fire_Success_RunsManualFlowNotifiesAndReschedulesNextDay()
    {
        var (scheduler, fake, _, clock, timer, notices) = Create(
            Schedule(DayOfWeekMask.EveryDay, new TimeOnly(23, 30)));
        scheduler.Start();
        clock.Now = FridayNight;

        timer.Fire();

        Assert.Equal(
            new[] { $"lock:{VolD}", $"dismount:{VolD}", "flush:1", "standby:1" },
            fake.Calls);
        Assert.Single(notices, m => m.Title == "定时任务完成" && m.Body.Contains("转入休眠"));
        Assert.Equal(TimeSpan.FromDays(1), timer.LastDueTime);
    }

    [Fact]
    public void Fire_TargetOffline_ExecutesOfflineFlow()
    {
        var (scheduler, fake, _, clock, timer, notices) = Create(
            Schedule(DayOfWeekMask.EveryDay, new TimeOnly(23, 30), ScheduleTargetState.Offline));
        scheduler.Start();
        clock.Now = FridayNight;

        timer.Fire();

        Assert.Contains("set-offline:1", fake.Calls);
        Assert.Single(notices, m => m.Body.Contains("转入离线"));
    }

    [Fact]
    public void Fire_LockFailure_SkipsNotifiesWithOccupants_NoRetry_ReschedulesNextWeek()
    {
        var (scheduler, fake, _, clock, timer, notices) = Create(
            Schedule(DayOfWeekMask.Friday, new TimeOnly(23, 30)));
        fake.LockFailures.Add(VolD);
        fake.OccupantsByVolume[VolD] = new List<string> { "SearchIndexer.exe" };
        scheduler.Start();
        clock.Now = FridayNight;

        timer.Fire();

        // 锁定失败：只执行一次完整流程（查占用者；首卷即失败，无已锁卷可回滚），绝不重试。
        Assert.Equal(
            new[] { $"lock:{VolD}", $"occupants:{VolD}" },
            fake.Calls);
        Assert.Single(notices, m => m.Title.StartsWith("定时任务已跳过", StringComparison.Ordinal));
        Assert.Contains("SearchIndexer.exe", notices.Single().Body);
        // 策略仅周五：下次触发顺延一周，不因失败补跑。
        Assert.Equal(TimeSpan.FromDays(7), timer.LastDueTime);
    }

    [Fact]
    public void Fire_DiskMissing_NotifiesSkipped_TouchesNoDevice()
    {
        var (scheduler, fake, settings, clock, timer, notices) = Create(
            Schedule(DayOfWeekMask.EveryDay, new TimeOnly(23, 30), serial: "GONE"));
        scheduler.Start();
        clock.Now = FridayNight;

        timer.Fire();

        Assert.Empty(fake.Calls);
        Assert.Single(notices, m => m.Title.StartsWith("定时任务已跳过", StringComparison.Ordinal));
        Assert.Contains("磁盘未接入", notices.Single().Body);
        // 策略保留，仍按原计划重排（下次成功接入时生效）。
        Assert.Equal(TimeSpan.FromDays(1), timer.LastDueTime);
    }

    [Fact]
    public void Fire_RestrictedDisk_NotifiesSkipped_TouchesNoDevice()
    {
        var (scheduler, fake, _, clock, timer, notices) = Create(
            Schedule(DayOfWeekMask.EveryDay, new TimeOnly(23, 30)));
        fake.AtaProbes[1] = false; // pass-through 不可用 → 受限
        scheduler.Start();
        clock.Now = FridayNight;

        timer.Fire();

        Assert.Empty(fake.Calls);
        Assert.Single(notices, m => m.Title.StartsWith("定时任务已跳过", StringComparison.Ordinal));
        Assert.Contains("受限", notices.Single().Body);
    }

    [Fact]
    public void Reload_RecomputesEarliestOccurrence()
    {
        var (scheduler, _, settings, clock, timer, _) = Create(
            Schedule(DayOfWeekMask.EveryDay, new TimeOnly(23, 30)));
        scheduler.Start();

        settings.Current = new Settings
        {
            Schedules = new List<DiskSchedule>
            {
                settings.Current.Schedules[0],
                Schedule(DayOfWeekMask.EveryDay, new TimeOnly(11, 0)),
            },
        };
        scheduler.Reload();

        Assert.Equal(new TimeSpan(1, 0, 0), timer.LastDueTime);
    }

    private static DiskSchedule Schedule(
        DayOfWeekMask days,
        TimeOnly at,
        ScheduleTargetState target = ScheduleTargetState.Sleep,
        string serial = "HDD1") => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            SerialNumber = serial,
            At = at,
            Days = days,
            Target = target,
        };

    /// <summary>组装被测调度器：fake 设备 + 真状态机 + 真清单 + fake 时钟/定时器/设置存储。</summary>
    private static (
        DiskScheduler Scheduler,
        FakeDiskDeviceOps Fake,
        FakeSettingsStore Settings,
        FakeClock Clock,
        FakeTimer Timer,
        List<NotificationMessage> Notices) Create(params DiskSchedule[] schedules)
    {
        var fake = new FakeDiskDeviceOps();
        fake.Disks.Add(new DiskDescriptor
        {
            DiskNumber = 1,
            BusType = DiskBusType.Sata,
            Model = "ST1000DM010",
            SerialNumber = "HDD1",
            SizeBytes = 1_000_000_000_000,
        });
        fake.Penalties[1] = true;
        fake.Volumes.Add(new VolumeDescriptor
        {
            VolumePath = VolD,
            DiskNumber = 1,
            DriveLetter = "D",
            Label = "Data",
            TotalBytes = 500_000_000_000,
            FreeBytes = 100_000_000_000,
        });

        var settings = new FakeSettingsStore
        {
            Current = new Settings { Schedules = schedules.ToList() },
        };
        var clock = new FakeClock { Now = FridayMorning };
        var timer = new FakeTimer();
        var notices = new List<NotificationMessage>();

        var machine = new DiskStateMachine(fake, new FakeStateStore());
        var scheduler = new DiskScheduler(
            settings, new DiskInventory(fake), machine, clock, timer);
        scheduler.Notified += notices.Add;

        return (scheduler, fake, settings, clock, timer, notices);
    }
}
