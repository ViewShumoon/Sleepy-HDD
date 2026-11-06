using SleepyHDD.Core.Devices;
using SleepyHDD.Core.Inventory;
using SleepyHDD.Core.Tests.Fakes;

namespace SleepyHDD.Core.Tests.Inventory;

/// <summary>
/// 受管判定缝隙：DiskInventory.EnumerateManagedDisks 经 fake IDiskDeviceOps 验证。
/// </summary>
public class DiskInventoryTests
{
    [Fact]
    public void SataHddWithSeekPenalty_IsManagedWithDescriptorFields()
    {
        var fake = new FakeDiskDeviceOps
        {
            Disks =
            {
                new DiskDescriptor
                {
                    DiskNumber = 1,
                    BusType = DiskBusType.Sata,
                    Model = "ST1000DM010-2EP102",
                    SerialNumber = "ZAVR1ABC",
                    SizeBytes = 1_000_204_886_016,
                },
            },
        };
        fake.Penalties[1] = true;

        var disks = new DiskInventory(fake).EnumerateManagedDisks();

        var disk = Assert.Single(disks);
        Assert.Equal(1, disk.DiskNumber);
        Assert.Equal(DiskBusType.Sata, disk.BusType);
        Assert.Equal("ST1000DM010-2EP102", disk.Model);
        Assert.Equal("ZAVR1ABC", disk.SerialNumber);
        Assert.Equal(1_000_204_886_016, disk.SizeBytes);
    }

    [Fact]
    public void UsbDiskWithSeekPenalty_IsNotManaged()
    {
        var fake = new FakeDiskDeviceOps
        {
            Disks =
            {
                new DiskDescriptor
                {
                    DiskNumber = 2,
                    BusType = DiskBusType.Usb,
                    Model = "External HDD",
                    SerialNumber = "USB123",
                    SizeBytes = 2_000_398_934_016,
                },
            },
        };
        fake.Penalties[2] = true;

        var disks = new DiskInventory(fake).EnumerateManagedDisks();

        Assert.Empty(disks);
    }

    [Fact]
    public void SataSsdWithoutSeekPenalty_IsNotManaged()
    {
        var fake = new FakeDiskDeviceOps
        {
            Disks =
            {
                new DiskDescriptor
                {
                    DiskNumber = 0,
                    BusType = DiskBusType.Sata,
                    Model = "Samsung SSD 870 EVO 1TB",
                    SerialNumber = "S5X1NEO",
                    SizeBytes = 1_000_204_886_016,
                },
            },
        };
        fake.Penalties[0] = false;

        var disks = new DiskInventory(fake).EnumerateManagedDisks();

        Assert.Empty(disks);
    }

    [Fact]
    public void NvmeDiskWithSeekPenalty_IsNotManagedByBusType()
    {
        var fake = new FakeDiskDeviceOps
        {
            Disks =
            {
                new DiskDescriptor
                {
                    DiskNumber = 3,
                    BusType = DiskBusType.Nvme,
                    Model = "Samsung SSD 990 PRO 2TB",
                    SerialNumber = "S6ZTNP2",
                    SizeBytes = 2_000_398_934_016,
                },
            },
        };
        fake.Penalties[3] = true;

        var disks = new DiskInventory(fake).EnumerateManagedDisks();

        Assert.Empty(disks);
    }

    [Fact]
    public void UnknownSeekPenaltyWithHddModel_IsManaged()
    {
        var fake = new FakeDiskDeviceOps
        {
            Disks =
            {
                new DiskDescriptor
                {
                    DiskNumber = 4,
                    BusType = DiskBusType.Ata,
                    Model = "ST1000DM010-2EP102",
                    SerialNumber = "W9W1ABCD",
                    SizeBytes = 1_000_204_886_016,
                },
            },
        };
        fake.Penalties[4] = null;

        var disks = new DiskInventory(fake).EnumerateManagedDisks();

        var disk = Assert.Single(disks);
        Assert.Equal(4, disk.DiskNumber);
        Assert.Equal(DiskBusType.Ata, disk.BusType);
    }

    [Fact]
    public void UnknownSeekPenaltyWithSsdModel_IsNotManaged()
    {
        var fake = new FakeDiskDeviceOps
        {
            Disks =
            {
                new DiskDescriptor
                {
                    DiskNumber = 5,
                    BusType = DiskBusType.Sata,
                    Model = "Kingston SA400S37480G SSD",
                    SerialNumber = "50026B072345",
                    SizeBytes = 480_103_981_056,
                },
            },
        };
        fake.Penalties[5] = null;

        var disks = new DiskInventory(fake).EnumerateManagedDisks();

        Assert.Empty(disks);
    }

    [Fact]
    public void ScsiDiskWithSeekPenalty_IsManaged()
    {
        var fake = new FakeDiskDeviceOps
        {
            Disks =
            {
                new DiskDescriptor
                {
                    DiskNumber = 6,
                    BusType = DiskBusType.Scsi,
                    Model = "HL-DT-ST DVDRAM",
                    SerialNumber = "SCSI6",
                    SizeBytes = 500_107_862_016,
                },
            },
        };
        fake.Penalties[6] = true;

        var disks = new DiskInventory(fake).EnumerateManagedDisks();

        Assert.Single(disks);
    }

    [Fact]
    public void AtapiDiskWithSeekPenalty_IsNotManaged()
    {
        var fake = new FakeDiskDeviceOps
        {
            Disks =
            {
                new DiskDescriptor
                {
                    DiskNumber = 7,
                    BusType = DiskBusType.Atapi,
                    Model = "HL-DT-ST DVD+RW",
                    SerialNumber = "ATAPI7",
                    SizeBytes = 0,
                },
            },
        };
        fake.Penalties[7] = true;

        var disks = new DiskInventory(fake).EnumerateManagedDisks();

        Assert.Empty(disks);
    }

    [Fact]
    public void Volumes_AreMappedToOwningManagedDiskWithUsage()
    {
        const long total = 500_000_000_000;
        const long free = 100_000_000_000;
        var fake = new FakeDiskDeviceOps
        {
            Disks =
            {
                new DiskDescriptor
                {
                    DiskNumber = 1,
                    BusType = DiskBusType.Sata,
                    Model = "ST1000DM010",
                    SerialNumber = "HDD1",
                    SizeBytes = 1_000_204_886_016,
                },
                new DiskDescriptor
                {
                    DiskNumber = 2,
                    BusType = DiskBusType.Nvme,
                    Model = "WD SN850X",
                    SerialNumber = "NVME2",
                    SizeBytes = 2_000_398_934_016,
                },
            },
            Volumes =
            {
                new VolumeDescriptor
                {
                    VolumePath = @"\\?\Volume{hdd1-data}\",
                    DiskNumber = 1,
                    DriveLetter = "D",
                    Label = "Data",
                    TotalBytes = total,
                    FreeBytes = free,
                },
                new VolumeDescriptor
                {
                    VolumePath = @"\\?\Volume{hdd1-hidden}\",
                    DiskNumber = 1,
                    DriveLetter = null,
                    Label = null,
                    TotalBytes = 300_000_000_000,
                    FreeBytes = 290_000_000_000,
                },
                new VolumeDescriptor
                {
                    VolumePath = @"\\?\Volume{nvme-system}\",
                    DiskNumber = 2,
                    DriveLetter = "C",
                    Label = "Windows",
                    TotalBytes = 512_000_000_000,
                    FreeBytes = 200_000_000_000,
                },
            },
        };
        fake.Penalties[1] = true;
        fake.Penalties[2] = false;

        var disks = new DiskInventory(fake).EnumerateManagedDisks();

        var disk = Assert.Single(disks);
        var volume = Assert.Single(disk.Volumes);
        Assert.Equal("D", volume.DriveLetter);
        Assert.Equal("Data", volume.Label);
        Assert.Equal(total, volume.TotalBytes);
        Assert.Equal(total - free, volume.UsedBytes);
    }

    [Fact]
    public void NoDisks_ReturnsEmptyInventory()
    {
        var disks = new DiskInventory(new FakeDiskDeviceOps()).EnumerateManagedDisks();

        Assert.Empty(disks);
    }

    [Fact]
    public void DisksAreSortedByDiskNumber()
    {
        var fake = new FakeDiskDeviceOps
        {
            Disks =
            {
                Descriptor(3, "Disk3"),
                Descriptor(1, "Disk1"),
                Descriptor(2, "Disk2"),
            },
        };
        fake.Penalties[1] = true;
        fake.Penalties[2] = true;
        fake.Penalties[3] = true;

        var disks = new DiskInventory(fake).EnumerateManagedDisks();

        Assert.Equal(new[] { 1, 2, 3 }, disks.Select(d => d.DiskNumber));
    }

    private static DiskDescriptor Descriptor(int number, string model) => new()
    {
        DiskNumber = number,
        BusType = DiskBusType.Sata,
        Model = model,
        SerialNumber = $"SN{number}",
        SizeBytes = 1_000_204_886_016,
    };
}
