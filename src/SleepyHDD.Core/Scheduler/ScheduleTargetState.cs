namespace SleepyHDD.Core.Scheduler;

/// <summary>
/// 定时策略的目标状态（GLOSSARY.md：休眠/离线）；唤醒不在定时策略范围内。
/// </summary>
public enum ScheduleTargetState
{
    /// <summary>转入休眠（停转并拒绝卷访问，盘符仍可见）。</summary>
    Sleep,

    /// <summary>转入离线（在休眠基础上卷从系统完全消失）。</summary>
    Offline,
}
