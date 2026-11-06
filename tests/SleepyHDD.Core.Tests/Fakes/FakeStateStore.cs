using SleepyHDD.Core.StateMachine;
using SleepyHDD.Core.Store;

namespace SleepyHDD.Core.Tests.Fakes;

/// <summary>
/// IStateStore 的内存替身：记录调用序列供断言。
/// </summary>
internal sealed class FakeStateStore : IStateStore
{
    public List<string> Calls { get; } = new();

    public Dictionary<string, DiskState> Entries { get; } = new(StringComparer.Ordinal);

    public void Record(string serialNumber, DiskState state)
    {
        Calls.Add($"record:{serialNumber}:{state}");
        Entries[serialNumber] = state;
    }

    public void Forget(string serialNumber)
    {
        Calls.Add($"forget:{serialNumber}");
        Entries.Remove(serialNumber);
    }

    public IReadOnlyDictionary<string, DiskState> Load() => Entries;
}
