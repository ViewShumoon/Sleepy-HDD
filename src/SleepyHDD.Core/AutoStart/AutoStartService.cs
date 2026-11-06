using SleepyHDD.Core.Store;

namespace SleepyHDD.Core.AutoStart;

/// <summary>
/// 开机自启编排（票 #11）：保持 settings.json 的开关状态与任务计划程序实际注册状态一致。
/// 开启→以当前 exe 路径注册任务并持久化；关闭→注销任务并持久化；
/// 启动时校准：开关为开则重新注册以修正 exe 路径（应用被移动后自愈），
/// 注册失败则把开关拨回关；开关为关则清理残留任务。
/// </summary>
public sealed class AutoStartService
{
    private readonly ISettingsStore _settings;
    private readonly IAutoStartRegistrar _registrar;
    private readonly string _exePath;

    public AutoStartService(ISettingsStore settings, IAutoStartRegistrar registrar, string exePath)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _registrar = registrar ?? throw new ArgumentNullException(nameof(registrar));
        if (string.IsNullOrWhiteSpace(exePath))
        {
            throw new ArgumentException("exe 路径不能为空。", nameof(exePath));
        }

        _exePath = exePath;
    }

    /// <summary>持久化的开关状态（UI 显示的开关位置即此值）。</summary>
    public bool IsEnabled => _settings.Load().AutoStartEnabled;

    /// <summary>
    /// 用户拨动开关：先注册/注销任务，成功后才写回 settings.json；
    /// 注册或注销失败时返回 false，持久化状态保持不变（调用方据此回滚 UI）。
    /// </summary>
    public bool SetEnabled(bool enabled)
    {
        try
        {
            if (enabled)
            {
                _registrar.Register(_exePath);
            }
            else
            {
                _registrar.Unregister();
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // 任务计划操作失败（权限、系统异常等）：不持久化，由调用方回滚开关。
            return false;
        }

        _settings.Save(_settings.Load() with { AutoStartEnabled = enabled });
        return true;
    }

    /// <summary>
    /// 启动校准（组合根调用一次，不抛出）：开关为开时无条件重注册，把任务动作修正到
    /// 当前 exe 路径——覆盖"任务被外部删除"与"应用被移动"两种漂移，使 UI 反映真实注册状态；
    /// 重注册失败（如系统异常）则把开关拨回关并持久化。开关为关时注销可能残留的任务。
    /// 返回校准后的最终开关状态。
    /// </summary>
    public bool ReconcileOnStartup()
    {
        if (IsEnabled)
        {
            try
            {
                _registrar.Register(_exePath);
                return true;
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                // 无法保持注册：开关拨回关，避免 UI 与实际状态漂移。
                _settings.Save(_settings.Load() with { AutoStartEnabled = false });
                return false;
            }
        }

        try
        {
            // 关闭态下的残留任务（如上次注册成功后写回失败）：清理，使实际状态与开关一致。
            if (_registrar.IsRegistered())
            {
                _registrar.Unregister();
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // 清理失败不阻塞启动，留待下次校准。
        }

        return false;
    }
}
