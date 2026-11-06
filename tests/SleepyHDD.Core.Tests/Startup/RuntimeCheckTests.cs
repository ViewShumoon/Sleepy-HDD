using SleepyHDD.App.Startup;

namespace SleepyHDD.Core.Tests.Startup;

/// <summary>
/// issue #8：Windows App Runtime 检测的纯决策逻辑——版本常量编码与结果→动作映射。
/// </summary>
public class RuntimeCheckTests
{
    [Fact]
    public void MajorMinorVersion_EncodesMajorShifted16OrMinor()
        => Assert.Equal(0x00020006u, RuntimeCheck.MajorMinorVersion);

    [Fact]
    public void MinimumVersion_EncodesDottedQuadIntoUInt64()
        => Assert.Equal(0x0002000600050000UL, RuntimeCheck.MinimumVersion);

    [Fact]
    public void DisplayVersion_MatchesWindowsAppSdkRelease()
        => Assert.Equal("2.6.5.0", RuntimeCheck.DisplayVersion);

    [Fact]
    public void VersionTag_IsEmptyForGaRelease()
        => Assert.Empty(RuntimeCheck.VersionTag);

    [Theory]
    [InlineData(true, RuntimeCheckAction.ContinueStartup)]
    [InlineData(false, RuntimeCheckAction.GuideInstall)]
    public void DecideFromBootstrapResult_MapsBootstrapOutcome(bool succeeded, RuntimeCheckAction expected)
        => Assert.Equal(expected, RuntimeCheck.DecideFromBootstrapResult(succeeded));

    [Theory]
    [InlineData(true, InstallPromptAction.OpenDownloadAndRecheck)]
    [InlineData(false, InstallPromptAction.Abort)]
    public void DecideFromUserChoice_MapsInstallConsent(bool agreed, InstallPromptAction expected)
        => Assert.Equal(expected, RuntimeCheck.DecideFromUserChoice(agreed));
}
