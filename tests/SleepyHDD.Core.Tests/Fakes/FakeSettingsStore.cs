using SleepyHDD.Core.Store;

namespace SleepyHDD.Core.Tests.Fakes;

/// <summary>
/// ISettingsStore 的内存替身：以单条设置文档模拟持久化。
/// </summary>
internal sealed class FakeSettingsStore : ISettingsStore
{
    public Settings Current { get; set; } = new();

    public Settings Load() => Current;

    public void Save(Settings settings) => Current = settings;
}
