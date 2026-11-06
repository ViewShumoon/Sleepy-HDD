namespace SleepyHDD.App.Startup;

/// <summary>
/// 运行时检测结果对应的启动动作。
/// </summary>
public enum RuntimeCheckAction
{
    /// <summary>运行时可用，继续正常启动。</summary>
    ContinueStartup,

    /// <summary>运行时缺失，进入引导安装流程。</summary>
    GuideInstall,
}

/// <summary>
/// 引导安装提示中用户选择对应的动作。
/// </summary>
public enum InstallPromptAction
{
    /// <summary>用户同意安装：打开下载页并在其确认后重新检测。</summary>
    OpenDownloadAndRecheck,

    /// <summary>用户拒绝或未完成安装：中止启动。</summary>
    Abort,
}

/// <summary>
/// Windows App Runtime 检测的纯决策逻辑：版本常量与结果→动作映射，不依赖任何 WinUI/WinRT 类型，可被单元测试直接链接验证。
/// </summary>
public static class RuntimeCheck
{
    /// <summary>要求的 Windows App Runtime 主版本（对应 WindowsAppSDK 2.6.5）。</summary>
    public const ushort Major = 2;

    /// <summary>要求的次版本。</summary>
    public const ushort Minor = 6;

    /// <summary>要求的生成号。</summary>
    public const ushort Build = 5;

    /// <summary>要求的修订号。</summary>
    public const ushort Revision = 0;

    /// <summary>点分显示版本，用于提示文案。</summary>
    public static string DisplayVersion => $"{Major}.{Minor}.{Build}.{Revision}";

    /// <summary>bootstrap API 的 majorMinor 参数：major 左移 16 位按位或 minor。</summary>
    public const uint MajorMinorVersion = (uint)((Major << 16) | Minor);

    /// <summary>运行时版本标签，GA 版本为空串。</summary>
    public const string VersionTag = "";

    /// <summary>最低要求版本编码：major&lt;&lt;48 | minor&lt;&lt;32 | build&lt;&lt;16 | revision。</summary>
    public const ulong MinimumVersion =
        ((ulong)Major << 48) | ((ulong)Minor << 32) | ((ulong)Build << 16) | Revision;

    /// <summary>Windows App Runtime 下载页，缺失时引导用户前往安装。</summary>
    public const string DownloadPageUrl = "https://aka.ms/windowsappsdk";

    /// <summary>bootstrap 初始化成功与否决定继续启动还是引导安装。</summary>
    public static RuntimeCheckAction DecideFromBootstrapResult(bool bootstrapSucceeded)
        => bootstrapSucceeded ? RuntimeCheckAction.ContinueStartup : RuntimeCheckAction.GuideInstall;

    /// <summary>用户在引导安装提示中的选择决定打开下载页重试还是中止。</summary>
    public static InstallPromptAction DecideFromUserChoice(bool userAgreed)
        => userAgreed ? InstallPromptAction.OpenDownloadAndRecheck : InstallPromptAction.Abort;
}
