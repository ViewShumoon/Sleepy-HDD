using SleepyHDD.Core.StateMachine;

namespace SleepyHDD.Core.Store;

/// <summary>
/// 状态文件缝隙：记录"本软件置为休眠/离线的磁盘"，按序列号匹配（不受盘符/盘号变化影响）。
/// </summary>
public interface IStateStore
{
    /// <summary>记录（或更新）某序列号磁盘被本软件置入的状态。</summary>
    void Record(string serialNumber, DiskState state);

    /// <summary>移除某序列号的记录（唤醒后调用）。</summary>
    void Forget(string serialNumber);

    /// <summary>读取全部在案记录：序列号 → 状态。</summary>
    IReadOnlyDictionary<string, DiskState> Load();
}
