namespace SleepyHDD.Core.StateMachine;

/// <summary>
/// 全局操作对单块磁盘的结果：盘号 + 转换结果，供 UI 把失败反馈路由回对应卡片。
/// </summary>
public sealed record DiskOperationOutcome
{
    /// <summary>物理盘编号。</summary>
    public required int DiskNumber { get; init; }

    /// <summary>该盘的转换结果。</summary>
    public required DiskOperationResult Result { get; init; }
}
