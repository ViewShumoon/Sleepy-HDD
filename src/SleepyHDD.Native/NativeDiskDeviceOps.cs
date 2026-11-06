using System.Text;
using Microsoft.Win32.SafeHandles;
using SleepyHDD.Core.Devices;

namespace SleepyHDD.Native;

/// <summary>
/// IDiskDeviceOps 的 Win32 实现：探测 \\.\PhysicalDriveN 并 IOCTL 查询属性与 ATA pass-through、
/// Win32 卷 API 枚举卷。单个设备查询失败时跳过该设备（或降级字段）而非抛出。
/// </summary>
public sealed class NativeDiskDeviceOps : IDiskDeviceOps
{
    /// <inheritdoc />
    public IReadOnlyList<DiskDescriptor> EnumerateDisks()
    {
        var disks = new List<DiskDescriptor>();
        for (int number = 0; number <= NativeMethods.PhysicalDriveMaxIndex; number++)
        {
            string path = $@"\\.\PhysicalDrive{number}";
            DiskDescriptor? disk = ReadDisk(path, number);
            if (disk is not null)
            {
                disks.Add(disk);
            }
        }

        return disks;
    }

    /// <inheritdoc />
    public bool? GetSeekPenalty(int diskNumber)
    {
        string path = $@"\\.\PhysicalDrive{diskNumber}";

        // 优先 ATA pass-through IDENTIFY DEVICE 读 word 189（需要写权限，通常要求管理员）。
        byte[] identifyData = new byte[NativeMethods.IdentifyDataLength];
        if (TryRunAtaIdentifyDevice(path, identifyData))
        {
            ushort rotationRate = (ushort)(
                identifyData[NativeMethods.IdentifyWord189ByteOffset]
                | (identifyData[NativeMethods.IdentifyWord189ByteOffset + 1] << 8));
            if (rotationRate == NativeMethods.RotationRateNonRotating)
            {
                return false;
            }

            if (rotationRate > NativeMethods.RotationRateRpmThreshold)
            {
                return true;
            }

            // 0 或保留值：未报告，交由调用方走 property query 兜底。
        }

        // 兜底：StorageDeviceSeekPenaltyProperty 标准查询。
        return TryGetSeekPenaltyProperty(path);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetBootSystemDriveLetters()
    {
        var letters = new List<string>();
        TryAddDriveLetter(letters, QueryFixedPath(NativeMethods.GetSystemDirectoryW));
        TryAddDriveLetter(letters, QueryFixedPath(NativeMethods.GetWindowsDirectoryW));
        return letters;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetPagefilePaths()
    {
        if (NativeMethods.RegOpenKeyExW(
                NativeMethods.HKEY_LOCAL_MACHINE,
                NativeMethods.MemoryManagementKey,
                0,
                NativeMethods.KEY_QUERY_VALUE,
                out IntPtr key) != NativeMethods.ERROR_SUCCESS)
        {
            return Array.Empty<string>();
        }

        try
        {
            int size = 0;
            if (NativeMethods.RegQueryValueExW(
                    key, NativeMethods.PagingFilesValue, IntPtr.Zero, out uint type, null, ref size)
                != NativeMethods.ERROR_SUCCESS
                || type != NativeMethods.REG_MULTI_SZ
                || size <= 0)
            {
                return Array.Empty<string>();
            }

            byte[] data = new byte[size];
            if (NativeMethods.RegQueryValueExW(
                    key, NativeMethods.PagingFilesValue, IntPtr.Zero, out type, data, ref size)
                != NativeMethods.ERROR_SUCCESS)
            {
                return Array.Empty<string>();
            }

            var paths = new List<string>();
            foreach (string entry in Encoding.Unicode.GetString(data)
                         .Split('\0', StringSplitOptions.RemoveEmptyEntries))
            {
                // 每项形如 "path [初始大小] [最大值]"，取首个空白分隔 token 为路径；
                // 无法展开为盘符形式的路径（如 \Device\HarddiskVolumeN）由 Core 侧自然跳过。
                string expanded = Environment.ExpandEnvironmentVariables(entry).Trim();
                if (expanded.StartsWith(@"\??\", StringComparison.Ordinal))
                {
                    expanded = expanded[4..];
                }

                string[] tokens = expanded.Split(
                    new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length > 0 && tokens[0].Length > 0)
                {
                    paths.Add(tokens[0]);
                }
            }

            return paths;
        }
        finally
        {
            NativeMethods.RegCloseKey(key);
        }
    }

    /// <inheritdoc />
    public bool IsDynamicOrSpacesMember(int diskNumber)
    {
        string path = $@"\\.\PhysicalDrive{diskNumber}";
        using SafeFileHandle handle = NativeMethods.CreateFileW(
            path,
            NativeMethods.GENERIC_READ,
            NativeMethods.FILE_SHARE_READ_WRITE,
            IntPtr.Zero,
            NativeMethods.OPEN_EXISTING,
            0,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            return false;
        }

        // LDM 分区表：MBR 动态盘含类型 0x42 分区；GPT 动态盘/Storage Spaces 成员
        // 含 LDM/Space 专用分区（见 SpecialPartitionGuids）。
        byte[] mbr = new byte[NativeMethods.SectorSize];
        if (!TryReadAt(handle, 0, mbr)
            || mbr[510] != 0x55
            || mbr[511] != 0xAA)
        {
            return false;
        }

        for (int i = 0; i < 4; i++)
        {
            byte type = mbr[0x1BE + i * 16 + 2];
            if (type == NativeMethods.MbrPartitionTypeLdm)
            {
                return true;
            }

            if (type == NativeMethods.MbrPartitionTypeGptProtective)
            {
                return HasSpecialGptPartition(handle);
            }
        }

        return false;
    }

    /// <inheritdoc />
    public bool ProbeAtaPassThrough(int diskNumber)
    {
        string path = $@"\\.\PhysicalDrive{diskNumber}";
        byte[] identifyData = new byte[NativeMethods.IdentifyDataLength];
        return TryRunAtaIdentifyDevice(path, identifyData);
    }

    private static string? QueryFixedPath(Func<StringBuilder, uint, uint> getter)
    {
        var buffer = new StringBuilder(261);
        return getter(buffer, (uint)buffer.Capacity) == 0 ? null : buffer.ToString();
    }

    private static void TryAddDriveLetter(List<string> letters, string? path)
    {
        if (path is { Length: >= 2 } && path[1] == ':' && char.IsLetter(path[0]))
        {
            string letter = char.ToUpperInvariant(path[0]).ToString();
            if (!letters.Contains(letter, StringComparer.OrdinalIgnoreCase))
            {
                letters.Add(letter);
            }
        }
    }

    private static bool TryReadAt(SafeFileHandle handle, long offset, byte[] buffer)
    {
        // 物理盘读取按扇区对齐，单次 ReadFile 即返回全部请求字节；不足视为失败。
        if (!NativeMethods.SetFilePointerEx(handle, offset, out _, NativeMethods.FILE_BEGIN))
        {
            return false;
        }

        return NativeMethods.ReadFile(handle, buffer, (uint)buffer.Length, out uint read, IntPtr.Zero)
            && read == (uint)buffer.Length;
    }

    /// <summary>
    /// 已知特殊分区类型 GUID（GPT 条目首 16 字节）：动态盘 LDM 元数据/数据、Storage Spaces 成员元数据。
    /// 依据 Microsoft GPT 分区类型登记，属尽力探测：未收录的新变体不误报，由真机手测清单兜底。
    /// </summary>
    private static readonly byte[][] SpecialPartitionGuids =
    {
        new Guid("AF9B60A0-141F-4C4B-A649-3FB3F3721B3C").ToByteArray(), // Microsoft LDM metadata
        new Guid("5808C8AA-7E8F-42E0-85D2-E1E90A24CD2B").ToByteArray(), // Microsoft LDM data
        new Guid("E8D3AA94-01F7-4BF0-9A5C-5FEB14E6E7A4").ToByteArray(), // MSFT Space metadata
    };

    private static bool HasSpecialGptPartition(SafeFileHandle handle)
    {
        byte[] header = new byte[NativeMethods.SectorSize];
        if (!TryReadAt(handle, NativeMethods.SectorSize, header)
            || Encoding.ASCII.GetString(header, 0, NativeMethods.GptHeaderSignature.Length)
               != NativeMethods.GptHeaderSignature)
        {
            return false;
        }

        ulong entryLba = BitConverter.ToUInt64(header, NativeMethods.GptEntryLbaOffset);
        uint entryCount = BitConverter.ToUInt32(header, NativeMethods.GptEntryCountOffset);
        uint entrySize = BitConverter.ToUInt32(header, NativeMethods.GptEntrySizeOffset);
        if (entryCount == 0
            || entryCount > 1024
            || entrySize < 16
            || entrySize > 4096)
        {
            return false;
        }

        byte[] entries = new byte[entryCount * entrySize];
        if (!TryReadAt(handle, (long)entryLba * NativeMethods.SectorSize, entries))
        {
            return false;
        }

        for (int i = 0; i < entryCount; i++)
        {
            ReadOnlySpan<byte> typeGuid = entries.AsSpan((int)(i * entrySize), 16);
            foreach (byte[] known in SpecialPartitionGuids)
            {
                if (typeGuid.SequenceEqual(known))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 经 IOCTL_ATA_PASS_THROUGH_DIRECT 同步执行 IDENTIFY DEVICE：
    /// 打开 RW 句柄失败或 IOCTL 失败（如 Intel RST/RAID 驱动拒绝 pass-through）返回 false。
    /// </summary>
    private static unsafe bool TryRunAtaIdentifyDevice(string physicalDrivePath, byte[] identifyData)
    {
        using SafeFileHandle handle = NativeMethods.CreateFileW(
            physicalDrivePath,
            NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE,
            NativeMethods.FILE_SHARE_READ_WRITE,
            IntPtr.Zero,
            NativeMethods.OPEN_EXISTING,
            0,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            return false;
        }

        fixed (byte* data = identifyData)
        {
            var passthrough = default(NativeMethods.ATA_PASS_THROUGH_DIRECT);
            passthrough.Length = (ushort)sizeof(NativeMethods.ATA_PASS_THROUGH_DIRECT);
            passthrough.AtaFlags = NativeMethods.AtaFlagsDataIn | NativeMethods.AtaFlagsUseDma;
            passthrough.DataTransferLength = NativeMethods.IdentifyDataLength;
            passthrough.TimeOutValue = 2;
            passthrough.DataBuffer = (IntPtr)data;
            passthrough.CurrentTaskFile[5] = NativeMethods.AtaDeviceHeadA0;
            passthrough.CurrentTaskFile[6] = NativeMethods.AtaCommandIdentifyDevice;

            return NativeMethods.DeviceIoControl(
                handle,
                NativeMethods.IOCTL_ATA_PASS_THROUGH_DIRECT,
                (IntPtr)(&passthrough),
                (uint)sizeof(NativeMethods.ATA_PASS_THROUGH_DIRECT),
                (IntPtr)(&passthrough),
                (uint)sizeof(NativeMethods.ATA_PASS_THROUGH_DIRECT),
                out _,
                IntPtr.Zero);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<VolumeDescriptor> EnumerateVolumes()
    {
        var volumes = new List<VolumeDescriptor>();
        uint driveMask = NativeMethods.GetLogicalDrives();
        for (int bit = 0; bit < 26; bit++)
        {
            if ((driveMask & (1u << bit)) == 0)
            {
                continue;
            }

            string letter = ((char)('A' + bit)).ToString();
            string rootPath = letter + @":\";
            uint driveType = NativeMethods.GetDriveTypeW(rootPath);
            if (driveType != NativeMethods.DRIVE_FIXED && driveType != NativeMethods.DRIVE_REMOVABLE)
            {
                continue;
            }

            var volumeName = new StringBuilder(64);
            if (!NativeMethods.GetVolumeNameForVolumeMountPointW(rootPath, volumeName, (uint)volumeName.Capacity))
            {
                continue;
            }

            string volumePath = volumeName.ToString();
            (long? totalBytes, long? freeBytes) = TryGetVolumeUsage(rootPath);
            volumes.Add(new VolumeDescriptor
            {
                VolumePath = volumePath,
                DiskNumber = TryGetVolumeDiskNumber(volumePath),
                DriveLetter = letter,
                Label = TryGetVolumeLabel(rootPath),
                TotalBytes = totalBytes,
                FreeBytes = freeBytes,
            });
        }

        return volumes;
    }

    private static unsafe DiskDescriptor? ReadDisk(string physicalDrivePath, int expectedNumber)
    {
        using SafeFileHandle handle = NativeMethods.CreateFileW(
            physicalDrivePath,
            0,
            NativeMethods.FILE_SHARE_READ_WRITE,
            IntPtr.Zero,
            NativeMethods.OPEN_EXISTING,
            0,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            return null;
        }

        // STORAGE_DEVICE_DESCRIPTOR：总线类型、型号、序列号。
        if (!TryQueryDeviceProperty(
                handle,
                out DiskBusType busType,
                out string model,
                out string serial))
        {
            return null;
        }

        // 物理盘编号校验。
        NativeMethods.STORAGE_DEVICE_NUMBER* numberBuffer = stackalloc NativeMethods.STORAGE_DEVICE_NUMBER[1];
        if (!NativeMethods.DeviceIoControl(
                handle,
                NativeMethods.IOCTL_STORAGE_GET_DEVICE_NUMBER,
                IntPtr.Zero,
                0,
                (IntPtr)numberBuffer,
                (uint)sizeof(NativeMethods.STORAGE_DEVICE_NUMBER),
                out _,
                IntPtr.Zero)
            || numberBuffer->DeviceType != NativeMethods.FILE_DEVICE_DISK
            || numberBuffer->DeviceNumber != (uint)expectedNumber)
        {
            return null;
        }

        return new DiskDescriptor
        {
            DiskNumber = expectedNumber,
            BusType = busType,
            Model = model,
            SerialNumber = serial,
            SizeBytes = TryGetDiskLength(physicalDrivePath),
        };
    }

    /// <summary>
    /// IOCTL_DISK_GET_LENGTH_INFO 要求句柄具有读权限；非管理员进程打开物理盘会被拒绝，此时返回 0。
    /// </summary>
    private static unsafe long TryGetDiskLength(string physicalDrivePath)
    {
        using SafeFileHandle handle = NativeMethods.CreateFileW(
            physicalDrivePath,
            NativeMethods.GENERIC_READ,
            NativeMethods.FILE_SHARE_READ_WRITE,
            IntPtr.Zero,
            NativeMethods.OPEN_EXISTING,
            0,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            return 0;
        }

        long* lengthBuffer = stackalloc long[1];
        return NativeMethods.DeviceIoControl(
            handle,
            NativeMethods.IOCTL_DISK_GET_LENGTH_INFO,
            IntPtr.Zero,
            0,
            (IntPtr)lengthBuffer,
            sizeof(long),
            out _,
            IntPtr.Zero)
            ? *lengthBuffer
            : 0;
    }

    private static unsafe bool TryQueryDeviceProperty(
        SafeFileHandle handle,
        out DiskBusType busType,
        out string model,
        out string serial)
    {
        busType = DiskBusType.Unknown;
        model = string.Empty;
        serial = string.Empty;

        var query = new NativeMethods.STORAGE_PROPERTY_QUERY
        {
            PropertyId = NativeMethods.StorageDeviceProperty,
            QueryType = NativeMethods.PropertyStandardQuery,
        };

        // 输入缓冲需大于结构本身（驱动校验 AdditionalParameters 空间），实测 64 字节可用。
        byte* queryBuffer = stackalloc byte[(int)NativeMethods.StorageQueryInputBufferSize];
        new Span<byte>(queryBuffer, (int)NativeMethods.StorageQueryInputBufferSize).Clear();
        *(NativeMethods.STORAGE_PROPERTY_QUERY*)queryBuffer = query;

        byte* descriptorBuffer = stackalloc byte[(int)NativeMethods.StorageQueryOutputBufferSize];
        if (!NativeMethods.DeviceIoControl(
                handle,
                NativeMethods.IOCTL_STORAGE_QUERY_PROPERTY,
                (IntPtr)queryBuffer,
                NativeMethods.StorageQueryInputBufferSize,
                (IntPtr)descriptorBuffer,
                NativeMethods.StorageQueryOutputBufferSize,
                out _,
                IntPtr.Zero))
        {
            return false;
        }

        // STORAGE_DEVICE_DESCRIPTOR 头部偏移：VendorIdOffset=12、ProductIdOffset=16、
        // SerialNumberOffset=24、BusType=28。
        busType = (DiskBusType)ReadUInt32(descriptorBuffer, 28);
        model = ReadAnsiString(descriptorBuffer, ReadUInt32(descriptorBuffer, 16));
        if (model.Length == 0)
        {
            model = ReadAnsiString(descriptorBuffer, ReadUInt32(descriptorBuffer, 12));
        }

        serial = ReadAnsiString(descriptorBuffer, ReadUInt32(descriptorBuffer, 24));
        return true;
    }

    private static unsafe uint ReadUInt32(byte* buffer, int offset) => *(uint*)(buffer + offset);

    private static unsafe string ReadAnsiString(byte* basePtr, uint offset)
    {
        if (offset == 0)
        {
            return string.Empty;
        }

        byte* pointer = basePtr + offset;
        int length = 0;
        while (length < 256 && pointer[length] != 0)
        {
            length++;
        }

        return Encoding.ASCII.GetString(new ReadOnlySpan<byte>(pointer, length)).Trim();
    }

    private static unsafe bool? TryGetSeekPenaltyProperty(string physicalDrivePath)
    {
        using SafeFileHandle handle = NativeMethods.CreateFileW(
            physicalDrivePath,
            0,
            NativeMethods.FILE_SHARE_READ_WRITE,
            IntPtr.Zero,
            NativeMethods.OPEN_EXISTING,
            0,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            return null;
        }

        var query = new NativeMethods.STORAGE_PROPERTY_QUERY
        {
            PropertyId = NativeMethods.StorageDeviceSeekPenaltyProperty,
            QueryType = NativeMethods.PropertyStandardQuery,
        };
        byte* queryBuffer = stackalloc byte[(int)NativeMethods.StorageQueryInputBufferSize];
        new Span<byte>(queryBuffer, (int)NativeMethods.StorageQueryInputBufferSize).Clear();
        *(NativeMethods.STORAGE_PROPERTY_QUERY*)queryBuffer = query;

        byte* buffer = stackalloc byte[16];
        if (!NativeMethods.DeviceIoControl(
                handle,
                NativeMethods.IOCTL_STORAGE_QUERY_PROPERTY,
                (IntPtr)queryBuffer,
                NativeMethods.StorageQueryInputBufferSize,
                (IntPtr)buffer,
                16,
                out _,
                IntPtr.Zero))
        {
            return null;
        }

        // DEVICE_SEEK_PENALTY_DESCRIPTOR：Version(4) Size(4) IncursSeekPenalty(8)。
        return buffer[8] != 0;
    }

    private static unsafe int? TryGetVolumeDiskNumber(string volumePath)
    {
        // 打开卷根路径时去掉尾部反斜杠：带 \ 时 CreateFile 报 ERROR_PATH_NOT_FOUND（实测）。
        using SafeFileHandle handle = NativeMethods.CreateFileW(
            volumePath.TrimEnd('\\'),
            0,
            NativeMethods.FILE_SHARE_READ_WRITE,
            IntPtr.Zero,
            NativeMethods.OPEN_EXISTING,
            0,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            return null;
        }

        NativeMethods.STORAGE_DEVICE_NUMBER* numberBuffer = stackalloc NativeMethods.STORAGE_DEVICE_NUMBER[1];
        if (!NativeMethods.DeviceIoControl(
                handle,
                NativeMethods.IOCTL_STORAGE_GET_DEVICE_NUMBER,
                IntPtr.Zero,
                0,
                (IntPtr)numberBuffer,
                (uint)sizeof(NativeMethods.STORAGE_DEVICE_NUMBER),
                out _,
                IntPtr.Zero))
        {
            return null;
        }

        return numberBuffer->DeviceType == NativeMethods.FILE_DEVICE_DISK
            ? (int)numberBuffer->DeviceNumber
            : null;
    }

    private static string? TryGetVolumeLabel(string rootPath)
    {
        var label = new StringBuilder(261);
        if (!NativeMethods.GetVolumeInformationW(
                rootPath, label, (uint)label.Capacity, out _, out _, out _, null, 0))
        {
            return null;
        }

        string text = label.ToString();
        return text.Length == 0 ? null : text;
    }

    private static (long? Total, long? Free) TryGetVolumeUsage(string rootPath)
    {
        if (!NativeMethods.GetDiskFreeSpaceExW(rootPath, out ulong freeAvailable, out ulong total, out _))
        {
            return (null, null);
        }

        return ((long)total, (long)freeAvailable);
    }
}
