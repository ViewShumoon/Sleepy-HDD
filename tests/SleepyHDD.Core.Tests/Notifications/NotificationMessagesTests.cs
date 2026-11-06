using SleepyHDD.Core.Notifications;
using SleepyHDD.Core.StateMachine;

namespace SleepyHDD.Core.Tests.Notifications;

/// <summary>
/// 托盘通知文案（票 #7）：锁定失败附占用进程列表、磁盘插拔提示——纯文本逻辑。
/// </summary>
public class NotificationMessagesTests
{
    [Fact]
    public void OperationFailed_WithOccupants_ListsProcessNames()
    {
        var result = new DiskOperationResult
        {
            Success = false,
            Message = "卷 E: 正被其他程序占用，无法锁定。",
            OccupyingProcesses = new List<string> { "SearchIndexer.exe", "MsMpEng.exe" },
        };

        var message = NotificationMessages.OperationFailed("ST1000DM010", result);

        Assert.Contains("ST1000DM010", message.Title);
        Assert.Contains("卷 E: 正被其他程序占用，无法锁定。", message.Body);
        Assert.Contains("占用进程：SearchIndexer.exe、MsMpEng.exe", message.Body);
    }

    [Fact]
    public void OperationFailed_WithoutOccupants_OmitsProcessSection()
    {
        var result = new DiskOperationResult
        {
            Success = false,
            Message = "停转命令失败，已回滚卷锁。",
        };

        var message = NotificationMessages.OperationFailed("ST1000DM010", result);

        Assert.DoesNotContain("占用进程", message.Body);
        Assert.Contains("停转命令失败，已回滚卷锁。", message.Body);
    }

    [Fact]
    public void DiskArrived_UsesChineseTerminology()
    {
        var message = NotificationMessages.DiskArrived("WDC WD20EZBX");

        Assert.Equal("磁盘已接入", message.Title);
        Assert.Contains("WDC WD20EZBX", message.Body);
    }

    [Fact]
    public void DiskRemoved_UsesChineseTerminology()
    {
        var message = NotificationMessages.DiskRemoved("WDC WD20EZBX");

        Assert.Equal("磁盘已移除", message.Title);
        Assert.Contains("WDC WD20EZBX", message.Body);
    }
}
