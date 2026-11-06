namespace SleepyHDD.Core.Store;

/// <summary>
/// 应用设置文档（settings.json）：本票仅含默认动作，开机自启与定时策略由后续票扩展。
/// </summary>
public sealed record Settings
{
    /// <summary>默认动作；未设置时为休眠。</summary>
    public DefaultAction DefaultAction { get; init; } = DefaultAction.Sleep;
}
