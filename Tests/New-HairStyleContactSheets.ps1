param([Parameter(Mandatory=$true)][string]$ReportDirectory)
$ErrorActionPreference = 'Stop'
$folder = (Resolve-Path -LiteralPath $ReportDirectory).Path
$report = Get-Content -LiteralPath (Join-Path $folder 'report.json') -Raw | ConvertFrom-Json
Add-Type -AssemblyName System.Drawing
$groups = [ordered]@{
    directions = 'HairX | Anisotropy and strand direction'
    models = 'HairX | Continuous GGX, Toon blend and bands'
    shape = 'HairX | Toon highlight shape'
    antialiasing = 'HairX | Packed-normal AA diagnostic'
    lights = 'HairX | Per-light stylized highlights'
    layers = 'HairX | Shared art layers on crossing hair cards'
    geometry = 'HairX | Rim and inverted-hull geometry check'
}
$tile = 288
$captionHeight = 58
$header = 82
$resources = [Collections.Generic.List[IDisposable]]::new()
try {
    $titleFont = [Drawing.Font]::new('Segoe UI',18,[Drawing.FontStyle]::Bold)
    $font = [Drawing.Font]::new('Segoe UI',11)
    $small = [Drawing.Font]::new('Segoe UI',9)
    $ink = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(237,240,245))
    $muted = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(175,185,197))
    foreach ($item in @($titleFont,$font,$small,$ink,$muted)) { $resources.Add($item) }
    foreach ($group in $groups.GetEnumerator()) {
        $shots = @($report.shots | Where-Object group -eq $group.Key)
        if ($shots.Count -eq 0) { continue }
        $columns = if ($group.Key -in 'antialiasing','geometry') { 2 } else { 3 }
        $rows = [int][Math]::Ceiling($shots.Count / [double]$columns)
        $canvas = [Drawing.Bitmap]::new($columns*$tile,$header+$rows*($tile+$captionHeight))
        $graphics = [Drawing.Graphics]::FromImage($canvas)
        try {
            $graphics.Clear([Drawing.Color]::FromArgb(23,27,33))
            $graphics.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.DrawString($group.Value,$titleFont,$ink,12,8)
            $graphics.DrawString('Unity Camera.Render | Stencil OFF | Fixed x4 / Reinhard preview',$small,$muted,12,41)
            $graphics.DrawString('Metrics below each image are from unmodified float readback. No auto exposure.',$small,$muted,12,58)
            for ($i=0; $i -lt $shots.Count; $i++) {
                $shot = $shots[$i]
                if ([IO.Path]::GetFileName($shot.name) -ne $shot.name) { throw 'Unexpected image name in report.' }
                $x = ($i % $columns)*$tile
                $y = $header + [int][Math]::Floor($i/[double]$columns)*($tile+$captionHeight)
                $image = [Drawing.Image]::FromFile((Join-Path $folder ($shot.name+'.png')))
                try { $graphics.DrawImage($image,[Drawing.Rectangle]::new($x,$y,$tile,$tile)) }
                finally { $image.Dispose() }
                $label = [Drawing.RectangleF]::new($x+8,$y+$tile+4,$tile-12,24)
                $graphics.DrawString($shot.caption,$font,$ink,$label)
                $metrics = 'Peak {0:0.000} | highlight pixels {1}' -f $shot.peak,$shot.brightPixels
                $graphics.DrawString($metrics,$small,$muted,$x+8,$y+$tile+30)
            }
            $path = Join-Path $folder ($group.Key+'-sheet.png')
            $canvas.Save($path,[Drawing.Imaging.ImageFormat]::Png)
            Write-Output $path
        }
        finally { $graphics.Dispose(); $canvas.Dispose() }
    }
}
finally { foreach ($resource in $resources) { $resource.Dispose() } }
