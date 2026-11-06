namespace SleepyHDD.Core.Inventory;

/// <summary>
/// 受管磁盘上挂载了盘符的卷。
/// </summary>
public sealed record ManagedVolume
{
    /// <summary>卷路径，形如 \\?\Volume{guid}\，锁定/卸载/解锁以此为标识。</summary>
    public required string VolumePath { get; init; }

    /// <summary>盘符字母（如 "D"）。</summary>
    public required string DriveLetter { get; init; }

    /// <summary>卷标。</summary>
    public string? Label { get; init; }

    /// <summary>卷总字节数。</summary>
    public long? TotalBytes { get; init; }

    /// <summary>卷已用字节数。</summary>
    public long? UsedBytes { get; init; }
}
