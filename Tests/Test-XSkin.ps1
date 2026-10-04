param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference='Stop'
function Assert($ok,$message) { if(-not $ok) { throw $message } }
$shader=Get-Content -LiteralPath (Join-Path $Root 'Shaders/Tom/SkinX.shader') -Raw
$head=$shader.Substring(0,$shader.IndexOf('SubShader'))
$decl=@($head -split '\r?\n' | Where-Object {$_ -match '^\s*(?:\[[^\]]+\]\s*)*_\w+\s*\('} | ForEach-Object {$_.Trim()})
$opaque=Get-Content -LiteralPath (Join-Path $Root 'Shaders/Tom/MainOpaqueX.shader') -Raw
$common=@($opaque.Substring(0,$opaque.IndexOf('SubShader')) -split '\r?\n' | Where-Object {$_ -match '^\s*(?:\[[^\]]+\]\s*)*_\w+\s*\('} | ForEach-Object {$_.Trim()})
Assert (($decl[0..107] -join "`n") -ceq ($common -join "`n")) 'Skin inherited common/coat declarations differ'
$frozen=Join-Path $Root 'Tests/SkinX-v1.properties.txt'
Assert (Test-Path -LiteralPath $frozen) 'Missing SkinX contract snapshot'
Assert (($decl -join "`n") -ceq ((Get-Content -LiteralPath $frozen) -join "`n")) 'SkinX public contract changed'
$names=@([regex]::Matches($head,'(?m)^\s*(?:\[[^\]]+\]\s*)*_(\w+)\s*\(') | ForEach-Object {$_.Groups[1].Value})
Assert (@($names | Group-Object | Where-Object Count -gt 1).Count -eq 0) 'Duplicate SkinX property'
[xml]$manifest=Get-Content -LiteralPath (Join-Path $Root 'manifest.xml') -Raw
[xml]$tips=Get-Content -LiteralPath (Join-Path $Root 'Tooltips/tom_x_tooltips.xml') -Raw
foreach($doc in @($manifest,$tips)) {
    $entry=$doc.SelectSingleNode('//Shader[@Name="tom/SkinX"]')
    Assert ($null -ne $entry) 'SkinX metadata missing'
    Assert (@(Compare-Object $names @($entry.Property.Name)).Count -eq 0) 'SkinX metadata mismatch'
    Assert (@($entry.Property | Group-Object Name | Where-Object Count -gt 1).Count -eq 0) 'Duplicate SkinX metadata'
}
Assert ($manifest.SelectSingleNode('//Shader[@Name="tom/SkinX"]').Asset -eq 'a_TomSkinX') 'Wrong SkinX carrier'
Assert ($shader.Contains('Fallback Off')) 'Skin must not inherit unmasked fallback passes'
Assert ($shader -notmatch 'GrabPass|Stencil\s*\{|smoothNormalOS|tex2D\(_AlphaMask') 'Wrong skin screen/stencil/UV4/coverage path'
Assert ($shader.Contains('multi_compile_fwdadd_fullshadows')) 'Missing shadowed additional lights'
Assert ($shader.Contains('skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON')) 'Skin must not consume overlay UVs as lightmap UVs'
Assert (([regex]::Matches($shader,'TomSkinClip\(i.uv, TomSkinMainAlpha\(i.uv\)\)')).Count -eq 2) 'Outline/Shadow coverage not shared'
$surface=Get-Content -LiteralPath (Join-Path $Root 'Shaders/Tom/TomSkinSurface.cginc') -Raw
$coverage=Get-Content -LiteralPath (Join-Path $Root 'Shaders/Tom/TomSkinCoverage.cginc') -Raw
$coat=Get-Content -LiteralPath (Join-Path $Root 'Shaders/Tom/TomClearcoat.cginc') -Raw
Assert ($coverage.Contains('min(enabledMask.x, enabledMask.y)')) 'Clothing RG must intersect'
Assert ($surface.Contains('liquidUV = i.uv0 * _LiquidTiling.zw + _LiquidTiling.xy')) 'LiquidTiling order'
Assert ($surface.Contains('max(saturate(amount) * pattern.r, saturate(amount - 1.0) * pattern.g)')) 'Liquid stage union'
Assert ($coat.Contains('coat.weight *= material.skin.coatCoverage')) 'Skin coat must multiply common weight'
Assert ($surface -notmatch '_ClearCoatIOR|_FaceShadowG|viewNorm') 'Unexpected skin optical/shadow coupling'
$samples=0
foreach($rOn in @(0,1)) { foreach($gOn in @(0,1)) { foreach($r in @(0,.25,.75,1)) { foreach($g in @(0,.25,.75,1)) {
    $coverageValue=[math]::Min([math]::Max(1-$rOn,$r),[math]::Max(1-$gOn,$g))
    $expected=($rOn -eq 0 -or $r -ge .5) -and ($gOn -eq 0 -or $g -ge .5)
    Assert (($coverageValue -ge .5) -eq $expected) 'RG clipping algebra'
    $samples++
}}}}
foreach($r in @(0,.25,.75,1)) { foreach($g in @(0,.25,.75,1)) {
    $last=-1
    foreach($s in @(0,.5,1,1.5,2)) {
        $v=[math]::Max([math]::Min($s,1)*$r,[math]::Max(0,[math]::Min($s-1,1))*$g)
        Assert ($v -ge $last -and $v -le 1) 'Liquid pattern must be monotonic and bounded'
        if($s -eq 2) { Assert ($v -eq [math]::Max($r,$g)) 'Second liquid stage replaces first' }
        $last=$v; $samples++
    }
}}
Write-Output "PASS: $($names.Count) SkinX properties, frozen common/coat and metadata, UV/coverage/pass contracts, $samples algebra checks. GPU/game acceptance is separate."
