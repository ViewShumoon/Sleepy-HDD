using SleepyHDD.Core.StateMachine;
using SleepyHDD.Core.Store;
using SleepyHDD.Core.Tray;

namespace SleepyHDD.Core.Tests.Tray;

/// <summary>
/// 托盘纯逻辑（票 #7，DESIGN.md 4.3）：每盘快捷切换项文案与可用性、
/// 全局按钮文案随默认动作、图标"睡眠"变体选择——全部不依赖 UI。
/// </summary>
public class TrayMenuBuilderTests
{
    [Fact]
    public void BuildDiskItems_OnlineDisk_LabelsDefaultActionSleep()
    {
        var items = TrayMenuBuilder.BuildDiskItems(
            new[] { Entry(1, "HDD1", DiskState.Online) },
            DefaultAction.Sleep);

        var item = Assert.Single(items);
        Assert.Equal("HDD1：休眠", item.Text);
        Assert.True(item.IsEnabled);
        Assert.Equal(1, item.DiskNumber);
    }

    [Fact]
    public void BuildDiskItems_OnlineDisk_LabelsDefaultActionOffline()
    {
        var items = TrayMenuBuilder.BuildDiskItems(
            new[] { Entry(1, "HDD1", DiskState.Online) },
            DefaultAction.Offline);

        Assert.Equal("HDD1：离线", Assert.Single(items).Text);
    }

    [Theory]
    [InlineData(DiskState.Sleeping)]
    [InlineData(DiskState.Offline)]
    public void BuildDiskItems_SleptDisk_OfferWake(DiskState state)
    {
        var items = TrayMenuBuilder.BuildDiskItems(
            new[] { Entry(2, "HDD2", state) },
            DefaultAction.Sleep);

        var item = Assert.Single(items);
        Assert.Equal("HDD2：唤醒", item.Text);
        Assert.True(item.IsEnabled);
    }

    [Fact]
    public void BuildDiskItems_RestrictedDisk_DisabledAndMarked()
    {
        var items = TrayMenuBuilder.BuildDiskItems(
            new[] { RestrictedEntry(3, "HDD3") },
            DefaultAction.Sleep);

        var item = Assert.Single(items);
        Assert.Equal("HDD3（受限）", item.Text);
        Assert.False(item.IsEnabled);
    }

    [Fact]
    public void BuildDiskItems_PreservesDiskOrder()
    {
        var items = TrayMenuBuilder.BuildDiskItems(
            new[]
            {
                Entry(1, "HDD1", DiskState.Online),
                RestrictedEntry(2, "HDD2"),
                Entry(3, "HDD3", DiskState.Offline),
            },
            DefaultAction.Sleep);

        Assert.Equal(new[] { 1, 2, 3 }, items.Select(item => item.DiskNumber));
    }

    [Theory]
    [InlineData(DefaultAction.Sleep, "全部休眠")]
    [InlineData(DefaultAction.Offline, "全部离线")]
    public void SleepAllLabel_FollowsDefaultAction(DefaultAction action, string expected)
    {
        Assert.Equal(expected, TrayMenuBuilder.SleepAllLabel(action));
    }

    [Fact]
    public void SelectIconVariant_AllOnline_Normal()
    {
        var disks = new[]
        {
            Entry(1, "HDD1", DiskState.Online),
            RestrictedEntry(2, "HDD2"),
        };

        Assert.Equal(TrayIconVariant.Normal, TrayMenuBuilder.SelectIconVariant(disks));
    }

    [Theory]
    [InlineData(DiskState.Sleeping)]
    [InlineData(DiskState.Offline)]
    public void SelectIconVariant_AnyDiskSlept_SleepingVariant(DiskState state)
    {
        var disks = new[]
        {
            Entry(1, "HDD1", DiskState.Online),
            Entry(2, "HDD2", state),
        };

        Assert.Equal(TrayIconVariant.Sleeping, TrayMenuBuilder.SelectIconVariant(disks));
    }

    [Fact]
    public void SelectIconVariant_NoDisks_Normal()
    {
        Assert.Equal(TrayIconVariant.Normal, TrayMenuBuilder.SelectIconVariant(Array.Empty<TrayDiskEntry>()));
    }

    private static TrayDiskEntry Entry(int number, string model, DiskState state) =>
        new(number, model, IsRestricted: false, state);

    private static TrayDiskEntry RestrictedEntry(int number, string model) =>
        new(number, model, IsRestricted: true, DiskState.Online);
}
