param([string]$Root=(Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $Root '../../..'))
$build=Join-Path $project 'CodexBridge/Builds/TomX-0.3.1'
$bundle=Join-Path $build 'abdata/chara/tom/shaders/tomx.unity3d'
if(-not (Test-Path -LiteralPath $bundle)){throw 'Run Bridge buildxskin first.'}
[xml]$manifest=Get-Content -LiteralPath (Join-Path $Root 'manifest.xml') -Raw
if($manifest.manifest.version -ne '0.3.1'){throw 'Package version and manifest differ'}
$entries=@($manifest.SelectNodes('//MaterialEditor/Shader'))
if($entries.Count -ne 8 -or -not $manifest.SelectSingleNode('//Shader[@Name="tom/SkinX"]')){throw 'Expected eight X registrations including SkinX'}
$package=Join-Path $build ('TomShadersX-0.3.1-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.zipmod')
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::Open($package,[IO.Compression.ZipArchiveMode]::Create)
try {
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $Root 'manifest.xml'),'manifest.xml')
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$bundle,'abdata/chara/tom/shaders/tomx.unity3d')
} finally {$zip.Dispose()}
$check=[IO.Compression.ZipFile]::OpenRead($package)
try {
    if($check.Entries.Count -ne 2){throw 'Unexpected archive payload'}
    if($check.GetEntry('abdata/chara/tom/shaders/tomx.unity3d').Length -ne (Get-Item -LiteralPath $bundle).Length){throw 'Bundle archive size mismatch'}
} finally {$check.Dispose()}
Get-FileHash -LiteralPath $package -Algorithm SHA256
Write-Output "Packaged $package. SkinX experimental build, not installed. Existing optional HairFeather 0.3.0 remains a separate DLL/bundle pair."
