using SleepyHDD.Core.AutoStart;

namespace SleepyHDD.Core.Tests.Fakes;

/// <summary>
/// IAutoStartRegistrar 的内存替身：记录调用序列与注册路径，可注入各操作失败。
/// </summary>
internal sealed class FakeAutoStartRegistrar : IAutoStartRegistrar
{
    public List<string> Calls { get; } = new();

    public string? RegisteredExePath { get; private set; }

    public bool FailRegister { get; set; }

    public bool FailUnregister { get; set; }

    public bool FailQuery { get; set; }

    public void Register(string exePath)
    {
        Calls.Add($"register:{exePath}");
        if (FailRegister)
        {
            throw new InvalidOperationException("注册失败（替身注入）。");
        }

        RegisteredExePath = exePath;
    }

    public void Unregister()
    {
        Calls.Add("unregister");
        if (FailUnregister)
        {
            throw new InvalidOperationException("注销失败（替身注入）。");
        }

        RegisteredExePath = null;
    }

    public bool IsRegistered()
    {
        Calls.Add("is-registered");
        if (FailQuery)
        {
            throw new InvalidOperationException("查询失败（替身注入）。");
        }

        return RegisteredExePath is not null;
    }
}
