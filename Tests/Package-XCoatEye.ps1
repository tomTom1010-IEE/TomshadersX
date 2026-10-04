param([string]$Root=(Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $Root '../../..'))
$build=Join-Path $project 'CodexBridge/Builds/TomX-0.2.1'
$bundle=Join-Path $build 'abdata/chara/tom/shaders/tomx.unity3d'
if(-not (Test-Path -LiteralPath $bundle)){throw 'Run Bridge buildxcoateye first.'}
[xml]$manifest=Get-Content -LiteralPath (Join-Path $Root 'manifest.xml') -Raw
if($manifest.manifest.version -ne '0.2.1'){throw 'Package version and manifest differ'}
$package=Join-Path $build ('TomShadersX-0.2.1-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.zipmod')
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::Open($package,[IO.Compression.ZipArchiveMode]::Create)
try {
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $Root 'manifest.xml'),'manifest.xml')
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$bundle,'abdata/chara/tom/shaders/tomx.unity3d')
} finally {$zip.Dispose()}
Write-Output "Packaged $package. Experimental build; not installed. Optional HairFeather 0.3.0 remains a separate DLL/bundle pair."
