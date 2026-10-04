param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
function Assert($Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
[xml]$manifest = Get-Content -LiteralPath (Join-Path $Root 'manifest.xml') -Raw
[xml]$tips = Get-Content -LiteralPath (Join-Path $Root 'Tooltips/tom_x_tooltips.xml') -Raw
$coatContract = Get-Content -LiteralPath (Join-Path $Root 'Tests/Clearcoat-v1.properties.txt')
foreach ($name in @('MainOpaqueX','MainAlphaX','MainAlphaX2Pass','MainAlphaXBackFront','HairX','EyeWX','EyeX')) {
    $source = Get-Content -LiteralPath (Join-Path $Root "Shaders/Tom/$name.shader") -Raw
    $head = $source.Substring(0,$source.IndexOf('SubShader'))
    $coat = @($head -split '\r?\n' | Where-Object {$_ -match '^\s*(?:\[[^\]]+\]\s*)*_ClearCoat\w*\s*\('} | ForEach-Object {$_.Trim()})
    Assert (($coat -join "`n") -ceq ($coatContract -join "`n")) "$name coat property defaults/order/types differ"
    $names = @([regex]::Matches($head,'(?m)^\s*(?:\[[^\]]+\]\s*)*_(\w+)\s*\(') | ForEach-Object {$_.Groups[1].Value})
    $entry=$manifest.SelectSingleNode("//Shader[@Name='tom/$name']")
    $catalog=$tips.SelectSingleNode("//Shader[@Name='tom/$name']")
    Assert (@(Compare-Object $names @($entry.Property.Name)).Count -eq 0) "$name manifest mismatch"
    Assert (@(Compare-Object $names @($catalog.Property.Name)).Count -eq 0) "$name tooltip mismatch"
    Assert (@($names | Group-Object | Where-Object Count -gt 1).Count -eq 0) "$name duplicate properties"
    if ($name.StartsWith('Eye')) {
        $declarations = @($head -split '\r?\n' | Where-Object {$_ -match '^\s*(?:\[[^\]]+\]\s*)*_\w+\s*\('} | ForEach-Object {$_.Trim()})
        $frozen = Get-Content -LiteralPath (Join-Path $Root "Tests/$name-v1.properties.txt")
        Assert (($declarations -join "`n") -ceq ($frozen -join "`n")) "$name frozen v1 property contract changed; review compatibility before updating the snapshot"
        $passes=@([regex]::Matches($source,'Name "([^"\r\n]+)"') | ForEach-Object {$_.Groups[1].Value})
        Assert (($passes -join ',') -ceq 'StencilMask,FORWARD,FORWARDADD') "$name pass contract"
        Assert ($source.Contains('Ref 2 ReadMask 255 WriteMask 255 Comp Always Pass Replace')) "$name stencil writer contract"
        Assert ($source.Contains('Fallback Off')) "$name must not inherit shadow passes"
        Assert ($source -notmatch 'GrabPass|ZWrite On|ShadowCaster') "$name unexpected geometry/depth/screen pass"
        Assert (([regex]::Matches($source,'ZTest LEqual')).Count -eq 3) "$name depth test shared"
        Assert ($source.Contains('Blend '+$(if($name -eq 'EyeX') {'SrcAlpha'}else{'One'})+' One')) "$name Add alpha blend"
    }
}
$lighting=Get-Content -LiteralPath (Join-Path $Root 'Shaders/Tom/TomToonLighting.cginc') -Raw
$coat=Get-Content -LiteralPath (Join-Path $Root 'Shaders/Tom/TomClearcoat.cginc') -Raw
$coverage=Get-Content -LiteralPath (Join-Path $Root 'Shaders/Tom/TomEyeCoverage.cginc') -Raw
$optics=Get-Content -LiteralPath (Join-Path $Root 'Shaders/Tom/TomEyeOptics.cginc') -Raw
$plugin=Get-Content -LiteralPath (Join-Path $Root 'Runtime/TomHairFeatherPlugin.cs') -Raw
Assert ($coverage -notmatch 'refract\(|_IrisDepth|_ClearCoat|_EyeDispersion') 'Optics controls must not enter source coverage'
Assert ($optics -notmatch 'ddx\(hit|ddy\(hit|GrabPass|_CameraDepthTexture|while\s*\(') 'Unexpected optical sampling path'
Assert ($optics.Contains('SampleGrad')) 'Optics must use explicit gradients'
Assert ($coat.Contains('_ClearCoatIOR <= 1.0 ? 0.0')) 'IOR one must remove reflection'
Assert ($coat.Contains('light.physicalShadowAttenuation')) 'Coat direct needs physical shadow'
$add=$lighting.Substring($lighting.IndexOf('float4 TomFragAdd'))
Assert ($add -notmatch 'TomCoatEnvironment\(') 'Coat environment duplicated in Add'
Assert ($plugin.Contains('"tom/EyeWX", "tom/EyeX"')) 'Missing X eye writer registration'
Assert ($plugin.Contains('string pass = tomEye ||')) 'X eyes must require StencilMask'

# Reference algebra for bounded layer energy and index matching (not GPU cost).
$samples=0
foreach($ior in @(1,1.33,1.5,2.5)) {
    $f0=[math]::Pow(($ior-1)/($ior+1),2)
    foreach($weight in @(0,.3,1)) { foreach($nv in @(0,.1,.5,1)) { foreach($nl in @(0,.1,.5,1)) {
        $fv=if($ior -eq 1){0}else{$f0+(1-$f0)*[math]::Pow(1-$nv,5)}
        $fl=if($ior -eq 1){0}else{$f0+(1-$f0)*[math]::Pow(1-$nl,5)}
        $retain=(1-$weight*$fv)*(1-$weight*$fl)
        Assert ($retain -ge 0 -and $retain -le 1) 'Invalid energy retention'
        if($ior -eq 1 -or $weight -eq 0){Assert ($retain -eq 1) 'Index-matched/disabled coat changed retention'}
        $samples++
    }}}
}
Write-Output "PASS: seven matching coat contracts; frozen EyeWX/EyeX v1 properties; eye passes/metadata/coverage; adapter registration; $samples CPU layer checks."
