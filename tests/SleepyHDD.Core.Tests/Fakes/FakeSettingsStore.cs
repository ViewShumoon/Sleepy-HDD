using SleepyHDD.Core.Store;

namespace SleepyHDD.Core.Tests.Fakes;

/// <summary>
/// ISettingsStore 的内存替身：Load/Save 直接读写内存文档，供调度器与自启测试驱动设置变更。
/// </summary>
internal sealed class FakeSettingsStore : ISettingsStore
{
    public Settings Current { get; set; } = new();

    public Settings Load() => Current;

    public void Save(Settings settings) => Current = settings;
}
