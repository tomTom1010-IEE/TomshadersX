param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
[xml]$manifest = Get-Content -LiteralPath (Join-Path $Root 'manifest.xml') -Raw
[xml]$tips = Get-Content -LiteralPath (Join-Path $Root 'Tooltips/tom_x_tooltips.xml') -Raw
$hints = @{
    ClearCoat = 'Neutral dielectric surface layer. Zero bypasses coating and preserves the original X lighting. Does not change alpha or stencil.'
    ClearCoatRoughness = 'Perceptual coat roughness, independent of the substrate. Multiplied by coat-map G; floored at 0.04 and specular-filtered.'
    ClearCoatIOR = 'Coat reflection and layer-energy IOR only. 1 removes coat reflection. EyeX interior rays and dispersion use independent EyeRefractionIOR.'
    ClearCoatMap = 'Linear data: R weight, G roughness multiplier; B/A ignored. Independent ST; shares MainTex filtering/wrap sampler state. White is neutral.'
    ClearCoatNormalSource = '0 interpolated mesh normal; 1 main normal; 2 main plus detail; 3 independent coat normal. EyeX resolves this in the unwarped surface domain.'
    ClearCoatNormalMap = 'Unity tangent-space normal map, independent ST; shares NormalMap sampler state. Used only with normal source 3.'
    ClearCoatNormalScale = 'Strength of the independent coat normal. Does not modify substrate detail.'
    ClearCoatEnvironmentStrength = 'Coat reflection-probe strength. Independent of substrate ReflectionMode, reflection masks and environment tint.'
    ClearCoatEnergyBlend = 'Additional coat attenuation of diffuse body. 0 preserves Toon body brightness; 1 uses the layered approximation. Substrate specular attenuation is always applied.'
    ClearCoatDebugView = '0 final; 1 coat direct (Base+Add); 2 coat environment (Base only); 3 surface normal; 4 coat weight; 5 indirect body retention. Overrides Lighting Debug.'
    Alpha = 'Common unwarped source coverage multiplier. EyeX blends, EyeWX clips. Does not separately scale the coat.'
    StencilCutoff = 'Unwarped coverage threshold for StencilMask. Color Cutoff and zero-coverage rejection also apply. Refraction and coat never enlarge the stencil.'
    Color = 'Game EyeW color alias. 0.5 is neutral; RGB multiplies the X body by twice this value. BaseColor remains a separate multiplier.'
    overtex1 = 'Game highlight alpha on mesh UV1, original ST. Base-only surface art, not refracted. Shares MainTex sampler state.'
    overtex2 = 'Game highlight alpha on mesh UV2, original ST. Base-only surface art, not refracted. Shares MainTex sampler state.'
    overcolor1 = 'Game highlight 1 RGBA tint, retaining the legacy alpha/color composition.'
    overcolor2 = 'Game highlight 2 RGBA tint, retaining the legacy alpha/color composition.'
    isHighLight = 'Game-controlled highlight visibility. Highlights are independent of physical clearcoat.'
    expression = 'Game expression layer, retaining the legacy view offset, MainTex ST and size mapping. Its own texture ST is intentionally unused. Shares MainTex sampler.'
    exppower = 'Game expression alpha strength. Participates in the original composite coverage.'
    ExpressionSize = 'Game expression mapping size, clamped internally to at least 0.1.'
    ExpressionDepth = 'Legacy expression view offset only. Not the iris recess and not applied twice by refraction.'
    rotation = 'Game iris rotation in normalized turns, clockwise about UV0 center before MainTex ST.'
    EyeOpticsMode = '0 original iris sampling; 1 local single-interface refraction. Flat legacy path or shallow surface refinement, not self-occluding POM. No screen buffers or extra mesh. Default off.'
    EyeOpticsStrength = 'Optical displacement strength. Zero preserves the original iris mapping.'
    IrisCenterX = 'Procedural center X in final MainTex coordinates. Ignored when Use Eye Surface Mask is enabled.'
    IrisCenterY = 'Procedural center Y in final MainTex coordinates. Ignored when Use Eye Surface Mask is enabled.'
    IrisRadiusX = 'Horizontal procedural radius in final iris UV. In map mode it only calibrates depth/offset units; it does not clip the painted region.'
    IrisRadiusY = 'Vertical procedural radius in final iris UV. In map mode it only calibrates depth/offset units; it does not clip the painted region.'
    IrisDepth = 'Global recess gain for BOTH procedural shapes and surface maps, in local iris-radius units. Zero preserves source UV; start near 0.08. Not meters or pixels.'
    EyeOpticsEdgeFade = 'Procedural ellipse boundary fade only. Painted maps supply their own smooth edge and replace this control.'
    EyeOpticsMaxOffset = 'Maximum displacement in normalized iris-chart units. Limits extreme grazing offsets.'
    EyeUVMinX = 'Left safe atlas boundary for shifted samples. Inset internally by the filter footprint.'
    EyeUVMinY = 'Bottom safe atlas boundary for shifted samples. Inset internally by the filter footprint.'
    EyeUVMaxX = 'Right safe atlas boundary. Invalid footprints fall back to unwarped mapping.'
    EyeUVMaxY = 'Top safe atlas boundary. Invalid footprints fall back to unwarped mapping.'
    EyeDispersion = 'Optional three-ray RGB geometric dispersion. 0 uses one interior color sample. No rainbow coat or wave-optics simulation.'
    EyeDebugView = '0 final; 1 alpha; 2 stencil; 3 chart; 4 geometric UV shift before atlas fallback; 5 interface normal; 6 invalid hits; 7 normalized surface depth; 8 optical region (not stencil). Base-only.'
    EyeRefractionIOR = 'Independent interior-ray and RGB-dispersion IOR. Larger values reduce lateral parallax. 1 removes bending, not recess parallax. Default 1.5; for old optics copy the former Clearcoat IOR here once.'
    IrisDepthShape = 'Procedural 0 original flat recess; 1 shallow cone; 2 shallow bowl. All share Iris Depth gain. Ignored with Use Eye Surface Mask.'
    UseEyeSurfaceMask = '0 original procedural ellipse; 1 painted region and depth replace the ellipse/edge cap. Alpha, stencil and coat normals are unchanged. Default off.'
    EyeSurfaceMap = 'Linear data aligned with final MainTex UV including rotation/ST; no separate ST. Shares MainTex filtering/wrap. Disable sRGB on import; use smooth shallow profiles, not steep walls or noise.'
    EyeSurfaceMapMode = '0 grayscale HEIGHT: white=surface/no effect, black=deepest; alpha ignored. 1 R=DEPTH (white=deepest), A=region (white=inside). A tapers depth and the entry transition, never opacity. Both multiply Iris Depth.'
}
$enumRanges = @{
    ClearCoatNormalSource = '0,3'
    ClearCoatDebugView = '0,5'
    IrisDepthShape = '0,2'
}
$surfaceHints = @('EyeOpticsMode','EyeOpticsStrength','IrisCenterX','IrisCenterY',
    'IrisRadiusX','IrisRadiusY','IrisDepth','EyeOpticsEdgeFade','EyeDebugView',
    'EyeRefractionIOR','IrisDepthShape','UseEyeSurfaceMask','EyeSurfaceMap','EyeSurfaceMapMode')
foreach ($name in @('MainOpaqueX','MainAlphaX','MainAlphaX2Pass','MainAlphaXBackFront','HairX','EyeWX','EyeX')) {
    $source = Get-Content -LiteralPath (Join-Path $Root "Shaders/Tom/$name.shader") -Raw
    $head = $source.Substring(0, $source.IndexOf('SubShader'))
    $properties = @([regex]::Matches($head, '(?m)^\s*(?:\[[^\]]+\]\s*)*_(\w+)\s*\("([^"]*)",\s*(2D|Color|Float|Range\(([^)]*)\))\)'))
    $entry = $manifest.SelectSingleNode("//Shader[@Name='tom/$name']")
    $catalog = $tips.SelectSingleNode("//Shader[@Name='tom/$name']")
    if (-not $entry) {
        $entry = $manifest.SelectSingleNode('//Shader[@Name="tom/MainOpaqueX"]').CloneNode($true)
        $entry.SetAttribute('Name',"tom/$name"); $entry.SetAttribute('Asset',"a_Tom$name")
        [void]$manifest.manifest.MaterialEditor.AppendChild($entry)
        $catalog = $tips.SelectSingleNode('//Shader[@Name="tom/MainOpaqueX"]').CloneNode($true)
        $catalog.SetAttribute('Name',"tom/$name")
        $catalog.Tooltip = 'X-series eye surface. Explicit material settings; no mesh-role detection. New optics and coat default off. Hair feather needs adapter 0.3.0 for these writers.'
        [void]$tips.DocumentElement.AppendChild($catalog)
    }
    $names = @($properties | ForEach-Object {$_.Groups[1].Value})
    foreach ($node in @($entry.Property)) { if ($names -notcontains $node.Name) { [void]$entry.RemoveChild($node) } }
    foreach ($node in @($catalog.Property)) { if ($names -notcontains $node.Name) { [void]$catalog.RemoveChild($node) } }
    foreach ($p in $properties) {
        $key = $p.Groups[1].Value
        $enumRange = if ($key -eq 'EyeDebugView') { if ($name -eq 'EyeX') {'0,8'} else {'0,2'} } else { $enumRanges[$key] }
        if (-not $entry.SelectSingleNode("Property[@Name='$key']")) {
            $node = $manifest.CreateElement('Property'); $node.SetAttribute('Name',$key)
            $category = if ($key.StartsWith('ClearCoat')) {'Clearcoat'} elseif ($key -in @('Alpha','StencilCutoff')) {'Render Options'} else {'Eye Surface'}
            $node.SetAttribute('Category',$category)
            $type = switch ($p.Groups[3].Value) { '2D' {'Texture'} 'Color' {'Color'} default {'Float'} }
            $node.SetAttribute('Type',$type)
            if ($p.Groups[4].Success) { $node.SetAttribute('Range',$p.Groups[4].Value) }
            elseif ($type -eq 'Float') {
                $range = if ($enumRange) { $enumRange } else { '0,1' }
                if ($key -notin @('IrisCenterX','IrisCenterY')) { $node.SetAttribute('Range',$range) }
            }
            [void]$entry.AppendChild($node)
        }
        # Repair owned enum ranges from older catalogs without rewriting unrelated metadata.
        if ($enumRange) { $entry.SelectSingleNode("Property[@Name='$key']").SetAttribute('Range',$enumRange) }
        if (-not $catalog.SelectSingleNode("Property[@Name='$key']")) {
            if (-not $hints.ContainsKey($key)) { throw "Missing tooltip: $key" }
            $node = $tips.CreateElement('Property'); $node.SetAttribute('Name',$key); $node.InnerText = $hints[$key]
            if ($key -eq 'EyeDebugView' -and $name -eq 'EyeWX') {
                $node.InnerText = '0 final; 1 unwarped coverage; 2 stencil survival. Base-only, overrides coat and lighting debug.'
            }
            [void]$catalog.AppendChild($node)
        }
        if ($key -eq 'ClearCoatIOR' -or ($name -eq 'EyeX' -and $key -in $surfaceHints)) {
            $catalog.SelectSingleNode("Property[@Name='$key']").InnerText = $hints[$key]
        }
    }
    foreach ($category in @('Clearcoat','Eye Surface')) {
        if ($category -eq 'Eye Surface' -and -not $name.StartsWith('Eye')) { continue }
        if (-not $catalog.SelectSingleNode("Category[@Name='$category']")) {
            $node = $tips.CreateElement('Category'); $node.SetAttribute('Name',$category)
            $node.InnerText = if ($category -eq 'Clearcoat') {'Optional independent dielectric surface layer; defaults off.'} else {'Game-compatible eye layers and optional local optics; no role presets.'}
            [void]$catalog.AppendChild($node)
        }
    }
    if ($name.StartsWith('Eye')) {
        $catalog.SelectSingleNode('Property[@Name="MainTex"]').InnerText = if ($name -eq 'EyeX') {
            'RGB iris art, optionally refracted. Unwarped alpha combines with expression/highlight alpha, AlphaMask R and Alpha. Surviving coverage is alpha blended; optics never warps coverage.'
        } else {
            'RGB eye-area art, multiplied by twice the game Color alias and BaseColor. Main alpha times AlphaMask R times Alpha is cutout coverage; no iris refraction.'
        }
        $catalog.SelectSingleNode('Property[@Name="AlphaMask"]').InnerText = 'Linear R multiplies unwarped source coverage together with Alpha. EyeWX clips; EyeX alpha blends surviving pixels. Shared by color and StencilMask; not an optical-region mask.'
        $catalog.SelectSingleNode('Property[@Name="Cutoff"]').InnerText = 'Unwarped composite alpha cutoff shared by color and stencil survival. EyeX blends surviving coverage; EyeWX is cutout.'
        $catalog.SelectSingleNode('Property[@Name="CullOption"]').InnerText = '0 both faces, 1 back faces, 2 front faces; shared by color and the named StencilMask pass. No eye ShadowCaster.'
        $outline = $catalog.SelectSingleNode('Category[@Name="Outline"]'); if ($outline) { [void]$catalog.RemoveChild($outline) }
    }
}
# Release versioning is separate from metadata synchronization, including future versions.
$manifest.Save((Join-Path $Root 'manifest.xml'))
$tips.Save((Join-Path $Root 'Tooltips/tom_x_tooltips.xml'))
Write-Output 'Synchronized seven public shaders using XML DOM; release version and unrelated metadata retained.'
