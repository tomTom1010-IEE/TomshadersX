param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$source = Get-Content -LiteralPath (Join-Path $Root 'Shaders/Tom/SkinX.shader') -Raw
$header = $source.Substring(0, $source.IndexOf('SubShader'))
$declarations = @($header -split '\r?\n' | Where-Object { $_ -match '^\s*(?:\[[^\]]+\]\s*)*_\w+\s*\(' } | ForEach-Object { $_.Trim() })
$hints = @{
    MainTex='Skin color already composed by the game, followed by three overlays. Alpha is ignored unless SkinMainAlphaClip is on; clothing RG clipping stays independent.'
    AlphaMask='KKS clothing mask: R/G enabled by alpha_a/alpha_b, minimum coverage clipped at 0.5 in Base/Add/Outline/ShadowCaster. Independent ST.'
    Cutoff='MainTex alpha threshold only when SkinMainAlphaClip is on. Clothing AlphaMask R/G always uses its separate fixed 0.5 threshold. Shared by Base/Add/Outline/ShadowCaster.'
    OutlineNormalSource='SkinX uses mesh normals only. UV4ObjectSpace is unavailable because the KKS third overlay occupies UV4. This option does not reinterpret those UVs.'
    DetailNormalMapScale='Game detail strength: scales detail normal, LineMask R line attenuation and B painted shade. SkinDiffuseNormalDetail reduces diffuse detail independently.'
    DetailMask='Linear KKS data: R painted highlight/specular gate; G painted shade; B Rim and Outline suppression; A skin versus nail/lip gloss region. Own ST and sampler. Not ORM.'
    LineMask='Linear original channels: R internal lines, G game linewidth exponent, B painted detail shade; A unused. Shares DetailMask sampling state with independent ST.'
    NormalMask='Legacy face art data. G optionally blends diffuse normal toward the geometric normal through SkinFaceNormalStrength. B shadow bypass is deliberately not ported. Not a normal map.'
    SkinControlMap='Linear R soft-response coverage, G warm-transition coverage, B wetness coverage; A unused. Own ST, DetailMask sampler; default RG=1 B=0.'
    ColMask='Optional extra RGB color regions: sequential Col0 to Col1 by R, then Col2 by G, then Col3 by B. UV0 and MainTex sampler. Not the game texture compositor ColorMask.'
    overtex1='KKS nipple/lip layer on UV1 (Unity UV2), gated/remapped by vertex R and nip/nipsize. Ordinary RGBA or tex1mask legacy RG color encoding. Own sampler and ST.'
    overtex2='KKS underhair/dynamic face blush on UV2 (Unity UV3) times vertex B. Tinted RGBA, applied after overlay 1. Own sampler and ST.'
    overtex3='Third KKS RGBA layer on UV3 (Unity UV4), after overlay 2. Face eyeshadow; body role is asset-specific. Own sampler and ST.'
    overcolor1='Tint and alpha of the first KKS overlay. Default alpha zero is neutral; the game may replace this color.'
    overcolor2='Tint and alpha of the second KKS overlay. Face dynamic blush updates alpha at runtime; do not replace it with a static makeup bake.'
    overcolor3='Tint and alpha of the third KKS overlay. Default alpha zero is neutral.'
    nip='Blend between ordinary UV1 and the localized nipple-size remapping; vertex R still gates both.'
    nipsize='Legacy centered nipple remapping parameter, not ordinary texture tiling. Used when nip is nonzero.'
    nip_specular='Adds the legacy overlay-1 G painted shine inside its color encoding. This is not GGX roughness or Clearcoat.'
    tex1mask='Zero: ordinary tinted RGBA. One: legacy R colored shape with G painted shine. Texture/tint alpha still controls coverage.'
    alpha_a='Game clothing-mask R activation: zero bypasses R, one uses R. Never disables the G channel.'
    alpha_b='Game clothing-mask G activation: zero bypasses G, one uses G. Both active use the minimum.'
    SkinMainAlphaClip='Optional additional MainTex alpha cutoff. Clothing R/G clipping remains active regardless of this setting.'
    SpecularPower='Game body/cheek gloss gain in the DetailMask A skin region. Runtime skinTuyaRate may update it. Does not alter roughness, IOR or coat.'
    SpecularPowerNail='Game nail/lip gloss gain in the 1-DetailMask A region. It is not limited to nail meshes.'
    SkinGameGloss='Blend from unit gain to max(A*SpecularPower,(1-A)*SpecularPowerNail). Affects substrate direct/probe specular, not Clearcoat.'
    UseDetailRAsSpecularMap='Multiply ordinary direct specular by stationary DetailMask R. Does not reinterpret R as roughness.'
    notusetexspecular='One bypasses the moving Detail R highlight mask. Zero permits it at SkinPatternStrength. Ordinary X specular remains available.'
    SpeclarHeight='Legacy spelling retained. Moves painted Detail R lookup along tangent-space view using 0.8*(value-1). Not physical height, skin depth or refraction.'
    SkinPatternStrength='Moving Detail R gates the direct X highlight instead of adding an unlit legacy shine. Requires notusetexspecular below one. It remains per-light and shadowed.'
    linetexon='Legacy internal line activation; affects diffuse body terms only, not outer shell width, coat or emission.'
    SkinLineStrength='Independent gain for internal LineMask R/G after the game detail/linewidth calculation. No framebuffer-wide darkening.'
    SkinLineColor='Diffuse tint applied where internal lines attenuate the skin. Not the shell Outline color.'
    SkinGameLineColor='Blend line tint toward the game global LineColorG RGB. Game linewidthG still shapes LineMask G independently.'
    SkinShadeStrength='Painted diffuse shade from Detail G and Line B. Uses ShadowColor; does not replace shadow-map visibility or encode AO/thickness.'
    SkinDiffuseNormalDetail='One keeps full detail for diffuse; zero uses the main normal. Specular and coat detail are not weakened.'
    SkinFaceNormalStrength='New explicit art control: NormalMask G blends diffuse normal toward mesh normal. Default zero. Does not implement the old shadow floor.'
    SkinStrength='Zero preserves the neutral X diffuse response. Positive values blend wrapped front-side diffuse and transition warmth under the skin control map. Not true SSS.'
    SkinWrap='Broadens the diffuse lighting transition when SkinStrength is positive. Geometric back-facing surfaces and external blockers still reject direct light.'
    SkinWarmth='Warm tint amount near the light terminator; gated by skin strength, Control R/G and the lit diffuse contribution. No unshadowed red emission.'
    SkinWarmWidth='Width of the warm transition in diffuse NdotL, not world-space scattering distance.'
    SkinWarmColor='Transition hue, luminance-normalized before blending. Independent of albedo and coat color.'
    liquidmask='Linear RGB composite encoding of five game liquid regions. UV0 plus own ST, DetailMask sampler. Not tiled with Texture2/3.'
    Texture2='Liquid coverage pattern: R first stage, G second stage. For amount 0..2, union max(saturate(s)*R,saturate(s-1)*G). Own tiled sampler.'
    Texture3='Unity packed liquid tangent normal, not height. Uses LiquidTiling then own ST; shares Texture2 sampler. Final-skin slot, not the game compositor paint slot.'
    LiquidTiling='Raw vector exposed as a color by MaterialEditor compatibility: R/G=UV offset, B/A=UV scale. Default (0,0,1,1). No color interpretation or gamma conversion.'
    liquidftop='Game liquid amount 0..2 in the R-max(G,B) front-top region.'
    liquidfbot='Game liquid amount 0..2 in the G-max(R,B) front-bottom region.'
    liquidbtop='Game liquid amount 0..2 in the B-max(R,G) back-top region.'
    liquidbbot='Game liquid amount 0..2 in the (min(R,G)-0.1)/0.9 back-bottom region.'
    liquidface='Game liquid amount 0..2 in the (min(G,B)-0.1)/0.9 face region.'
    SkinLiquidColor='Pigmented liquid substrate color under resolved coverage. Does not tint Clearcoat or automatically make skin transparent.'
    SkinLiquidColorStrength='Liquid color coverage multiplier. Zero keeps underlying skin color while liquid normal/wet coat may still operate.'
    SkinLiquidNormalScale='Strength of the Texture3 packed normal before coverage blending. Independent of base/detail normal scales.'
    SkinLiquidMaterial='Blend substrate roughness toward SkinLiquidRoughness under liquid coverage. Does not enable Clearcoat.'
    SkinLiquidRoughness='Substrate roughness of covered liquid regions; independent of ClearCoatRoughness.'
    SkinCoatCoverage='0 common coat map only; 1 liquid; 2 authored wetness; 3 union. Multiplies existing coat weight/map. ClearCoat=0 always stays off.'
    SkinWetness='Authored wetness gain times SkinControlMap B. Used only by wetness/union coat coverage; no pigment or geometry simulation.'
    SkinLiquidCoatNormal='Optional blend from the selected common coat normal toward the liquid normal under liquid coverage. Does not change the shared normal-source enum.'
    SkinDebugView='0 off; 1 albedo; 2 Detail RGB; 3 Line RGB; 4 liquid; 5 diffuse normal; 6 gloss; 7/8/9 UV1/2/3; 10 control; 11 clothing coverage; 12 liquid normal; 13 diffuse tint. Add/Outline suppressed for data views.'
}
function Category([string]$name) {
    if ($name -match '^(over|nip|tex1|Col)') { return 'KKS Color Layers' }
    if ($name -match '^(alpha_|SkinMainAlpha)') { return 'KKS Clothing Coverage' }
    if ($name -match '^(liquid|Texture[23]|Liquid|SkinLiquid)') { return 'KKS Liquid' }
    if ($name -match '^(SkinCoat|SkinWet)') { return 'Skin Wet Surface' }
    if ($name -match '^(SpecularPower|Speclar|notuse|UseDetailR|SkinPattern|SkinGameGloss)') { return 'KKS Gloss' }
    if ($name -eq 'SkinDebugView') { return 'Debug' }
    return 'Skin Response'
}
foreach ($relative in @('manifest.xml','Tooltips/tom_x_tooltips.xml')) {
    $path = Join-Path $Root $relative
    $doc = [xml]::new()
    $doc.PreserveWhitespace = $true
    $doc.Load($path)
    $old = $doc.SelectSingleNode('//Shader[@Name="tom/SkinX"]')
    $node = $doc.SelectSingleNode('//Shader[@Name="tom/MainOpaqueX"]').CloneNode($true)
    $node.SetAttribute('Name','tom/SkinX')
    $isManifest = $relative -eq 'manifest.xml'
    if ($isManifest) { $node.SetAttribute('Asset','a_TomSkinX') }
    else { $node.SelectSingleNode('Tooltip').InnerText = 'KKS face/body skin on X lighting: legacy overlays and clothing masks, regional gloss, optional soft skin and shared wet Clearcoat. D3D11 skinned characters; no lightmap UV or UV4 outline-normal support.' }
    foreach ($line in $declarations) {
        if ($line -notmatch '_(\w+)\s*\("([^"]+)"\s*,\s*(.*?)\)\s*=') { throw "Unparsed property: $line" }
        $name=$Matches[1]; $label=$Matches[2]; $type=$Matches[3]
        $property=$node.SelectSingleNode("Property[@Name='$name']")
        if (-not $property) {
            $property=$doc.CreateElement('Property'); $property.SetAttribute('Name',$name)
            if ($isManifest) {
                $property.SetAttribute('Category',(Category $name))
                $property.SetAttribute('Type',$(if($type -eq '2D'){'Texture'}elseif($type -in @('Color','Vector')){'Color'}else{'Float'}))
                if ($type -match '^Range\(([^)]+)\)') { $property.SetAttribute('Range',$Matches[1]) }
                elseif ($type -eq 'Float') {
                    $property.SetAttribute('Range',$(if($name -eq 'SkinDebugView'){'0,13'}elseif($name -eq 'SkinCoatCoverage'){'0,3'}else{'0,1'}))
                }
            }
            [void]$node.AppendChild($doc.CreateWhitespace("`r`n      "))
            [void]$node.AppendChild($property)
        }
        if (-not $isManifest) {
            if ($hints.ContainsKey($name)) { $property.InnerText=$hints[$name] }
            elseif (-not $property.InnerText) { $property.InnerText="$label. Extra color tint only; does not replace the game's baked MainTex customization." }
            if ($name -in @('SpecularMask','MetallicMap','RoughnessMap','OcclusionMap','ReflectionMask','EmissionMask')) {
                $property.InnerText += ' SkinX UV0 atlas sampling shares MainTex filtering/wrap state with independent ST; match atlas import settings.'
            }
        }
    }
    if (-not $isManifest) {
        $categories = [ordered]@{
            'KKS Color Layers'='Game-composed skin and UV1/2/3 overlays. Preserve game-owned textures, tints and vertex gates.'
            'KKS Clothing Coverage'='Independent clothing R/G cutout and optional MainTex alpha cutoff.'
            'KKS Gloss'='Legacy skin/nail gloss and painted highlight controls; separate from X roughness and Clearcoat.'
            'Skin Response'='Optional front-side diffuse softening, warmth and painted detail; not volumetric SSS.'
            'KKS Liquid'='Game liquid region amounts, tiled coverage and normal; no simulation or automatic transparency.'
            'Skin Wet Surface'='Liquid/authored wetness coverage for the shared Clearcoat; ClearCoat must be enabled.'
        }
        foreach ($category in $categories.GetEnumerator()) {
            $item=$doc.CreateElement('Category'); $item.SetAttribute('Name',$category.Key); $item.InnerText=$category.Value
            [void]$node.AppendChild($doc.CreateWhitespace("`r`n      ")); [void]$node.AppendChild($item)
        }
    }
    $parent=if($isManifest){$doc.SelectSingleNode('//MaterialEditor')}else{$doc.DocumentElement}
    if ($old) { [void]$parent.ReplaceChild($node,$old) }
    else {
        [void]$parent.AppendChild($doc.CreateWhitespace("`r`n    "))
        [void]$parent.AppendChild($node)
        [void]$parent.AppendChild($doc.CreateWhitespace("`r`n  "))
    }
    $doc.Save($path)
}
Write-Output "SkinX metadata regenerated for $($declarations.Count) properties; existing shader nodes preserved."
