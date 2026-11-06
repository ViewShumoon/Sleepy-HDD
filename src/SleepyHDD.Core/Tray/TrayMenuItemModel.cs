namespace SleepyHDD.Core.Tray;

/// <summary>
/// 托盘每盘快捷切换菜单项的描述模型：文本 + 可用性 + 关联盘号（App 层据此生成真实菜单控件）。
/// </summary>
/// <param name="Text">菜单项文案（含动作，如 "HDD1：休眠"、"HDD2：唤醒"、"HDD3（受限）"）。</param>
/// <param name="IsEnabled">受限盘为 false（无动作，DESIGN.md 4.1 同一规则）。</param>
/// <param name="DiskNumber">关联的物理盘编号，点击时路由回对应磁盘。</param>
public sealed record TrayMenuItemModel(string Text, bool IsEnabled, int DiskNumber);
