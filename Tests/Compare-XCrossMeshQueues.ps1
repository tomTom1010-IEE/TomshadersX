param(
    [Parameter(Mandatory=$true)][string]$ReferenceDirectory,
    [Parameter(Mandatory=$true)][string]$CandidateDirectory
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
function Get-ObjectRegionHash([string]$Path) {
    $bitmap = [Drawing.Bitmap]::FromFile($Path)
    $data = $null
    $hash = [Security.Cryptography.SHA256]::Create()
    try {
        if ($bitmap.Width -ne 1800 -or $bitmap.Height -ne 1000) { throw "Unexpected dimensions: $Path" }
        # Exclude queue captions; compare every pixel of all three object/grid columns.
        $region = [Drawing.Rectangle]::new(0, 220, 1800, 604)
        $data = $bitmap.LockBits($region, [Drawing.Imaging.ImageLockMode]::ReadOnly, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        if ($data.Stride -ne 7200) { throw 'Unexpected row stride' }
        $bytes = [byte[]]::new($data.Stride * $region.Height)
        [Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
        [BitConverter]::ToString($hash.ComputeHash($bytes)).Replace('-', '')
    } finally {
        if ($null -ne $data) { $bitmap.UnlockBits($data) }
        $bitmap.Dispose(); $hash.Dispose()
    }
}
$reference = (Resolve-Path -LiteralPath $ReferenceDirectory).Path
$candidate = (Resolve-Path -LiteralPath $CandidateDirectory).Path
foreach ($file in @('01-full-coverage.png','02-full-toon.png','03-side-coverage.png','04-side-toon.png','05-control-late-rear.png')) {
    $a = Get-ObjectRegionHash (Join-Path $reference $file)
    $b = Get-ObjectRegionHash (Join-Path $candidate $file)
    [pscustomobject]@{
        Image = $file
        Region = 'x=0 y=220 width=1800 height=604'
        PixelsCompared = 1087200
        ReferenceHash = $a
        CandidateHash = $b
        Identical = $a -ceq $b
    }
}
