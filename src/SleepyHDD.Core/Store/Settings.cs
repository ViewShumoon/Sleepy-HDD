using SleepyHDD.Core.Scheduler;

namespace SleepyHDD.Core.Store;

/// <summary>
/// 应用设置文档（settings.json）：默认动作与定时策略；开机自启由后续票扩展。
/// </summary>
public sealed record Settings
{
    /// <summary>默认动作；未设置时为休眠。</summary>
    public DefaultAction DefaultAction { get; init; } = DefaultAction.Sleep;

    /// <summary>定时策略列表；旧文件缺失该字段时为空列表。</summary>
    public List<DiskSchedule> Schedules { get; init; } = new();
}
