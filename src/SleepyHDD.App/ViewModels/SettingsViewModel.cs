using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SleepyHDD.Core.AutoStart;
using SleepyHDD.Core.Inventory;
using SleepyHDD.Core.Scheduler;
using SleepyHDD.Core.Store;

// MVVMTK0045 推荐的 partial property 需 C# 14（.NET 9），本项目目标 net8.0-windows，只能沿用字段。
#pragma warning disable MVVMTK0045

namespace SleepyHDD.App.ViewModels;

/// <summary>
/// 设置视图模型：默认动作单选（休眠/离线，GLOSSARY.md）、开机自启开关（票 #11）与定时策略列表（票 #10，DESIGN.md 4.2），
/// 变更即持久化到 settings.json 并广播 <see cref="SchedulesChanged"/> 供调度器重排；
/// 由主窗口顶栏、磁盘卡片与设置页共享同一实例。
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _store;
    private readonly AutoStartService _autoStart;
    private bool _revertingAutoStart;
    private IReadOnlyList<ManagedDisk> _availableDisks = Array.Empty<ManagedDisk>();

    public SettingsViewModel(ISettingsStore store, AutoStartService autoStart)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _autoStart = autoStart ?? throw new ArgumentNullException(nameof(autoStart));
        var loaded = store.Load();
        _defaultAction = loaded.DefaultAction;
        _autoStartEnabled = loaded.AutoStartEnabled;
        foreach (var schedule in loaded.Schedules)
        {
            AddRow(schedule);
        }
    }

    /// <summary>创建策略行并挂接删除命令（模板内 ElementName 受名称作用域限制，命令随行提供）。</summary>
    private ScheduleRowViewModel AddRow(DiskSchedule schedule)
    {
        var row = new ScheduleRowViewModel(schedule);
        row.DeleteCommand = new RelayCommand(() => DeleteSchedule(row));
        Schedules.Add(row);
        return row;
    }

    /// <summary>默认动作；设置即写入 settings.json，重启后保留。</summary>
    [ObservableProperty]
    private DefaultAction _defaultAction;

    partial void OnDefaultActionChanged(DefaultAction value)
    {
        // 读-改-写保留其他字段（开机自启开关、定时策略），避免整体覆盖。
        _store.Save(_store.Load() with { DefaultAction = value });
        OnPropertyChanged(nameof(DefaultActionIndex));
        OnPropertyChanged(nameof(DefaultActionName));
    }

    /// <summary>单选控件索引：0=休眠、1=离线。</summary>
    public int DefaultActionIndex
    {
        get => (int)DefaultAction;
        set => DefaultAction = (DefaultAction)value;
    }

    /// <summary>默认动作文案：休眠/离线（磁盘卡片动作按钮文本用）。</summary>
    public string DefaultActionName => DefaultAction == DefaultAction.Sleep ? "休眠" : "离线";

    /// <summary>
    /// 开机自动启动开关（票 #11，默认关）：开→注册任务计划程序任务（登录触发 + 最高权限），
    /// 关→注销；注册/注销或写回失败时开关拨回原状态，不留下与实际不符的设置。
    /// </summary>
    [ObservableProperty]
    private bool _autoStartEnabled;

    partial void OnAutoStartEnabledChanged(bool value)
    {
        if (_revertingAutoStart)
        {
            return;
        }

        bool persisted;
        try
        {
            persisted = _autoStart.SetEnabled(value);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            persisted = false;
        }

        if (!persisted)
        {
            _revertingAutoStart = true;
            AutoStartEnabled = !value;
            _revertingAutoStart = false;
        }
    }

    /// <summary>定时策略列表行。</summary>
    public ObservableCollection<ScheduleRowViewModel> Schedules { get; } = new();

    /// <summary>策略集合变更事件：组合根据此调用 DiskScheduler.Reload 重排触发。</summary>
    public event Action? SchedulesChanged;

    /// <summary>按最新磁盘快照刷新各行的异常状态（磁盘缺失/受限）与对话框候选磁盘。</summary>
    public void UpdateDisks(IReadOnlyList<ManagedDisk> disks)
    {
        _availableDisks = disks;
        foreach (var row in Schedules)
        {
            row.UpdateProblem(FindDisk(row.Schedule.SerialNumber));
        }

        OnPropertyChanged(nameof(DialogDisks));
    }

    // ---- 新增策略对话框编辑态（ContentDialog 绑定；由 code-behind 打开并提交） ----

    /// <summary>对话框候选磁盘：仅当前非受限的受管盘。</summary>
    public IReadOnlyList<DiskOption> DialogDisks =>
        _availableDisks
            .Where(disk => !disk.IsRestricted)
            .Select(disk => new DiskOption(disk.SerialNumber, $"{disk.Model} · {disk.SerialNumber}"))
            .ToList();

    /// <summary>对话框选中的磁盘；null 表示未选。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAddSchedule))]
    private DiskOption? _dialogDisk;

    /// <summary>对话框触发时刻（TimePicker 绑定，本地时间）。</summary>
    [ObservableProperty]
    private TimeSpan _dialogTime = new(23, 30, 0);

    /// <summary>对话框星期多选：周一。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAddSchedule))]
    private bool _isMonday = true;

    /// <summary>对话框星期多选：周二。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAddSchedule))]
    private bool _isTuesday = true;

    /// <summary>对话框星期多选：周三。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAddSchedule))]
    private bool _isWednesday = true;

    /// <summary>对话框星期多选：周四。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAddSchedule))]
    private bool _isThursday = true;

    /// <summary>对话框星期多选：周五。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAddSchedule))]
    private bool _isFriday = true;

    /// <summary>对话框星期多选：周六。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAddSchedule))]
    private bool _isSaturday = true;

    /// <summary>对话框星期多选：周日。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAddSchedule))]
    private bool _isSunday = true;

    /// <summary>对话框目标状态单选索引：0=休眠、1=离线。</summary>
    [ObservableProperty]
    private int _dialogTargetIndex;

    /// <summary>已勾选的星期集合。</summary>
    public DayOfWeekMask SelectedDays
    {
        get
        {
            var mask = DayOfWeekMask.None;
            if (IsMonday) mask |= DayOfWeekMask.Monday;
            if (IsTuesday) mask |= DayOfWeekMask.Tuesday;
            if (IsWednesday) mask |= DayOfWeekMask.Wednesday;
            if (IsThursday) mask |= DayOfWeekMask.Thursday;
            if (IsFriday) mask |= DayOfWeekMask.Friday;
            if (IsSaturday) mask |= DayOfWeekMask.Saturday;
            if (IsSunday) mask |= DayOfWeekMask.Sunday;
            return mask;
        }
    }

    /// <summary>对话框可提交：已选磁盘且至少勾选一天。</summary>
    public bool CanAddSchedule => DialogDisk is not null && SelectedDays != DayOfWeekMask.None;

    /// <summary>打开对话框前重置编辑态：默认每天 23:30 转入休眠。</summary>
    public void ResetDialog()
    {
        DialogDisk = DialogDisks.FirstOrDefault();
        DialogTime = new TimeSpan(23, 30, 0);
        IsMonday = IsTuesday = IsWednesday = IsThursday = IsFriday = IsSaturday = IsSunday = true;
        DialogTargetIndex = 0;
        OnPropertyChanged(nameof(CanAddSchedule));
    }

    /// <summary>提交新增策略：写入 settings.json 并广播重排。</summary>
    public void CommitAddSchedule()
    {
        if (DialogDisk is not { } disk || SelectedDays == DayOfWeekMask.None)
        {
            return;
        }

        var schedule = new DiskSchedule
        {
            Id = Guid.NewGuid().ToString("N"),
            SerialNumber = disk.SerialNumber,
            At = TimeOnly.FromDateTime(DateTime.Today.Add(DialogTime)),
            Days = SelectedDays,
            Target = DialogTargetIndex == 1 ? ScheduleTargetState.Offline : ScheduleTargetState.Sleep,
        };

        AddRow(schedule).UpdateProblem(FindDisk(schedule.SerialNumber));
        SaveSchedules();
    }

    /// <summary>删除策略：从列表与 settings.json 移除并广播重排。</summary>
    private void DeleteSchedule(ScheduleRowViewModel row)
    {
        Schedules.Remove(row);
        SaveSchedules();
    }

    private ManagedDisk? FindDisk(string serialNumber) =>
        _availableDisks.FirstOrDefault(disk => string.Equals(disk.SerialNumber, serialNumber, StringComparison.Ordinal));

    /// <summary>整体写回设置（默认动作 + 定时策略）；读-改-写保留开机自启开关，避免覆盖。</summary>
    private void Save()
    {
        _store.Save(_store.Load() with
        {
            DefaultAction = DefaultAction,
            Schedules = Schedules.Select(row => row.Schedule).ToList(),
        });
    }

    /// <summary>策略集合变更后广播调度器重排。</summary>
    private void SaveSchedules()
    {
        Save();
        SchedulesChanged?.Invoke();
    }

    /// <summary>对话框磁盘下拉项：序列号 + 展示文本。</summary>
    public sealed record DiskOption(string SerialNumber, string Label);
}
