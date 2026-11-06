using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using SleepyHDD.Core.Inventory;
using SleepyHDD.Core.Notifications;
using SleepyHDD.Core.StateMachine;
using SleepyHDD.Core.Store;

// MVVMTK0045 推荐的 partial property 需 C# 14（.NET 9），本项目目标 net8.0-windows，只能沿用字段。
#pragma warning disable MVVMTK0045

namespace SleepyHDD.App.ViewModels;

/// <summary>
/// 主窗口视图模型：持有受管磁盘卡片集合与空态标记，并把状态机的后台状态事件编组回 UI 线程；
/// 顶栏全局按钮（全部休眠/离线随默认动作 + 全部唤醒）与设置页切换也在此编排（票 #6，DESIGN.md 4.1/4.2）。
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly DiskInventory _inventory;
    private readonly DiskStateMachine _machine;
    private readonly SettingsViewModel _settings;
    private readonly DispatcherQueue _dispatcher;
    private IReadOnlyList<ManagedDisk> _managedDisks = Array.Empty<ManagedDisk>();
    private bool _hasRefreshed;

    public MainViewModel(DiskInventory inventory, DiskStateMachine machine, SettingsViewModel settings)
    {
        _inventory = inventory;
        _machine = machine;
        _settings = settings;
        // 构造发生在 UI 线程，此处捕获的调度队列即窗口的 DispatcherQueue。
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _machine.StateChanged += OnMachineStateChanged;
        _settings.PropertyChanged += OnSettingsPropertyChanged;
        Disks.CollectionChanged += OnDisksChanged;
    }

    /// <summary>受管磁盘卡片。</summary>
    public ObservableCollection<DiskCardViewModel> Disks { get; } = new();

    /// <summary>受管磁盘热插拔增删事件（新增, 移除）；首次刷新不触发，托盘据此发通知（票 #7）。</summary>
    public event Action<IReadOnlyList<ManagedDisk>, IReadOnlyList<ManagedDisk>>? DiskPlugChanged;

    /// <summary>共享设置视图模型（设置页与卡片按钮绑定用）。</summary>
    public SettingsViewModel Settings => _settings;

    /// <summary>无受管磁盘时显示空态。</summary>
    public bool IsEmpty => Disks.Count == 0;

    /// <summary>有受管磁盘时显示列表。</summary>
    public bool HasDisks => Disks.Count > 0;

    /// <summary>顶栏全局动作按钮文案：跟随默认动作设置。</summary>
    public string SleepAllLabel => _settings.DefaultAction == DefaultAction.Sleep ? "全部休眠" : "全部离线";

    /// <summary>顶栏标题：磁盘列表为"受管磁盘"，设置页为"设置"。</summary>
    public string HeaderText => ShowSettings ? "设置" : "受管磁盘";

    /// <summary>当前显示设置页。</summary>
    [ObservableProperty]
    private bool _showSettings;

    partial void OnShowSettingsChanged(bool value)
    {
        OnPropertyChanged(nameof(IsDiskViewVisible));
        OnPropertyChanged(nameof(IsSettingsVisible));
        OnPropertyChanged(nameof(HeaderText));
    }

    /// <summary>磁盘列表视图可见（非设置页时）。</summary>
    public bool IsDiskViewVisible => !ShowSettings;

    /// <summary>设置页视图可见。</summary>
    public bool IsSettingsVisible => ShowSettings;

    /// <summary>全局操作进行中：禁用顶栏按钮，避免重复触发。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DefaultActionAllCommand))]
    [NotifyCanExecuteChangedFor(nameof(WakeAllCommand))]
    private bool _isGlobalBusy;

    private bool CanRunGlobal() => !IsGlobalBusy;

    /// <summary>重新枚举受管磁盘并刷新卡片集合，卡片初始状态取状态机当前值；与上次枚举比对得出插拔增删并通知订阅方。</summary>
    public void Refresh()
    {
        var disks = _inventory.EnumerateManagedDisks().ToList();
        if (_hasRefreshed)
        {
            var (added, removed) = DiskChangeDiff.Compute(_managedDisks, disks);
            if (added.Count > 0 || removed.Count > 0)
            {
                DiskPlugChanged?.Invoke(added, removed);
            }
        }
        _hasRefreshed = true;
        _managedDisks = disks;

        Disks.Clear();
        foreach (var disk in disks)
        {
            Disks.Add(new DiskCardViewModel(disk, _machine, _settings));
        }
    }

    /// <summary>打开设置页。</summary>
    [RelayCommand]
    private void OpenSettings() => ShowSettings = true;

    /// <summary>从设置页返回磁盘列表。</summary>
    [RelayCommand]
    private void CloseSettings() => ShowSettings = false;

    /// <summary>全局动作（按默认动作对全部受管磁盘执行休眠或离线）；受限盘跳过，单盘失败不影响其余。</summary>
    [RelayCommand(CanExecute = nameof(CanRunGlobal))]
    private async Task DefaultActionAllAsync()
    {
        IsGlobalBusy = true;
        try
        {
            var outcomes = _settings.DefaultAction == DefaultAction.Sleep
                ? await _machine.SleepAllAsync(_managedDisks)
                : await _machine.OfflineAllAsync(_managedDisks);
            ReportOutcomes(outcomes);
        }
        finally
        {
            IsGlobalBusy = false;
        }
    }

    /// <summary>全局唤醒（对全部受管磁盘执行唤醒）；受限盘跳过，单盘失败不影响其余。</summary>
    [RelayCommand(CanExecute = nameof(CanRunGlobal))]
    private async Task WakeAllAsync()
    {
        IsGlobalBusy = true;
        try
        {
            var outcomes = await _machine.WakeAllAsync(_managedDisks);
            ReportOutcomes(outcomes);
        }
        finally
        {
            IsGlobalBusy = false;
        }
    }

    /// <summary>把全局操作里失败盘的结果反馈路由到对应卡片（成功盘的状态变化经 StateChanged 事件更新）。</summary>
    private void ReportOutcomes(IReadOnlyList<DiskOperationOutcome> outcomes)
    {
        foreach (var outcome in outcomes)
        {
            if (outcome.Result.Success)
            {
                continue;
            }

            foreach (var card in Disks)
            {
                if (card.DiskNumber == outcome.DiskNumber)
                {
                    card.ReportFailure(outcome.Result);
                }
            }
        }
    }

    private void OnDisksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasDisks));
    }

    /// <summary>默认动作变化后刷新顶栏按钮文案与各卡片动作按钮文案。</summary>
    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.DefaultAction))
        {
            OnPropertyChanged(nameof(SleepAllLabel));
            foreach (var card in Disks)
            {
                card.NotifyDefaultActionChanged();
            }
        }
    }

    /// <summary>状态机事件可能在后台线程触发，经 DispatcherQueue 编组后按盘号更新对应卡片。</summary>
    private void OnMachineStateChanged(int diskNumber, DiskState state)
    {
        _dispatcher.TryEnqueue(() =>
        {
            foreach (var card in Disks)
            {
                if (card.DiskNumber == diskNumber)
                {
                    card.State = state;
                }
            }
        });
    }
}
