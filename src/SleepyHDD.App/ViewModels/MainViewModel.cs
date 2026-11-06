using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using SleepyHDD.Core.Inventory;
using SleepyHDD.Core.StateMachine;

namespace SleepyHDD.App.ViewModels;

/// <summary>
/// 主窗口视图模型：持有受管磁盘卡片集合与空态标记，并把状态机的后台状态事件编组回 UI 线程。
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly DiskInventory _inventory;
    private readonly DiskStateMachine _machine;
    private readonly DispatcherQueue _dispatcher;

    public MainViewModel(DiskInventory inventory, DiskStateMachine machine)
    {
        _inventory = inventory;
        _machine = machine;
        // 构造发生在 UI 线程，此处捕获的调度队列即窗口的 DispatcherQueue。
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _machine.StateChanged += OnMachineStateChanged;
        Disks.CollectionChanged += OnDisksChanged;
    }

    /// <summary>受管磁盘卡片。</summary>
    public ObservableCollection<DiskCardViewModel> Disks { get; } = new();

    /// <summary>无受管磁盘时显示空态。</summary>
    public bool IsEmpty => Disks.Count == 0;

    /// <summary>有受管磁盘时显示列表。</summary>
    public bool HasDisks => Disks.Count > 0;

    /// <summary>重新枚举受管磁盘并刷新卡片集合，卡片初始状态取状态机当前值。</summary>
    public void Refresh()
    {
        var disks = _inventory.EnumerateManagedDisks()
            .Select(disk => new DiskCardViewModel(disk, _machine))
            .ToList();

        Disks.Clear();
        foreach (var disk in disks)
        {
            Disks.Add(disk);
        }
    }

    private void OnDisksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasDisks));
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
