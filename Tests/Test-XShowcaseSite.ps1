param([string]$Root=(Split-Path $PSScriptRoot -Parent),[string]$OriginalReportDirectory)
$ErrorActionPreference='Stop'
function Assert($condition,[string]$message){if(-not $condition){throw $message}}
$site=(Resolve-Path -LiteralPath (Join-Path $Root 'docs')).Path
$report=Get-Content -LiteralPath (Join-Path $site 'report.json') -Raw | ConvertFrom-Json
$receipt=Get-Content -LiteralPath (Join-Path $site 'export-receipt.json') -Raw | ConvertFrom-Json
Assert ($report.shots.Count -eq 96) 'Expected 96 shared captures'
Assert ($report.checks.Count -eq 109 -and @($report.checks | Where-Object {-not $_.passed}).Count -eq 0) 'GPU evidence is incomplete or failing'
Assert ($receipt.defaultLanguage -eq 'en' -and $receipt.languages.Count -eq 2) 'Both languages and English default required'
foreach($file in $receipt.files){
    $path=[IO.Path]::GetFullPath((Join-Path $site $file.file))
    Assert ($path.StartsWith($site+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) 'Receipt path escapes site'
    Assert ((Get-FileHash -LiteralPath $path).Hash -eq $file.sha256) ('Changed export: '+$file.file)
}
$pages=@('index.html','Gallery.html','Gallery.zh-CN.html','UserManual.html','UserManual.zh-CN.html')
$style=$null
foreach($name in $pages){
    $html=Get-Content -LiteralPath (Join-Path $site $name) -Raw
    $language=if($name.Contains('zh-CN')){'zh-CN'}else{'en'}
    Assert ($html.Contains('<html lang="'+$language+'">')) ('Incorrect language: '+$name)
    $sheet=[regex]::Match($html,'<style>([\s\S]*?)</style>').Groups[1].Value
    if($null -eq $style){$style=$sheet}else{Assert ($sheet -ceq $style) ('Layout diverged: '+$name)}
    Assert ($sheet.Contains('max-width:1000px') -and $sheet.Contains('max-width:540px')) 'Responsive layout missing'
    Assert ($html -notmatch '<script\b|file:///|[A-Z]:\\Users\\') 'Unexpected script or local/private dependency'
    foreach($match in [regex]::Matches($html,'(?:src|href)="([^"]+)"')){
        $link=[Net.WebUtility]::HtmlDecode($match.Groups[1].Value)
        if($link.StartsWith('#')){
            Assert ($html.Contains('id="'+$link.Substring(1)+'"')) ('Missing anchor: '+$link)
        }elseif($link -notmatch '^https://'){
            Assert (-not $link.StartsWith('/')) ('Repo-subpath-unsafe URL: '+$link)
            $path=Join-Path $site $link
            Assert (Test-Path -LiteralPath $path -PathType Leaf) ('Missing link: '+$name+' -> '+$link)
            Assert ((Get-Item -LiteralPath $path).Name -ceq [IO.Path]::GetFileName($link)) ('Case mismatch: '+$link)
        }
    }
    if($name.StartsWith('UserManual')){
        Assert ([regex]::Matches($html,'<h2\b').Count -eq 11) ('Incomplete manual: '+$name)
        foreach($term in @('EyeRefractionIOR','ClearCoatIOR','EyeSurfaceMapMode','SkinCoatCoverage','ProbeRefresh','POM')){
            Assert ($html.Contains($term)) ('Missing manual topic: '+$term)
        }
        if($language -eq 'en'){
            $article=[regex]::Match($html,'<article>([\s\S]*?)</article>').Groups[1].Value
            Assert ($article -notmatch '[\u3400-\u9fff]') 'Untranslated English manual content'
        }
    }else{
        Assert ([regex]::Matches($html,'<figure>').Count -eq 96) ('Wrong image count: '+$name)
        Assert ([regex]::Matches($html,'<section id=').Count -eq 24) ('Wrong group count: '+$name)
    }
}
Assert ((Get-FileHash -LiteralPath (Join-Path $site 'index.html')).Hash -eq (Get-FileHash -LiteralPath (Join-Path $site 'Gallery.html')).Hash) 'Default route differs from English gallery'
Add-Type -AssemblyName System.Drawing
function PixelBandHash([string]$path){
    $image=[Drawing.Bitmap]::new($path)
    try{
        Assert ($image.Width -eq 2048 -and $image.Height -eq 674) ('Unexpected board dimensions: '+$path)
        $band=$image.Clone([Drawing.Rectangle]::new(0,90,2048,512),[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $stream=[IO.MemoryStream]::new();$hash=[Security.Cryptography.SHA256]::Create()
        try{
            $band.Save($stream,[Drawing.Imaging.ImageFormat]::Bmp);$stream.Position=0
            [BitConverter]::ToString($hash.ComputeHash($stream))
        }finally{$hash.Dispose();$stream.Dispose();$band.Dispose()}
    }finally{$image.Dispose()}
}
$groups=@($report.shots | Group-Object group)
Assert ($groups.Count -eq 24) 'Unexpected report groups'
$readme=Get-Content -LiteralPath (Join-Path $Root 'README.md') -Raw
foreach($group in $groups){
    $english=Join-Path $site ($group.Name+'-board.png')
    $chinese=Join-Path $site ($group.Name+'-board.zh-CN.png')
    Assert ((PixelBandHash $english) -eq (PixelBandHash $chinese)) ('Translated board changed rendered pixels: '+$group.Name)
    Assert ($readme.Contains('docs/'+$group.Name+'-board.png')) ('Missing README board: '+$group.Name)
}
if($OriginalReportDirectory){
    foreach($shot in $report.shots){
        Assert ((Get-FileHash -LiteralPath (Join-Path $site $shot.file)).Hash -eq (Get-FileHash -LiteralPath (Join-Path $OriginalReportDirectory $shot.file)).Hash) ('Original render changed: '+$shot.file)
    }
}
Assert (Test-Path -LiteralPath (Join-Path $site '.nojekyll')) 'Static Pages marker missing'
Write-Output 'PASS: English-default/Chinese galleries and 11-section manuals, identical responsive layouts, all local links, 96 shared captures, 24 pixel-identical translated board pairs, README coverage and export hashes.'
