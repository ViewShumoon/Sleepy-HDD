using SleepyHDD.Core.Scheduler;

namespace SleepyHDD.Core.Tests.Scheduler;

/// <summary>
/// 下次触发时刻纯函数（票 #10，DESIGN.md 第 5 节）：星期集合 + 时刻 → 下次触发时间，
/// 覆盖跨天、跨周、恰好等于当前时刻与闰日边界。
/// </summary>
public class ScheduleCalculatorTests
{
    private static DiskSchedule Schedule(DayOfWeekMask days, int hour, int minute) => new()
    {
        Id = "s1",
        SerialNumber = "HDD1",
        At = new TimeOnly(hour, minute),
        Days = days,
        Target = ScheduleTargetState.Sleep,
    };

    [Fact]
    public void Today_MatchesAndTimeAhead_ReturnsToday()
    {
        // 2026-11-06 是周五。
        var from = new DateTime(2026, 11, 6, 10, 0, 0);

        var next = ScheduleCalculator.NextOccurrence(Schedule(DayOfWeekMask.Friday, 23, 30), from);

        Assert.Equal(new DateTime(2026, 11, 6, 23, 30, 0), next);
    }

    [Fact]
    public void Today_MatchesButTimePassed_ReturnsSameWeekdayNextWeek()
    {
        var from = new DateTime(2026, 11, 6, 10, 0, 0); // 周五 10:00

        var next = ScheduleCalculator.NextOccurrence(Schedule(DayOfWeekMask.Friday, 9, 0), from);

        Assert.Equal(new DateTime(2026, 11, 13, 9, 0, 0), next);
    }

    [Fact]
    public void CrossDay_NextWeekdayMatches_ReturnsTomorrow()
    {
        var from = new DateTime(2026, 11, 6, 23, 50, 0); // 周五深夜

        var next = ScheduleCalculator.NextOccurrence(Schedule(DayOfWeekMask.Saturday, 0, 10), from);

        Assert.Equal(new DateTime(2026, 11, 7, 0, 10, 0), next);
    }

    [Fact]
    public void ExactTimeEqualsNow_ConsideredPassed()
    {
        var from = new DateTime(2026, 11, 6, 23, 30, 0);

        var next = ScheduleCalculator.NextOccurrence(Schedule(DayOfWeekMask.EveryDay, 23, 30), from);

        Assert.Equal(new DateTime(2026, 11, 7, 23, 30, 0), next);
    }

    [Fact]
    public void LeapDay_Feb29NightToMar1_CrossesYearBoundaryCorrectly()
    {
        // 2028-02-29 是闰日（周二）。
        var from = new DateTime(2028, 2, 29, 23, 0, 0);

        var next = ScheduleCalculator.NextOccurrence(Schedule(DayOfWeekMask.Wednesday, 0, 30), from);

        Assert.Equal(new DateTime(2028, 3, 1, 0, 30, 0), next);
    }

    [Fact]
    public void LeapDay_Feb28ToFeb29_LandsOnLeapDay()
    {
        var from = new DateTime(2028, 2, 28, 10, 0, 0); // 周一

        var next = ScheduleCalculator.NextOccurrence(Schedule(DayOfWeekMask.Tuesday, 8, 0), from);

        Assert.Equal(new DateTime(2028, 2, 29, 8, 0, 0), next);
    }

    [Fact]
    public void NoWeekdaySelected_ReturnsNull()
        => Assert.Null(ScheduleCalculator.NextOccurrence(Schedule(DayOfWeekMask.None, 23, 30), new DateTime(2026, 11, 6)));

    [Fact]
    public void WeekendMask_FromThursday_ReturnsSaturday()
    {
        var from = new DateTime(2026, 11, 5, 12, 0, 0); // 周四

        var next = ScheduleCalculator.NextOccurrence(
            Schedule(DayOfWeekMask.Saturday | DayOfWeekMask.Sunday, 2, 0), from);

        Assert.Equal(new DateTime(2026, 11, 7, 2, 0, 0), next);
    }

    [Fact]
    public void SundayMask_FromSaturday_ReturnsSunday()
    {
        var from = new DateTime(2026, 11, 7, 10, 0, 0); // 周六

        var next = ScheduleCalculator.NextOccurrence(Schedule(DayOfWeekMask.Sunday, 8, 0), from);

        Assert.Equal(new DateTime(2026, 11, 8, 8, 0, 0), next);
    }
}
