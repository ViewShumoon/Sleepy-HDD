using SleepyHDD.Core.StateMachine;

namespace SleepyHDD.Core.Tray;

/// <summary>
/// 托盘生成菜单项与选择图标变体所需的单盘快照（App 层从磁盘卡片映射而来，Core 不依赖 UI 类型）。
/// </summary>
/// <param name="DiskNumber">物理盘编号，菜单点击回传用于定位对应磁盘。</param>
/// <param name="Model">磁盘型号，作为菜单项文本。</param>
/// <param name="IsRestricted">受限展示态：菜单项禁用、无动作。</param>
/// <param name="State">软件视角状态（受限盘恒为在线）。</param>
public sealed record TrayDiskEntry(
    int DiskNumber,
    string Model,
    bool IsRestricted,
    DiskState State);
