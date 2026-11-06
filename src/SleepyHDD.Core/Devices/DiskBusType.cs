namespace SleepyHDD.Core.Devices;

/// <summary>
/// 磁盘总线类型，取值镜像 Windows STORAGE_BUS_TYPE。
/// </summary>
public enum DiskBusType
{
    Unknown = 0,
    Scsi = 1,
    Atapi = 2,
    Ata = 3,
    Ieee1394 = 4,
    Ssa = 5,
    Fibre = 6,
    Usb = 7,
    Raid = 8,
    IScsi = 9,
    Sas = 10,
    Sata = 11,
    Sd = 12,
    Mmc = 13,
    Virtual = 14,
    FileBackedVirtual = 15,
    Spaces = 16,
    Nvme = 17,
    Scm = 18,
    Ufs = 19,
    NvmeOf = 20,
}
