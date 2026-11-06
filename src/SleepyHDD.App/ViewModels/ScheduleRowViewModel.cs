using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SleepyHDD.Core.Inventory;
using SleepyHDD.Core.Scheduler;
using SleepyHDD.Core.StateMachine;

// MVVMTK0045 推荐的 partial property 需 C# 14（.NET 9），本项目目标 net8.0-windows，只能沿用字段。
#pragma warning disable MVVMTK0045

namespace SleepyHDD.App.ViewModels;

/// <summary>
/// 定时策略列表行（票 #10，DESIGN.md 4.2）：磁盘、时刻、星期、目标状态的展示文本；
/// 每次磁盘刷新后经 <see cref="UpdateProblem"/> 标记异常状态（磁盘缺失/受限）。
/// </summary>
public sealed partial class ScheduleRowViewModel : ObservableObject
{
    private static readonly (DayOfWeekMask Mask, string Name)[] WeekdayOrder =
    {
        (DayOfWeekMask.Monday, "周一"),
        (DayOfWeekMask.Tuesday, "周二"),
        (DayOfWeekMask.Wednesday, "周三"),
        (DayOfWeekMask.Thursday, "周四"),
        (DayOfWeekMask.Friday, "周五"),
        (DayOfWeekMask.Saturday, "周六"),
        (DayOfWeekMask.Sunday, "周日"),
    };

    public ScheduleRowViewModel(DiskSchedule schedule)
    {
        Schedule = schedule;
        DiskLabel = schedule.SerialNumber;
        TimeText = schedule.At.ToString("HH:mm");
        DaysText = FormatDays(schedule.Days);
        TargetText = schedule.Target.ToTargetLabel();
    }

    /// <summary>持久化的策略本体。</summary>
    public DiskSchedule Schedule { get; }

    /// <summary>本行删除命令；由 SettingsViewModel 创建行时挂接（模板内 ElementName 受名称作用域限制不可用）。</summary>
    public IRelayCommand? DeleteCommand { get; internal set; }

    /// <summary>磁盘展示名：在线受管盘用型号，磁盘缺失时退回序列号。</summary>
    [ObservableProperty]
    private string _diskLabel;

    /// <summary>触发时刻文本，如 "23:30"。</summary>
    public string TimeText { get; }

    /// <summary>星期集合文本，如 "周一、周五"。</summary>
    public string DaysText { get; }

    /// <summary>目标状态文本：休眠/离线。</summary>
    public string TargetText { get; }

    /// <summary>异常状态文本（磁盘缺失/受限）；空则正常。</summary>
    [ObservableProperty]
    private string _problemText = string.Empty;

    /// <summary>有异常状态时行内显示红字。</summary>
    public bool HasProblem => ProblemText.Length > 0;

    partial void OnProblemTextChanged(string value) => OnPropertyChanged(nameof(HasProblem));

    /// <summary>按最新磁盘快照刷新异常状态；null 表示序列号对应的盘已消失。</summary>
    public void UpdateProblem(ManagedDisk? disk)
    {
        if (disk is null)
        {
            DiskLabel = Schedule.SerialNumber;
            ProblemText = "磁盘缺失";
            return;
        }

        DiskLabel = disk.Model.Length > 0 ? disk.Model : disk.SerialNumber;
        ProblemText = disk.IsRestricted ? "受限" : string.Empty;
    }

    private static string FormatDays(DayOfWeekMask days) =>
        string.Join("、", WeekdayOrder.Where(w => days.HasFlag(w.Mask)).Select(w => w.Name));
}
