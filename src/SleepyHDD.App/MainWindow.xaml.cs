using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SleepyHDD.App.Devices;
using WinRT.Interop;
using SleepyHDD.App.Scheduler;
using SleepyHDD.App.Tray;
using SleepyHDD.App.ViewModels;
using SleepyHDD.Core.AutoStart;
using SleepyHDD.Core.Inventory;
using SleepyHDD.Core.Lifecycle;
using SleepyHDD.Core.Scheduler;
using SleepyHDD.Core.StateMachine;
using SleepyHDD.Core.Store;
using SleepyHDD.Native;
using SleepyHDD.Native.AutoStart;

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
            // 状态文件单一实例：状态机记录/移除与生命周期恢复/唤醒共享同一份在案记录。
            var stateStore = new FileStateStore();
            var machine = new DiskStateMachine(deviceOps, stateStore, settingsStore);
            var lifecycle = new DiskLifecycle(deviceOps, stateStore);
            // 开机自启（票 #11，DESIGN.md 2.8）：启动校准——开关为开时以当前 exe 路径重注册任务
            // （应用被移动后自愈），为关时清理残留任务；ReconcileOnStartup 内部吞异常、不阻塞启动。
            string exePath = Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? throw new InvalidOperationException("无法确定当前可执行文件路径。");
            var autoStart = new AutoStartService(settingsStore, new TaskSchedulerAutoStartRegistrar(), exePath);
            autoStart.ReconcileOnStartup();

            var settings = new SettingsViewModel(settingsStore, autoStart);
            ViewModel = new MainViewModel(inventory, machine, settings);

            // 生命周期启动恢复（票 #9，ADR-0005）：按序列号匹配，为在案且仍离线的盘清障、
            // 清理已消失盘的记录；作为崩溃/被强杀的兜底，任何异常都不阻塞应用启动。
            try
            {
                lifecycle.RecoverOnStartup();
            }
            catch
            {
                // 恢复失败留待下次启动重试，不影响本次启动。
            }

            // WinUI 3 的 Window 没有 DataContext，绑定源设在内容根元素上。
            ((FrameworkElement)Content).DataContext = ViewModel;
            ViewModel.Refresh();

            // 退出唤醒（票 #9）：托盘"退出"是唯一真正关窗的路径，进程结束前唤醒全部在案盘；
            // 唤醒异常不阻止退出，遗漏由下次启动恢复兜底。
            Closed += (_, _) =>
            {
                try
                {
                    lifecycle.WakeAllForExit(ViewModel.ManagedDisksSnapshot);
                }
                catch
                {
                    // 尽力而为：退出优先，残留状态交下次启动恢复。
                }
            };

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
