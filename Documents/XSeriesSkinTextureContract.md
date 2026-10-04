# KKS Skin Texture Contract Audit

Date: 2026-10-04. Status: source audit underlying the implemented SkinX adapter.
This document distinguishes reference behavior from the replacement's choices;
see [SkinX material contract](XSeriesSkinContract.md) for actual defaults and
migration differences, and [SkinX design](XSeriesSkinDesign.md) for design intent.

## Evidence And Limits

Three different things must not be confused: the game's texture-composition
inputs, the textures bound to the final skin material, and a replacement shader's
choice of lighting response. Matching a property name alone does not establish
that all three use the same channels.

The principal final-material reference is the local V+ LTS source in
`Assets/Mods/KKShadersPlus-release1.7.1`:

- `Shaders/Skin/SkinPlus.shader`: properties, passes and outline mask.
- `Shaders/Skin/KKPSkinInput.cginc`: UV sets, vertex color and legacy uniforms.
- `Shaders/Skin/KKPDiffuse.cginc`: color overlays and clothing alpha.
- `Shaders/Skin/KKPSkinFrag.cginc`: detail/line channels and liquid composition.
- `Shaders/Skin/KKPNormals.cginc`: packed normal decoding and combination.
- `Shaders/KKPLighting.cginc`: drawn highlight and face-mask behavior.
- `Shaders/KKPCoom.cginc`: liquid region and two-stage pattern decoding.

The local KKS lilToon port's `Shader/Includes/LTSKKSKKSSkin.cginc`,
`LTSKKSLiquidCore.cginc` and `LTSKKSKKSLiquid.cginc` provide a second adapter
reference. Its choices are not a universal upstream lilToon texture contract.
The simplified `Assets/Shaders/Shader Forge_main_skin.shader` is not sufficient
evidence for the complete runtime skin shading implementation.

Game-side assignments were inspected read-only with Mono.Cecil in:

`D:/Program Files/KoikatuSunshine/KoikatsuSunshine_Data/Managed/Assembly-CSharp.dll`

- Assembly version: `0.0.0.0`.
- MVID: `30a11360-597c-49df-9ecb-5d1517a95164`.
- SHA256: `D6A1FC24375FDC05165DFD268F524DF373A844EF43847DD5818E13B0C7DD9924`.

The SHA256 above is an evidence identifier, not a supported-version restriction.
Custom head/body mods may author different regional content. This audit confirms
the consumers and game setters, not every installed texture or a new game render.

## UV And Color Inputs

Use zero-based shader UV names throughout this document:

| Shader input | Unity mesh channel | Final skin use |
| --- | --- | --- |
| `TEXCOORD0` / UV0 | `mesh.uv` | Main color, normal maps, detail, lines, alpha, liquid regions/patterns |
| `TEXCOORD1` / UV1 | `mesh.uv2` | First overlay, including nipple/lip mapping |
| `TEXCOORD2` / UV2 | `mesh.uv3` | Second overlay, including underhair/dynamic blush |
| `TEXCOORD3` / UV3 | `mesh.uv4` | Third overlay, including eyeshadow |
| `COLOR.r` | Vertex color R | First-overlay UV gating/remapping |
| `COLOR.b` | Vertex color B | Second-overlay UV gating/remapping |

Vertex color is not a general multiply of skin albedo in this reference. Replacing
these UV sets with UV0, treating them as baked-lightmap UVs, or reading UV3 as an
outline smooth normal changes the material contract. Each texture has its own ST
where declared. Normal maps use Unity's packed normal decoding, not raw RGB XYZ.

## Final Material Color Layers

| Property | Meaning and operation |
| --- | --- |
| `_MainTex` | Main skin RGB, often already generated from game color/paint customization. UV0 plus its ST. Ordinary SkinPlus does not use its alpha for clothing clipping. |
| `_ColMask`, `_Col0`..`_Col3` | V+ extension: RGB selects sequential color lerps, multiplied into MainTex. Not the game's separate composition `_ColorMask`. |
| `_overtex1`, `_overcolor1` | Body nipple/areola or face lip layer. UV1, vertex R and the nipple UV controls; alpha times tint alpha blends the layer. |
| `_overtex2`, `_overcolor2` | Body underhair or face dynamic blush. UV2 times vertex B, then ST; ordinary tinted RGBA blending. |
| `_overtex3`, `_overcolor3` | Third tinted RGBA layer on UV3; face eyeshadow is confirmed by the game setter. A universal body role is not confirmed. |

Order is MainTex -> overlay 1 -> overlay 2 -> overlay 3. This is sequential
coverage composition, not three independently additive color contributions.
For the V+ color extension, the sequence is `c=Col0; c=lerp(c,Col1,R);`
`c=lerp(c,Col2,G); c=lerp(c,Col3,B)`; RGB need not sum to one.

Overlay 1 has two interpretations controlled by `_tex1mask`:

- Ordinary tinted RGBA when zero.
- Legacy patterned tint when one: R supplies the colored shape; G contributes
  `_nip_specular * 0.33` inside the color expression. This is not a physical GGX
  specular channel. Texture alpha times `_overcolor1.a` still supplies coverage.

`_nip` and `_nipsize` control the existing localized UV1 remapping around the
center. It includes a radial transition, not just uniform texture scaling.
Preserve that mapping and the vertex-color gates before applying overlay ST.

Do not assume a black default texture has zero alpha. Neutral missing overlays
need explicitly transparent data or a zero tint alpha. Do not identify the third
body layer from the old source comment "seems"; preserve it as an authored layer.

## Detail Mask

`_DetailMask` is a mixed artistic/region data map on UV0. It is not ORM, a height
map, or a direct substitute for X `_MaterialMap`.

| Original channel | Verified V+ consumer | SkinX compatibility requirement |
| --- | --- | --- |
| R | Painted highlight pattern; sampled again with a view-dependent UV offset. Optional `_UseDetailRAsSpecularMap` also uses stationary R as a mesh-specular mask. | Preserve patterned highlight data and its optional specular gate; do not reinterpret as roughness or geometric height. |
| G | Drawn shadow/detail; `1-G` enters drawn-shadow shaping. Larger G gives more darkening. | Artistic diffuse detail, separately weighted from physical shadow/AO. |
| B | Restricts the old Rim response; outline width uses `1-B`. | Preserve legacy Rim/outline restriction separately from X's own layer/outline masks. |
| A | Skin/body versus special-region weighting for legacy gloss. White selects ordinary skin; black selects the nail-side response. | Preserve regional gloss distinction; on face, the game also uses the special-region control for lips. Not opacity. |

The fragment swizzles/inverts channels before calling the highlight helper.
In original-channel notation the regional highlight gain is:

```text
legacyGloss = max(DetailA * SpecularPower,
                  (1 - DetailA) * SpecularPowerNail)
```

It is a maximum of two weighted gains, not a roughness interpolation. The old
`_SpeclarHeight` spelling is intentional. It changes the R-pattern lookup by
`0.8 * (SpeclarHeight - 1) * float2(dot(T,V), dot(B,V))` before DetailMask ST.
It does not represent millimeters of skin relief or eye refraction depth.

The local lilToon adapter uses DetailMask A to modulate smoothness and `1-G` for
a shadow/AO-style operation. Those are adapter choices, not proof that the source
texture is a universal smoothness/AO map. SkinX must define its own mapping.

## Line Mask

`_LineMask` is authored internal line/detail data on UV0, not shell-outline width.
The reference swizzle can obscure the original channel meanings:

| Original channel | Verified role |
| --- | --- |
| R | Interior line attenuation, affected by detail strength |
| G | Exponent shaping the interior line factor from `_linewidthG` |
| B | Additional drawn shadow/detail signal |
| A | Not consumed by the examined skin fragment |

Equivalent expressions, with `d = _DetailNormalMapScale`, are:

```text
drawnShadow = min(1 - DetailMask.g, 1 - LineMask.b * d)
lineBase = 0.8 * (1 - _linewidthG) + 0.2
lineFactor = min(pow(lineBase, LineMask.g), 1 - 0.5 * LineMask.r * d)
lineFactor = lerp(1, lineFactor, _linetexon)
```

The reference also computes a tinted line color using `_LineColorG` and its skin
color machinery. SkinX can replace that color machinery with X's composition,
but cannot simply sample LineMask R and discard G/B. Preserve the existing game
detail-strength link, with optional independent SkinX multipliers. New clamping
for out-of-range material values must be documented as safety behavior.

## Normals And Face Art Mask

| Property | Verified role |
| --- | --- |
| `_NormalMap` | Base tangent-space packed normal, UV0 plus ST and `_NormalMapScale` |
| `_NormalMapDetail` | Detail packed normal, independent ST and `_DetailNormalMapScale`; combined with the base normal |
| `_NormalMask` | Face art/lighting adjustment data, not a normal texture |

In local V+ `GetShadowAttenuation`, original NormalMask G weights a view-facing
normal expression through `_FaceNormalG`, but the resulting `viewNorm` is not
used by its final Lambert evaluation. Do not advertise that dead expression as
working face-normal smoothing. Original B, through `_FaceShadowG`, participates
in a shadow-attenuation floor; this can weaken external physical shadows.

SkinX should keep that legacy input identifiable but not silently port the
shadow floor into the X visibility core. An explicitly authored diffuse-normal
softening control is a different, testable feature. No current evidence permits
calling NormalMask R/A thickness, subsurface color or another useful spare map.

## Clothing Coverage

`_AlphaMask` R/G are clothing/body-hiding channels. The ordinary SkinPlus rule is:

```text
a = max(1 - _alpha_a, AlphaMask.r)
b = max(1 - _alpha_b, AlphaMask.g)
visibility = min(a, b)
clip(visibility - 0.5)
```

At the usual binary values, zero disables a channel and one enables it. Both
enabled means either mask can hide the pixel. This differs from MainOpaqueX's
ordinary MainTex-alpha times AlphaMask-R convention. AlphaMask has its own ST.
Other channels are not available for repacking without a new explicit contract.

Keep clothing clipping identical in Base, Add, Outline and ShadowCaster. MainTex
alpha clipping, if offered, must be a separate opt-in. An opaque cutout body does
not need alpha blending. Plugin-side mask marker conversion happens before the
shader; do not interpret authoring marker colors a second time in SkinX.

## Liquid System

Three texture roles must be kept separate:

| Input | Role | Coordinates |
| --- | --- | --- |
| `_liquidmask` | Encodes five anatomical/control regions in RGB; A unused by this reference | UV0 plus its own ST, no liquid tiling |
| `_Texture2` | R/G hold two stages of a liquid coverage pattern; B/A unused here | Liquid UV, then Texture2 ST |
| `_Texture3` | Packed tangent-space liquid normal | Liquid UV, then Texture3 ST |
| `_LiquidTiling` | XY offset, ZW scale; not ordinary texture ST ordering | `liquidUV = uv0 * zw + xy` |

For sampled region channels `R,G,B`, `KKPCoom.GetCumVals` derives:

```text
frontTop    = R - max(G, B)
frontBottom = G - max(R, B)
backTop     = B - max(R, G)
backBottom  = (min(R, G) - 0.1) / 0.9
face        = (min(G, B) - 0.1) / 0.9
```

These correspond respectively to `_liquidftop`, `_liquidfbot`, `_liquidbtop`,
`_liquidbbot`, `_liquidface`. Region labels describe the game's control mapping,
not a guarantee about the contents of every custom UV atlas.

For each control `s` in 0..2 and sampled pattern `T = Texture2`:

```text
pattern(s) = max(saturate(s) * T.r, saturate(s - 1) * T.g)
coverage = max_over_regions(min(region, pattern(regionControl)))
```

At zero there is no coverage; at one the R pattern is present; at two the union
of R and G is present. This is not a crossfade replacing R with G. The local
reference's intermediate regions can be negative; saturating final coverage is
a proposed SkinX safety measure, not an exact quote of the reference code.

V+ uses the result to blend liquid normal and a pale liquid appearance with its
lighting. This is a patterned surface overlay, not simulated fluid geometry,
transparency, volume absorption or refraction. A liquid color layer and a clear
wet sheen are separate material operations even if they share coverage.

## Game Setter Evidence

`ChaShader..cctor` registers the legacy property-name IDs. Inspection of the
following `ChaControl` methods confirms that renaming these inputs would break
game-driven updates unless a separate runtime adapter translated every write:

| Game method | Relevant assignment |
| --- | --- |
| `UpdateSiru` | Five `_liquid*` amount controls |
| `ChangeAlphaMask` | `_alpha_a`, `_alpha_b` |
| `ChangeSettingBodyDetail`, `ChangeSettingFaceDetail` | Detail/line texture setup, including `_NormalMapDetail` and `_LineMask` in the material-draw path |
| `ChangeSettingEyeShadow` and color setter | `_overtex3`, `_overcolor3` |
| Lip/nipple/underhair setting methods | `_overtex1/2` and corresponding colors |
| `ChangeHohoAkaRate` | Face `_overcolor2.a`, normally `lerp(0,0.2,hohoAkaRate)`; settings can force zero |
| `ChangeSettingCheekGlossPower` | Face `_SpecularPower` from cheek gloss |
| `ChangeSettingLipGlossPower` | Face `_SpecularPowerNail` from lip gloss |
| `ChangeSettingSkinGlossPower` | Body `_SpecularPower = lerp(skinGlossPower,1,skinTuyaRate)` |
| `ChangeSettingNailGlossPower` | Body `_SpecularPowerNail` from nail gloss |

These establish interface semantics, not a required reproduction of every V+
highlight formula. The X lighting model is intentionally different.

### Same Name, Different Composition Stage

`CreateBodyTexture` / `CreateFaceTexture` also feed `CustomTextureCreate` and its
material. In that composition path, `_Texture3` is a paint source, not the final
skin's liquid normal. Body uses `_Texture6` for a second paint layer; face uses
`_Texture7`. Other source slots cover sunburn, cheek makeup, lip line and moles.
Skin main/sub colors and nail color are also fed into composition.

The resulting MainTex can therefore contain color customization, paint, tan or
makeup already. Dynamic face blush through `_overtex2` is distinct from baked
cheek makeup. Do not reproduce the whole game texture compositor inside SkinX,
or infer the final material's texture meaning from a compositor property name.

## Import And Fallback Policy For Implementation

- Main color and color overlays are color data. Newly authored masks are linear
  data, and normal textures use Unity normal import/decoding. Inspect existing
  runtime bindings before changing any legacy asset's import settings.
- No bulk reimport or edits to V+/lilToon assets are authorized by this audit.
- DetailMask white is not neutral: it would enable drawn shadow and suppress
  outlines. An explicit neutral data asset such as RGBA `(1,0,0,1)` is a candidate
  for stationary specular gating, with drawn highlights separately disabled.
- LineMask zero, AlphaMask white, zero liquid amounts and transparent overlays
  provide useful neutral cases. Missing normals resolve to flat normals.
- Create any new default textures/materials and import settings through Unity
  APIs/CodexBridge during implementation. Do not hand-edit native assets or meta.
- Test decoded channels and effective UVs before judging full lit pictures; dark
  regions in a beauty render cannot identify which contract has failed.
