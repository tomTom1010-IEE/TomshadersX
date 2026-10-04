param([Parameter(Mandatory=$true)][string]$UnityProject)
$ErrorActionPreference = 'Stop'
$path = Join-Path $UnityProject 'Assets/Editor/CodexBridge.cs'
$source = [IO.File]::ReadAllText($path)
$commands = [ordered]@{
    renderxshowcase = 'TomXShowcase.Run()'
    validatexskin = 'TomXSkinBuild.Validate()'
    preparexskin = 'TomXSkinBuild.Prepare()'
    buildxskin = 'TomXSkinBuild.Build()'
    renderxskin = 'TomXSkinValidation.Run()'
    benchmarkxskin = 'TomXGpuBenchmark.StartSkin()'
    benchmarkxgpu = 'TomXGpuBenchmark.Start(command.gridX > 0 ? command.gridX : 32)'
    validatexcoateye = 'TomXCoatEyeBuild.Validate()'
    renderxcoateye = 'TomXCoatEyeValidation.Run()'
    preparexeyes = 'TomXCoatEyeBuild.Prepare()'
    buildxcoateye = 'TomXCoatEyeBuild.Build()'
    validatexhair = 'TomXStageTwoValidation.Validate("tom/HairX")'
    buildxhairfeather = 'TomXHairFeatherBuild.Build()'
    renderxhair = 'TomXHairValidation.Run()'
    renderxhairwidth = 'TomXHairWidthValidation.Run()'
    renderxhairstyle = 'TomXHairStyleValidation.Run()'
    validatexstage2 = 'TomXStageTwoValidation.Validate()'
    validatexalpha = 'TomXStageTwoValidation.Validate("tom/MainAlphaX") + "\n" + TomXStageTwoValidation.Validate("tom/MainAlphaX2Pass")'
    renderxalpha = 'TomXAlphaValidation.Run()'
    renderxbackfront = 'TomXAlphaValidation.Run(true)'
    renderxtwistedalpha = 'TomXTwistedAlphaPreview.Run()'
    retakextwistedalpha = 'TomXTwistedAlphaPreview.Retake(command.assetPath)'
    renderxcrossmeshalpha = 'TomXCrossMeshAlphaPreview.Run(command.assetPath)'
    renderxcrossmeshalpha2450 = 'TomXCrossMeshAlphaPreview.Run(command.assetPath, true)'
    renderxalphamerged = 'TomXAlphaValidation.Run(true, true)'
    renderxcrossmeshalphamerged = 'TomXCrossMeshAlphaPreview.Run(command.assetPath, true, true)'
    validatexbackfront = 'TomXStageTwoValidation.Validate("tom/MainAlphaXBackFront")'
    preparexstage2 = 'TomXStageTwoReferences.Create()'
    finishxstage2references = 'TomXStageTwoReferences.FinishAndInspect()'
    renderxstage2closure = 'TomXStageTwoClosure.Run()'
    archivexstage2references = 'TomXStageTwoClosure.ArchiveReferences()'
}
$anchor = '                case "ping":'
if (-not $source.Contains($anchor)) { throw 'Expected CodexBridge dispatcher not found; do not modify automatically.' }
$added = 0
foreach ($entry in $commands.GetEnumerator()) {
    if ($source.Contains('case "' + $entry.Key + '":')) { continue }
    $block = '                case "' + $entry.Key + '":' + "`r`n" +
        '                    message = ' + $entry.Value + ';' + "`r`n" +
        '                    break;' + "`r`n`r`n"
    $source = $source.Replace($anchor, $block + $anchor)
    $added++
}
if ($added -gt 0) {
    [IO.File]::WriteAllText($path, $source, (New-Object Text.UTF8Encoding($false)))
}
Write-Output "Added $added X regression commands. Refresh Unity to compile before sending commands."
