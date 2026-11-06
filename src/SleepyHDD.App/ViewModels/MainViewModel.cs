using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using SleepyHDD.Core.Inventory;

namespace SleepyHDD.App.ViewModels;

/// <summary>
/// 主窗口视图模型：持有受管磁盘卡片集合与空态标记。
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly DiskInventory _inventory;

    public MainViewModel(DiskInventory inventory)
    {
        _inventory = inventory;
        Disks.CollectionChanged += OnDisksChanged;
    }

    /// <summary>受管磁盘卡片。</summary>
    public ObservableCollection<DiskCardViewModel> Disks { get; } = new();

    /// <summary>无受管磁盘时显示空态。</summary>
    public bool IsEmpty => Disks.Count == 0;

    /// <summary>有受管磁盘时显示列表。</summary>
    public bool HasDisks => Disks.Count > 0;

    /// <summary>重新枚举受管磁盘并刷新卡片集合。</summary>
    public void Refresh()
    {
        var disks = _inventory.EnumerateManagedDisks()
            .Select(disk => new DiskCardViewModel(disk))
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
}
