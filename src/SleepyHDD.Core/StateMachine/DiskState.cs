namespace SleepyHDD.Core.StateMachine;

/// <summary>
/// 受管磁盘的软件视角状态（术语见 GLOSSARY.md；受限为展示态，不在此枚举内）。
/// </summary>
public enum DiskState
{
    /// <summary>磁盘旋转、卷可正常访问。</summary>
    Online,

    /// <summary>全部卷锁定并卸载，磁盘已停转；盘符可见但访问被拒（保护模式）。</summary>
    Sleeping,

    /// <summary>在休眠基础上再置离线属性，卷从系统完全消失。</summary>
    Offline,
}
