using System.Diagnostics;
using SleepyHDD.Core.AutoStart;

namespace SleepyHDD.Native.AutoStart;

/// <summary>
/// IAutoStartRegistrar 的任务计划程序实现（票 #11，DESIGN.md 2.8）：经 schtasks.exe 创建
/// 登录触发（/SC ONLOGON）+ 最高权限运行（/RL HIGHEST）的任务，动作指向指定 exe 完整路径；
/// 应用本身以管理员运行（ADR-0003），注册与登录触发拉起均不弹 UAC。
/// 选择 schtasks 而非 ITaskService COM API：零额外依赖、命令行语义确定且可审计。
/// </summary>
public sealed class TaskSchedulerAutoStartRegistrar : IAutoStartRegistrar
{
    /// <summary>任务在任务计划程序中的名称（中文 UI 一致性，GLOSSARY.md）。</summary>
    public const string TaskName = "Sleepy HDD 开机自动启动";

    /// <inheritdoc />
    public void Register(string exePath)
    {
        // /F 覆盖同名任务：重注册即把动作路径修正为当前 exe（应用移动后自愈）。
        // /TR 值内嵌引号：exe 路径含空格时任务命令需带引号保存，否则计划程序解析会截断。
        var (exit, _, stderr) = Run(
            "/Create", "/F",
            "/TN", TaskName,
            "/TR", $"\"{exePath}\"",
            "/SC", "ONLOGON",
            "/RL", "HIGHEST");

        if (exit != 0)
        {
            throw new InvalidOperationException($"注册开机自启任务失败（退出码 {exit}）：{stderr}");
        }
    }

    /// <inheritdoc />
    public void Unregister()
    {
        if (!IsRegistered())
        {
            return;
        }

        var (exit, _, stderr) = Run("/Delete", "/TN", TaskName, "/F");
        if (exit != 0)
        {
            throw new InvalidOperationException($"注销开机自启任务失败（退出码 {exit}）：{stderr}");
        }
    }

    /// <inheritdoc />
    public bool IsRegistered()
    {
        // /Query 对不存在的任务返回非零退出码。
        var (exit, _, _) = Run("/Query", "/TN", TaskName);
        return exit == 0;
    }

    private static (int ExitCode, string StdOut, string StdErr) Run(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("schtasks.exe")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动 schtasks.exe。");

        // 先读空两条输出流再等待退出，避免管道缓冲区写满导致死锁。
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();

        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("schtasks.exe 执行超时。");
        }

        return (process.ExitCode, stdout, stderr);
    }
}
