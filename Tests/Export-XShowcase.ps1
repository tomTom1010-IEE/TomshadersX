param([Parameter(Mandatory=$true)][string]$ReportDirectory,
      [string]$OutputDirectory,
      [string]$Root=(Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference='Stop'
$source=(Resolve-Path -LiteralPath $ReportDirectory).Path
if(-not $OutputDirectory){$OutputDirectory=$source}
[void](New-Item -ItemType Directory -Path $OutputDirectory -Force)
$folder=(Resolve-Path -LiteralPath $OutputDirectory).Path
$report=Get-Content -LiteralPath (Join-Path $source 'report.json') -Raw | ConvertFrom-Json
if($report.shots.Count -ne 96 -or $report.checks.Count -eq 0 -or @($report.checks | Where-Object {-not $_.passed}).Count){throw 'Incomplete or failing GPU showcase; do not publish.'}
if($folder -ne $source){
    foreach($shot in $report.shots){
        if([IO.Path]::GetFileName($shot.file) -cne $shot.file){throw 'Unsafe image filename'}
        Copy-Item -LiteralPath (Join-Path $source $shot.file) -Destination (Join-Path $folder $shot.file)
    }
    Copy-Item -LiteralPath (Join-Path $source 'report.json') -Destination (Join-Path $folder 'report.json')
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $source 'Textures') -File -Recurse){
        $destination=Join-Path $folder ([IO.Path]::GetRelativePath($source,$file.FullName))
        [void](New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force)
        Copy-Item -LiteralPath $file.FullName -Destination $destination
    }
}
Add-Type -AssemblyName System.Drawing
$names=[ordered]@{
    '01-diffuse'=@('Diffuse and Toon Shading','漫反射与 Toon 明暗')
    '02-shadow'=@('Cast Shadows and Toon Shadow Response','实际投影与 Toon 阴影')
    '03-specular'=@('GGX and Toon Highlights','GGX 与 Toon 高光')
    '04-material'=@('Metallic and Roughness','金属度与粗糙度')
    '05-sh'=@('Directional SH Diffuse','SH 方向性漫射')
    '06-environment'=@('Environment Reflection and Stylization','环境反射与风格化')
    '07-matcap'=@('MatCap Combinations','MatCap 组合')
    '08-clearcoat'=@('Independent Clearcoat Surface Layer','独立 Clearcoat 表面层')
    '09-art-layers'=@('Rim, Outline and Emission','Rim、描边与发光')
    '10-normals'=@('Main, Detail and Coat Normals','主法线、细节与涂层法线')
    '11-multilight'=@('ForwardAdd Multi-Light Response','ForwardAdd 多灯响应')
    '12-anisotropy'=@('Anisotropy and Flow Direction','各向异性与流向图')
    '13-hair-toon'=@('Hair Toon Highlights','头发 Toon 高光')
    '14-skin'=@('Skin Softening, Warmth and Wetness','皮肤柔化、暖色与湿润')
    '15-skin-art'=@('Skin Masks and Liquid Layers','皮肤标准遮罩与液体')
    '16-eyew'=@('EyeWX Surface and Tint','EyeWX 眼表与调色')
    '17-eye-shapes'=@('Flat Floor, Shallow Cone and Bowl','平底、浅锥和浅碗')
    '18-eye-angles'=@('Painted Depth at Different Angles','自绘深度的多角度效果')
    '19-eye-dispersion'=@('Local Refraction and RGB Dispersion','局部折射与 RGB 色散')
    '20-ior-isolation'=@('Independent Coat and Refraction IOR','涂层 IOR 与折射 IOR 分离')
    '21-eye-angle-controls'=@('Same-Angle Optics On/Off Controls','同角度光学开关对照')
    '22-transparency'=@('Alpha Coverage and Drawing Strategies','透明覆盖与绘制策略')
    '23-hairfront'=@('Inward Stencil Feathering','Stencil 内侧羽化')
    '24-finished-materials'=@('Combined Material Styles','组合风格材质')
}
$resources=[Collections.Generic.List[IDisposable]]::new()
$titleFont=[Drawing.Font]::new('Microsoft YaHei UI',23,[Drawing.FontStyle]::Bold)
$font=[Drawing.Font]::new('Segoe UI',15)
$small=[Drawing.Font]::new('Segoe UI',11)
$ink=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(235,235,235))
$muted=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(157,166,163))
foreach($r in @($titleFont,$font,$small,$ink,$muted)){$resources.Add($r)}
function Board([string]$filename,[string]$title,$shots,[int]$tile=512,[int]$columns=4){
    $header=90;$caption=72;$rows=[int][Math]::Ceiling($shots.Count/[double]$columns)
    $bitmap=[Drawing.Bitmap]::new($columns*$tile,$header+$rows*($tile+$caption))
    $g=[Drawing.Graphics]::FromImage($bitmap)
    try{
        $g.Clear([Drawing.Color]::Black);$g.TextRenderingHint=[Drawing.Text.TextRenderingHint]::AntiAliasGridFit
        $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.DrawString($title,$titleFont,$ink,18,8)
        $g.DrawString('TOM X  |  Unity 2019.4 / D3D11  |  Shared UV sphere  |  Fixed exposure / no auto normalization',$small,$muted,20,52)
        for($i=0;$i -lt $shots.Count;$i++){
            $s=$shots[$i]
            if([IO.Path]::GetFileName($s.file) -cne $s.file){throw 'Unsafe image filename'}
            $x=($i%$columns)*$tile;$y=$header+[int][Math]::Floor($i/[double]$columns)*($tile+$caption)
            $img=[Drawing.Image]::FromFile((Join-Path $folder $s.file))
            try{$g.DrawImage($img,[Drawing.Rectangle]::new($x,$y,$tile,$tile))}finally{$img.Dispose()}
            $g.DrawString($s.title,$font,$ink,[Drawing.RectangleF]::new($x+16,$y+$tile+1,$tile-28,45))
            $g.DrawString($s.shader,$small,$muted,$x+16,$y+$tile+46)
        }
        $bitmap.Save((Join-Path $folder $filename),[Drawing.Imaging.ImageFormat]::Png)
    }finally{$g.Dispose();$bitmap.Dispose()}
}
function Escape([string]$text){[Net.WebUtility]::HtmlEncode($text)}
$css=@'
*{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;background:#101312;color:#e8ebea;font:16px/1.7 "Segoe UI","Microsoft YaHei UI",sans-serif;letter-spacing:0}header,main,footer{max-width:1500px;margin:auto;padding:28px}header{border-bottom:1px solid #39413d}h1{font-size:30px;margin:8px 0}h2{font-size:23px;margin:28px 0 12px}h3{font-size:19px}p{max-width:100ch}a{color:#81c9b5;text-underline-offset:3px}section{padding:12px 0 22px;border-bottom:1px solid #39413d}.grid{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:18px}figure{margin:0;min-width:0}img{display:block;width:100%;height:auto;background:#000}figcaption{padding:10px 0;line-height:1.4;overflow-wrap:anywhere}span{color:#aab6ae;font-size:14px}summary{cursor:pointer;color:#81c9b5}details{font-size:13px;overflow-wrap:anywhere}table{border-collapse:collapse;max-width:100%;display:block;overflow-x:auto;font-size:14px}td,th{padding:8px 12px;border:1px solid #39413d;text-align:left;vertical-align:top}code{color:#e6c78d;overflow-wrap:anywhere}nav{display:flex;gap:16px;flex-wrap:wrap}article{max-width:1060px;margin:auto}article h2{padding-top:16px;border-top:1px solid #39413d}li{margin:5px 0}footer{color:#aab6ae}@media(max-width:1000px){.grid{grid-template-columns:repeat(2,minmax(0,1fr))}}@media(max-width:540px){header,main,footer{padding:16px}.grid{grid-template-columns:1fr}h1{font-size:25px}table{font-size:13px}}@media print{body{background:white;color:#111}a,code{color:#254d40}header,main,footer{max-width:none}.grid{grid-template-columns:repeat(2,1fr)}details{display:none}section{break-inside:avoid}}
'@
$locales=@(
    @{lang='en';index=0;suffix='';gallery='Gallery.html';manual='UserManual.html';manualSource='TomShadersX-UserManual.en.md';
      title='Tom Shaders X Sphere Showcase';intro='96 actual Unity GPU renders in 24 comparisons. One shared UV sphere and fixed exposure, with no per-image brightening. Open any image at full size or expand its complete material settings.';
      manualLabel='User manual';overview='Overview';record='Render record';eye='Eye optics';hair='Anisotropy';parameters='Parameters and lighting';board='Open the 2048px comparison';
      explanation='The SH group uses known coefficients to isolate diffuse response. Transparency adds a checker background; shadow tests use a shadow-only occluder. Parallax views identify camera angles and include same-angle on/off controls. Dispersion is an RGB geometric approximation, not wave optics. Recorded settings are demonstrations, not default presets.';
      disclaimer='No in-game audit or performance claim';overviewTitle='TOM X  /  Materials and Optics';contactTitle='TOM X  /  Complete Gallery';manualTitle='Tom Shaders X User Manual';sourceLabel='Markdown source';galleryLabel='Sphere showcase';switchGallery='Gallery.zh-CN.html';switchManual='UserManual.zh-CN.html';switchLabel='中文';auditLabel='Closing audit'},
    @{lang='zh-CN';index=1;suffix='.zh-CN';gallery='Gallery.zh-CN.html';manual='UserManual.zh-CN.html';manualSource='TomShadersX-UserManual.zh-CN.md';
      title='Tom Shaders X 标准球展示';intro='96 张实际 Unity GPU 渲染，24 组对照。同一标准 UV 球网格，固定曝光，无后期逐图提亮；每张可打开原尺寸和完整材质参数。';
      manualLabel='中文用户手册';overview='展示总览';record='渲染记录';eye='眼睛光学';hair='各向异性';parameters='参数与灯光';board='打开本组 2048px 对照图';
      explanation='SH 组使用已知系数场隔离漫射；透明组增加棋盘背景；阴影组有不可见投影体。视差组标明相机角度，并提供同角度开关对照。色散是几何 RGB 近似，不是波动光学。完整参数以记录为准，不代表默认预设。';
      disclaimer='无游戏内审计或性能结论';overviewTitle='TOM X  /  材质与光学标准球';contactTitle='TOM X  /  完整图集';manualTitle='Tom Shaders X 用户手册';sourceLabel='Markdown 原文';galleryLabel='标准球展示图集';switchGallery='index.html';switchManual='UserManual.html';switchLabel='English';auditLabel='收尾审计（英文）'}
)
try{
    foreach($locale in $locales){
        $gallery=[Text.StringBuilder]::new()
        foreach($entry in $names.GetEnumerator()){
            $shots=@($report.shots | Where-Object group -eq $entry.Key)
            if($shots.Count -ne 4){throw "Expected four controls: $($entry.Key)"}
            $boardName=$entry.Key+'-board'+$locale.suffix+'.png'
            $heading=$entry.Value[$locale.index]
            Board $boardName ($entry.Key.Substring(0,2)+'  '+$heading) $shots
            [void]$gallery.AppendLine('<section id="'+$entry.Key+'"><h2>'+(Escape $heading)+'</h2><div class="grid">')
            foreach($shot in $shots){
                $props=($shot.properties | ForEach-Object {'<tr><td>'+(Escape $_.name)+'</td><td>'+(Escape $_.value)+'</td></tr>'}) -join ''
                $loading=if($entry.Key -eq '01-diffuse'){'eager'}else{'lazy'}
                [void]$gallery.AppendLine('<figure><a href="'+$shot.file+'"><img loading="'+$loading+'" width="768" height="768" src="'+$shot.file+'" alt="'+(Escape $shot.title)+'"></a><figcaption><b>'+(Escape $shot.title)+'</b><br><span>'+(Escape $shot.shader)+'</span></figcaption><details><summary>'+$locale.parameters+'</summary><p>'+(Escape $shot.lighting)+'</p><table>'+$props+'</table></details></figure>')
            }
            [void]$gallery.AppendLine('</div><p><a href="'+$boardName+'">'+$locale.board+'</a></p></section>')
        }
        $selected=@(0,1,8,10,18,21,25,29,45,50,53,55,65,69,74,93 | ForEach-Object {$report.shots[$_]})
        Board ('Overview'+$locale.suffix+'.png') $locale.overviewTitle $selected 384 4
        for($page=0;$page -lt 4;$page++){
            $shots=@($report.shots | Select-Object -Skip ($page*24) -First 24)
            Board ('Contact-'+($page+1)+$locale.suffix+'.png') ($locale.contactTitle+' '+($page+1)+'/4') $shots 320 4
        }
        $nav='<a href="'+$locale.manual+'">'+$locale.manualLabel+'</a><a href="Overview'+$locale.suffix+'.png">'+$locale.overview+'</a><a href="report.json">'+$locale.record+'</a><a href="#17-eye-shapes">'+$locale.eye+'</a><a href="#12-anisotropy">'+$locale.hair+'</a><a href="'+$locale.switchGallery+'">'+$locale.switchLabel+'</a><a href="https://github.com/tomTom1010-IEE/TomshadersX">GitHub</a>'
        $html=@"
<!doctype html><html lang="$($locale.lang)"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>$($locale.title)</title><style>$css</style></head><body>
<header><h1>$($locale.title)</h1><p>$($locale.intro)</p><nav>$nav</nav><p>$($locale.explanation)</p></header><main>$gallery</main><footer>$(Escape $report.unity) · $(Escape $report.gpu) · $(Escape $report.api) · $(Escape $report.colorSpace) · $($locale.disclaimer) · <a href="XSeriesClosingAudit.md">$($locale.auditLabel)</a></footer></body></html>
"@
        [IO.File]::WriteAllText((Join-Path $folder $locale.gallery),$html,[Text.UTF8Encoding]::new($false))
        if($locale.lang -eq 'en'){[IO.File]::WriteAllText((Join-Path $folder 'index.html'),$html,[Text.UTF8Encoding]::new($false))}
        $manualPath=Join-Path $Root ('Documents/'+$locale.manualSource)
        $manual=(ConvertFrom-Markdown -LiteralPath $manualPath).Html
        $doc=@"
<!doctype html><html lang="$($locale.lang)"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>$($locale.manualTitle)</title><style>$css</style></head><body><header><nav><a href="$($locale.gallery)">$($locale.galleryLabel)</a><a href="$($locale.manualSource)">$($locale.sourceLabel)</a><a href="$($locale.switchManual)">$($locale.switchLabel)</a><a href="https://github.com/tomTom1010-IEE/TomshadersX">GitHub</a></nav></header><main><article>$manual</article></main></body></html>
"@
        [IO.File]::WriteAllText((Join-Path $folder $locale.manual),$doc,[Text.UTF8Encoding]::new($false))
        Copy-Item -LiteralPath $manualPath -Destination (Join-Path $folder $locale.manualSource)
    }
}finally{foreach($r in $resources){$r.Dispose()}}
Copy-Item -LiteralPath (Join-Path $Root 'Documents/XSeriesClosingAudit.md') -Destination (Join-Path $folder 'XSeriesClosingAudit.md')
$receipt=[ordered]@{generated=(Get-Date).ToString('o');source='Actual Unity Camera.Render; original GPU pixels reused';defaultLanguage='en';languages=@('en','zh-CN');shots=$report.shots.Count;boardsPerLanguage=$names.Count;files=@()}
$receipt.files=@(Get-ChildItem -LiteralPath $folder -File -Recurse | Where-Object {$_.Name -ne 'export-receipt.json' -and $_.Extension -ne '.meta'} | ForEach-Object {
    [ordered]@{file=[IO.Path]::GetRelativePath($folder,$_.FullName);bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
[IO.File]::WriteAllText((Join-Path $folder 'export-receipt.json'),($receipt|ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
Write-Output "Exported English-default and Chinese galleries/manuals, $($report.shots.Count) shared raw renders and $($names.Count) boards per language: $folder"
