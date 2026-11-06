using System.Text.Json;
using System.Text.Json.Serialization;

namespace SleepyHDD.Core.Store;

/// <summary>
/// 设置的 JSON 持久化：默认路径 %LOCALAPPDATA%\SleepyHDD\settings.json，
/// 文件缺失或损坏时回退到默认设置（默认动作=休眠）。
/// </summary>
public sealed class FileSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly object _gate = new();
    private readonly string _path;

    /// <param name="path">设置文件完整路径；null 时使用 %LOCALAPPDATA%\SleepyHDD\settings.json。</param>
    public FileSettingsStore(string? path = null)
    {
        _path = path
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SleepyHDD",
                "settings.json");
    }

    /// <inheritdoc />
    public Settings Load()
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
            {
                return new Settings();
            }

            try
            {
                return JsonSerializer.Deserialize<Settings>(
                    File.ReadAllText(_path), SerializerOptions) ?? new Settings();
            }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
            {
                // 文件损坏或不可读时以默认设置起步，后续 Save 会重写文件。
                return new Settings();
            }
        }
    }

    /// <inheritdoc />
    public void Save(Settings settings)
    {
        lock (_gate)
        {
            var directory = Path.GetDirectoryName(_path);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_path, JsonSerializer.Serialize(settings, SerializerOptions));
        }
    }
}
