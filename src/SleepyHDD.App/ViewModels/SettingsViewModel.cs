using CommunityToolkit.Mvvm.ComponentModel;
using SleepyHDD.Core.AutoStart;
using SleepyHDD.Core.Store;

// MVVMTK0045 推荐的 partial property 需 C# 14（.NET 9），本项目目标 net8.0-windows，只能沿用字段。
#pragma warning disable MVVMTK0045

namespace SleepyHDD.App.ViewModels;

/// <summary>
/// 设置视图模型：默认动作单选（休眠/离线，GLOSSARY.md）与开机自启开关（票 #11），
/// 变更即持久化到 settings.json；由主窗口顶栏、磁盘卡片与设置页共享同一实例。
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _store;
    private readonly AutoStartService _autoStart;
    private bool _revertingAutoStart;

    public SettingsViewModel(ISettingsStore store, AutoStartService autoStart)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _autoStart = autoStart ?? throw new ArgumentNullException(nameof(autoStart));
        var loaded = store.Load();
        _defaultAction = loaded.DefaultAction;
        _autoStartEnabled = loaded.AutoStartEnabled;
    }

    /// <summary>默认动作；设置即写入 settings.json，重启后保留。</summary>
    [ObservableProperty]
    private DefaultAction _defaultAction;

    partial void OnDefaultActionChanged(DefaultAction value)
    {
        // 读-改-写保留其他字段（开机自启开关），避免整体覆盖。
        _store.Save(_store.Load() with { DefaultAction = value });
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

    /// <summary>
    /// 开机自动启动开关（票 #11，默认关）：开→注册任务计划程序任务（登录触发 + 最高权限），
    /// 关→注销；注册/注销或写回失败时开关拨回原状态，不留下与实际不符的设置。
    /// </summary>
    [ObservableProperty]
    private bool _autoStartEnabled;

    partial void OnAutoStartEnabledChanged(bool value)
    {
        if (_revertingAutoStart)
        {
            return;
        }

        bool persisted;
        try
        {
            persisted = _autoStart.SetEnabled(value);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            persisted = false;
        }

        if (!persisted)
        {
            _revertingAutoStart = true;
            AutoStartEnabled = !value;
            _revertingAutoStart = false;
        }
    }
}
