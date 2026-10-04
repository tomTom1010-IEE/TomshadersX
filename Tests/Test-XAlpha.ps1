param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
function Assert($Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
[xml]$manifest = Get-Content (Join-Path $Root 'manifest.xml') -Raw
[xml]$tooltips = Get-Content (Join-Path $Root 'Tooltips/tom_x_tooltips.xml') -Raw
$common = Get-Content (Join-Path $Root 'Shaders/Tom/TomAlphaCommon.cginc') -Raw
foreach ($name in @('MainAlphaX', 'MainAlphaX2Pass', 'MainAlphaXBackFront')) {
    $source = Get-Content (Join-Path $Root "Shaders/Tom/$name.shader") -Raw
    $properties = $source.Substring(0, $source.IndexOf('SubShader'))
    $names = @([regex]::Matches($properties, '(?m)^\s*(?:\[[^\]]+\]\s*)*_(\w+)\s*\(') | ForEach-Object { $_.Groups[1].Value })
    $entry = $manifest.SelectSingleNode("//Shader[@Name='tom/$name']")
    $catalog = $tooltips.SelectSingleNode("//Shader[@Name='tom/$name']")
    $expectedCount = if ($name -eq 'MainAlphaX2Pass') { 114 } else { 115 }
    Assert ($names.Count -eq $expectedCount) "$name property count"
    Assert (@(Compare-Object $names @($entry.Property.Name)).Count -eq 0) "$name manifest mismatch"
    Assert (@(Compare-Object $names @($catalog.Property.Name)).Count -eq 0) "$name tooltip mismatch"
    Assert (@($entry.Property | Group-Object Name | Where-Object Count -gt 1).Count -eq 0) "$name duplicate property"
    Assert ($entry.Asset -eq "a_Tom$name") "$name asset binding"
    Assert ($source.Contains('Shader "tom/' + $name + '"')) "$name shader identity"
    Assert ($source.Contains('"Queue" = "Transparent"')) "$name queue"
    Assert ($source.Contains('Blend [_AlphaBlendMode] OneMinusSrcAlpha, One OneMinusSrcAlpha')) "$name Base blend"
    Assert ($source.Contains('Blend [_AlphaBlendMode] One, Zero One')) "$name Add blend"
    Assert ($source.Contains('multi_compile_fwdadd_fullshadows')) "$name full-shadow variants"
    Assert ($properties -match '_AlphaBlendMode[^\r\n]*= 5') "$name straight default"
    Assert ($properties -match '_AlphaOptionCutoff[^\r\n]*= 0') "$name continuous alpha default"
    $expectedZ = if ($name -eq 'MainAlphaX') { 0 } else { 1 }
    Assert ($properties -match "_AlphaOptionZWrite[^\r\n]*= $expectedZ") "$name ZWrite default"
    Assert ($source.Contains('Name "DEPTH_PREPASS"') -eq (-not $name.EndsWith('BackFront'))) "$name depth architecture"
    if (-not $name.EndsWith('BackFront')) {
        Assert ($source.Contains('#include "TomAlphaDepth.cginc"')) "$name shared depth implementation"
        $passes = @([regex]::Matches($source, 'Name "([A-Z_]+)"') | ForEach-Object { $_.Groups[1].Value })
        Assert (($passes -join ',') -eq 'DEPTH_PREPASS,OUTLINE,FORWARD,FORWARDADD,SHADOWCASTER') "$name depth/outline/color order"
    }
    if ($name -eq 'MainAlphaX') {
        Assert ($properties -match '_DepthPrepass[^\r\n]*= 0') 'Prepass must default off'
        Assert ($source.Contains('ZWrite [_AlphaOptionZWrite]')) 'Preserve standard color-depth state'
        Assert (-not $source.Contains('#define TOM_ALPHA_PREPASS')) 'Standard color must preserve conditional depth-threshold clipping'
        Assert ($entry.SelectSingleNode('Property[@Name="DepthPrepass"]').Category -eq 'Render Options') 'Prepass category'
    } else {
        Assert (-not ($names -contains 'DepthPrepass')) "$name compatibility property surface changed"
    }
    if ($name.EndsWith('BackFront')) {
        Assert ($properties -match '_BackfaceZWrite[^\r\n]*= 1') "$name back depth default"
        Assert ($source.Contains('#define TOM_ALPHA_BACKFRONT_SHADOW')) "$name two-sided shadow contract"
        Assert ($source.Contains('#include "TomAlphaBackFront.cginc"')) "$name shared back lighting"
        $passes = @([regex]::Matches($source, 'Name "([A-Z_]+)"') | ForEach-Object { $_.Groups[1].Value })
        Assert (($passes -join ',') -eq 'FORWARD_BACK,FORWARDADD_BACK,OUTLINE,FORWARD,FORWARDADD,SHADOWCASTER') "$name layer order"
        Assert ($source -notmatch 'GrabPass|Dither') "$name unexpected screen sampling/dither"
    }
    $base = @(Get-Content (Join-Path $Root 'Tests/MainOpaqueX-StageTwo.properties.txt'))
    $lines = @($properties -split '\r?\n' | Where-Object { $_ -match '^\s*(?:\[[^\]]+\]\s*)*_\w+\s*\(' } | ForEach-Object { $_.Trim() })
    Assert (($base -join "`n") -ceq ($lines[0..97] -join "`n")) "$name changed inherited Stage Two property contract"
}
Assert ($common.Contains('clip(coverage > 0.0 ? 1.0 : -1.0)')) 'Zero coverage must always discard'
Assert ($common.Contains('TomAlphaClipGlobal(coverage);')) 'Depth and shadow must apply global cutoff'
Assert ($common.Contains('mainAlpha * mask * _Alpha')) 'Coverage formula'
$depth = Get-Content (Join-Path $Root 'Shaders/Tom/TomAlphaDepth.cginc') -Raw
Assert ($depth.Contains('clip(_DepthPrepass - 0.5);')) 'Independent standard prepass switch'
Assert ($depth.Contains('clip(_AlphaOptionZWrite - 0.5);')) 'Legacy prepass switch preserved'
Assert ($depth.IndexOf('clip(_DepthPrepass') -lt $depth.IndexOf('tex2D(')) 'Disabled prepass must discard before sampling'
Assert ($depth.Contains('TomAlphaClipDepthShadow(TomAlphaCoverage(a, mask));')) 'Depth must use shared coverage and cutoff'
foreach ($file in @('TomAlphaOutline.cginc','TomAlphaShadow.cginc')) {
    $code = Get-Content (Join-Path $Root "Shaders/Tom/$file") -Raw
    Assert ($code.Contains('TomAlphaCommon.cginc')) "$file missing common alpha"
}
& (Join-Path $PSScriptRoot 'Test-XStageTwo.ps1') -Root $Root
Write-Output 'PASS: Alpha/legacy 2Pass/BackFront have 115/114/115 matching manifest/tooltip properties; optional shared prepass and compatibility contracts; inherited Opaque properties unchanged.'
