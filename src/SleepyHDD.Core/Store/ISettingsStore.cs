namespace SleepyHDD.Core.Store;

/// <summary>
/// 设置持久化缝隙：settings.json 的读写，Core 定义、FileSettingsStore 实现、测试可注入替身。
/// </summary>
public interface ISettingsStore
{
    /// <summary>读取设置；文件缺失或损坏时返回默认值。</summary>
    Settings Load();

    /// <summary>整体写回设置。</summary>
    void Save(Settings settings);
}
