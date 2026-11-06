using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using SleepyHDD.Native;

namespace SleepyHDD.App.Devices;

/// <summary>
/// 通过子类化窗口过程监听 WM_DEVICECHANGE，磁盘接入/移除时经防抖回调触发刷新（DESIGN.md 2.1）。
/// 刷新本身重新全量枚举磁盘、幂等，多余广播仅消耗一次防抖后的枚举。
/// 子类化与广播头读取的 P/Invoke 收敛在 Native 层（<see cref="NativeWindowSubclass"/>，DESIGN.md 3）。
/// </summary>
public sealed class DeviceChangeWatcher : IDisposable
{
    private const int WM_DEVICECHANGE = 0x0219;
    private const int DBT_DEVICEARRIVAL = 0x8000;
    private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;
    private const int DBT_DEVNODES_CHANGED = 0x0017;

    private const uint DBT_DEVTYP_VOLUME = 0x00000002;
    private const uint DBT_DEVTYP_DEVICEINTERFACE = 0x00000005;
    private const uint DBT_DEVTYP_HANDLE = 0x00000006;

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private readonly IntPtr _hwnd;
    private readonly IntPtr _oldWndProc;
    private readonly WndProcDelegate _newWndProc; // 字段引用防止委托被 GC
    private readonly DispatcherQueueTimer _debounceTimer;
    private bool _disposed;

    /// <param name="hwnd">主窗口 HWND（经 WindowNative.GetWindowHandle 获取）。</param>
    /// <param name="onDiskChange">磁盘接入/移除后经防抖触发的回调（UI 线程）。</param>
    /// <param name="dispatcherQueue">窗口消息循环所属的 DispatcherQueue。</param>
    public DeviceChangeWatcher(IntPtr hwnd, Action onDiskChange, DispatcherQueue dispatcherQueue)
    {
        _hwnd = hwnd;
        _debounceTimer = dispatcherQueue.CreateTimer();
        _debounceTimer.Interval = TimeSpan.FromMilliseconds(500);
        _debounceTimer.IsRepeating = false;
        _debounceTimer.Tick += (_, _) => onDiskChange();

        _newWndProc = (hWnd, msg, wParam, lParam) =>
        {
            if (msg == WM_DEVICECHANGE && IsDiskBroadcast(wParam, lParam))
            {
                _debounceTimer.Stop();
                _debounceTimer.Start();
            }

            return NativeWindowSubclass.CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        };

        IntPtr newPtr = Marshal.GetFunctionPointerForDelegate(_newWndProc);
        _oldWndProc = NativeWindowSubclass.SetWindowProc(_hwnd, newPtr);
        if (_oldWndProc == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SetWindowLongPtrW 子类化窗口过程失败，Win32 错误 {Marshal.GetLastWin32Error()}。");
        }
    }

    /// <summary>
    /// 只响应可能涉及磁盘/卷的广播：设备接口（磁盘类设备接入/移除）、卷（盘符挂载/卸载）、
    /// 句柄（设备句柄建立/关闭），以及无 lParam 结构的 DEVNODES_CHANGED。
    /// </summary>
    private static bool IsDiskBroadcast(IntPtr wParam, IntPtr lParam)
    {
        int code = (int)(wParam.ToInt64() & 0xFFFF);
        return code switch
        {
            DBT_DEVNODES_CHANGED => true,
            DBT_DEVICEARRIVAL or DBT_DEVICEREMOVECOMPLETE =>
                NativeWindowSubclass.GetDeviceBroadcastType(lParam)
                    is DBT_DEVTYP_VOLUME or DBT_DEVTYP_DEVICEINTERFACE or DBT_DEVTYP_HANDLE,
            _ => false,
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _debounceTimer.Stop();
        NativeWindowSubclass.SetWindowProc(_hwnd, _oldWndProc);
    }
}
