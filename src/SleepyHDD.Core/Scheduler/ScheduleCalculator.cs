namespace SleepyHDD.Core.Scheduler;

/// <summary>
/// 下次触发时刻纯函数（DESIGN.md 第 5 节可测重点）：由策略的星期集合与时刻，
/// 计算严格晚于参考时刻的下一次本地触发时间；跨天/跨周/闰日由 DateTime 加法自然处理。
/// </summary>
public static class ScheduleCalculator
{
    /// <summary>
    /// 计算 <paramref name="schedule"/> 在 <paramref name="from"/> 之后的下次触发时刻；
    /// 星期集合为空时返回 null。恰好等于 <paramref name="from"/> 视为已过（避免同一时刻重复触发）。
    /// </summary>
    public static DateTime? NextOccurrence(DiskSchedule schedule, DateTime from)
    {
        if (schedule.Days == DayOfWeekMask.None)
        {
            return null;
        }

        // 8 天窗口（今天起算，含下周期同日）必覆盖全部星期；Days 非空时循环内必返回。
        for (var offset = 0; offset <= 7; offset++)
        {
            var day = from.Date.AddDays(offset);
            if (!schedule.Days.HasFlag(ToMask(day.DayOfWeek)))
            {
                continue;
            }

            var candidate = day + schedule.At.ToTimeSpan();
            if (candidate > from)
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>DayOfWeek（周日=0 起）到星期掩码位的映射。</summary>
    private static DayOfWeekMask ToMask(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => DayOfWeekMask.Monday,
        DayOfWeek.Tuesday => DayOfWeekMask.Tuesday,
        DayOfWeek.Wednesday => DayOfWeekMask.Wednesday,
        DayOfWeek.Thursday => DayOfWeekMask.Thursday,
        DayOfWeek.Friday => DayOfWeekMask.Friday,
        DayOfWeek.Saturday => DayOfWeekMask.Saturday,
        DayOfWeek.Sunday => DayOfWeekMask.Sunday,
        _ => DayOfWeekMask.None,
    };
}
