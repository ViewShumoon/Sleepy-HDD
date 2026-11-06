using System;
using System.Diagnostics;
using Microsoft.Windows.ApplicationModel.DynamicDependency;
using SleepyHDD.App.Startup;
using SleepyHDD.Native;

namespace SleepyHDD.App
{
    /// <summary>
    /// 自定义入口点（替代 XAML 生成的 Main，见 DISABLE_XAML_GENERATED_MAIN）：
    /// 先检测并引导 Windows App Runtime，再启动 WinUI 3 应用。
    /// </summary>
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // 在触碰任何 WinRT/WinUI 类型之前初始化 COM wrappers（与生成的 Main 行为一致）。
            WinRT.ComWrappersSupport.InitializeComWrappers();

            if (!EnsureWindowsAppRuntime())
            {
                return;
            }

            Microsoft.UI.Xaml.Application.Start((p) =>
            {
                var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                    Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                App.XamlGeneratedCreateApplicationInstance();
            });
        }

        /// <summary>
        /// 检测并初始化 Windows App Runtime：存在则静默通过、无额外打扰；
        /// 缺失则引导用户前往下载页安装，安装完成后重新检测，成功即继续启动。
        /// </summary>
        private static bool EnsureWindowsAppRuntime()
        {
            while (true)
            {
                if (RuntimeCheck.DecideFromBootstrapResult(TryBootstrap()) == RuntimeCheckAction.ContinueStartup)
                {
                    return true;
                }

                // 运行时缺失：询问是否引导安装。
                bool agreed = NativeMessageBox.ConfirmWarning(
                        $"Sleepy HDD 需要 Windows App Runtime {RuntimeCheck.DisplayVersion} 或更高版本，未在本机检测到。\n\n现在打开下载页面进行安装吗？",
                        "缺少 Windows App Runtime");

                if (RuntimeCheck.DecideFromUserChoice(agreed) == InstallPromptAction.Abort)
                {
                    return false;
                }

                OpenDownloadPage();

                // 等待用户手动完成安装后循环重新检测。
                bool completed = NativeMessageBox.ConfirmInformation(
                        "Windows App Runtime 是否已安装完成？\n\n选择“否”将退出程序。",
                        "安装 Windows App Runtime");

                if (!completed)
                {
                    return false;
                }
            }
        }

        private static bool TryBootstrap()
        {
            // InitializeOptions.None 禁用系统自动修复 UI，提示流程完全由本引导循环掌控。
            return Bootstrap.TryInitialize(
                RuntimeCheck.MajorMinorVersion,
                RuntimeCheck.VersionTag,
                new PackageVersion(RuntimeCheck.MinimumVersion),
                Bootstrap.InitializeOptions.None,
                out _);
        }

        private static void OpenDownloadPage()
        {
            try
            {
                Process.Start(new ProcessStartInfo(RuntimeCheck.DownloadPageUrl) { UseShellExecute = true });
            }
            catch
            {
                // 无法拉起浏览器时退回手动提示，不中断引导流程。
                NativeMessageBox.Warn(
                    $"无法自动打开浏览器，请手动访问：\n{RuntimeCheck.DownloadPageUrl}",
                    "下载 Windows App Runtime");
            }
        }
    }
}
