namespace SleepyHDD.Core.Scheduler;

/// <summary>
/// 定时策略的星期集合（flags）：策略在所选星期各触发一次。
/// </summary>
[Flags]
public enum DayOfWeekMask
{
    /// <summary>不重复：无触发日，调度器跳过该策略。</summary>
    None = 0,

    /// <summary>周一。</summary>
    Monday = 1,

    /// <summary>周二。</summary>
    Tuesday = 2,

    /// <summary>周三。</summary>
    Wednesday = 4,

    /// <summary>周四。</summary>
    Thursday = 8,

    /// <summary>周五。</summary>
    Friday = 16,

    /// <summary>周六。</summary>
    Saturday = 32,

    /// <summary>周日。</summary>
    Sunday = 64,

    /// <summary>每天。</summary>
    EveryDay = Monday | Tuesday | Wednesday | Thursday | Friday | Saturday | Sunday,
}
