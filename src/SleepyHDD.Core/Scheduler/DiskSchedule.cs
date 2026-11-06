namespace SleepyHDD.Core.Scheduler;

/// <summary>
/// 定时策略（GLOSSARY.md）：在指定星期集合的指定时刻，把指定磁盘转入指定状态。
/// 按磁盘序列号绑定身份（DESIGN.md 2.1），不受盘符/盘号变化影响；持久化于 settings.json。
/// </summary>
public sealed record DiskSchedule
{
    /// <summary>策略标识（GUID），供 UI 编辑/删除定位。</summary>
    public required string Id { get; init; }

    /// <summary>目标磁盘的序列号。</summary>
    public required string SerialNumber { get; init; }

    /// <summary>触发时刻（本地时间）。</summary>
    public required TimeOnly At { get; init; }

    /// <summary>触发的星期集合。</summary>
    public required DayOfWeekMask Days { get; init; }

    /// <summary>触发后要把磁盘转入的目标状态。</summary>
    public required ScheduleTargetState Target { get; init; }
}
