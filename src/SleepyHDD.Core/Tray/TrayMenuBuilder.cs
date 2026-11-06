using SleepyHDD.Core.StateMachine;
using SleepyHDD.Core.Store;

namespace SleepyHDD.Core.Tray;

/// <summary>
/// 托盘菜单与图标的纯决策逻辑（票 #7，DESIGN.md 4.3）：
/// 每盘快捷切换项（在线↔默认动作，休眠/离线→唤醒，受限禁用）、
/// 全局按钮文案随默认动作、任一磁盘休眠/离线即"睡眠"图标变体。
/// </summary>
public static class TrayMenuBuilder
{
    /// <summary>按磁盘快照顺序生成每盘快捷切换菜单项。</summary>
    public static IReadOnlyList<TrayMenuItemModel> BuildDiskItems(
        IEnumerable<TrayDiskEntry> disks,
        DefaultAction defaultAction)
    {
        var items = new List<TrayMenuItemModel>();
        foreach (var disk in disks)
        {
            if (disk.IsRestricted)
            {
                items.Add(new TrayMenuItemModel($"{disk.Model}（受限）", IsEnabled: false, disk.DiskNumber));
                continue;
            }

            string action = disk.State.ToToggleLabel(defaultAction);
            items.Add(new TrayMenuItemModel($"{disk.Model}：{action}", IsEnabled: true, disk.DiskNumber));
        }

        return items;
    }

    /// <summary>全局休眠按钮文案：跟随默认动作（与主窗口顶栏同一措辞）。</summary>
    public static string SleepAllLabel(DefaultAction defaultAction) =>
        defaultAction.ToAllActionLabel();

    /// <summary>托盘图标变体选择：任一磁盘休眠/离线即"睡眠"变体。</summary>
    public static TrayIconVariant SelectIconVariant(IEnumerable<TrayDiskEntry> disks) =>
        disks.Any(disk => disk.State != DiskState.Online)
            ? TrayIconVariant.Sleeping
            : TrayIconVariant.Normal;
}
