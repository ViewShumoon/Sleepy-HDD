# Read memory at a virtual address from a minidump Memory64List stream.
# Usage: powershell -File read-minidump-memory.ps1 -DumpPath <path> -Address <hex> [-Length 256]
param(
    [Parameter(Mandatory=$true)][string]$DumpPath,
    [Parameter(Mandatory=$true)][string]$Address,
    [int]$Length = 256
)

$va = [Convert]::ToUInt64($Address, 16)
$fs = [System.IO.File]::OpenRead($DumpPath)
$br = New-Object System.IO.BinaryReader($fs)
$found = $false
try {
    $sig = $br.ReadUInt32()
    if ($sig -ne 0x504D444D) { throw "not a minidump" }
    $fs.Seek(0, 'Begin') | Out-Null
    $br.ReadUInt32() | Out-Null
    $br.ReadUInt32() | Out-Null
    $streamCount = $br.ReadInt32()
    $br.ReadUInt32() | Out-Null
    $br.ReadUInt32() | Out-Null
    $br.ReadUInt32() | Out-Null
    $br.ReadUInt64() | Out-Null
    $fs.Seek(32, 'Begin') | Out-Null
    $mem64Rva = 0L
    for ($i = 0; $i -lt $streamCount; $i++) {
        $type = $br.ReadUInt32()
        $size = $br.ReadUInt32()
        $rva = $br.ReadUInt32()
        if ($type -eq 9) { $mem64Rva = $rva; break }
    }
    if ($mem64Rva -eq 0) { throw "no Memory64List stream" }
    $fs.Seek($mem64Rva, 'Begin') | Out-Null
    $rangeCount = $br.ReadUInt64()
    $descs = New-Object System.Collections.ArrayList
    for ($i = 0; $i -lt $rangeCount; $i++) {
        $startVa = $br.ReadUInt64()
        $dataSize = $br.ReadUInt32()
        $rva2 = $br.ReadUInt32()
        [void]$descs.Add(@($startVa, [uint64]$dataSize, [uint64]$rva2))
    }
    foreach ($d in $descs) {
        $lo = $d[0]
        $hi = $d[0] + $d[1]
        if ($va -ge $lo -and ($va + [uint64]$Length) -le $hi) {
            $off = $d[2] + ($va - $lo)
            $fs.Seek($off, 'Begin') | Out-Null
            $bytes = $br.ReadBytes($Length)
            for ($row = 0; $row -lt $Length; $row += 16) {
                $end = $row + 15
                if ($end -ge $Length) { $end = $Length - 1 }
                $hexParts = @()
                $asciiParts = @()
                for ($c = $row; $c -le $end; $c++) {
                    $b = $bytes[$c]
                    $hexParts += ('{0:X2}' -f $b)
                    if ($b -ge 32 -and $b -le 126) { $asciiParts += [string][char]$b } else { $asciiParts += '.' }
                }
                Write-Host ('{0:X16}  {1,-47}  {2}' -f ($va + [uint64]$row), ($hexParts -join ' '), ($asciiParts -join ''))
            }
            $found = $true
            break
        }
    }
    if (-not $found) { throw ('address 0x{0:X} not covered by memory ranges' -f $va) }
} finally {
    $br.Close()
    $fs.Close()
}
