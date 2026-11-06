using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SleepyHDD.App.Devices;
using WinRT.Interop;
using SleepyHDD.App.Scheduler;
using SleepyHDD.App.Tray;
using SleepyHDD.App.ViewModels;
using SleepyHDD.Core.Inventory;
using SleepyHDD.Core.Scheduler;
using SleepyHDD.Core.StateMachine;
using SleepyHDD.Core.Store;
using SleepyHDD.Native;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SleepyHDD.App
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private readonly DeviceChangeWatcher _deviceWatcher;
        private readonly TrayIconController _tray;

        /// <summary>组合根：App 层负责把 Native 设备实现与设置/状态存储注入 Core 磁盘清单、休眠状态机与设置视图模型。</summary>
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            InitializeComponent();
            var deviceOps = new NativeDiskDeviceOps();
            var settingsStore = new FileSettingsStore();
            var inventory = new DiskInventory(deviceOps);
            var machine = new DiskStateMachine(deviceOps, new FileStateStore());
            var settings = new SettingsViewModel(settingsStore);
            ViewModel = new MainViewModel(inventory, machine, settings);
            // WinUI 3 的 Window 没有 DataContext，绑定源设在内容根元素上。
            ((FrameworkElement)Content).DataContext = ViewModel;
            ViewModel.Refresh();

            // 设备热插拔：磁盘接入/移除时自动增删卡片（票 #3，WM_DEVICECHANGE）。
            _deviceWatcher = new DeviceChangeWatcher(
                WindowNative.GetWindowHandle(this), ViewModel.Refresh, DispatcherQueue);
            Closed += (_, _) => _deviceWatcher.Dispose();

            // 托盘常驻（票 #7）：菜单复用主窗口同一批 Core 操作；关窗最小化到托盘，托盘"退出"才结束进程。
            _tray = new TrayIconController(ViewModel, this);
            Closed += (_, _) => _tray.Dispose();

            // 定时策略（票 #10，DESIGN.md 2.6）：进程内定时器在计划时刻复用状态机执行与手动一致的转换；
            // 与设置页共用同一 settings 存储实例，策略增删即重排，触发结果经托盘通知。
            var scheduler = new DiskScheduler(settingsStore, inventory, machine, new SystemClock(), new OneShotScheduleTimer());
            settings.SchedulesChanged += scheduler.Reload;
            // 调度器回调来自定时器后台线程，通知编组回 UI 线程后交托盘展示。
            scheduler.Notified += message => DispatcherQueue.TryEnqueue(() => _tray.ShowNotification(message));
            scheduler.Start();
            Closed += (_, _) =>
            {
                settings.SchedulesChanged -= scheduler.Reload;
                scheduler.Dispose();
            };
        }

        /// <summary>打开"新增定时策略"对话框（磁盘、时间、星期、目标状态，DESIGN.md 4.2）；保存即持久化并通知调度器重排。</summary>
        private async void OnAddScheduleClick(object sender, RoutedEventArgs e)
        {
            if (RootGrid.Resources["AddScheduleDialog"] is not ContentDialog dialog)
            {
                return;
            }

            dialog.XamlRoot = ((FrameworkElement)Content).XamlRoot;
            dialog.DataContext = ViewModel.Settings;
            ViewModel.Settings.ResetDialog();
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                ViewModel.Settings.CommitAddSchedule();
            }
        }
    }
}
