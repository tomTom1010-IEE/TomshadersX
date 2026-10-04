# SkinX Overall Design

Date: 2026-10-04. Status: approved design, implemented as an experimental baseline.
Target: KKS, Unity 2019.4.9f1, Built-in Forward, initially D3D11.
The plan below records design intent, not a claim that every release gate has
passed. The implemented 167-property entry, defaults, explicit sampling policy and
remaining game checks are in [SkinX contract](XSeriesSkinContract.md) and
[implementation evidence](XSeriesSkinProgress.md).

## Scheduling Decision

The user retains the current EyeWX/EyeX appearance and features. EyeX multi-light
optimization is a follow-up, not a blocker for current closure or SkinX work.
The measured Editor baseline is documented in [GPU baseline](XSeriesGpuBaseline.md);
representative whole-game/device qualification remains distinct. Do not reduce
optical iterations, dispersion or additional-light fidelity without a new scope.

The eyebrow/eyeline versus sclera split remains deferred. This proposal does not
reopen that split, the eye optics design or existing HairX stencil behavior.

## Product Direction

Add one public `tom/SkinX` for face and body. Its purpose is to preserve KKS
character customization and authored skin details while making them participate
in the same coherent Toon lighting, probes and optional Clearcoat as the X family.
It is not a photorealistic head renderer and not a verbatim V+ lighting port.

Three responsibilities remain separate:

1. KKS input adapter: legacy names, texture channels, UVs, vertex color, overlays,
   clothing clipping, regional gloss and liquid controls.
2. X baseline: light ownership, Toon transfer, GGX/specular shaping, shadows,
   GI/probes, MatCap, Rim, emission and the existing Clearcoat layer.
3. Skin response: clean diffuse normals, controlled painted detail, soft/warm
   light transitions and an optional localized wet surface.

No automatic mesh-role presets or detection by renderer/material name. Face and
body can be tuned explicitly on their own materials. KKS special-region data
distinguishes skin from lips/nails without requiring separate public shaders.

## Compatibility Boundary

The [texture contract audit](XSeriesSkinTextureContract.md) is the evidence base.
Preserve data and runtime writes; visual equivalence to V+ is not the goal.

| Input group | Required v1 behavior |
| --- | --- |
| Main/overlays | Preserve the game's composite MainTex, ordered three overlays, UV1/2/3, vertex R/B gates and nipple remapping. Do not apply baked makeup/color twice. |
| DetailMask | R highlight pattern/gate; G painted shade; B old Rim/outline suppression; A regional gloss. Do not reuse it as X MaterialMap. |
| LineMask | Preserve R/G interior-line response and B detail shade, including game detail strength. Not a shell-outline map. |
| Clothing mask | Preserve AlphaMask R/G with `_alpha_a/b` in every coverage pass. MainTex alpha is optional and independent. |
| Normal maps | Preserve base/detail normal controls. Offer a separate diffuse-detail reduction without weakening specular detail. |
| Liquid | Preserve five region controls, Texture2 RG pattern stages, Texture3 packed normal and LiquidTiling ordering. |
| Game gloss | Preserve `_SpecularPower` and `_SpecularPowerNail` writes for body/cheek and nail/lip regions. Keep X roughness and IOR independent. |
| NormalMask/global face art | Keep the input recognizable; do not claim the dead local V+ G expression works, or silently restore a physical-shadow bypass. |

Legacy aliases belong in SkinX only. They do not change the frozen 98 common
properties, ten Clearcoat properties or existing seven public shaders. Preserve
the misspelled `_SpeclarHeight` where the legacy highlight mode consumes it.
If a V+ extension is unsupported, say so in migration notes instead of exposing
a no-op control as compatible. A general existing V+ card is not automatically
pixel-identical after replacing its shader.

## Surface And Pass Layout

Proposed default: opaque color with cutout coverage, `ZWrite On`, backface culling,
queue `AlphaTest-100` (2400), matching the reference skin ordering. Explicit
material queue overrides remain possible. SkinX does not become a hair stencil
writer, and must not clear or overwrite the eyes' existing stencil protocol.

Use ForwardBase, ForwardAdd, ShadowCaster and optional shell Outline. No new
screen texture, depth dependency, GrabPass, render feature or runtime plugin.
Clothing coverage stays active independently of any cosmetic alpha toggle.

Keep one shared coverage function. Default clothing threshold is 0.5; an optional
MainTex-alpha cutout uses the common `_Cutoff` without reinterpreting AlphaMask G.
An opaque body has alpha one after clipping. Transparent skin, internal backface
sealing and tessellation are separate future scopes, not additional v1 variants.

## Skin Color And Painted Detail

Resolve game color layers before X material lighting. `_BaseColor` remains a
neutral-white artistic multiplier by default. Optional V+ `_ColMask` migration
must retain its sequential lerps and remain distinct from game color composition.

Build two explicit detail signals from the audited channels:

- Painted shade from DetailMask G and LineMask B: an artistic diffuse modifier
  or tone-selection bias, with an independent SkinX strength. Do not call it
  measured AO, use it as thickness, or replace light shadow-map visibility.
- Interior line factor from LineMask R/G: a tinted attenuation of diffuse-like
  body terms. It participates consistently in Base and Add, but is not a final
  multiplication of specular, coat, emission and the complete framebuffer.

Preserve `_DetailNormalMapScale` as the game's detail control and normal scale.
Skin-specific line/shade multipliers adjust the legacy linkage without changing
that property's meaning on the other X shaders. Keep a debug view for each
signal to distinguish missing maps, wrong channels and overly strong styling.

The original `_LineColorG` / `_linewidthG` can be explicit line-adapter inputs.
Do not multiply X's whole lighting result by `_ambientshadowG`, or replace the
common ramp with `_RampG` implicitly. X ambient/probe and shadow ownership remain
authoritative; optional game-art influence requires an explicit control.

## Diffuse Normal And Soft Skin Response

Use separate resolved normals:

- `Nspec`: the full normal-map surface, including detail and liquid where enabled.
- `Ndiff`: base normal plus independently reduced micro-detail for stable Toon
  bands. Do not smooth all geometric facial structure away.
- `Ncoat`: the shared Clearcoat source selection, independent of diffuse softness.

Interpolate normal contributions in a consistent tangent-space construction and
renormalize; retain tangent handedness and mirrored-UV handling. This lets pores
or small wrinkles affect highlights without making large skin tones look noisy.
It is not mesh smoothing and cannot repair broken/skipped skinning normals.

For v1, recommend cheap local skin-like diffuse shaping rather than true SSS:

1. Broaden the front-facing light transition using a bounded wrap-style input
   before the common Toon transfer.
2. Apply a restrained warm hue adjustment around that transition, gated by the
   light's visibility and skin mask. Do not add an always-on red glow.
3. Blend with the unchanged X transfer using a zero-default skin strength.

A candidate input is `q=saturate((dot(Ndiff,L)+w)/(1+w))`; final placement relative
to ramp/shadow remapping requires the fixture tests below. A normalized color
tint can preserve approximate luminance while changing transition hue. This is
an artistic approximation, not measured wavelength-dependent light transport.

Integration detail matters: the present X surface terms share one shading normal
and multiply both shading/geometric horizon guards. Merely inserting wrap before
that multiplication will erase much of the intended softening. Add a Skin-only
diffuse surface evaluation using Ndiff, while leaving specular and coat guards
unchanged. V1 retains the geometric front-side guard and does not light the back
of an opaque surface through an occluder. Its geometric silhouette therefore
still fades to zero: this is front-side softness, not ear transmission.

Keep light distance, cookie and physical visibility separate from artistic
diffuse transfer, preserving the existing X shadow controls. There must be no
unconditional `max(shadow,skinMinimum)` or new shadow floor. Soft skin is not an
excuse to make an external blocker ineffective.

Indirect diffuse remains a Base-only SH/probe/LPPV/custom-volume contribution;
it can use Ndiff consistently without an uncalibrated extra skin ambient term.
Any indirect color styling must remain explicit, not copy direct-light warmth
into every environment contribution by default.

## Specular And Game Gloss

Default skin is dielectric (`_Metallic=0`) with moderate substrate roughness.
Reuse common GGX and its stylized highlight controls; no new physical BRDF fork.
Keep authored specular mask, material roughness, specular IOR and environment
response under the established X controls.

The game gloss adapter should use the legacy regional gain from DetailMask A
and `_SpecularPower` / `_SpecularPowerNail`, with an explicit artist master and
an opt-out. This gain should affect the substrate's direct and probe response
consistently. It must not secretly change Clearcoat IOR, enable the coat or alter
skin roughness merely because the game updates `skinTuyaRate`.

The exact gain curve is a proposed compatibility mapping, not physical units.
Use a monotonic tested mapping and preserve region endpoints; the audited legacy
maximum formula is the initial reference. Do not translate `_SpecularPower` into
the old Phong exponent and call it GGX roughness.

Offer an explicit legacy-pattern highlight adapter for DetailMask R, including
the view shift controlled by `_SpeclarHeight`. Distinguish a stationary GGX mask
from the moving drawn-highlight option. `_notusetexspecular` selects whether the
legacy pattern participates; it must not remove all ordinary X lighting. Any
moving-pattern contribution is lit and shadowed per light, not emission repeated
in every Add. Do not blindly stack full-strength V+ and X highlights.

Legacy nail/lip gloss and liquid material response may need separate gains even
with a shared shader. They are region controls, not automatic face/body presets.

## Liquid Color Versus Wet Clearcoat

Keep two independent concepts:

| Layer | Responsibility |
| --- | --- |
| Legacy liquid surface | Five-region Texture2 coverage, optional tinted/pigmented liquid appearance, Texture3 normal, and a localized substrate material adjustment |
| Transparent wet sheen | Existing common Clearcoat reflection and energy weighting, optionally masked by that coverage or a separately authored wetness mask |

At zero liquid amounts, avoid liquid pattern/normal reads when possible. At
zero Clearcoat, keep the common exact-off behavior, even if liquid is present.
Pigmented liquid must still exist with coat disabled; coat alone must not turn
the underlying color white or create a liquid normal automatically.

Propose a Skin-only coat coverage selector: common coat map only, liquid region,
authored wetness, or the union of liquid/wetness. In every case:

```text
effectiveCoat = commonClearCoatWeight * selectedSkinCoverage
```

The common map still controls weight/roughness with its existing channels and ST.
Use one coat lobe and the existing `TomClearcoat.cginc` composition, not an added
second wet-skin lobe. A Skin-only liquid-normal blend may feed the resolved coat
normal when explicitly enabled; do not change the common normal-source enum.

Natural dry/oily skin can use substrate specular without Clearcoat. A stronger
wet surface is an optional second layer. Skin diffuse softness describes a
different phenomenon, so these controls are complementary rather than duplicates.

## Candidate Skin-Only Controls

Names, ranges and defaults below are proposals, not frozen manifest declarations.
Avoid another full lighting panel; use common X controls where they already fit.

| Group | Proposed controls | Initial behavior |
| --- | --- | --- |
| Game compatibility | Audited legacy property aliases, game gloss influence, optional MainTex cutout | Runtime writes honored; MainTex cutout off |
| Detail art | Painted shade strength, interior line strength/tint, legacy specular-pattern mode | Preserve authored data; explicit modes rather than forced V+ final-color math |
| Diffuse normal | Diffuse detail-normal influence | Full detail at neutral setting; lowering affects diffuse only |
| Soft skin | Skin response strength, transition width/wrap, warm tint/amount | Strength zero returns neutral X response |
| Liquid surface | Legacy five amounts, appearance tint/strength, normal/material influence | Five amounts zero; no automatic coat |
| Wet coverage | Coat coverage source, wetness strength, optional liquid coat-normal blend | Common coat map only; Clearcoat still defaults to zero |
| Debug | Resolved UVs/overlays, detail channels, clothing coverage, liquid regions, normals and skin response | Debug off; reuse common lighting/coat debug where possible |

An optional new `_SkinControlMap` could use R for soft-response coverage, G for
warm-transition coverage, B for wetness coverage and A reserved/ignored. Use its
own UV0 ST. It is not required for ordinary skin; a neutral fallback can supply
R/G=1 and B=0. Wetness remains opt-in through a nonzero amount and coat selection.
Do not repack any legacy map or require users to redraw all standard textures.
New aliases and defaults are frozen only after the integration fixtures pass.

## Engineering Integration

Proposed small modules, subject to the final dependency graph:

| Module | Responsibility |
| --- | --- |
| `SkinX.shader` | Properties, states and pass entry points |
| `TomSkinInput.cginc` | Skin-only aliases, records and optional texture declarations |
| `TomSkinCoverage.cginc` | Shared legacy RG clothing clipping and optional MainTex cutoff |
| `TomSkinSurface.cginc` | UV/overlay composition, channel decoding, liquid and gloss adapter |
| `TomSkinLighting.cginc` | Skin-only diffuse normal/transfer and detail-art hooks |
| Existing X core | Actual light/GI/probe/specular/coat calculations and pass ownership |

Use a narrowly scoped `TOM_SKIN` specialization. Do not duplicate the whole
lighting core or import live includes/assets from the V+ or lilToon packages.

Before writing the beauty shader, resolve these concrete integration constraints:

- Current `TomVertexData` lacks UV3 and vertex color. Add conditional Skin inputs
  and packed varyings; preserve all four original UV sets. Keep nonlinear nipple
  remapping in the appropriate sampling path rather than an inaccurate vertex
  approximation. Check compiled interpolator use with shadows/fog/instancing.
- KKS UV1/2 are overlays, not guaranteed lightmap UVs. V1 targets skinned characters
  with probe/SH lighting. Do not silently consume them for static/dynamic lightmaps
  or shadowmask UVs. Static-baked SkinX needs a separately agreed mapping policy.
- X optional UV4 outline normals collide with overlay UV3. Skin v1 uses mesh
  normals for Outline; advertise UV4ObjectSpace as unavailable for this entry,
  not a silently successful option. Future spare-channel support must be explicit.
- Outline width is the common width/mask multiplied by legacy `1-DetailMask.b`.
  Legacy B can additionally restrict Rim; it must not mask coat/probes implicitly.
- Texture/sampler pressure is real: the
  [D3D11 sampler-state limit](https://learn.microsoft.com/en-us/windows/win32/direct3d11/overviews-direct3d-11-resources-limits)
  is 16 per stage.
  Audit compiled variants before freezing features. Share sampler states only
  where filtering/wrap semantics match; a clamped face map must not accidentally
  impose clamp on a tiled liquid normal. Extra textures are not free because they
  live in a different include. Conditional disabled fetches need compiled checks.
- Base owns GI, environment probes, additive MatCap, Rim and emission. Each Add
  owns only that light's diffuse/specular/coat. Multiplicative MatCap and skin
  diffuse art must be applied consistently to their own terms in both passes.
- Never normalize accumulated multi-light HDR energy or insert per-pass saturate
  merely to hide over-bright styling. Existing seven-shader regression fixtures
  must remain unchanged when shared input/normal hooks are extended.

## Staged Implementation And Acceptance

### S0: Contract Fixture

Create Bridge-managed materials/fixtures with independent RGBA ramps, UV0..3
checkerboards, mirrored tangents and vertex-color gates. Establish assertions
for ordered overlays, clothing RG toggles, Detail/Line channels and five liquid
controls before tuning skin appearance. Test real face and body material writes,
including blush, skinTuyaRate and cheek/lip/nail gloss after card reload.

### S1: Dry Skin

Implement one entry on X with the legacy adapter, internal lines, regional gloss,
diffuse-normal control and optional soft/warm skin response. Compare neutral
SkinX against MainOpaqueX only with neutral adapter inputs and matching pass
states; arbitrary V+ material equivalence is not the assertion. Compare actual
face/body art for customization retention and improved multi-light consistency.

### S2: Liquid And Wet Surface

Verify liquid controls at 0/1/2, all region combinations, pattern tiling and normal
ST. Add the coat coverage adapter without changing common coat semantics. Test
dry, pigmented liquid without coat, coat without liquid and both together. Keep
cloth holes and outline behavior identical under all those combinations.

### S3: Regression, Performance And Packaging

Required checks before claiming v1 complete:

1. Base/Add/Outline/ShadowCaster agree on clothing holes; MainTex alpha opt-in
   does not break game masks. Skin writes no new eye/hair stencil state.
2. Face/body overlays retain alignment across UV seams, mirrored islands,
   expression changes, nipple size, blush and makeup combinations.
3. Turning skin response and coat off returns the defined neutral path. Game
   gloss remains regional and monotonic, independent of IOR and diffuse softness.
4. Occluders, shadowed point/spot lights, cookies and colored lights still work;
   soft skin does not cause unshadowed leaks or repeat GI in Add.
5. Probe/LPPV/custom SH and reflection blending remain compatible. Toon body
   color stays legible with strong highlights, and distant detail does not alias.
6. Existing X fixtures and public property snapshots remain unchanged. Check
   sampler/interpolator counts, actual compiled variants and disabled cost.
7. Measure GPU increments on full-body/face coverage, one/many pixel lights and
   one/many characters. Do not extrapolate body cost from small eye-pair proxies.
8. Add manifest/tooltips, Bridge-created carrier assets and a frozen skin contract
   only after these checks; distinguish Editor evidence from game acceptance.

No quality-tier cuts are preselected. Profile adapter, liquid, coat and diffuse
skin contributions separately, then choose optimizations or optional tiers only
when representative frame budgets justify the tradeoff.

## Deferred Features And Research Basis

Cheap wrap/transition coloring is supported as an approximation by
[GPU Gems, Chapter 16](https://developer.nvidia.com/gpugems/gpugems/part-iii-materials/chapter-16-real-time-approximations-subsurface-scattering).
It cannot reproduce spatial diffusion, true thin-part transmission or geometry
self-occlusion. That limitation is appropriate for an initial stylized material.

Texture-space diffusion is described in
[GPU Gems 3, Chapter 14](https://developer.nvidia.com/gpugems/gpugems3/part-iii-rendering/chapter-14-advanced-techniques-realistic-real-time-skin).
It adds irradiance/filtering work and UV requirements; it is not just another
single-pass mask. Do not add screen/texture-space SSS before KKS integration and
full-body profiling demonstrate a need for it.

[Filament's clearcoat model](https://google.github.io/filament/main/filament.html)
provides a useful distinction between a surface reflection layer and the base
material, including attenuation of the underlying response. SkinX reuses the
already implemented Tom coat, not a new port of Filament or a skin-only duplicate.

Further scopes, not v1 requirements:

- Ear/finger transmission: needs explicit thickness data and a defensible
  treatment of self versus external occlusion. Legacy DetailMask is not thickness;
  bypassing a front shadow map is not a correct transmission solution.
- Face SDF: requires authored directional data and a reliable head-local frame;
  arbitrary skinned-renderer object axes cannot be assumed to be head orientation.
- POM/displacement: requires an explicit height contract and a demonstrated visual
  need. EyeX's possible first-visible-hit follow-up is independent of SkinX.
- Separate face/body public entries, transparent body variants, dual substrate
  specular lobes, physically calibrated diffusion and runtime wetness simulation.

S0 through S2 are implemented and covered by Editor fixtures. Continue with the
remaining S3 game/device acceptance described in the progress record. Existing
character data has priority over adding more skin optical effects.
