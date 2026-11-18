# 解析 minidump 异常流（stream type 6），输出崩溃线程、异常码、异常地址与原始参数。
# 用法: powershell -File read-minidump-exception.ps1 -DumpPath <path>
param([Parameter(Mandatory=$true)][string]$DumpPath)

$fs = [System.IO.File]::OpenRead($DumpPath)
$br = New-Object System.IO.BinaryReader($fs)
try {
    $sig = $br.ReadUInt32()
    if ($sig -ne 0x504D444D) { throw "not a minidump (sig=0x$($sig.ToString('X8')))" }
    $br.ReadUInt32() | Out-Null # version (minor+major)
    $streamCount = $br.ReadInt32()
    $br.ReadUInt32() | Out-Null # checksum
    $br.ReadUInt32() | Out-Null # time
    $br.ReadUInt32() | Out-Null # reserved0
    $br.ReadUInt64() | Out-Null # flags
    $dirRva = 32L # 目录表固定紧随 32 字节头
    $fs.Seek($dirRva, 'Begin') | Out-Null
    $exceptionRva = 0L
    $moduleRva = 0L
    for ($i = 0; $i -lt $streamCount; $i++) {
        $type = $br.ReadUInt32(); $size = $br.ReadUInt32(); $rva = $br.ReadUInt32()
        if ($type -eq 6) { $exceptionRva = $rva }
        if ($type -eq 4) { $moduleRva = $rva }
    }
    if ($exceptionRva -eq 0) { throw 'no exception stream' }
    $fs.Seek($exceptionRva, 'Begin') | Out-Null
    # MINIDUMP_EXCEPTION_STREAM: ThreadId(4) __alignment(4) ExceptionRecord ThreadContext(8)
    $threadId = $br.ReadUInt32()
    $br.ReadUInt32() | Out-Null
    # MINIDUMP_EXCEPTION: Code(4) Flags(4) Record(8) NumberParameters(4) Unused(4) Info[15](120) Address(8)
    $code = $br.ReadUInt32()
    $flags = $br.ReadUInt32()
    $br.ReadUInt64() | Out-Null
    $numParams = $br.ReadUInt32()
    $br.ReadUInt32() | Out-Null
    $info = New-Object 'uint64[]' 15
    for ($p = 0; $p -lt 15; $p++) { $info[$p] = $br.ReadUInt64() }
    $addr = $br.ReadUInt64()
    Write-Host ("ThreadId       : 0x{0:X4} ({0})" -f $threadId)
    Write-Host ("ExceptionCode  : 0x{0:X8}" -f $code)
    Write-Host ("ExceptionFlags : 0x{0:X8}" -f $flags)
    Write-Host ("ExceptionAddr  : 0x{0:X16}" -f $addr)
    for ($p = 0; $p -lt [Math]::Min($numParams, 15); $p++) {
        Write-Host ("Parameter[{0}]   : 0x{1:X16}" -f $p, $info[$p])
    }
    if ($moduleRva -ne 0) {
        $fs.Seek($moduleRva, 'Begin') | Out-Null
        $count = $br.ReadUInt32()
        for ($m = 0; $m -lt $count; $m++) {
            # MINIDUMP_MODULE: Base(8) Size(4) Checksum(4) Timestamp(4) NameRva(4) FFI(52) Cv(8) Misc(8) = 92
            $b = New-Object byte[] 92
            $fs.Read($b, 0, 92) | Out-Null
            $base = [BitConverter]::ToUInt64($b, 0)
            $sizeOfImg = [BitConverter]::ToUInt32($b, 8)
            $nameRva = [BitConverter]::ToUInt32($b, 20)
            if ($addr -ge $base -and $addr -lt ($base + $sizeOfImg)) {
                $saved = $fs.Position
                $fs.Seek($nameRva, 'Begin') | Out-Null
                $len = $br.ReadUInt32()
                $bytes = $br.ReadBytes([int]$len)
                $name = [System.Text.Encoding]::Unicode.GetString($bytes)
                Write-Host ("FaultingModule : {0}  (base 0x{1:X}, offset 0x{2:X})" -f $name, $base, ($addr - $base))
                $fs.Seek($saved, 'Begin') | Out-Null
                break
            }
        }
    }
} finally {
    $br.Close(); $fs.Close()
}
