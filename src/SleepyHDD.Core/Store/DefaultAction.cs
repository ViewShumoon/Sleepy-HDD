namespace SleepyHDD.Core.Store;

/// <summary>
/// 默认动作（GLOSSARY.md）：全局设置，决定对在线磁盘执行的动作是"休眠"还是"离线"；默认值为"休眠"。
/// </summary>
public enum DefaultAction
{
    /// <summary>停转并拒绝卷访问，盘符仍可见。</summary>
    Sleep,

    /// <summary>在休眠基础上再置离线属性，卷从系统完全消失。</summary>
    Offline,
}
