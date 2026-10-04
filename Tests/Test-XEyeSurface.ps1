param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
function Assert($Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
$eye = Get-Content -LiteralPath (Join-Path $Root 'Shaders/Tom/EyeX.shader') -Raw
$optics = Get-Content -LiteralPath (Join-Path $Root 'Shaders/Tom/TomEyeOptics.cginc') -Raw
$surface = Get-Content -LiteralPath (Join-Path $Root 'Shaders/Tom/TomEyeSurface.cginc') -Raw
$coverage = Get-Content -LiteralPath (Join-Path $Root 'Shaders/Tom/TomEyeCoverage.cginc') -Raw
$coat = Get-Content -LiteralPath (Join-Path $Root 'Shaders/Tom/TomClearcoat.cginc') -Raw
$inputSource = Get-Content -LiteralPath (Join-Path $Root 'Shaders/Tom/TomEyeInput.cginc') -Raw
Assert ($eye -match '_EyeRefractionIOR[^\r\n]*= 1\.5') 'Independent IOR default changed'
Assert ($eye -match '_IrisDepthShape[^\r\n]*= 0') 'Old procedural flat must remain default'
Assert ($eye -match '_UseEyeSurfaceMask[^\r\n]*= 0') 'Surface map must be opt-in'
Assert ($eye -match '_EyeSurfaceMapMode[^\r\n]*= 0') 'Grayscale height must be default'
Assert ($eye -match '_EyeSurfaceMap[^\r\n]*= "white"') 'Missing height map must give zero recess'
Assert ($eye.Contains('[Enum(TomShadersX.EyeDebugMode)]')) 'Nine debug entries need a named Unity 2019 enum'
Assert ($eye -match '_IrisDepth \([^\r\n]*Range\(0,0\.5\)\) = 0\.08') 'Existing depth gain/default changed'
Assert ($optics -notmatch '_ClearCoatIOR') 'Coat IOR leaked into optical rays/dispersion'
Assert ($coat -notmatch '_EyeRefractionIOR|_EyeSurfaceMap|_IrisDepth') 'Eye geometry leaked into shared coat'
Assert ($coverage -notmatch '_UseEyeSurfaceMask|_EyeSurfaceMap|_EyeRefractionIOR|_IrisDepthShape') 'Optical region changed alpha/stencil'
Assert ($inputSource.Contains('DECLARE_TEX2D_NOSAMPLER(_EyeSurfaceMap)')) 'Unexpected extra sampler'
Assert (($inputSource+$surface) -notmatch '_EyeSurfaceMap_ST') 'Surface map must share final iris UV'
Assert ($surface.Contains('_EyeSurfaceMap.SampleGrad(sampler_MainTex, uv, dx, dy)')) 'Surface sampling needs explicit aligned gradients'
Assert ($surface.Contains('1.0 - map.r')) 'Gray height polarity is reversed'
Assert ($surface.Contains('float2(map.r * map.a, map.a)')) 'Packed depth/region definition changed'
Assert ($optics.IndexOf('TomEyeSurfaceHit(') -lt $optics.IndexOf('0.8 * max(0.0, 1.0 - length(q))')) 'Template must replace the legacy aperture cap'
Assert ($surface -notmatch 'while\s*\(|ddx\(|ddy\(') 'No unbounded traversal or nested derivatives'

# Reference only: six local secant refinements vs an independent high-precision
# bisection for shallow, smooth, single-root cone/bowl fields. GPU tests are separate.
function Depth([double]$X, [double]$Y, [int]$Shape) {
    $r2 = $X*$X + $Y*$Y
    $value = if ($Shape -eq 0) { 1.0 } elseif ($Shape -eq 1) { 1.0 - [math]::Sqrt($r2) } else { 1.0 - $r2 }
    [math]::Max(0.0, [math]::Min(1.0, $value))
}
$count = 0; $worst = 0.0
foreach ($shape in @(0,1,2)) { foreach ($x in @(-.8,-.4,0,.4,.8)) { foreach ($y in @(-.4,0,.4)) {
    foreach ($tx in @(-.25,-.1,0,.1,.25)) { foreach ($ty in @(-.1,0,.1)) {
        $lo=0.0; $hi=1.0; $flo=-(Depth $x $y $shape); $fhi=1.0-(Depth ($x+$tx) ($y+$ty) $shape)
        $t=0.0
        for ($i=0; $i -lt 6; $i++) {
            $fraction=[math]::Max(0.0,[math]::Min(1.0,-$flo/[math]::Max($fhi-$flo,.000001)))
            $t=$lo+($hi-$lo)*$fraction
            $residual=$t-(Depth ($x+$tx*$t) ($y+$ty*$t) $shape)
            if ($residual -lt 0) {$lo=$t;$flo=$residual} else {$hi=$t;$fhi=$residual}
        }
        $lo=0.0; $hi=1.0
        for ($i=0; $i -lt 48; $i++) {
            $mid=($lo+$hi)*.5
            if ($mid -lt (Depth ($x+$tx*$mid) ($y+$ty*$mid) $shape)) {$lo=$mid} else {$hi=$mid}
        }
        $rootError=[math]::Abs($t-($lo+$hi)*.5);$worst=[math]::Max($worst,$rootError)
        Assert ($rootError -lt .002) "Shallow intersection error: $rootError"
        Assert ($t -ge 0 -and $t -le 1) 'Intersection outside normalized depth range'
        $count++
    }}
}}}
foreach ($angle in @(15,35,55)) {
    $s=[math]::Sin($angle*[math]::PI/180);$previous=[double]::PositiveInfinity
    foreach ($ior in @(1,1.33,1.5,2.5)) {
        $offset=$s/[math]::Sqrt($ior*$ior-$s*$s)
        Assert ($offset -lt $previous) 'Refraction IOR should reduce unclamped plane parallax'
        $previous=$offset
    }
}
Write-Output "PASS: independent IOR, unchanged coverage/defaults, map UV/encoding, $count shallow-root cases; worst normalized root error=$worst."
