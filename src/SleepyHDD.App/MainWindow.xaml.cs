using Microsoft.UI.Xaml;
using SleepyHDD.App.Devices;
using WinRT.Interop;
using SleepyHDD.App.ViewModels;
using SleepyHDD.Core.Inventory;
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

        /// <summary>组合根：App 层负责把 Native 设备实现与设置/状态存储注入 Core 磁盘清单、休眠状态机与设置视图模型。</summary>
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            InitializeComponent();
            var deviceOps = new NativeDiskDeviceOps();
            var machine = new DiskStateMachine(deviceOps, new FileStateStore());
            var settings = new SettingsViewModel(new FileSettingsStore());
            ViewModel = new MainViewModel(new DiskInventory(deviceOps), machine, settings);
            // WinUI 3 的 Window 没有 DataContext，绑定源设在内容根元素上。
            ((FrameworkElement)Content).DataContext = ViewModel;
            ViewModel.Refresh();

            // 设备热插拔：磁盘接入/移除时自动增删卡片（票 #3，WM_DEVICECHANGE）。
            _deviceWatcher = new DeviceChangeWatcher(
                WindowNative.GetWindowHandle(this), ViewModel.Refresh, DispatcherQueue);
            Closed += (_, _) => _deviceWatcher.Dispose();
        }
    }
}
