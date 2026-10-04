param([Parameter(Mandatory=$true)][string]$ReportDirectory)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = (Resolve-Path -LiteralPath $ReportDirectory).Path
$results = foreach ($pair in @(
    @('01-coverage-open-view.png', '05-coverage-reversed-triangles.png'),
    @('02-toon-open-view.png', '07-toon-reversed-triangles.png'))) {
    $a = [Drawing.Bitmap]::FromFile((Join-Path $root $pair[0]))
    $b = [Drawing.Bitmap]::FromFile((Join-Path $root $pair[1]))
    try {
        if ($a.Width -ne 1800 -or $a.Height -ne 1000 -or $b.Size -ne $a.Size) { throw 'Unexpected capture dimensions' }
        foreach ($column in 0..2) {
            $sum = 0.0; $max = 0; $changed = 0; $count = 0
            # Sample object/grid only, excluding all changing captions, every two pixels.
            for ($y = 225; $y -lt 815; $y += 2) {
                for ($x = $column * 600 + 25; $x -lt $column * 600 + 575; $x += 2) {
                    $p = $a.GetPixel($x, $y); $q = $b.GetPixel($x, $y)
                    $r = [Math]::Abs([int]$p.R - [int]$q.R)
                    $g = [Math]::Abs([int]$p.G - [int]$q.G)
                    $blue = [Math]::Abs([int]$p.B - [int]$q.B)
                    $difference = [Math]::Max($r, [Math]::Max($g, $blue))
                    $max = [Math]::Max($max, $difference)
                    $sum += $r + $g + $blue; $count++
                    if ($difference -gt 3) { $changed++ }
                }
            }
            [pscustomobject]@{
                Image = $pair[0]
                Variant = @('MainAlphaX', 'MainAlphaX2Pass', 'MainAlphaXBackFront')[$column]
                Samples = $count
                MeanChannelError = $sum / ($count * 3 * 255)
                Maximum8BitDifference = $max
                SamplesAbove3 = $changed
            }
        }
    } finally { $a.Dispose(); $b.Dispose() }
}
$results
