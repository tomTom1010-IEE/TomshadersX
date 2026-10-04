param(
    [string]$GameRoot = 'D:/Program Files/KoikatuSunshine',
    [string]$Root = (Split-Path $PSScriptRoot -Parent),
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
if (-not $OutputDirectory) {
    $project = [IO.Path]::GetFullPath((Join-Path $Root '../../..'))
    $OutputDirectory = Join-Path $project 'CodexBridge/Builds/TomHairFeather'
}
$managed = Join-Path $GameRoot 'KoikatsuSunshine_Data/Managed'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw "Missing compiler: $compiler" }
$references = @((Join-Path $GameRoot 'BepInEx/core/BepInEx.dll'))
$references += @(Get-ChildItem -LiteralPath $managed -Filter 'UnityEngine*.dll' | ForEach-Object FullName)
$references += Join-Path $managed 'netstandard.dll'
foreach ($reference in $references) { if (-not (Test-Path -LiteralPath $reference)) { throw "Missing reference: $reference" } }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$arguments = @('/nologo','/target:library','/optimize+','/define:TOM_X_BEPINEX',('/out:'+(Join-Path $OutputDirectory 'TomHairFeather.dll')))
$arguments += @($references | ForEach-Object { '/reference:' + $_ })
$arguments += @((Join-Path $Root 'Runtime/TomHairFeatherCamera.cs'),(Join-Path $Root 'Runtime/TomHairFeatherPlugin.cs'))
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Hair feather plugin compilation failed' }
Write-Output "Built optional DLL in $OutputDirectory. No files installed in the game. Build the paired hidden shader bundle through buildxhairfeather."
