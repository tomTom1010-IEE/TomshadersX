param([string]$Root=(Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference='Stop'
function Assert($condition,[string]$message){if(-not $condition){throw $message}}
$temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\','/')
$fixture=Join-Path $temp ('TomXSkinMetadata-'+[guid]::NewGuid().ToString('N'))
try {
    foreach($dir in @('Shaders/Tom','Tooltips')) {New-Item -ItemType Directory -Path (Join-Path $fixture $dir) -Force | Out-Null}
    foreach($file in @('Shaders/Tom/SkinX.shader','manifest.xml','Tooltips/tom_x_tooltips.xml')) {
        Copy-Item -LiteralPath (Join-Path $Root $file) -Destination (Join-Path $fixture $file)
    }
    $manifest=Join-Path $fixture 'manifest.xml'; $tips=Join-Path $fixture 'Tooltips/tom_x_tooltips.xml'
    [xml]$doc=Get-Content -LiteralPath $manifest -Raw
    $others=@($doc.SelectNodes('//Shader') | Where-Object Name -ne 'tom/SkinX' | ForEach-Object OuterXml)
    $version=[string]$doc.manifest.version
    & (Join-Path $Root 'Tests/Update-SkinMetadata.ps1') -Root $fixture | Out-Null
    $first=(Get-FileHash -LiteralPath $manifest).Hash+(Get-FileHash -LiteralPath $tips).Hash
    & (Join-Path $Root 'Tests/Update-SkinMetadata.ps1') -Root $fixture | Out-Null
    Assert ($first -ceq ((Get-FileHash -LiteralPath $manifest).Hash+(Get-FileHash -LiteralPath $tips).Hash)) 'Skin metadata regeneration must be byte-idempotent'
    [xml]$doc=Get-Content -LiteralPath $manifest -Raw
    Assert ($doc.manifest.version -ceq $version) 'Skin metadata does not own release version'
    $after=@($doc.SelectNodes('//Shader') | Where-Object Name -ne 'tom/SkinX' | ForEach-Object OuterXml)
    Assert (($others -join '') -ceq ($after -join '')) 'Skin metadata changed an existing shader'
    $skin=$doc.SelectSingleNode('//Shader[@Name="tom/SkinX"]'); [void]$skin.ParentNode.RemoveChild($skin)
    $doc.manifest.version='9.8.7'; $doc.Save($manifest)
    & (Join-Path $Root 'Tests/Update-SkinMetadata.ps1') -Root $fixture | Out-Null
    [xml]$doc=Get-Content -LiteralPath $manifest -Raw
    Assert ($doc.manifest.version -ceq '9.8.7') 'Skin metadata must preserve future versions'
    Assert ($doc.SelectNodes('//Shader[@Name="tom/SkinX"]/Property').Count -eq 167) 'Skin registration regeneration incomplete'
    Assert ($doc.SelectSingleNode('//Shader[@Name="tom/SkinX"]/Property[@Name="SkinDebugView"]').Range -eq '0,13') 'Missing debug modes'
    Write-Output 'PASS: Skin metadata idempotence, seven-shader isolation, current/future version preservation and complete regeneration.'
}
finally {
    if(Test-Path -LiteralPath $fixture){
        $resolved=(Resolve-Path -LiteralPath $fixture).Path
        if(-not $resolved.StartsWith($temp+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notlike 'TomXSkinMetadata-*'){throw "Unsafe cleanup path: $resolved"}
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
