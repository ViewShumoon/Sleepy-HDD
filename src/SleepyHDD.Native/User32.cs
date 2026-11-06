using System.Runtime.InteropServices;

namespace SleepyHDD.Native;

/// <summary>
/// user32 消息框：App 层启动引导的询问/提示经此调用，P/Invoke 全部收敛在 Native 层（DESIGN.md 3）。
/// </summary>
public static class NativeMessageBox
{
    private const uint MbYesNo = 0x00000004;
    private const uint MbIconWarning = 0x00000030;
    private const uint MbIconInformation = 0x00000040;
    private const int Idyes = 6;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    /// <summary>警告图标的是/否询问框（无所有者窗口）；用户选"是"返回 true。</summary>
    public static bool ConfirmWarning(string text, string caption) =>
        MessageBoxW(IntPtr.Zero, text, caption, MbYesNo | MbIconWarning) == Idyes;

    /// <summary>信息图标的是/否询问框（无所有者窗口）；用户选"是"返回 true。</summary>
    public static bool ConfirmInformation(string text, string caption) =>
        MessageBoxW(IntPtr.Zero, text, caption, MbYesNo | MbIconInformation) == Idyes;

    /// <summary>警告图标的单按钮提示框（无所有者窗口）。</summary>
    public static void Warn(string text, string caption) =>
        _ = MessageBoxW(IntPtr.Zero, text, caption, MbIconWarning);
}

/// <summary>
/// 窗口子类化与 WM_DEVICECHANGE 广播头读取（user32）：
/// App 层监听设备变化经此薄封装，不自行声明任何 DllImport（DESIGN.md 3）。
/// </summary>
public static class NativeWindowSubclass
{
    private const int GWLP_WNDPROC = -4;

    [StructLayout(LayoutKind.Sequential)]
    private struct DEV_BROADCAST_HDR
    {
        public uint DbchSize;
        public uint DbchDevicetype;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProcW(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>替换窗口过程并返回原窗口过程指针；返回 IntPtr.Zero 表示失败（调用方取 Marshal.GetLastWin32Error）。</summary>
    public static IntPtr SetWindowProc(IntPtr hWnd, IntPtr newWndProc) =>
        SetWindowLongPtrW(hWnd, GWLP_WNDPROC, newWndProc);

    /// <summary>把消息转发给原窗口过程。</summary>
    public static IntPtr CallWindowProc(IntPtr previousWndProc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam) =>
        CallWindowProcW(previousWndProc, hWnd, msg, wParam, lParam);

    /// <summary>读取 DEV_BROADCAST_HDR 的 DbchDevicetype 字段；lParam 为空指针时返回 0。</summary>
    public static uint GetDeviceBroadcastType(IntPtr lParam) =>
        lParam == IntPtr.Zero
            ? 0u
            : Marshal.PtrToStructure<DEV_BROADCAST_HDR>(lParam).DbchDevicetype;
}
