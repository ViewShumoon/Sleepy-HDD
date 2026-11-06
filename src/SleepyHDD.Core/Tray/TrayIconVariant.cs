namespace SleepyHDD.Core.Tray;

/// <summary>
/// 托盘图标变体（DESIGN.md 4.3）：常态与"睡眠"变体，由 App 层映射到具体图标资源。
/// </summary>
public enum TrayIconVariant
{
    /// <summary>没有受管磁盘处于休眠/离线。</summary>
    Normal,

    /// <summary>任一磁盘处于休眠/离线，显示"睡眠"变体。</summary>
    Sleeping,
}
