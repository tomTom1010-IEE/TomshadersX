param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
function Assert($condition,$message) { if (-not $condition) { throw $message } }
[xml]$manifest = Get-Content -LiteralPath (Join-Path $Root 'manifest.xml') -Raw
[xml]$tooltips = Get-Content -LiteralPath (Join-Path $Root 'Tooltips/tom_x_tooltips.xml') -Raw
$entry = $manifest.SelectSingleNode('//Shader[@Name="tom/HairX"]')
$tips = $tooltips.SelectSingleNode('//Shader[@Name="tom/HairX"]')
$shader = Get-Content -LiteralPath (Join-Path $Root 'Shaders/Tom/HairX.shader') -Raw
$properties = @([regex]::Matches($shader.Split('SubShader')[0], '(?m)^\s*(?:\[[^\]]+\]\s*)?_(\w+)\s*\(') | ForEach-Object {$_.Groups[1].Value})
$contract = @(Get-Content -LiteralPath (Join-Path $Root 'Tests/HairX-v1.properties.txt'))
$currentContract = @($shader.Split('SubShader')[0] -split '\r?\n' | Where-Object { $_ -match '^\s*(?:\[[^\]]+\]\s*)*_\w+\s*\(' } | ForEach-Object { $_.Trim() })
Assert (($contract -join "`n") -ceq ($currentContract[0..109] -join "`n")) 'Frozen HairX v1 property names/types/defaults changed. Review migration and contract version.'
Assert ($properties.Count -eq 120) 'Expected 110 frozen HairX plus ten coat properties'
Assert ($entry.Asset -eq 'a_TomHairX') 'Wrong HairX carrier'
Assert (@(Compare-Object $properties @($entry.Property.Name)).Count -eq 0) 'HairX manifest properties differ'
Assert (@(Compare-Object $properties @($tips.Property.Name)).Count -eq 0) 'HairX property tooltips differ'
Assert (@($entry.Property | Where-Object {-not $_.Category}).Count -eq 0) 'Uncategorized HairX property'
Assert ($shader.Contains('_HairFrontZWrite ("HairFront ZWrite", Float) = 1')) 'Default ZWrite changed'
Assert ($shader.Contains('_HairFrontMode ("HairFront Mode", Float) = 0')) 'HairFront must default Off'
Assert ($shader.Contains('_HairFeatherThreshold ("HairFront Feather Midpoint", Range(0.05,0.95)) = 0.5')) 'Feather midpoint default/range changed'
Assert ($shader.Contains('_HairFeatherPower ("HairFront Feather Power", Range(0.5,4)) = 1')) 'Feather power default/range changed'
foreach ($name in 'HairFeatherThreshold','HairFeatherPower') {
    $property = $entry.SelectSingleNode('Property[@Name="'+$name+'"]')
    Assert ($property.Category -eq 'HairFront' -and $property.Type -eq 'Float') "Wrong feather category/type: $name"
}
Assert ($entry.SelectSingleNode('Property[@Name="HairFeatherThreshold"]').Range -eq '0.05, 0.95') 'Manifest midpoint range differs'
Assert ($entry.SelectSingleNode('Property[@Name="HairFeatherPower"]').Range -eq '0.5, 4') 'Manifest power range differs'
Assert ($shader.Contains('_StrandDirectionBlend ("Tangent / Direction Map Blend", Range(0,1))')) 'Flow direction must be continuous'
Assert (([regex]::Matches($shader,'multi_compile_fwdadd_fullshadows')).Count -eq 2) 'Both stencil regions need full-shadow Add'
Assert (([regex]::Matches($shader,'Comp Equal Pass Keep')).Count -eq 3) 'Inside stencil groups missing'
Assert (([regex]::Matches($shader,'Comp NotEqual Pass Keep')).Count -eq 3) 'Outside stencil groups missing'
Assert (-not $shader.Contains('GrabPass')) 'Scene-color grab is forbidden'
$shadow = Get-Content -LiteralPath (Join-Path $Root 'Shaders/Tom/TomHairShadow.cginc') -Raw
Assert (-not ($shadow -match 'HairFront|Stencil|Feather')) 'Camera coverage must not enter shadowcaster'
$coverage = Get-Content -LiteralPath (Join-Path $Root 'Shaders/Tom/TomHairCoverage.cginc') -Raw
Assert ($coverage.Contains('clip(coverage - 0.00001)')) 'Invisible hair must not occlude depth'
Assert ($coverage.Contains('_TomHairFeatherParams.x > 0.5')) 'Missing-provider fallback missing'
Assert ($coverage.Contains('float weight = TomHairFeatherWeight(distance, widthPixels);')) 'Feather curve is not connected'
Assert ($coverage.Contains('float weight = smoothstep(0.0, widthPixels, distance);') -and $coverage.Contains('if (_HairFeatherThreshold != 0.5 || _HairFeatherPower != 1.0)')) 'Default must retain the original smoothstep path'
Assert ($shader.Contains('_HairFeatherWidthMode ("HairFront Feather Width Mode", Float) = 0')) 'Legacy pixel units must remain default'
Assert ($coverage.Contains('unity_OrthoParams.w') -and $coverage.Contains('_TomHairFeatherParams.w')) 'World width needs orthographic conversion and a search budget'
Assert (-not $coverage.Contains('_CameraDepthTexture')) 'Feather must not depend on camera depth texture'
Assert ($coverage.Contains('clamp(_HairFeatherThreshold, 0.05, 0.95)') -and $coverage.Contains('clamp(_HairFeatherPower, 0.5, 4.0)')) 'Script-set curve values must be bounded'
$runtime = Get-Content -LiteralPath (Join-Path $Root 'Runtime/TomHairFeatherCamera.cs') -Raw
Assert ($runtime.Contains('BeforeForwardOpaque')) 'Cannot capture after opaque cutout hair'
Assert ($runtime.Contains('if (!needed') -and $runtime.Contains('Release(); return;')) 'Disabled feather must release work'
Assert ($runtime.Contains('material.renderQueue >= earliestHairQueue')) 'Late eye writers must not invent coverage'
Assert (-not ($runtime -match 'HairFeatherThreshold|HairFeatherPower')) 'Curve must not change distance-provider scheduling'

function Get-FeatherWeight([double]$u, [double]$threshold, [double]$power) {
    $u = [Math]::Max(0.0, [Math]::Min(1.0, $u))
    $threshold = [Math]::Max(0.05, [Math]::Min(0.95, $threshold))
    $power = [Math]::Max(0.5, [Math]::Min(4.0, $power))
    $before = $u * (1 - $threshold)
    $after = (1 - $u) * $threshold
    $biased = $before / ($before + $after)
    $rising = [Math]::Pow($biased, $power)
    $falling = [Math]::Pow(1 - $biased, $power)
    $s = $rising / ($rising + $falling)
    return $s * $s * (3 - 2 * $s)
}
$samples = 0
foreach ($threshold in 0.05,0.25,0.5,0.75,0.95) {
    foreach ($power in 0.5,1,2,4) {
        Assert ((Get-FeatherWeight 0 $threshold $power) -eq 0) 'Outer endpoint changed'
        Assert ((Get-FeatherWeight 1 $threshold $power) -eq 1) 'Inner endpoint changed'
        Assert ([Math]::Abs((Get-FeatherWeight $threshold $threshold $power) - 0.5) -lt 1e-12) 'Power moved the midpoint'
        $previous = -1
        foreach ($i in 0..100) {
            $u = $i / 100.0
            $weight = Get-FeatherWeight $u $threshold $power
            Assert (-not [double]::IsNaN($weight) -and -not [double]::IsInfinity($weight)) 'Nonfinite feather weight'
            Assert ($weight -ge 0 -and $weight -le 1 -and $weight -ge $previous) 'Feather must be bounded and monotonic'
            Assert ([Math]::Abs($weight + (Get-FeatherWeight (1-$u) (1-$threshold) $power) - 1) -lt 1e-12) 'Curve lost complementary symmetry'
            if ($threshold -eq 0.5 -and $power -eq 1) {
                Assert ([Math]::Abs($weight - $u*$u*(3-2*$u)) -lt 1e-12) 'Default differs from original smoothstep'
            }
            $previous = $weight
            $samples++
        }
    }
    $previousSlope = 0
    foreach ($power in 0.5,1,2,4) {
        $slope = (Get-FeatherWeight ($threshold+0.001) $threshold $power) - (Get-FeatherWeight ($threshold-0.001) $threshold $power)
        Assert ($slope -gt $previousSlope) 'Increasing Power must steepen the midpoint'
        $previousSlope = $slope
    }
}
Assert ([Math]::Abs((1-0.8*(Get-FeatherWeight 0.25 0.25 4)) - 0.6) -lt 1e-12) 'Half progress must not force alpha 0.5'
& (Join-Path $PSScriptRoot 'Test-XAlpha.ps1') -Root $Root
Write-Output "PASS: HairX frozen v1 contract + coat, 120 properties/tooltips, $samples feather samples, stencil/cutout/depth contracts, isolated shadow, optional distance provider."
