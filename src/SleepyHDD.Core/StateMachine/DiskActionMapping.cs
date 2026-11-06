using SleepyHDD.Core.Inventory;
using SleepyHDD.Core.Scheduler;
using SleepyHDD.Core.Store;

namespace SleepyHDD.Core.StateMachine;

/// <summary>
/// DefaultAction / ScheduleTargetState / DiskState 的统一映射（唯一真源）：
/// → 展示文案（休眠/离线/唤醒/全部休眠/全部离线，GLOSSARY.md）与 → 状态机操作。
/// 卡片按钮、托盘菜单、顶栏、定时任务与通知文案一律经此取用，避免同一级联散落多处。
/// </summary>
public static class DiskActionMapping
{
    /// <summary>单盘动作文案：休眠/离线。</summary>
    public static string ToActionLabel(this DefaultAction action) =>
        action == DefaultAction.Sleep ? "休眠" : "离线";

    /// <summary>全局动作按钮文案：全部休眠/全部离线。</summary>
    public static string ToAllActionLabel(this DefaultAction action) =>
        action == DefaultAction.Sleep ? "全部休眠" : "全部离线";

    /// <summary>定时目标转入文案：休眠/离线。</summary>
    public static string ToTargetLabel(this ScheduleTargetState target) =>
        target == ScheduleTargetState.Sleep ? "休眠" : "离线";

    /// <summary>单盘切换按钮文案：在线 → 默认动作（休眠/离线），休眠/离线 → 唤醒。</summary>
    public static string ToToggleLabel(this DiskState state, DefaultAction defaultAction) =>
        state == DiskState.Online ? defaultAction.ToActionLabel() : "唤醒";

    /// <summary>在状态机上执行默认动作对应的单盘迁移。</summary>
    public static Task<DiskOperationResult> RunAsync(
        this DefaultAction action, DiskStateMachine machine, ManagedDisk disk) =>
        action == DefaultAction.Sleep ? machine.SleepAsync(disk) : machine.OfflineAsync(disk);

    /// <summary>在状态机上执行默认动作对应的全局迁移。</summary>
    public static Task<IReadOnlyList<DiskOperationOutcome>> RunAllAsync(
        this DefaultAction action, DiskStateMachine machine, IReadOnlyList<ManagedDisk> disks) =>
        action == DefaultAction.Sleep ? machine.SleepAllAsync(disks) : machine.OfflineAllAsync(disks);

    /// <summary>在状态机上执行定时目标对应的单盘迁移。</summary>
    public static Task<DiskOperationResult> RunAsync(
        this ScheduleTargetState target, DiskStateMachine machine, ManagedDisk disk) =>
        target == ScheduleTargetState.Sleep ? machine.SleepAsync(disk) : machine.OfflineAsync(disk);

    /// <summary>在状态机上执行单盘切换：在线 → 默认动作，休眠/离线 → 唤醒。</summary>
    public static Task<DiskOperationResult> RunToggleAsync(
        this DiskState state, DiskStateMachine machine, ManagedDisk disk, DefaultAction defaultAction) =>
        state == DiskState.Online ? defaultAction.RunAsync(machine, disk) : machine.WakeAsync(disk);
}
