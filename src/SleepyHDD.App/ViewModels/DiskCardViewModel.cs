using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;
using SleepyHDD.Core.Inventory;
using SleepyHDD.Core.StateMachine;

// MVVMTK0045 推荐的 partial property 需 C# 14（.NET 9），本项目目标 net8.0-windows，只能沿用字段。
#pragma warning disable MVVMTK0045

namespace SleepyHDD.App.ViewModels;

/// <summary>
/// 受管磁盘卡片展示项：受限盘仅以红字徽章说明原因、无任何动作按钮（DESIGN.md 4.1）；
/// 正常盘按单按钮表动作（在线→默认动作[当前为休眠]，休眠/离线→唤醒），
/// 徽章在线绿/休眠黄/离线灰（保护模式，tickets #4/#5）。
/// </summary>
public sealed partial class DiskCardViewModel : ObservableObject
{
    private static readonly SolidColorBrush OnlineBadgeBrush =
        new(Windows.UI.Color.FromArgb(255, 0x2E, 0x7D, 0x32));
    private static readonly SolidColorBrush SleepingBadgeBrush =
        new(Windows.UI.Color.FromArgb(255, 0xF9, 0xA8, 0x25));
    private static readonly SolidColorBrush OfflineBadgeBrush =
        new(Windows.UI.Color.FromArgb(255, 0x9E, 0x9E, 0x9E));

    private readonly ManagedDisk _disk;
    private readonly DiskStateMachine _machine;

    public DiskCardViewModel(ManagedDisk disk, DiskStateMachine machine)
    {
        _disk = disk;
        _machine = machine;
        Model = disk.Model.Length > 0 ? disk.Model : $"磁盘 #{disk.DiskNumber}";
        CapacityText = ByteFormat.Format(disk.SizeBytes);
        Volumes = disk.Volumes
            .Select(volume => new VolumeCardViewModel(volume))
            .ToList();
        IsRestricted = disk.IsRestricted;
        IsNotRestricted = !disk.IsRestricted;
        StatusText = disk.PrimaryRestrictedReason is { } reason
            ? $"受限 · {Describe(reason)}"
            : string.Empty;
        RestrictedReasonsText = disk.IsRestricted
            ? string.Join("；", disk.RestrictedReasons.Select(Describe))
            : string.Empty;
        _state = machine.GetState(disk.DiskNumber);
    }

    /// <summary>磁盘型号。</summary>
    public string Model { get; }

    /// <summary>容量文本，如 "931 GB"。</summary>
    public string CapacityText { get; }

    /// <summary>该盘上的卷。</summary>
    public IReadOnlyList<VolumeCardViewModel> Volumes { get; }

    /// <summary>物理盘编号，用于状态事件路由回本卡片。</summary>
    public int DiskNumber => _disk.DiskNumber;

    /// <summary>受限展示态：徽章显示红字原因，无动作按钮。</summary>
    public bool IsRestricted { get; }

    /// <summary>不受限展示态：徽章显示在线/休眠状态，可执行休眠/唤醒。</summary>
    public bool IsNotRestricted { get; }

    /// <summary>受限徽章文本："受限 · 主因说明"；不受限时为空（正常盘徽章用 <see cref="StateText"/>）。</summary>
    public string StatusText { get; }

    /// <summary>全部受限原因说明（悬停提示用）；不受限时为空。</summary>
    public string RestrictedReasonsText { get; }

    /// <summary>磁盘软件视角状态；由 MainViewModel 经 UI 线程分发更新。</summary>
    [ObservableProperty]
    private DiskState _state;

    /// <summary>操作进行中禁用按钮，避免同盘重复触发。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleCommand))]
    private bool _isBusy;

    /// <summary>失败反馈（如卷被占用及占用进程列表）；空则不显示。</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>状态徽章文本：在线/休眠/离线。</summary>
    public string StateText => State switch
    {
        DiskState.Online => "在线",
        DiskState.Sleeping => "休眠",
        DiskState.Offline => "离线",
        _ => "未知",
    };

    /// <summary>徽章底色：在线绿、休眠黄、离线灰。</summary>
    public SolidColorBrush BadgeBrush => State switch
    {
        DiskState.Online => OnlineBadgeBrush,
        DiskState.Sleeping => SleepingBadgeBrush,
        DiskState.Offline => OfflineBadgeBrush,
        _ => OnlineBadgeBrush,
    };

    /// <summary>动作按钮文本：在线时"休眠"，否则"唤醒"。</summary>
    public string ActionText => State == DiskState.Online ? "休眠" : "唤醒";

    /// <summary>有失败反馈时显示状态文本。</summary>
    public bool HasStatusMessage => StatusMessage.Length > 0;

    partial void OnStateChanged(DiskState value)
    {
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(BadgeBrush));
        OnPropertyChanged(nameof(ActionText));
    }

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatusMessage));

    private bool CanToggle() => !IsBusy && !IsRestricted;

    /// <summary>休眠/唤醒切换：在线→转休眠，休眠→唤醒；失败时展示占用进程报告。</summary>
    [RelayCommand(CanExecute = nameof(CanToggle))]
    private async Task ToggleAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            DiskOperationResult result = State == DiskState.Online
                ? await _machine.SleepAsync(_disk)
                : await _machine.WakeAsync(_disk);

            if (!result.Success)
            {
                string occupants = result.OccupyingProcesses.Count > 0
                    ? $" 占用进程：{string.Join("、", result.OccupyingProcesses)}"
                    : string.Empty;
                StatusMessage = $"{result.Message}{occupants}";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string Describe(RestrictedReason reason) => reason switch
    {
        RestrictedReason.BootOrSystemVolume => "承载启动/系统卷",
        RestrictedReason.Pagefile => "承载页面文件",
        RestrictedReason.DynamicOrSpacesMember => "动态盘/Storage Spaces 成员",
        RestrictedReason.AtaPassThroughUnavailable => "ATA pass-through 不可用",
        _ => reason.ToString(),
    };
}
