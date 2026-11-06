using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using SleepyHDD.App.ViewModels;
using SleepyHDD.Core.Inventory;
using SleepyHDD.Core.Notifications;
using SleepyHDD.Core.StateMachine;
using SleepyHDD.Core.Tray;

namespace SleepyHDD.App.Tray;

/// <summary>
/// 托盘常驻（票 #7，DESIGN.md 4.3，H.NotifyIcon.WinUI）：
/// 任一磁盘休眠/离线时切换"睡眠"变体图标；菜单＝打开主窗口 + 每盘快捷切换 +
/// 全部休眠（或全部离线，随默认动作）/全部唤醒 + 退出，动作直接复用主窗口同一批
/// ViewModel 命令（即同一 Core 操作），状态变化经卡片事件实时重建菜单与图标；
/// 锁定失败（附占用进程）与磁盘插拔通知由卡片/视图模型事件路由至此。
/// 关闭主窗口＝隐藏到托盘，托盘"退出"才真正结束进程（退出唤醒逻辑属生命周期票 #9，此处仅留退出通路）。
/// 菜单项文案与图标变体选择等纯决策逻辑在 Core 的 <see cref="TrayMenuBuilder"/>，已单测覆盖。
/// </summary>
public sealed class TrayIconController : IDisposable
{
    private readonly TaskbarIcon _tray;
    private readonly MainViewModel _viewModel;
    private readonly Window _window;
    private readonly MenuFlyout _flyout = new();
    private readonly BitmapImage _normalIcon;
    private readonly BitmapImage _sleepingIcon;
    private readonly Dictionary<DiskCardViewModel, PropertyChangedEventHandler> _cardHandlers = new();
    private bool _isExiting;
    private bool _disposed;

    /// <param name="viewModel">主窗口视图模型：磁盘卡片、全局命令与设置状态的单一事实来源。</param>
    /// <param name="window">主窗口：托盘"打开主窗口"显示它，"退出"关闭它。</param>
    public TrayIconController(MainViewModel viewModel, Window window)
    {
        _viewModel = viewModel;
        _window = window;

        var assetsDirectory = Path.Combine(AppContext.BaseDirectory, "Assets");
        _normalIcon = new BitmapImage(new Uri(Path.Combine(assetsDirectory, "tray-normal.png")));
        _sleepingIcon = new BitmapImage(new Uri(Path.Combine(assetsDirectory, "tray-sleeping.png")));

        _tray = new TaskbarIcon
        {
            IconSource = _normalIcon,
            ToolTipText = "Sleepy HDD",
            ContextFlyout = _flyout,
        };
        // WinUI 版 TaskbarIcon 以命令暴露托盘交互：双击打开主窗口，右键弹出 ContextFlyout（默认 MenuActivation）。
        _tray.DoubleClickCommand = new RelayCommand(ShowMainWindow);

        // 关窗拦截：未走托盘"退出"时取消关闭并隐藏（AC：最小化到托盘后继续休眠/唤醒与通知查看）。
        window.AppWindow.Closing += OnWindowClosing;

        _viewModel.Disks.CollectionChanged += OnDisksChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.DiskPlugChanged += OnDiskPlugChanged;
        foreach (var card in _viewModel.Disks)
        {
            HookCard(card);
        }

        Rebuild();

        // TaskbarIcon 不在可视化树中，Loaded 不会触发；显式创建底层托盘图标，确保常驻。
        _tray.ForceCreate();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _viewModel.Disks.CollectionChanged -= OnDisksChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.DiskPlugChanged -= OnDiskPlugChanged;
        foreach (var card in _cardHandlers.Keys.ToList())
        {
            UnhookCard(card);
        }

        _tray.Dispose();
    }

    /// <summary>按当前磁盘快照重建图标变体与菜单（受限盘项禁用）。</summary>
    private void Rebuild()
    {
        var entries = _viewModel.Disks
            .Select(card => new TrayDiskEntry(card.DiskNumber, card.Model, card.IsRestricted, card.State))
            .ToList();

        _tray.IconSource = TrayMenuBuilder.SelectIconVariant(entries) == TrayIconVariant.Sleeping
            ? _sleepingIcon
            : _normalIcon;

        _flyout.Items.Clear();
        _flyout.Items.Add(Item("打开主窗口", ShowMainWindow));
        _flyout.Items.Add(new MenuFlyoutSeparator());

        if (entries.Count == 0)
        {
            _flyout.Items.Add(new MenuFlyoutItem { Text = "无受管磁盘", IsEnabled = false });
        }
        else
        {
            foreach (var item in TrayMenuBuilder.BuildDiskItems(entries, _viewModel.Settings.DefaultAction))
            {
                var card = _viewModel.Disks.FirstOrDefault(c => c.DiskNumber == item.DiskNumber);
                _flyout.Items.Add(new MenuFlyoutItem
                {
                    Text = item.Text,
                    IsEnabled = item.IsEnabled,
                    Command = card?.ToggleCommand,
                });
            }
        }

        _flyout.Items.Add(new MenuFlyoutSeparator());
        _flyout.Items.Add(new MenuFlyoutItem
        {
            Text = TrayMenuBuilder.SleepAllLabel(_viewModel.Settings.DefaultAction),
            Command = _viewModel.DefaultActionAllCommand,
        });
        _flyout.Items.Add(new MenuFlyoutItem { Text = "全部唤醒", Command = _viewModel.WakeAllCommand });
        _flyout.Items.Add(new MenuFlyoutSeparator());
        _flyout.Items.Add(Item("退出", ExitApplication));
    }

    /// <summary>无命令的固定菜单项（点击直接执行动作）。</summary>
    private static MenuFlyoutItem Item(string text, Action action)
    {
        var item = new MenuFlyoutItem { Text = text };
        item.Click += (_, _) => action();
        return item;
    }

    private void ShowMainWindow()
    {
        _window.AppWindow.Show();
        _window.Activate();
    }

    private void ExitApplication()
    {
        _isExiting = true;
        _window.Close();
    }

    private void OnWindowClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (!_isExiting)
        {
            args.Cancel = true;
            sender.Hide();
        }
    }

    /// <summary>卡片操作失败（含锁定失败+占用进程）→ 托盘通知；失败文本由 Core 生成。</summary>
    private void OnCardOperationFailed(string model, DiskOperationResult result)
    {
        var message = NotificationMessages.OperationFailed(model, result);
        _tray.ShowNotification(message.Title, message.Body, NotificationIcon.Warning);
    }

    /// <summary>展示 Core 生成的通知（定时触发结果等，票 #10）；级别由消息自带。</summary>
    public void ShowNotification(NotificationMessage message)
    {
        _tray.ShowNotification(
            message.Title,
            message.Body,
            message.Severity == NotificationSeverity.Warning ? NotificationIcon.Warning : NotificationIcon.Info);
    }

    private void OnDiskPlugChanged(IReadOnlyList<ManagedDisk> added, IReadOnlyList<ManagedDisk> removed)
    {
        foreach (var disk in added)
        {
            var message = NotificationMessages.DiskArrived(disk.Model);
            _tray.ShowNotification(message.Title, message.Body, NotificationIcon.Info);
        }

        foreach (var disk in removed)
        {
            var message = NotificationMessages.DiskRemoved(disk.Model);
            _tray.ShowNotification(message.Title, message.Body, NotificationIcon.Info);
        }
    }

    private void OnDisksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var card in _cardHandlers.Keys.ToList())
            {
                UnhookCard(card);
            }
        }
        else
        {
            foreach (var card in e.OldItems?.OfType<DiskCardViewModel>() ?? Enumerable.Empty<DiskCardViewModel>())
            {
                UnhookCard(card);
            }

            foreach (var card in e.NewItems?.OfType<DiskCardViewModel>() ?? Enumerable.Empty<DiskCardViewModel>())
            {
                HookCard(card);
            }
        }

        Rebuild();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 默认动作变化（SleepAllLabel）与全局操作忙闲都会影响菜单文案/可用性。
        if (e.PropertyName is nameof(MainViewModel.SleepAllLabel) or nameof(MainViewModel.IsGlobalBusy))
        {
            Rebuild();
        }
    }

    private void HookCard(DiskCardViewModel card)
    {
        var handler = new PropertyChangedEventHandler((_, e) =>
        {
            // 状态或动作文案变化（休眠/唤醒完成、默认动作切换）→ 重建图标变体与菜单。
            if (e.PropertyName is nameof(DiskCardViewModel.State) or nameof(DiskCardViewModel.ActionText))
            {
                Rebuild();
            }
        });
        card.PropertyChanged += handler;
        card.OperationFailed += OnCardOperationFailed;
        _cardHandlers[card] = handler;
    }

    private void UnhookCard(DiskCardViewModel card)
    {
        if (_cardHandlers.Remove(card, out var handler))
        {
            card.PropertyChanged -= handler;
            card.OperationFailed -= OnCardOperationFailed;
        }
    }
}
