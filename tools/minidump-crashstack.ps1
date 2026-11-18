# Parse minidump: exception stream -> thread context (RIP/RSP), module list,
# then scan stack memory for return addresses inside loaded modules.
# Usage: powershell -NoProfile -File minidump-crashstack.ps1 -DumpPath <path>
param(
    [Parameter(Mandatory=$true)][string]$DumpPath,
    [string]$StackAddress = '',
    [int]$ScanLen = 0x8000
)

$fs = [System.IO.File]::OpenRead($DumpPath)
$br = New-Object System.IO.BinaryReader($fs)
try {
    $fs.Seek(0,'Begin') | Out-Null
    $sig = $br.ReadUInt32()
    if ($sig -ne 0x504D444D) { throw "not a minidump" }
    $br.ReadUInt32() | Out-Null
    $streamCount = $br.ReadInt32()
    $br.ReadUInt32() | Out-Null
    $br.ReadUInt32() | Out-Null
    $br.ReadUInt32() | Out-Null
    $br.ReadUInt64() | Out-Null

    $fs.Seek(32,'Begin') | Out-Null
    $excRva = 0L; $modRva = 0L; $mem64Rva = 0L; $threadListRva = 0L
    for ($i=0; $i -lt $streamCount; $i++) {
        $type = $br.ReadUInt32(); $size = $br.ReadUInt32(); $rva = $br.ReadUInt32()
        if ($type -eq 6) { $excRva = $rva }
        if ($type -eq 4) { $modRva = $rva }
        if ($type -eq 9) { $mem64Rva = $rva }
        if ($type -eq 3) { $threadListRva = $rva }
    }

    # --- exception stream ---
    if ($excRva -ne 0) {
        $fs.Seek($excRva,'Begin') | Out-Null
        $threadId = $br.ReadUInt32()
        $br.ReadUInt32() | Out-Null
        $code = $br.ReadUInt32()
        $br.ReadUInt32() | Out-Null
        $br.ReadUInt64() | Out-Null
        $numParams = $br.ReadUInt32()
        $br.ReadUInt32() | Out-Null
        $info = @()
        for ($p=0; $p -lt 15; $p++) { $info += $br.ReadUInt64() }
        # ThreadContext location descriptor: DataSize(4) then Rva(4)
        $ctxSize = $br.ReadUInt32()
        $ctxRva = $br.ReadUInt32()
        Write-Host ("ExceptionThread=0x{0:X} Code=0x{1:X8}" -f $threadId, $code)
        for ($p=0; $p -lt [Math]::Min($numParams,15); $p++) { Write-Host ("  Info[{0}] = 0x{1:X16}" -f $p, $info[$p]) }

        # context: RIP/RSP/RBP
        if ($ctxRva -ne 0) {
            $fs.Seek($ctxRva,'Begin') | Out-Null
            $ctx = $br.ReadBytes([int]$ctxSize)
            $rsp = [BitConverter]::ToUInt64($ctx, 0x98)
            $rbp = [BitConverter]::ToUInt64($ctx, 0xA0)
            $rip = [BitConverter]::ToUInt64($ctx, 0xF8)
            Write-Host ("CONTEXT RIP=0x{0:X16} RSP=0x{1:X16} RBP=0x{2:X16}" -f $rip, $rsp, $rbp)
            $script:ExcRsp = $rsp
        }
    }

    # --- thread list: stack ranges per thread ---
    if ($threadListRva -ne 0) {
        $fs.Seek($threadListRva,'Begin') | Out-Null
        $tCount = $br.ReadUInt32()
        Write-Host ("Threads: {0}" -f $tCount)
        $wanted = if ($excRva -ne 0) { $threadId } else { 0 }
        for ($t=0; $t -lt $tCount; $t++) {
            $tid = $br.ReadUInt32()
            $br.ReadUInt32() | Out-Null # suspend count
            $br.ReadUInt32() | Out-Null # priority class
            $br.ReadUInt32() | Out-Null # priority
            $br.ReadUInt64() | Out-Null # teb
            $stkStart = $br.ReadUInt64()
            $stkSize = $br.ReadUInt32()
            $stkRva = $br.ReadUInt32()
            $ctxS = $br.ReadUInt32()
            $ctxR = $br.ReadUInt32()
            if ($tid -eq $wanted) {
                Write-Host ("  fault thread stack: start=0x{0:X16} size=0x{1:X} rva=0x{2:X} ctxRva=0x{3:X}" -f $stkStart, $stkSize, $stkRva, $ctxR)
                $script:FaultStackVa = $stkStart
                $script:FaultStackSize = $stkSize
                $script:FaultStackRva = $stkRva
                if ($ctxR -ne 0) {
                    $saved = $fs.Position
                    $fs.Seek($ctxR,'Begin') | Out-Null
                    $c = $br.ReadBytes(0x400)
                    $rsp2 = [BitConverter]::ToUInt64($c, 0x98)
                    $rip2 = [BitConverter]::ToUInt64($c, 0xF8)
                    Write-Host ("  thread-list CONTEXT RIP=0x{0:X16} RSP=0x{1:X16}" -f $rip2, $rsp2)
                    $fs.Seek($saved,'Begin') | Out-Null
                }
            }
        }
    }

    # --- module list ---
    $fs.Seek($modRva,'Begin') | Out-Null
    $modCount = $br.ReadUInt32()
    $mods = New-Object System.Collections.ArrayList
    $fileLen = $fs.Length
    for ($m=0; $m -lt $modCount; $m++) {
        $b = $br.ReadBytes(108)
        $base = [BitConverter]::ToUInt64($b, 0)
        $sz   = [BitConverter]::ToUInt32($b, 8)
        $nameRva = [BitConverter]::ToUInt32($b, 20)
        $name = "?"
        if ($nameRva -ne 0 -and ($nameRva + 4) -lt $fileLen) {
            $saved = $fs.Position
            $fs.Seek($nameRva,'Begin') | Out-Null
            $nlen = $br.ReadUInt32()
            if ($nlen -gt 0 -and $nlen -lt 4096 -and ($nameRva + 4 + $nlen) -le $fileLen) {
                $nbytes = $br.ReadBytes([int]$nlen)
                $name = [System.Text.Encoding]::Unicode.GetString($nbytes)
            }
            $fs.Seek($saved,'Begin') | Out-Null
        }
        [void]$mods.Add(@($base, [uint64]$sz, $name))
    }
    Write-Host ("Modules: {0}" -f $mods.Count)
    foreach ($md in $mods) {
        Write-Host ("  0x{0:X16} +0x{1:X8}  {2}" -f $md[0], $md[1], $md[2])
    }

    function Find-Module([uint64]$addr) {
        foreach ($md in $mods) {
            if ($addr -ge $md[0] -and $addr -lt ($md[0] + $md[1])) {
                return ("{0}+0x{1:X}" -f (Split-Path $md[2] -Leaf), ($addr - $md[0]))
            }
        }
        return $null
    }

    # --- memory ranges (Memory64ListStream: descriptors are StartVa(8)+DataSize(8);
    #     data blocks follow the descriptor array in order) ---
    $fs.Seek($mem64Rva,'Begin') | Out-Null
    $rangeCount = $br.ReadUInt64()
    $baseRva = $br.ReadUInt64()   # header is NumberOfMemoryRanges(8) + BaseRva(8)
    $ranges = New-Object System.Collections.ArrayList
    $curRva = [uint64]$baseRva
    for ($i=0; $i -lt $rangeCount; $i++) {
        $sv = $br.ReadUInt64(); $ds = $br.ReadUInt64()
        [void]$ranges.Add(@($sv, $ds, $curRva))
        $curRva += $ds
    }

    if ($StackAddress -ne '' -or $script:FaultStackRva) {
        if ($StackAddress -ne '') {
            $rsp = [Convert]::ToUInt64($StackAddress, 16)
            $stackRange = $null
            foreach ($rg in $ranges) {
                if ($rsp -ge $rg[0] -and $rsp -lt ($rg[0] + $rg[1])) { $stackRange = $rg; break }
            }
            if (-not $stackRange) { throw "stack address not in dumped memory" }
            $len = [Math]::Min($ScanLen, [int](($stackRange[0] + $stackRange[1]) - $rsp))
            if ($len -lt 8) { $len = 8 }
            Write-Host ("range: va=0x{0:X} size=0x{1:X} rva=0x{2:X} file=0x{3:X}" -f $stackRange[0], $stackRange[1], $stackRange[2], $fs.Length)
            $fs.Seek([int64]($stackRange[2] + ($rsp - $stackRange[0])),'Begin') | Out-Null
        } else {
            $rsp = $script:FaultStackVa
            $len = [int]$script:FaultStackSize
            $fs.Seek([int64]$script:FaultStackRva,'Begin') | Out-Null
        }
        $stack = $br.ReadBytes($len)
        Write-Host ("--- stack scan at 0x{0:X16} len 0x{1:X} (module-code addresses) ---" -f $rsp, $stack.Length)
        $hits = 0
        for ($off = 0; $off -lt ($stack.Length - 8) -and $hits -lt 80; $off += 8) {
            $val = [BitConverter]::ToUInt64($stack, $off)
            $mr = Find-Module $val
            if ($mr) {
                Write-Host ("+0x{0:X4}: 0x{1:X16}  {2}" -f $off, $val, $mr)
                $hits++
            }
        }
    }
} finally {
    $br.Close(); $fs.Close()
}
