using SleepyHDD.Core.Scheduler;

namespace SleepyHDD.Core.Store;

/// <summary>
/// 应用设置文档（settings.json）：默认动作、开机自启开关与定时策略。
/// </summary>
public sealed record Settings
{
    /// <summary>默认动作；未设置时为休眠。</summary>
    public DefaultAction DefaultAction { get; init; } = DefaultAction.Sleep;

    /// <summary>开机自动启动开关（票 #11）；未设置时为关。实际注册状态由 AutoStartService 校准。</summary>
    public bool AutoStartEnabled { get; init; }

    /// <summary>定时策略列表；旧文件缺失该字段时为空列表。</summary>
    public List<DiskSchedule> Schedules { get; init; } = new();
}
