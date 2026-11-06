using System.Text.Json;
using System.Text.Json.Serialization;
using SleepyHDD.Core.StateMachine;

namespace SleepyHDD.Core.Store;

/// <summary>
/// 状态文件的 JSON 持久化：默认路径 %LOCALAPPDATA%\SleepyHDD\state.json，
/// 记录本软件置为休眠/离线的磁盘（按序列号）。
/// </summary>
public sealed class FileStateStore : IStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly object _gate = new();
    private readonly string _path;

    /// <param name="path">状态文件完整路径；null 时使用 %LOCALAPPDATA%\SleepyHDD\state.json。</param>
    public FileStateStore(string? path = null)
    {
        _path = path
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SleepyHDD",
                "state.json");
    }

    /// <inheritdoc />
    public void Record(string serialNumber, DiskState state)
    {
        lock (_gate)
        {
            var disks = ReadFile();
            disks.RemoveAll(entry => string.Equals(entry.SerialNumber, serialNumber, StringComparison.Ordinal));
            disks.Add(new DiskEntry { SerialNumber = serialNumber, State = state });
            WriteFile(disks);
        }
    }

    /// <inheritdoc />
    public void Forget(string serialNumber)
    {
        lock (_gate)
        {
            var disks = ReadFile();
            disks.RemoveAll(entry => string.Equals(entry.SerialNumber, serialNumber, StringComparison.Ordinal));
            WriteFile(disks);
        }
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, DiskState> Load()
    {
        lock (_gate)
        {
            var map = new Dictionary<string, DiskState>(StringComparer.Ordinal);
            foreach (var entry in ReadFile())
            {
                map[entry.SerialNumber] = entry.State;
            }

            return map;
        }
    }

    private List<DiskEntry> ReadFile()
    {
        if (!File.Exists(_path))
        {
            return new List<DiskEntry>();
        }

        try
        {
            var document = JsonSerializer.Deserialize<StateDocument>(
                File.ReadAllText(_path), SerializerOptions);
            return document?.Disks ?? new List<DiskEntry>();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            // 文件损坏或不可读时以空记录起步，后续 Record 会重写文件。
            return new List<DiskEntry>();
        }
    }

    private void WriteFile(List<DiskEntry> disks)
    {
        var directory = Path.GetDirectoryName(_path);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(new StateDocument { Disks = disks }, SerializerOptions);
        File.WriteAllText(_path, json);
    }

    private sealed class StateDocument
    {
        public List<DiskEntry> Disks { get; set; } = new();
    }

    private sealed class DiskEntry
    {
        public string SerialNumber { get; set; } = string.Empty;
        public DiskState State { get; set; }
    }
}
