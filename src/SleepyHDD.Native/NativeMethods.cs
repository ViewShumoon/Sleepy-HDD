using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace SleepyHDD.Native;

/// <summary>
/// 磁盘枚举所需的全部 Win32 声明与结构，unsafe 代码只出现在 Native 层。
/// </summary>
internal static class NativeMethods
{
    public const string Kernel32 = "kernel32.dll";

    // IOCTL 控制码（来自 ntddstor.h / ntdddisk.h / ntddscsi.h，按 CTL_CODE 展开核实）。
    public const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x002D1400;
    public const uint IOCTL_STORAGE_GET_DEVICE_NUMBER = 0x002D1080;

    /// <summary>CTL_CODE(FILE_DEVICE_DISK, 0x17, BUFFERED, FILE_READ_ACCESS)。</summary>
    public const uint IOCTL_DISK_GET_LENGTH_INFO = 0x0007405C;

    /// <summary>CTL_CODE(FILE_DEVICE_CONTROLLER, 0x40C, BUFFERED, READ|WRITE)。</summary>
    public const uint IOCTL_ATA_PASS_THROUGH_DIRECT = 0x0004D030;

    // FSCTL 卷控制码（CTL_CODE(FILE_DEVICE_FILE_SYSTEM=9, n, BUFFERED, ANY_ACCESS) 展开）。
    public const uint FSCTL_LOCK_VOLUME = 0x00090018;
    public const uint FSCTL_UNLOCK_VOLUME = 0x0009001C;
    public const uint FSCTL_DISMOUNT_VOLUME = 0x00090020;

    // STORAGE_PROPERTY_ID 与查询类型。
    public const uint StorageDeviceProperty = 0;
    public const uint StorageDeviceSeekPenaltyProperty = 7;
    public const uint PropertyStandardQuery = 0;

    /// <summary>
    /// QUERY_PROPERTY 的输入缓冲区大小：部分驱动（如 NVMe）要求至少 sizeof(STORAGE_PROPERTY_QUERY)
    /// 加上 AdditionalParameters 空间，实测 64 字节在所有目标驱动上均可通过。
    /// </summary>
    public const uint StorageQueryInputBufferSize = 64;

    /// <summary>QUERY_PROPERTY 的输出缓冲区大小：足以容纳 STORAGE_DEVICE_DESCRIPTOR。</summary>
    public const uint StorageQueryOutputBufferSize = 4096;

    /// <summary>探测 PhysicalDrive 的最大编号（含）。</summary>
    public const int PhysicalDriveMaxIndex = 31;

    // CreateFile 参数。
    public const uint GENERIC_READ = 0x80000000;
    public const uint GENERIC_WRITE = 0x40000000;
    public const uint FILE_SHARE_READ_WRITE = 0x3;
    public const uint FILE_SHARE_DELETE = 0x4;
    public const uint OPEN_EXISTING = 3;

    // GetDriveTypeW 返回值。
    public const uint DRIVE_REMOVABLE = 2;
    public const uint DRIVE_FIXED = 3;

    /// <summary>STORAGE_DEVICE_NUMBER.DeviceType 中表示物理磁盘的 FILE_DEVICE_DISK。</summary>
    public const uint FILE_DEVICE_DISK = 7;

    /// <summary>
    /// ATA 命令字节：IDENTIFY DEVICE / CACHE FLUSH EXT / STANDBY IMMEDIATE。
    /// 依 ADR-0001 停转只用 0xE0，绝不发送 SLEEP (0xE6)。
    /// </summary>
    public const byte AtaCommandIdentifyDevice = 0xEC;
    public const byte AtaCommandCacheFlushExt = 0xE7;
    public const byte AtaCommandStandbyImmediate = 0xE0;
    public const byte AtaDeviceHeadA0 = 0xA0;

    /// <summary>ATA_FLAGS：向主机传数据 + DMA。</summary>
    public const ushort AtaFlagsDataIn = 0x2;
    public const ushort AtaFlagsUseDma = 0x10;

    /// <summary>IDENTIFY DEVICE 数据长度与 word 189（media rotation rate）字节偏移。</summary>
    public const uint IdentifyDataLength = 512;
    public const int IdentifyWord189ByteOffset = 378;

    /// <summary>word 189 == 1 表示非旋转介质；0 表示未报告；&gt; 0x0400 表示 RPM。</summary>
    public const ushort RotationRateNonRotating = 1;
    public const ushort RotationRateRpmThreshold = 0x0400;

    [StructLayout(LayoutKind.Sequential)]
    public struct STORAGE_PROPERTY_QUERY
    {
        public uint PropertyId;
        public uint QueryType;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct STORAGE_DEVICE_NUMBER
    {
        public uint DeviceType;
        public uint DeviceNumber;
        public uint PartitionNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DEVICE_SEEK_PENALTY_DESCRIPTOR
    {
        public uint Version;
        public uint Size;
        public byte IncursSeekPenalty;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ATA_PASS_THROUGH_DIRECT
    {
        public ushort Length;
        public ushort AtaFlags;
        public byte PathId;
        public byte TargetId;
        public byte Lun;
        public byte ReservedAsUchar;
        public uint DataTransferLength;
        public uint TimeOutValue;
        public uint ReservedAsUlong;
        public IntPtr DataBuffer;
        public fixed byte PreviousTaskFile[8];
        public fixed byte CurrentTaskFile[8];
    }

    [DllImport(Kernel32, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport(Kernel32, SetLastError = true)]
    public static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport(Kernel32)]
    public static extern uint GetLogicalDrives();

    [DllImport(Kernel32, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint GetDriveTypeW(string lpRootPathName);

    [DllImport(Kernel32, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool GetVolumeNameForVolumeMountPointW(
        string lpszVolumeMountPoint,
        StringBuilder lpszVolumeName,
        uint cchBufferLength);

    [DllImport(Kernel32, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool GetVolumeInformationW(
        string lpRootPathName,
        StringBuilder? lpVolumeNameBuffer,
        uint nVolumeNameSize,
        out uint lpVolumeSerialNumber,
        out uint lpMaximumComponentLength,
        out uint lpFileSystemFlags,
        StringBuilder? lpFileSystemNameBuffer,
        uint nFileSystemNameSize);

    [DllImport(Kernel32, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool GetDiskFreeSpaceExW(
        string lpDirectoryName,
        out ulong lpFreeBytesAvailable,
        out ulong lpTotalNumberOfBytes,
        out ulong lpTotalNumberOfFreeBytes);

    // Restart Manager（rstrtmgr.dll）：列出占用卷的进程。
    public const string Rstrtmgr = "rstrtmgr.dll";

    /// <summary>RmStartSession 的会话密钥缓冲区字符数（CCH_RM_SESSION_KEY + 1）。</summary>
    public const int RmSessionKeyBufferChars = 65;

    /// <summary>RmGetList 首次调用返回的"需要更大缓冲"错误码。</summary>
    public const int ErrorMoreData = 234;

    [StructLayout(LayoutKind.Sequential)]
    public struct RM_UNIQUE_PROCESS
    {
        public int dwProcessId;
        public long ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct RM_PROCESS_INFO
    {
        public RM_UNIQUE_PROCESS Process;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strAppName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string strServiceShortName;

        public uint ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;

        [MarshalAs(UnmanagedType.Bool)]
        public bool bBootable;
    }

    [DllImport(Rstrtmgr, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint RmStartSession(
        out uint pSessionHandle,
        uint dwSessionFlags,
        StringBuilder strSessionKey);

    [DllImport(Rstrtmgr)]
    public static extern uint RmEndSession(uint dwSessionHandle);

    [DllImport(Rstrtmgr, CharSet = CharSet.Unicode)]
    public static extern uint RmRegisterResources(
        uint dwSessionHandle,
        uint nFiles,
        string[] rgsFilenames,
        uint nApplications,
        RM_UNIQUE_PROCESS[]? rgApplications,
        uint nServices,
        string[]? rgsServiceNames);

    [DllImport(Rstrtmgr)]
    public static extern uint RmGetList(
        uint dwSessionHandle,
        out uint pnProcInfoNeeded,
        ref uint pnProcInfo,
        [Out] RM_PROCESS_INFO[]? rgAffectedApps,
        out uint lpdwRebootReasons);
}
