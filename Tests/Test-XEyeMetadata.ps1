param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
function Assert($Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/')
$fixtureRoot = Join-Path $tempBase ('TomXEyeMetadata-' + [guid]::NewGuid().ToString('N'))
$sync = Join-Path $Root 'Tests/Sync-CoatEyeMetadata.ps1'
$newProperties = @('EyeRefractionIOR','IrisDepthShape','UseEyeSurfaceMask','EyeSurfaceMap','EyeSurfaceMapMode')
try {
    foreach ($directory in @('Shaders/Tom','Tooltips')) {
        New-Item -ItemType Directory -Path (Join-Path $fixtureRoot $directory) -Force | Out-Null
    }
    Copy-Item -LiteralPath (Join-Path $Root 'manifest.xml') -Destination (Join-Path $fixtureRoot 'manifest.xml')
    Copy-Item -LiteralPath (Join-Path $Root 'Tooltips/tom_x_tooltips.xml') -Destination (Join-Path $fixtureRoot 'Tooltips/tom_x_tooltips.xml')
    foreach ($name in @('MainOpaqueX','MainAlphaX','MainAlphaX2Pass','MainAlphaXBackFront','HairX','EyeWX','EyeX')) {
        Copy-Item -LiteralPath (Join-Path $Root "Shaders/Tom/$name.shader") -Destination (Join-Path $fixtureRoot "Shaders/Tom/$name.shader")
    }
    $manifestPath = Join-Path $fixtureRoot 'manifest.xml'
    $tipsPath = Join-Path $fixtureRoot 'Tooltips/tom_x_tooltips.xml'
    [xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
    [xml]$tips = Get-Content -LiteralPath $tipsPath -Raw
    $version = [string]$manifest.manifest.version
    $originalManifest = $manifest.OuterXml
    $originalTips = $tips.OuterXml
    & $sync -Root $fixtureRoot | Out-Null
    [xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
    [xml]$tips = Get-Content -LiteralPath $tipsPath -Raw
    Assert ($manifest.manifest.version -ceq $version) 'Metadata sync must not downgrade the package version'
    Assert ($manifest.OuterXml -ceq $originalManifest) 'Current manifest changed after a no-op sync'
    Assert ($tips.OuterXml -ceq $originalTips) 'Current tooltip catalog changed after a no-op sync'

    # Simulate an old catalog missing the surface extension, without touching live files.
    $eye = $manifest.SelectSingleNode("//Shader[@Name='tom/EyeX']")
    $catalog = $tips.SelectSingleNode("//Shader[@Name='tom/EyeX']")
    $expectedTips = @{}
    foreach ($key in $newProperties) {
        $expectedTips[$key] = $catalog.SelectSingleNode("Property[@Name='$key']").InnerText
        [void]$eye.RemoveChild($eye.SelectSingleNode("Property[@Name='$key']"))
        [void]$catalog.RemoveChild($catalog.SelectSingleNode("Property[@Name='$key']"))
    }
    $eye.SelectSingleNode("Property[@Name='EyeDebugView']").SetAttribute('Range','0,6')
    $expectedCoatTip = $catalog.SelectSingleNode("Property[@Name='ClearCoatIOR']").InnerText
    [void]$catalog.RemoveChild($catalog.SelectSingleNode("Property[@Name='ClearCoatIOR']"))
    $expectedDebugTip = $catalog.SelectSingleNode("Property[@Name='EyeDebugView']").InnerText
    [void]$catalog.RemoveChild($catalog.SelectSingleNode("Property[@Name='EyeDebugView']"))
    $tips.SelectSingleNode("//Shader[@Name='tom/EyeWX']/Property[@Name='ClearCoatIOR']").InnerText = 'EyeX also uses this IOR for its interior ray.'
    $expectedRadiusTip = $catalog.SelectSingleNode("Property[@Name='IrisRadiusX']").InnerText
    $catalog.SelectSingleNode("Property[@Name='IrisRadiusX']").InnerText = 'Horizontal radius of the optical iris region in final MainTex UV, not pupil radius.'
    $manifest.manifest.version = '9.8.7'
    $manifest.Save($manifestPath)
    $tips.Save($tipsPath)
    & $sync -Root $fixtureRoot | Out-Null
    [xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
    [xml]$tips = Get-Content -LiteralPath $tipsPath -Raw
    $eye = $manifest.SelectSingleNode("//Shader[@Name='tom/EyeX']")
    $catalog = $tips.SelectSingleNode("//Shader[@Name='tom/EyeX']")
    Assert ($manifest.manifest.version -ceq '9.8.7') 'Metadata sync owns no release version, including future versions'
    foreach ($key in $newProperties) {
        Assert ($null -ne $eye.SelectSingleNode("Property[@Name='$key']")) "Missing regenerated property: $key"
        Assert ($catalog.SelectSingleNode("Property[@Name='$key']").InnerText -ceq $expectedTips[$key]) "Incorrect regenerated tooltip: $key"
    }
    Assert ($catalog.SelectSingleNode("Property[@Name='ClearCoatIOR']").InnerText -ceq $expectedCoatTip) 'Regenerated coat tooltip must keep the IORs independent'
    Assert ($tips.SelectSingleNode("//Shader[@Name='tom/EyeWX']/Property[@Name='ClearCoatIOR']").InnerText -ceq $expectedCoatTip) 'Existing coupled-IOR tooltip was not migrated'
    Assert ($catalog.SelectSingleNode("Property[@Name='IrisRadiusX']").InnerText -ceq $expectedRadiusTip) 'Existing map-radius tooltip was not migrated'
    Assert ($catalog.SelectSingleNode("Property[@Name='EyeDebugView']").InnerText -ceq $expectedDebugTip) 'Regenerated debug tooltip must include depth and region'
    Assert ($eye.SelectSingleNode("Property[@Name='EyeDebugView']").Range -ceq '0,8') 'Old EyeX debug range was not repaired'
    Assert ($eye.SelectSingleNode("Property[@Name='IrisDepthShape']").Range -ceq '0,2') 'Bowl shape is unreachable in generated metadata'
    Assert ($eye.SelectSingleNode("Property[@Name='EyeRefractionIOR']").Range -ceq '1,2.5') 'Refraction IOR range differs from the shader'
    Assert ($eye.SelectSingleNode("Property[@Name='EyeSurfaceMap']").Type -ceq 'Texture') 'Surface map must be a texture property'
    Assert ($manifest.SelectSingleNode("//Shader[@Name='tom/EyeWX']/Property[@Name='EyeDebugView']").Range -ceq '0,2') 'EyeWX inherited unsupported iris debug modes'
    Assert ($null -eq $manifest.SelectSingleNode("//Shader[@Name='tom/EyeWX']/Property[@Name='EyeRefractionIOR']")) 'EyeWX inherited EyeX optics'
    $eye.SelectSingleNode("Property[@Name='IrisDepthShape']").SetAttribute('Range','0,1')
    $manifest.Save($manifestPath)
    & $sync -Root $fixtureRoot | Out-Null
    [xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
    Assert ($manifest.SelectSingleNode("//Shader[@Name='tom/EyeX']/Property[@Name='IrisDepthShape']").Range -ceq '0,2') 'Old procedural shape range was not repaired'
    $first = (Get-FileHash -LiteralPath $manifestPath).Hash + (Get-FileHash -LiteralPath $tipsPath).Hash
    & $sync -Root $fixtureRoot | Out-Null
    $second = (Get-FileHash -LiteralPath $manifestPath).Hash + (Get-FileHash -LiteralPath $tipsPath).Hash
    Assert ($first -ceq $second) 'Regeneration is not idempotent'
    Write-Output 'PASS: metadata no-op, current/future version preservation, surface tooltip regeneration, enum-range repair, EyeWX separation and byte-idempotence.'
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        $resolved = (Resolve-Path -LiteralPath $fixtureRoot).Path
        if (-not $resolved.StartsWith($tempBase + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
            [IO.Path]::GetFileName($resolved) -notlike 'TomXEyeMetadata-*') { throw "Unsafe fixture cleanup path: $resolved" }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
