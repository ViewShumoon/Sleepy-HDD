namespace SleepyHDD.Core.AutoStart;

/// <summary>
/// 开机自启注册缝隙（DESIGN.md 2.8）：任务计划程序任务的注册/注销/查询，
/// Core 定义接口、Native 实现（登录触发 + 最高权限）、测试可注入替身。
/// </summary>
public interface IAutoStartRegistrar
{
    /// <summary>注册或覆盖开机自启任务，动作为拉起指定完整路径的 exe；失败时抛出异常。</summary>
    void Register(string exePath);

    /// <summary>注销开机自启任务；任务不存在视为已注销，不抛出。</summary>
    void Unregister();

    /// <summary>开机自启任务当前是否已注册。</summary>
    bool IsRegistered();
}
