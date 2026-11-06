using CommunityToolkit.Mvvm.ComponentModel;
using SleepyHDD.Core.Store;

// MVVMTK0045 推荐的 partial property 需 C# 14（.NET 9），本项目目标 net8.0-windows，只能沿用字段。
#pragma warning disable MVVMTK0045

namespace SleepyHDD.App.ViewModels;

/// <summary>
/// 设置视图模型：默认动作单选（休眠/离线，GLOSSARY.md），变更即持久化到 settings.json；
/// 由主窗口顶栏、磁盘卡片与设置页共享同一实例。
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _store;

    public SettingsViewModel(ISettingsStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _defaultAction = store.Load().DefaultAction;
    }

    /// <summary>默认动作；设置即写入 settings.json，重启后保留。</summary>
    [ObservableProperty]
    private DefaultAction _defaultAction;

    partial void OnDefaultActionChanged(DefaultAction value)
    {
        _store.Save(new Settings { DefaultAction = value });
        OnPropertyChanged(nameof(DefaultActionIndex));
        OnPropertyChanged(nameof(DefaultActionName));
    }

    /// <summary>单选控件索引：0=休眠、1=离线。</summary>
    public int DefaultActionIndex
    {
        get => (int)DefaultAction;
        set => DefaultAction = (DefaultAction)value;
    }

    /// <summary>默认动作文案：休眠/离线（磁盘卡片动作按钮文本用）。</summary>
    public string DefaultActionName => DefaultAction == DefaultAction.Sleep ? "休眠" : "离线";
}
