using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel;
// Windows.ApplicationModel 命名空间下存在同名的 UWP AppInstance，显式消歧为 AppLifecycle 版本。
using AppInstance = Microsoft.Windows.AppLifecycle.AppInstance;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SleepyHDD.App
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>单实例注册键（issue #8），同名键在机器上唯一标识主实例。</summary>
        private const string SingleInstanceKey = "ViewShumoon.SleepyHDD.Main";

        private Window? _window;
        private AppInstance? _mainInstance;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // issue #8：单实例——第二实例把本次激活重定向给主实例后自行退出。
            _mainInstance = AppInstance.FindOrRegisterForKey(SingleInstanceKey);
            if (!_mainInstance.IsCurrent)
            {
                _mainInstance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs())
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
                Application.Current.Exit();
                return;
            }

            // 主实例收到重定向的激活时，回到 UI 线程聚焦主窗口。
            _mainInstance.Activated += (_, _) =>
                _window?.DispatcherQueue?.TryEnqueue(() => _window?.Activate());

            _window = new MainWindow();
            _window.Activate();
        }
    }
}
