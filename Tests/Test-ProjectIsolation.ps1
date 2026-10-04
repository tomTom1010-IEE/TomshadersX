param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
function Assert($ok, $message) { if (-not $ok) { throw $message } }
$Root = (Resolve-Path $Root).Path
[xml]$manifest = Get-Content (Join-Path $Root 'manifest.xml') -Raw
Assert ($manifest.manifest.guid -eq 'tom.Shaders.X') 'Wrong mod identity'
Assert ($manifest.manifest.game -eq 'Koikatsu Sunshine') 'Wrong game'
$names = @('tom/MainOpaqueX','tom/MainAlphaX','tom/MainAlphaX2Pass','tom/MainAlphaXBackFront','tom/HairX','tom/EyeWX','tom/EyeX','tom/SkinX')
Assert (@(Compare-Object $names @($manifest.manifest.MaterialEditor.Shader.Name)).Count -eq 0) 'Wrong shader registrations'
foreach ($entry in @($manifest.manifest.MaterialEditor.Shader) + @($manifest.manifest.MaterialEditor.TooltipCatalog)) {
    Assert ($entry.AssetBundle -eq 'chara/tom/shaders/tomx.unity3d') 'Legacy/shared bundle reference'
}
$builtins = @('UnityCG.cginc','UnityStandardUtils.cginc','AutoLight.cginc','Lighting.cginc',
    'UnityStandardBRDF.cginc','UnityGlobalIllumination.cginc')
$count = 0
foreach ($file in Get-ChildItem (Join-Path $Root 'Shaders') -Recurse -File | Where-Object Extension -in @('.shader','.cginc')) {
    $text = Get-Content -LiteralPath $file.FullName -Raw
    foreach ($match in [regex]::Matches($text, '#include\s+"([^"]+)"')) {
        $include = $match.Groups[1].Value
        if ($builtins -contains $include) { continue }
        $path = [IO.Path]::GetFullPath((Join-Path $file.DirectoryName $include))
        Assert ($path.StartsWith($Root + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) "External include: $path"
        Assert (Test-Path -LiteralPath $path -PathType Leaf) "Missing include: $path"
        $count++
    }
}
foreach ($name in @('MainOpaqueX','MainAlphaX','MainAlphaX2Pass','MainAlphaXBackFront','HairX','EyeWX','EyeX','SkinX')) {
    $prefab = Join-Path $Root "Prefab/a_Tom$name.prefab"
    $material = Join-Path $Root "Material/m_Tom$name.mat"
    Assert (Test-Path $prefab) "Missing prefab $name"
    Assert (Test-Path $material) "Missing material $name"
    $matGuid = [regex]::Match((Get-Content "$material.meta" -Raw),'(?m)^guid: (\w+)').Groups[1].Value
    $shaderGuid = [regex]::Match((Get-Content (Join-Path $Root "Shaders/Tom/$name.shader.meta") -Raw),'(?m)^guid: (\w+)').Groups[1].Value
    Assert ((Get-Content $prefab -Raw).Contains("guid: $matGuid")) "Broken prefab reference $name"
    Assert ((Get-Content $material -Raw).Contains("guid: $shaderGuid")) "Broken shader reference $name"
    Assert ((Get-Content "$prefab.meta" -Raw).Contains('assetBundleName: chara/tom/shaders/tomx.unity3d')) "Wrong importer bundle $name"
}
$legacy = Join-Path (Split-Path $Root -Parent) 'KKShadersPlus-release1.7.1'
if (Test-Path (Join-Path $legacy 'manifest.xml')) {
    [xml]$old = Get-Content (Join-Path $legacy 'manifest.xml') -Raw
    Assert ($old.manifest.guid -eq 'xukmi.Shaders.VanillaPlus') 'Legacy identity changed'
    Assert (@($old.SelectNodes('//Shader[starts-with(@Name,"tom/")]')).Count -eq 0) 'Duplicate X registration'
    Assert ($null -eq $old.SelectSingleNode('//TooltipCatalog[@Asset="tom_x_tooltips"]')) 'Duplicate X catalog'
    Assert (Test-Path (Join-Path $legacy 'Shaders/KKPDeclarations.cginc')) 'Legacy common include removed'
}
& (Join-Path $PSScriptRoot 'Test-XAlpha.ps1') -Root $Root
Write-Output "PASS: $count local includes resolve inside TomShadersX; separate manifest/bundle; all eight asset bindings."
