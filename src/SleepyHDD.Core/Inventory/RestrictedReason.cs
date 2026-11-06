namespace SleepyHDD.Core.Inventory;

/// <summary>
/// 受限原因（GLOSSARY：受限=受管磁盘因系统原因不可被操作）。
/// 声明顺序即判定优先级：一块盘命中多个原因时，排在前面者为主因。
/// </summary>
public enum RestrictedReason
{
    /// <summary>承载启动/系统卷。</summary>
    BootOrSystemVolume = 0,

    /// <summary>承载页面文件。</summary>
    Pagefile = 1,

    /// <summary>动态盘或 Storage Spaces 成员。</summary>
    DynamicOrSpacesMember = 2,

    /// <summary>ATA pass-through 试探失败（含 Intel RST/RAID 模式），无法安全发送停转命令。</summary>
    AtaPassThroughUnavailable = 3,
}
