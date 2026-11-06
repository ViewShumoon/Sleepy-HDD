using SleepyHDD.Core.AutoStart;
using SleepyHDD.Core.Store;
using SleepyHDD.Core.Tests.Fakes;

namespace SleepyHDD.Core.Tests.AutoStart;

/// <summary>
/// 开机自启编排（票 #11）：开关状态与任务注册/持久化的一致性、启动校准与自愈，
/// 全部经 fake IAutoStartRegistrar 验证调用序列，不触碰 Native 实现。
/// </summary>
public class AutoStartServiceTests
{
    private const string ExePath = @"D:\Apps\Sleepy HDD\SleepyHDD.App.exe";

    private static (AutoStartService Service, FakeSettingsStore Settings, FakeAutoStartRegistrar Registrar) Create(
        Settings? initial = null)
    {
        var settings = new FakeSettingsStore { Current = initial ?? new Settings() };
        var registrar = new FakeAutoStartRegistrar();
        return (new AutoStartService(settings, registrar, ExePath), settings, registrar);
    }

    [Fact]
    public void IsEnabled_DefaultSettings_IsOff()
    {
        var (service, _, _) = Create();

        Assert.False(service.IsEnabled);
    }

    [Fact]
    public void SetEnabled_On_RegistersCurrentExePathAndPersists()
    {
        var (service, settings, registrar) = Create();

        Assert.True(service.SetEnabled(true));

        Assert.Equal(new[] { $"register:{ExePath}" }, registrar.Calls);
        Assert.True(settings.Current.AutoStartEnabled);
    }

    [Fact]
    public void SetEnabled_Off_UnregistersAndPersists()
    {
        var (service, settings, registrar) = Create(new Settings { AutoStartEnabled = true });

        Assert.True(service.SetEnabled(false));

        Assert.Equal(new[] { "unregister" }, registrar.Calls);
        Assert.False(settings.Current.AutoStartEnabled);
    }

    [Fact]
    public void SetEnabled_On_PreservesDefaultAction()
    {
        var (service, settings, _) = Create(new Settings { DefaultAction = DefaultAction.Offline });

        Assert.True(service.SetEnabled(true));

        Assert.Equal(DefaultAction.Offline, settings.Current.DefaultAction);
        Assert.True(settings.Current.AutoStartEnabled);
    }

    [Fact]
    public void SetEnabled_RegisterFails_ReturnsFalseAndKeepsStateOff()
    {
        var (service, settings, registrar) = Create();
        registrar.FailRegister = true;

        Assert.False(service.SetEnabled(true));

        Assert.False(settings.Current.AutoStartEnabled);
    }

    [Fact]
    public void SetEnabled_UnregisterFails_ReturnsFalseAndKeepsStateOn()
    {
        var (service, settings, registrar) = Create(new Settings { AutoStartEnabled = true });
        registrar.FailUnregister = true;

        Assert.False(service.SetEnabled(false));

        Assert.True(settings.Current.AutoStartEnabled);
    }

    [Fact]
    public void Reconcile_Enabled_ReRegistersCurrentExePathAndStaysOn()
    {
        // 应用被移动后：设置仍为开，启动时以当前 exe 路径重注册即自愈。
        var (service, settings, registrar) = Create(new Settings { AutoStartEnabled = true });

        Assert.True(service.ReconcileOnStartup());

        Assert.Equal(new[] { $"register:{ExePath}" }, registrar.Calls);
        Assert.Equal(ExePath, registrar.RegisteredExePath);
        Assert.True(settings.Current.AutoStartEnabled);
    }

    [Fact]
    public void Reconcile_Enabled_RegisterFails_FlipsSwitchOffAndPersists()
    {
        // 任务被外部删除且重注册失败：开关拨回关，UI 与实际状态一致。
        var (service, settings, registrar) = Create(new Settings { AutoStartEnabled = true });
        registrar.FailRegister = true;

        Assert.False(service.ReconcileOnStartup());

        Assert.False(settings.Current.AutoStartEnabled);
    }

    [Fact]
    public void Reconcile_Disabled_WithLeftoverTask_Unregisters()
    {
        // 上次注册成功后写回失败留下的残留任务：启动校准清理，使实际状态与开关一致。
        var (service, _, registrar) = Create();
        registrar.Register(ExePath);
        registrar.Calls.Clear();

        Assert.False(service.ReconcileOnStartup());

        Assert.Equal(new[] { "is-registered", "unregister" }, registrar.Calls);
    }

    [Fact]
    public void Reconcile_Disabled_NoTask_LeavesRegistrarUntouched()
    {
        var (service, _, registrar) = Create();

        Assert.False(service.ReconcileOnStartup());

        Assert.Equal(new[] { "is-registered" }, registrar.Calls);
    }

    [Fact]
    public void Reconcile_Disabled_QueryFails_DoesNotThrow()
    {
        // 校准绝不阻塞启动：查询异常被吞，开关状态不变。
        var (service, settings, registrar) = Create();
        registrar.FailQuery = true;

        Assert.False(service.ReconcileOnStartup());

        Assert.False(settings.Current.AutoStartEnabled);
    }
}
