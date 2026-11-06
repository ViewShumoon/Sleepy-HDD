using SleepyHDD.Core.Devices;
using SleepyHDD.Core.Inventory;
using SleepyHDD.Core.StateMachine;
using SleepyHDD.Core.Tests.Fakes;

// 本组测试刻意以阻塞方式取回 Task 结果，以便同步断言调用序列与并发/串行语义。
#pragma warning disable xUnit1031

namespace SleepyHDD.Core.Tests.StateMachine;

/// <summary>
/// 转休眠/唤醒编排缝隙：DiskStateMachine 经 fake IDiskDeviceOps 验证调用序列、
/// 锁定失败回滚、同盘串行与跨盘并行，不触碰 Native 内部实现。
/// </summary>
public class DiskStateMachineTests
{
    private const string VolD = @"\\?\Volume{dd-ddata}\";
    private const string VolE = @"\\?\Volume{ee-edata}\";

    [Fact]
    public void SleepAsync_Success_OrchestratesLockDismountFlushStandbyInOrder()
    {
        var fake = new FakeDiskDeviceOps();
        var store = new FakeStateStore();
        var machine = new DiskStateMachine(fake, store);
        var disk = TwoVolumeDisk();

        var result = machine.SleepAsync(disk).GetAwaiter().GetResult();

        Assert.True(result.Success);
        Assert.Equal(
            new[]
            {
                $"lock:{VolD}",
                $"dismount:{VolD}",
                $"lock:{VolE}",
                $"dismount:{VolE}",
                "flush:1",
                "standby:1",
            },
            fake.Calls);
        Assert.Equal(DiskState.Sleeping, machine.GetState(1));
        Assert.Equal(new[] { "record:HDD1:Sleeping" }, store.Calls);
    }

    [Fact]
    public void SleepAsync_LockFailure_RollsBackLockedVolumesAndReportsOccupants()
    {
        var fake = new FakeDiskDeviceOps();
        fake.LockFailures.Add(VolE);
        fake.OccupantsByVolume[VolE] = new List<string> { "notepad.exe", "SearchIndexer.exe" };
        var store = new FakeStateStore();
        var machine = new DiskStateMachine(fake, store);
        var disk = TwoVolumeDisk();

        var result = machine.SleepAsync(disk).GetAwaiter().GetResult();

        Assert.False(result.Success);
        Assert.NotNull(result.Message);
        Assert.Equal(new[] { "notepad.exe", "SearchIndexer.exe" }, result.OccupyingProcesses);
        // 第二卷锁定失败：先查占用者，再回滚已锁定的第一卷；绝不走到 flush/standby。
        Assert.Equal(
            new[]
            {
                $"lock:{VolD}",
                $"dismount:{VolD}",
                $"lock:{VolE}",
                $"occupants:{VolE}",
                $"unlock:{VolD}",
            },
            fake.Calls);
        Assert.Equal(DiskState.Online, machine.GetState(1));
        Assert.Empty(store.Calls);
    }

    [Fact]
    public void WakeAsync_UnlocksAllVolumesAndReturnsOnline()
    {
        var fake = new FakeDiskDeviceOps();
        var store = new FakeStateStore();
        var machine = new DiskStateMachine(fake, store);
        var disk = TwoVolumeDisk();

        machine.SleepAsync(disk).GetAwaiter().GetResult();
        fake.Calls.Clear();

        var result = machine.WakeAsync(disk).GetAwaiter().GetResult();

        Assert.True(result.Success);
        Assert.Equal(new[] { $"unlock:{VolD}", $"unlock:{VolE}" }, fake.Calls);
        Assert.Equal(DiskState.Online, machine.GetState(1));
        Assert.Equal(new[] { "record:HDD1:Sleeping", "forget:HDD1" }, store.Calls);
    }

    [Fact]
    public void SleepAsync_SameDisk_OperationsAreSerialized()
    {
        var fake = new FakeDiskDeviceOps();
        using var entered = new ManualResetEventSlim(false);
        using var gate = new ManualResetEventSlim(false);
        fake.StandbyGates[1] = gate;
        fake.StandbyEntered[1] = entered;
        var machine = new DiskStateMachine(fake, new FakeStateStore());
        var disk = TwoVolumeDisk();

        var sleeping = machine.SleepAsync(disk);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "未进入 standby 调用");

        var waking = machine.WakeAsync(disk);
        // 同盘串行：standby 尚未结束，唤醒必须等待。
        Assert.NotSame(waking, Task.WhenAny(waking, Task.Delay(150)).GetAwaiter().GetResult());

        gate.Set();
        Assert.True(sleeping.GetAwaiter().GetResult().Success);
        Assert.True(waking.GetAwaiter().GetResult().Success);

        // 调用序列上 standby 先于全部 unlock，证明未交错。
        Assert.True(
            fake.IndexOfCall("standby:1") < fake.IndexOfCall("unlock:"),
            $"standby 与 unlock 顺序错误: {string.Join(", ", fake.Calls)}");
    }

    [Fact]
    public void SleepAsync_DifferentDisks_RunInParallel()
    {
        var fake = new FakeDiskDeviceOps();
        using var entered = new ManualResetEventSlim(false);
        using var gate = new ManualResetEventSlim(false);
        fake.StandbyGates[1] = gate;
        fake.StandbyEntered[1] = entered;
        var machine = new DiskStateMachine(fake, new FakeStateStore());

        var disk1 = Disk(1, "HDD1", VolD);
        var disk2 = Disk(2, "HDD2", VolE);

        var sleeping1 = machine.SleepAsync(disk1);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)), "未进入 standby 调用");

        // 跨盘并行：disk1 停在 standby 时 disk2 应能独立完成。
        var finished = Task.WhenAny(sleeping1, machine.SleepAsync(disk2)).GetAwaiter().GetResult();
        Assert.NotSame(sleeping1, finished);
        Assert.True(finished.GetAwaiter().GetResult().Success);

        gate.Set();
        Assert.True(sleeping1.GetAwaiter().GetResult().Success);
    }

    [Fact]
    public void SleepAsync_FiresStateChangedEventWithSerialAgnosticDiskNumber()
    {
        var fake = new FakeDiskDeviceOps();
        var machine = new DiskStateMachine(fake, new FakeStateStore());
        var observed = new List<(int Disk, DiskState State)>();
        machine.StateChanged += (disk, state) => observed.Add((disk, state));

        machine.SleepAsync(TwoVolumeDisk()).GetAwaiter().GetResult();

        Assert.Equal(new[] { (1, DiskState.Sleeping) }, observed);
    }

    private static ManagedDisk TwoVolumeDisk() => Disk(
        1,
        "HDD1",
        VolD,
        VolE);

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
}
