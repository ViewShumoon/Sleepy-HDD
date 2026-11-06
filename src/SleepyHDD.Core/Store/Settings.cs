namespace SleepyHDD.Core.Store;

/// <summary>
/// 应用设置文档（settings.json）：含默认动作与开机自启开关，定时策略由后续票扩展。
/// </summary>
public sealed record Settings
{
    /// <summary>默认动作；未设置时为休眠。</summary>
    public DefaultAction DefaultAction { get; init; } = DefaultAction.Sleep;

    /// <summary>开机自动启动开关（票 #11）；未设置时为关。实际注册状态由 AutoStartService 校准。</summary>
    public bool AutoStartEnabled { get; init; }
}
