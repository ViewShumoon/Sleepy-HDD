using System.Diagnostics;
using SleepyHDD.Core.Devices;
using SleepyHDD.Core.Inventory;
using SleepyHDD.Core.StateMachine;
using SleepyHDD.Core.Tests.Fakes;

// 本组测试刻意以阻塞方式取回 Task 结果，以便同步断言全局编排的串并行与部分失败语义。
#pragma warning disable xUnit1031

namespace SleepyHDD.Core.Tests.StateMachine;

/// <summary>
/// 全局操作编排缝隙（票 #6）：SleepAll/OfflineAll/WakeAll 跳过受限盘、
/// 跨盘并行、按盘串行，单盘失败不影响其余盘，全部经 fake IDiskDeviceOps 验证。
/// </summary>
public class GlobalOperationTests
{
    private const string VolD = @"\\?\Volume{dd-ddata}\";
    private const string VolE = @"\\?\Volume{ee-edata}\";
    private const string VolF = @"\\?\Volume{ff-ffata}\";

    [Fact]
    public void SleepAllAsync_SkipsRestrictedDisks()
    {
        var fake = new FakeDiskDeviceOps();
        var machine = new DiskStateMachine(fake, new FakeStateStore());
        var disks = new List<ManagedDisk>
        {
            Disk(1, "HDD1", VolD),
            RestrictedDisk(2, "HDD2", VolE),
        };

        var outcomes = machine.SleepAllAsync(disks).GetAwaiter().GetResult();

        // 受限盘不参与、不产生任何设备调用。
        var outcome = Assert.Single(outcomes);
        Assert.Equal(1, outcome.DiskNumber);
        Assert.True(outcome.Result.Success);
        Assert.False(fake.HasCall($"lock:{VolE}"));
        Assert.Equal(DiskState.Sleeping, machine.GetState(1));
        Assert.Equal(DiskState.Online, machine.GetState(2));
    }

    [Fact]
    public void SleepAllAsync_PartialFailure_DoesNotAffectOtherDisks()
    {
        var fake = new FakeDiskDeviceOps();
        fake.LockFailures.Add(VolE);
        fake.OccupantsByVolume[VolE] = new List<string> { "SearchIndexer.exe" };
        var store = new FakeStateStore();
        var machine = new DiskStateMachine(fake, store);
        var disks = new List<ManagedDisk>
        {
            Disk(1, "HDD1", VolD),
            Disk(2, "HDD2", VolE),
            Disk(3, "HDD3", VolF),
        };

        var outcomes = machine.SleepAllAsync(disks).GetAwaiter().GetResult();

        Assert.Equal(3, outcomes.Count);
        Assert.True(OutcomeFor(outcomes, 1).Success);
        Assert.True(OutcomeFor(outcomes, 3).Success);
        var failed = OutcomeFor(outcomes, 2);
        Assert.False(failed.Success);
        Assert.NotNull(failed.Message);
        Assert.Equal(new[] { "SearchIndexer.exe" }, failed.OccupyingProcesses);
        // 失败盘保持在线并回滚，成功盘照常进入休眠。
        Assert.Equal(DiskState.Sleeping, machine.GetState(1));
        Assert.Equal(DiskState.Online, machine.GetState(2));
        Assert.Equal(DiskState.Sleeping, machine.GetState(3));
        Assert.Contains("record:HDD1:Sleeping", store.Calls);
        Assert.Contains("record:HDD3:Sleeping", store.Calls);
        Assert.DoesNotContain("record:HDD2:Sleeping", store.Calls);
    }

    [Fact]
    public void OfflineAllAsync_AppliesOfflineToEveryUnrestrictedDisk()
    {
        var fake = new FakeDiskDeviceOps();
        var machine = new DiskStateMachine(fake, new FakeStateStore());
        var disks = new List<ManagedDisk>
        {
            Disk(1, "HDD1", VolD),
            Disk(2, "HDD2", VolE),
            RestrictedDisk(3, "HDD3", VolF),
        };

        var outcomes = machine.OfflineAllAsync(disks).GetAwaiter().GetResult();

        Assert.Equal(2, outcomes.Count);
        Assert.All(outcomes, outcome => Assert.True(outcome.Result.Success));
        Assert.True(fake.HasCall("set-offline:1"));
        Assert.True(fake.HasCall("set-offline:2"));
        Assert.False(fake.HasCall("set-offline:3"));
        Assert.Equal(DiskState.Offline, machine.GetState(1));
        Assert.Equal(DiskState.Offline, machine.GetState(2));
        Assert.Equal(DiskState.Online, machine.GetState(3));
    }

    [Fact]
    public void WakeAllAsync_WakesEveryDiskAndClearsStates()
    {
        var fake = new FakeDiskDeviceOps();
        var machine = new DiskStateMachine(fake, new FakeStateStore());
        var disks = new List<ManagedDisk>
        {
            Disk(1, "HDD1", VolD),
            Disk(2, "HDD2", VolE),
        };

        machine.SleepAllAsync(disks).GetAwaiter().GetResult();
        fake.Calls.Clear();

        var outcomes = machine.WakeAllAsync(disks).GetAwaiter().GetResult();

        Assert.Equal(2, outcomes.Count);
        Assert.All(outcomes, outcome => Assert.True(outcome.Result.Success));
        Assert.True(fake.HasCall($"unlock:{VolD}"));
        Assert.True(fake.HasCall($"unlock:{VolE}"));
        Assert.Equal(DiskState.Online, machine.GetState(1));
        Assert.Equal(DiskState.Online, machine.GetState(2));
    }

    [Fact]
    public void WakeAllAsync_QueuesBehindSameDiskOperation_RunsOtherDisksInParallel()
    {
        var fake = new FakeDiskDeviceOps();
        using var entered = new ManualResetEventSlim(false);
        using var gate = new ManualResetEventSlim(false);
        fake.StandbyGates[1] = gate;
        fake.StandbyEntered[1] = entered;
        var machine = new DiskStateMachine(fake, new FakeStateStore());
        var disk1 = Disk(1, "HDD1", VolD);
        var disk2 = Disk(2, "HDD2", VolE);

        // disk1 的手动休眠阻塞在停转命令中。
        var sleeping = machine.SleepAsync(disk1);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "未进入 standby 调用");

        // 全局唤醒覆盖 disk1 与 disk2：disk2 应并行完成，disk1 在同盘信号量上排队。
        var waking = machine.WakeAllAsync(new List<ManagedDisk> { disk1, disk2 });
        Assert.True(
            WaitUntil(() => fake.HasCall($"unlock:{VolE}")),
            $"disk2 未被并行唤醒: {string.Join(", ", fake.Calls)}");
        Assert.False(waking.IsCompleted, "disk1 的唤醒不应越过同盘串行直接完成");

        gate.Set();
        Assert.True(sleeping.GetAwaiter().GetResult().Success);
        var outcomes = waking.GetAwaiter().GetResult();
        Assert.All(outcomes, outcome => Assert.True(outcome.Result.Success));

        // 同盘串行：disk1 的唤醒解锁只发生在其停转命令完成之后。
        Assert.True(
            fake.IndexOfCall("standby:1") < fake.IndexOfCall($"unlock:{VolD}"),
            $"同盘操作交错: {string.Join(", ", fake.Calls)}");
    }

    private static DiskOperationResult OutcomeFor(
        IReadOnlyList<DiskOperationOutcome> outcomes,
        int diskNumber)
        => outcomes.Single(outcome => outcome.DiskNumber == diskNumber).Result;

    private static bool WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(10);
        }

        return condition();
    }

    private static ManagedDisk Disk(int number, string serial, params string[] volumePaths) => new()
    {
        DiskNumber = number,
        BusType = DiskBusType.Sata,
        Model = "ST1000DM010",
        SerialNumber = serial,
        SizeBytes = 1_000_204_886_016,
        RestrictedReasons = Array.Empty<RestrictedReason>(),
        Volumes = volumePaths
            .Select((path, index) => new ManagedVolume
            {
                VolumePath = path,
                DriveLetter = ((char)('D' + index)).ToString(),
            })
            .ToList(),
    };

    private static ManagedDisk RestrictedDisk(int number, string serial, params string[] volumePaths) => new()
    {
        DiskNumber = number,
        BusType = DiskBusType.Sata,
        Model = "ST1000DM010",
        SerialNumber = serial,
        SizeBytes = 1_000_204_886_016,
        RestrictedReasons = new[] { RestrictedReason.BootOrSystemVolume },
        Volumes = volumePaths
            .Select((path, index) => new ManagedVolume
            {
                VolumePath = path,
                DriveLetter = ((char)('D' + index)).ToString(),
            })
            .ToList(),
    };
}
