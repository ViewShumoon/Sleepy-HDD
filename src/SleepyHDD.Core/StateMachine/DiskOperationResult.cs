namespace SleepyHDD.Core.StateMachine;

/// <summary>
/// 一次状态转换的结果：失败时携带原因与（锁定失败场景下）占用卷的进程名列表。
/// </summary>
public sealed record DiskOperationResult
{
    /// <summary>转换是否成功。</summary>
    public required bool Success { get; init; }

    /// <summary>失败原因；成功时为 null。</summary>
    public string? Message { get; init; }

    /// <summary>阻止卷锁定的占用进程名；仅在锁定失败时有值。</summary>
    public IReadOnlyList<string> OccupyingProcesses { get; init; } = Array.Empty<string>();
}
