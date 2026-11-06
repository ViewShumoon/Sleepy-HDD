using SleepyHDD.Core.Inventory;

namespace SleepyHDD.App.ViewModels;

/// <summary>
/// 磁盘卡片内的单卷展示项。
/// </summary>
public sealed class VolumeCardViewModel
{
    public VolumeCardViewModel(ManagedVolume volume)
    {
        Header = volume.Label is { Length: > 0 } label
            ? $"{volume.DriveLetter}: {label}"
            : $"{volume.DriveLetter}:";
        UsageText = ByteFormat.FormatUsage(volume.TotalBytes, volume.UsedBytes);
    }

    /// <summary>盘符与卷标，如 "D: Data"。</summary>
    public string Header { get; }

    /// <summary>用量文本，如 "已用 123 GB / 共 466 GB"。</summary>
    public string UsageText { get; }
}
