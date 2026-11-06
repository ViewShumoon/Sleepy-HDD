using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SleepyHDD.App.ViewModels;
using SleepyHDD.Core.Inventory;
using SleepyHDD.Core.StateMachine;
using SleepyHDD.Core.Store;
using SleepyHDD.Native;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SleepyHDD.App
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        /// <summary>组合根：App 层负责把 Native 设备实现注入 Core 磁盘清单与休眠状态机。</summary>
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            InitializeComponent();
            var deviceOps = new NativeDiskDeviceOps();
            var machine = new DiskStateMachine(deviceOps, new FileStateStore());
            ViewModel = new MainViewModel(new DiskInventory(deviceOps), machine);
            // WinUI 3 的 Window 没有 DataContext，绑定源设在内容根元素上。
            ((FrameworkElement)Content).DataContext = ViewModel;
            ViewModel.Refresh();
        }
    }
}
