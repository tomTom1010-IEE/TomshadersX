# SkinX Material Contract

`tom/SkinX` is one face/body shader for KKS, Unity 2019.4 Built-in Forward on
D3D11. Experimental package 0.3.0 adds 59 skin properties to the unchanged 98 X
properties and ten Clearcoat properties. The 167 declarations are snapshotted in
`Tests/SkinX-v1.properties.txt`; metadata/tooltips expose every property.

This preserves the audited game input contract, not V+ lighting pixel identity.
No extra runtime plugin, screen pass, GrabPass, POM or stencil writes are added.
Face/body role detection and automatic presets are intentionally absent.

## Defaults And First Test

The queue is 2400 (`AlphaTest-100`), with depth writing and backface culling.
ForwardBase, ForwardAdd, Outline and ShadowCaster share clothing cutout.
MainTex alpha is ignored until `_SkinMainAlphaClip=1`; `_Cutoff` then applies in
addition to clothing RG. Coat, soft skin, authored wetness and liquid amounts
default off. Existing X lighting defaults remain unchanged.

1. Change the intended face/body material to `tom/SkinX`, retaining game textures,
   their transforms and runtime aliases. Check all three overlays before styling.
2. Start with ordinary X ramp/roughness/specular controls. `_SkinStrength=0` is
   neutral X diffuse under neutral adapter inputs, not a V+ appearance preset.
3. For a visible soft-skin trial, use `_SkinStrength=0.7`, `_SkinWrap=0.4` and
   `_SkinWarmth=0.3`; these are fixture example values, not mandatory presets.
4. Lower `_SkinDiffuseNormalDetail` for cleaner diffuse bands without reducing
   specular normal detail. Optional `_SkinFaceNormalStrength` uses NormalMask G.
5. For wet sheen, enable common `_ClearCoat` explicitly, then choose
   `_SkinCoatCoverage`. Changing coverage or IOR alone never enables the coat.

## Texture And Runtime Inputs

| Input | Implemented meaning |
| --- | --- |
| MainTex | Already game-composited RGB on UV0, multiplied by optional extra ColMask/Col0..3 tint, then overlays and X BaseColor |
| overtex1 / overcolor1 | UV1 (Unity uv2), vertex R gating, legacy nipple-size remap and optional RG color encoding |
| overtex2 / overcolor2 | UV2 (Unity uv3), vertex B gating, tinted RGBA; dynamic face blush alpha remains writable |
| overtex3 / overcolor3 | UV3 (Unity uv4), third tinted RGBA layer; confirmed face eyeshadow, asset-specific body role |
| DetailMask | R highlight gate, G painted shade, B Rim/Outline suppression, A skin versus nail/lip gloss |
| LineMask | R attenuation, G game linewidth exponent, B painted shade; A ignored |
| AlphaMask | Independent R/G clothing coverage enabled by `_alpha_a/b`; active channels intersect and clip at 0.5 |
| NormalMap / NormalMapDetail | Existing Unity packed normal inputs; game `_DetailNormalMapScale` also scales legacy line R and shade B |
| NormalMask | G optionally softens diffuse normals toward mesh normals; not a normal texture or thickness map |
| SkinControlMap | R soft response, G warm transition, B authored wetness, A reserved/ignored |
| liquidmask | Original five-region RGB compound encoding on UV0 |
| Texture2 | Tiled liquid coverage stages R/G, union `max(saturate(s)*R,saturate(s-1)*G)` for amount 0..2 |
| Texture3 | Packed tangent-space liquid normal, not height or a compositor paint input |

UV numbers above are zero-based. All declared ST transforms remain independent.
Overlays are ordered, not additive. Game setters for gloss, overlay colors,
nipple remapping, liquid amounts and clothing masks retain their property names.
`_LiquidTiling.xy` is offset, `.zw` is scale, followed by each pattern/normal's ST.
MaterialEditor exposes that vector as raw RGBA, not a gamma-corrected tint.

Neutral importer defaults are packaged Texture2D assets created through Bridge:
Detail `(1,0,0,1)`, Control `(1,1,0,1)`, and transparent missing overlays. Overlay
tint alpha also defaults to zero. These assets belong to the X package.

## Lighting And Region Controls

Game gloss is `max(A*SpecularPower,(1-A)*SpecularPowerNail)`, blended from unit
gain by `_SkinGameGloss`. It scales substrate direct and environment specular,
not roughness, IOR, Clearcoat, MatCap or diffuse color. A=0.5 can attenuate both
regions because this is the legacy maximum formula, not a linear interpolation.

`_UseDetailRAsSpecularMap` is a stationary direct-specular gate. The optional
moving painted gate uses `_notusetexspecular=0`, `_SkinPatternStrength` and the
legacy spelling `_SpeclarHeight`; it gates lit X specular instead of adding an
unshadowed V+ highlight. These gates do not redefine roughness or the probe map.

Painted shade and internal lines tint diffuse body terms consistently in Base
and Add. They do not multiply coat, emission or the completed framebuffer.
`_SkinGameLineColor` opts into global `_LineColorG`; `_linewidthG` shapes Line G.
NormalMask does not reinstate the old face shadow floor. `_ambientshadowG`,
`_RampG`, legacy reflection families and tessellation are not silently ported.

The soft response wraps only front-side diffuse input before X Toon transfer.
Warmth is a luminance-normalized transition tint under Control R/G and actual
lit diffuse. Geometric horizon, distance, cookies and shadow visibility remain.
This is not true SSS, ear transmission, spatial diffusion or physical thickness.

Indirect diffuse uses the diffuse normal; full surface normals remain available
for substrate specular. Base owns GI, probes, additive MatCap, Rim and emission;
Add owns only its light. Common Clearcoat is reused once, not duplicated.

## Liquid And Coat

The five liquid controls preserve front-top, front-bottom, back-top, back-bottom
and face compound-mask semantics from the source audit. Liquid pigment,
Texture3 normal and localized substrate roughness are independent of coat.
Liquid normal strength zero produces a flat liquid surface normal; it does not
mean bypassing liquid coverage or restoring the underlying detail normal.

`_SkinCoatCoverage`: 0 common map only; 1 liquid; 2 Control B times SkinWetness;
3 union of liquid and authored wetness. This multiplies the existing coat weight
and map. `_SkinLiquidCoatNormal` optionally blends the selected coat normal toward
the liquid normal under coverage. Clearcoat zero is exact off in every mode.

## Sampling And Geometry Limits

D3D11 sampler pressure requires explicit sharing within SkinX only:

- SpecularMask, MetallicMap, RoughnessMap, OcclusionMap, ReflectionMask,
  EmissionMask and ColMask share MainTex filtering/wrap, retaining their own ST.
- LineMask, NormalMask, SkinControlMap and liquidmask share DetailMask sampling.
- Texture3 shares Texture2 sampling, separate from clamped face/body masks.
- Each overlay keeps its own sampler. Existing seven shaders are unchanged.

Use matching import settings within each group. Independent ST does not restore
independent filtering/wrap. Data masks should be linear; actual game texture
formats/import settings still need in-game checks. Skin does not consume overlay
UVs as lightmap/shadowmask UVs. Skinned characters use probe/SH lighting instead.
UV4ObjectSpace outline normals are unavailable because UV4 belongs to overlay 3;
Skin uses mesh normals and advertises this in its inherited control's tooltip.

## Debug And Validation

SkinDebugView 0 off, 1 albedo, 2 Detail RGB, 3 Line RGB, 4 liquid coverage,
5 diffuse normal, 6 gloss, 7/8/9 UV1/2/3, 10 control, 11 clothing coverage,
12 liquid normal, 13 diffuse tint. Skin data views suppress Add/Outline and fog.
Common lighting/coat debug remains available. Unity Inspector uses a named enum
to avoid Unity 2019's limit on inline Enum drawer arguments.

See [implementation evidence](XSeriesSkinProgress.md) for reproducible tests and
remaining actual-character acceptance. Do not infer KKS card/expression or device
performance certification from synthetic Editor fixtures.
