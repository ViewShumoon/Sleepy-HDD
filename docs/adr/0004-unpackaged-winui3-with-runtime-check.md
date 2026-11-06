# 免安装（unpackaged）WinUI 3 发布，而非 MSIX

用户要求免安装 exe。WinUI 3 unpackaged 应用仍依赖一次性安装的 Windows App Runtime，故启动时检测运行时（bootstrapper API），缺失则引导安装后继续。被否决的替代：MSIX——依赖声明体验最好，但本质是"安装"。

Consequences: 放弃 MSIX 意味着自己处理：单实例（AppInstance.FindOrRegisterForKey）、托盘图标（WinUI 3 无内置托盘，用 H.NotifyIcon 或 Shell_NotifyIcon P/Invoke）、开机自启与任务计划注册（无包身份，HKLM/HKCU 操作需提权，而应用本身已提权，可行）。仅支持 Windows 11。
