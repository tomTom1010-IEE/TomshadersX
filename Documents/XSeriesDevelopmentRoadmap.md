# X Series Three-Stage Development Roadmap

## Independent Project Boundary (2026-10-02)

The X series now lives in Assets/Mods/TomShadersX with its own Git repository,
manifest GUID tom.Shaders.X and bundle chara/tom/shaders/tomx.unity3d.
V+ LTS retains its original identity, shaders, assets and independent common code.
All stages below describe this X project, not new features for V+.
See ProjectSeparation.md for migration evidence and packaging compatibility.

## Current Closing Checkpoint (2026-10-04)

The requested code, Unity and ME-metadata audit is complete for the eight-shader
0.3.1 package. All ten static suites and 1,132 existing Unity assertions pass;
the 96-shot standard-sphere showcase adds 109 passing checks. Only metadata/help,
documentation and Editor test/export tooling were changed in this audit, not
production lighting algorithms. See [closing evidence](XSeriesClosingAudit.md)
and the [Chinese user manual](TomShadersX-UserManual.zh-CN.md).

The approved shared indirect diffuse default is 0.25; saved material overrides
are preserved. EyeX multi-light optimization, conditional POM, HairX dual lobes
and the EyeWX role split remain explicit follow-ups. No new in-game audit or GPU
performance run was requested or performed. Earlier implementation checkpoints
below retain their historical scope and do not override this closing record.

## Earlier Planning Checkpoint (2026-10-04)

EyeWX/EyeX v1 features and scoped non-performance KKS game acceptance are closed.
The local Editor GPU baseline is complete; full-game/device certification remains
separate. The user explicitly retains current effects and defers EyeX multi-light
optimization: it is not a blocker for current closure or the next design task.

SkinX now has an experimental implementation. The single `tom/SkinX` entry combines
KKS body/face texture compatibility with the established X light/probe/coat core
and restrained skin-specific diffuse shaping. See
[SkinX design](XSeriesSkinDesign.md) and
[KKS skin texture contract audit](XSeriesSkinTextureContract.md).
The user approved implementation; package 0.3.0 adds its carrier, 167 registered
properties and neutral default data. See [SkinX contract](XSeriesSkinContract.md)
and [implementation evidence](XSeriesSkinProgress.md). Editor fixtures do not
replace actual KKS face/body acceptance or whole-character GPU qualification.

## Stage Three Hair Feature Baseline (2026-10-03)

Added one public tom/HairX. The X lighting core is reused through TOM_HAIR hooks:
cutout cards, continuous tangent/flow-map strand direction, anisotropic GGX-shaped
Toon highlights, full-shadow additional lights, indirect lighting, MatCap, Rim and
Outline. Hard HairFront reads stencil 2 with default depth writing and consistent
Base/Add coverage. Its camera opacity never cuts the light-space shadow.

Optional inward feather has a separate camera provider and experimental KKS
BepInEx adapter. It replays registered eye writers into a binary proxy; it does
not copy camera color or claim exact occlusion-aware stencil reconstruction.
Off/Hard remove auxiliary camera work. The unified shader still submits both
complementary stencil pass groups; final single-shader performance acceptance
requires KKS GPU profiling, not just compilation or CPU Camera.Render timings.

Material/prefab carrier binding and 110 manifest/tooltips properties are complete.
HairFront feather now has independent width, midpoint and power controls; the
default midpoint 0.5 / power 1 retains the original smoothstep without new passes.
Adapter 0.2.0 supports Maker-style partial viewports and optional projected
world-unit feather width, retaining Pixels as the legacy default. Bridge report
`XHairWidth-20261003-094651` passes 198 checks across 208 images (152 camera captures,
56 diagnostics), including distance/FOV/resolution/viewport changes,
Base/Add/Outline consistency and overlapping eye-writer unions. All 96 legacy
HairX regression PNGs remain byte-identical in XHair-20261003-090409. The user
resolved the internal-eye artifact by using EyeWPlus for sclera at queue 2472;
no feather formula change is required. Broader game acceptance and GPU profiling
remain open; see the progress record for deployment requirements.
Bridge evidence and remaining checks are recorded in XSeriesHairProgress.md;
[HairX contract](XSeriesHairContract.md) defines the defaults and optional module.
The current feature scope is closed and 110 property declarations are snapshotted;
see [HairX closure](XSeriesHairClosure.md). V+ and the KK port are unchanged.
Shared Clearcoat and EyeWX/EyeX now have an experimental implementation and Editor
render coverage; SkinX is also implemented experimentally. Stage Three game/device
release hardening remains; the whole stage is not complete.

### Clearcoat Then EyeWX And EyeX Implementation (2026-10-03)

Step one extends the shared baseline with an optional Clearcoat module on all
five current X shaders. Append common properties without changing old meanings;
default zero, independent roughness/normal/IOR and explicit layer weighting.
Verify disabled image equivalence, Base/Add ownership, Alpha/Hair coverage and
resource/GPU cost. Disabled-image equivalence and Editor pixel checks now pass;
representative in-game GPU cost remains open. This is an additive extension,
not a retroactive rewrite of Stage Two or HairX v1 acceptance.

Step two adds EyeWX for eye-area cutout materials and EyeX for iris/pupil art and
local optics. Both reuse the shared coat, not an eye-only corneal reflection fork.
Establish KKS texture/UV aliases and shared actual/proxy StencilMask coverage,
then bounded local analytic refraction and optional RGB dispersion. EyeWX has one
default configuration; no mesh-role classification or separate automatic sclera,
eyeline or eyebrow presets. Settings and queue overrides remain explicit.

Coat and optics are independent and default off. EyeX uses the shared interface
normal; since 0.2.1 its refraction IOR is independent of coat IOR. Iris depth/UV
mapping remain eye-local. Dispersion is opt-in.
Use the user's continuous, sphere-like iris UV layout as the primary target.
Do not add GrabPass, camera-depth dependencies or POM as baseline requirements.
Additional corneal/tear meshes are excluded from this shader-only plan.
Seven public entries, coat metadata, both eye carrier assets and adapter 0.3.0
writer registration are implemented. The original 98/110-property snapshots stay
unchanged; ten optional coat properties are appended. See
[rendered implementation evidence](XSeriesCoatEyeProgress.md) for release limitations.

See [shared Clearcoat design and C0-C3 gates](XSeriesClearcoatDesign.md),
[eye design and E0-E3 gates](XSeriesEyeDesign.md) and
[algorithm research and sources](XSeriesEyeOpticsResearch.md).

### HairX Enhancement Priorities (2026-10-03)

Retain four future enhancements: dual specular lobes, internal hair scattering,
backlight transmission and anisotropic Probe IBL. **The user prioritizes dual
specular lobes above the other three for their Toon styling value.** A narrow
primary highlight and independently shaped/tinted/shifted secondary sheen are
the leading design direction; existing single-lobe SpecularBands does not fulfill
this requirement. Future secondary-lobe defaults must preserve the current look,
reuse strand direction and per-light shadow/visibility rules, and have measured
disabled/enabled cost. Exact controls remain subject to design and validation.

The other enhancements remain deferred: transmission as a distinct thin-hair
backlight response, anisotropic IBL as an optional improvement to the current
isotropic probe approximation, and internal scattering as longer-term research.
Do not weaken reflection horizon guards to implement transmission or allow the
new layers to wash out the Toon body colors.

These are not implemented features or new HairX v1 closure requirements. Finish
the existing game/geometry/performance acceptance and proceed with SkinX/EyeX
without making this backlog a prerequisite. See the detailed scope and candidate
acceptance criteria in [HairX enhancement backlog](XSeriesHairProgress.md#enhancement-backlog-2026-10-03).

## Stage Three Alpha Consolidation (2026-10-03)

The two primary transparent entries are MainAlphaX (optional DepthPrepass)
and MainAlphaXBackFront (two color layers). MainAlphaX2Pass is retained only
for compatibility with existing materials; its historical name is not reused
for the back/front implementation. No carrier assets or GUIDs are migrated.
MainAlphaX defaults stay ordinary alpha. DepthPrepass=1 with
AlphaOptionZWrite=0 reproduces the legacy depth strategy. The shared depth
implementation is TomAlphaDepth.cginc; light and material libraries are unchanged.
Unity Bridge passed 161 merged/compatibility checks and ten cross-mesh checks
at queues 2450/2451/2452. All 142 pre-edit images and 39 corresponding
legacy-versus-merged depth captures are byte-identical. The five cross-mesh
comparison regions also match. Native production assets remain bound as before;
bundle rebuild, game acceptance and performance profiling are still pending.
See [the current controls and acceptance scope](XSeriesAlphaContract.md).

## Stage Three Back/Front Comparison (2026-10-03)

Added tom/MainAlphaXBackFront alongside the unchanged Alpha and depth-2Pass
variants. It draws back color plus additional lights before front color plus
additional lights, with continuous coverage and independent face depth writes.
It consumes the same frozen lighting core; there is no dither, screen-color
sampling, new absorption model or automatic triangle sorting.
Unity Bridge passed 114 scoped pixel checks, including differently colored
layers and separate point/spot contributions against sorted reference renderers.
The new carrier assets are bound to the independent X bundle. User comparisons
on folded/translucent KKS clothing and performance acceptance remain pending.
See [three-strategy comparison](XSeriesBackFrontComparison.md).

## Stage Three Alpha Baseline (2026-10-02)

MainAlphaX and MainAlphaX2Pass now share the frozen Stage Two lighting core.
Coverage, global cutoff, coupled depth/shadow cutoff, straight/premultiplied
output and the nearest-surface prepass strategy are implemented for KKS only.
The two shaders passed scoped Unity Bridge compile and pixel tests. Opaque
regression images match the accepted Stage Two baseline.
See [Alpha contract, evidence and remaining acceptance](XSeriesAlphaContract.md).
The two Alpha materials and shader-carrier prefabs are now bound through CodexBridge.
Game visual acceptance, AssetBundle build, post-processing compatibility and
production profiling are still pending; Stage Three is not complete.

## Stage Two Accepted (2026-10-02)

The user accepted reference-scene and packaged KKS appearance. Stage Two is now
closed as the versioned Toon material baseline. Follow-up CodexBridge rendering
passed the scoped regression checks without modifying shader appearance.
See [closure evidence and remaining hardening work](XSeriesStageTwoClosure.md).
The 98-property interface and texture-channel contract are frozen. The next
development target is MainAlphaX, not additional opaque material features.
Production GPU profiling and untested edge cases remain explicit Stage Three
release-hardening work; closure does not retroactively pass every Stage One gate.

## Stage Two Working Baseline (2026-09-20)

The KKS MainOpaqueX working implementation now targets stable Toon body color and
independently controllable material highlights. Stage One descriptions below record
the historical foundation and its original acceptance baseline; they are not a claim
that removed prototype controls still exist.

- Optional DiffuseEnergyBlend replaces implicit Fresnel suppression at the default.
- SpecularToonBlend selects continuous GGX or GGX-shaped Toon highlights. The old
  GGXSpecularBlend/Legacy Blinn and SpecularPower controls are removed.
- Environment reflection has separate selection, softness, exposure, hue influence,
  tint and Fresnel controls. Roughness remains part of the material contract.
- Procedural and secondary direct tone transfer, indirect tone shaping, simplified
  Rim, diffuse-only MatCap Multiply and emission base-color tint are implemented.
- MatCap Add remains Base-only. MatCap Multiply now also runs conditionally in Add
  so all diffuse lights receive the same tint; this supersedes the old Add cost rule.
- Outline adds a width mask and optional UV4 object-space smooth normals.
- 98 shader/manifest properties have scoped tooltips. Reference scenes are generated
  through CodexBridge without rendering or changing existing user materials.

See [Stage Two progress](XSeriesStageTwoProgress.md) for implementation evidence and
[material contract and user tests](XSeriesStageTwoAcceptance.md) for composition,
migration, texture ownership, limitations and the pending visual acceptance gate.
Stage Two code does not imply Stage One's manual gates have retrospectively passed.

## 1. Project Definition

The X series is a new shader architecture informed by the practical experience of the xukmi/V+ shaders. It is not intended to reproduce every V+ option or every historical shader variant.

The target is a Toon-first hybrid material system that:

- Preserves the clear light and shadow grouping of the existing Koikatsu toon style.
- Uses modern real-time material principles for specular response, reflections, indirect lighting, and energy handling.
- Responds consistently to directional, point, and spot lights, including real-time shadows.
- Uses the useful capabilities of Unity 2019.4.9f1 Built-in Forward rendering.
- Remains understandable, modular, and economical enough to maintain as a new shader family.

KKS with Unity 2019.4.9f1 is the primary design target. KK/Unity 5.6 compatibility is a best-effort backport target and must not prevent the 2019.4 implementation from using appropriate engine features.

## 2. Core Design Rules

1. Toon shading controls the large-scale light and shadow composition.
2. PBR principles control material response, especially specular, Fresnel, roughness, metallic response, and probe reflections.
3. Physical visibility and artistic shadow shaping must remain separate.
4. MatCap, rim, outline, and similar effects are optional art-direction layers, not substitutes for scene lighting.
5. A new shader variant is created only when its render state or material model is genuinely different.
6. Reflection is a material module. The X series does not need separate `Reflect` variants.
7. Studio lighting must use the same material model. The X series does not need separate `Studio` variants.
8. Tessellation and displacement are special-purpose features, not mandatory variants of every shader.
9. Old behavior may be removed or redesigned when it is difficult to explain, scene-dependent, or incompatible with the new lighting model.
10. Feature count is not a measure of modernity. Predictable behavior and coherent composition take priority.

## 3. Current Baseline

The current prototype is `tom/MainOpaqueX`, supported by:

- `Shaders/Tom/TomToonInput.cginc`
- `Shaders/Tom/TomToonLighting.cginc`

The prototype already demonstrates:

- `TomMaterialData`, `TomShadowData`, `TomRawLightData`, `TomSurfaceLightTerms`, and `TomLightingResult` separation.
- ForwardBase, ForwardAdd with full shadows, ShadowCaster, and outline passes.
- Toon diffuse ramp and adjustable shadow shaping.
- Production GGX direct specular with an experimental Legacy Blinn compatibility blend.
- Metallic, roughness, IOR/F0, Fresnel, and specular masks.
- Geometric-normal and shading-normal horizon restrictions.
- SH indirect diffuse, Reflection Probe blending, box projection, roughness mip, and AO.
- Base-pass-only MatCap, probe reflection, rim, and emission.

This is an architecture prototype, not the final material model. Existing implementation is evidence that the pipeline works, but it may be rewritten during Stage One and Stage Two.

---

## Stage One: Modern Built-in Lighting Foundation

### Goal

Complete a correct, efficient, scene-aware lighting foundation for `tom/MainOpaqueX`. At the end of this stage, the shader should be a reliable general opaque/cutout shader even before advanced stylization controls are added.

### Required Work

- Preserve the layered material, raw-light, surface-light, and lighting-result architecture, refining the fields where necessary.
- Separate distance attenuation, cookie attenuation, real-time shadow visibility, NdotL, and artistic shadow shaping.
- Remove attenuation reconstruction based on dividing combined attenuation by shadow attenuation.
- Keep main-light and additional-light evaluation on the same material model.
- Support ForwardAdd full shadows without repeating IBL, MatCap, rim, emission, or indirect lighting for every additional light.
- Build pass-specific material sampling so ForwardAdd only reads textures required for direct diffuse and specular.
- Use GGX as the stable direct-specular foundation.
- Complete energy-conserving diffuse/specular interaction.
- Replace the simplified environment term with an appropriate pre-integrated or Unity-compatible environment BRDF approximation.
- Support SH Light Probes and Light Probe Proxy Volumes where available.
- Support baked lightmaps, directional lightmaps, Mixed Lighting, and Shadowmask variants relevant to Built-in Forward.
- Support Reflection Probe HDR decode, blending, skybox blending, box projection, and roughness mip selection.
- Apply AO to indirect diffuse and indirect specular without incorrectly darkening direct lighting.
- Add Built-in fog support.
- Add GPU instancing support where compatible with the target renderer.
- Define explicit single-sided and double-sided normal policies.
- Keep Forward, Outline, and ShadowCaster alpha cutoff behavior consistent.
- Review ShadowCaster culling and offset behavior for thin and double-sided geometry.

### Current Status

- KKS foundation code for `tom/MainOpaqueX` is implemented. Stage One remains open until the manual scene and performance gates below are accepted.
- The final static audit found no required Stage One source item intentionally left unimplemented inside KKShadersPlus. Further Stage One code changes should be driven by a failed acceptance test; the placeable SH provider remains an external probe-track dependency.
- `TomMaterialData`, `TomShadowData`, `TomRawLightData`, and `TomSurfaceLightTerms` now keep material response, engine shadow visibility, raw light input, and material-dependent surface interpretation separate.
- Distance attenuation and cookie attenuation are sampled independently for directional, point, spot, point-cookie, and directional-cookie variants. The old combined-attenuation division has been removed.
- ForwardAdd uses a direct-only material path and no longer samples AO, reflection mask, emission, MatCap, or Reflection Probes.
- Built-in GI now covers SH, LPPV, static and dynamic lightmaps, directional lightmaps, Mixed Lighting, and Shadowmask variants generated by Unity's Forward pragmas.
- Static Subtractive lightmaps own the baked main-light direct term, so the custom realtime main-light evaluator is disabled for that variant. Realtime, Shadowmask, and Distance Shadowmask paths retain realtime direct lighting and their corresponding shadow visibility.
- Vertex lights are a separate geometry-normal diffuse fill rather than part of SH or indirect GI. They do not use normal maps, Toon Ramp, GGX/specular, cookies, realtime shadows, AO, indirect-light intensity, or main-light shadow tint, and have a dedicated low default intensity.
- Reflection Probe IBL now uses Unity 2019's HDR decode, roughness mip mapping, two-probe blending, box projection, and Unity-compatible environment BRDF response.
- Material AO affects indirect diffuse and indirect specular only. Direct light remains independent of AO.
- `_GGXSpecularBlend = 1` is the production direct-specular path and uses Unity 2019's GGX distribution, Smith joint visibility, Schlick Fresnel, and energy-conserving diffuse suppression. `0` retains Legacy Blinn for experimental compatibility; intermediate values are not guaranteed to conserve energy.
- Built-in fog, GPU instancing, stereo plumbing, and explicit back-face normal/TBN correction are connected to the relevant passes.
- Forward, Outline, and ShadowCaster use the same `_MainTex.a * _AlphaMask.r` cutoff contract and the same front/back-face convention. ShadowCaster uses manual `VFACE` culling so each material keeps its own cull state on Unity 2019.4.
- Single-sided shadow casters use Unity Normal Bias along the rendered side (`+normal` for Cull Back, `-normal` for Cull Front). Cull Off casts both sides without normal offset and relies on Unity depth bias plus optional material raster offset, which defaults to zero.
- Outline expansion uses normalized world-space normals and manual face selection so odd/even negative scale does not introduce a separate outline-facing convention.
- The KKS receiver can optionally consume runtime-captured local L2 SH volumes supplied by a placeable probe provider. `tom/MainOpaqueX` blends this custom diffuse volume at the material level while preserving native lightmaps, direct lights, ForwardAdd shadows, and Reflection Probe specular.
- The placeable probe items are delivered by the independent `tom.lightingprobes` Studio item mod. KKShadersPlus retains only the X-series receiving shader code and MaterialEditor declarations.
- Dated implementation and test results are tracked in [XSeriesStageOneProgress.md](XSeriesStageOneProgress.md).

### Implementation Audit

| Workstream | Implementation evidence | Current state |
| --- | --- | --- |
| Material and light data separation | `TomMaterialData`, `TomShadowData`, `TomRawLightData`, `TomSurfaceLightTerms`, and `TomLightingResult` | Implemented and import-verified |
| Main and additional direct lights | Shared `TomBuildRawLightData`, `TomBuildSurfaceLightTerms`, and `TomEvaluateDirectLight`; `multi_compile_fwdadd_fullshadows` | Implemented; scene gate open |
| Distance, cookie, and shadow ownership | Independent attenuation fields; no division of combined attenuation by shadow | Implemented; scene gate open |
| ForwardAdd material path | Seven direct material samples plus one ramp sample; no Base-only layers | Static audit passed; GPU gate open |
| Production direct specular | Unity 2019 GGX NDF, Smith joint visibility, Schlick Fresnel, and safe half-vector handling | Implemented; grazing-angle gate open |
| Energy interaction | GGX endpoint suppresses diffuse with Fresnel and metallic response | Implemented; material gate open |
| Indirect diffuse and specular | Unity GI, SH/LPPV receiver, lightmaps, Unity-compatible probe BRDF, and indirect-only AO | Implemented; scene gate open |
| Mixed Lighting ownership | Subtractive baked direct ownership; realtime direct retained for Realtime and Shadowmask paths | Implemented; bake matrix open |
| Vertex lights | Geometry-normal diffuse fill with dedicated intensity and no specular, shadow, cookie, ramp, or AO | Implemented; demotion test open |
| Reflection Probes | HDR decode, roughness mip, blending, box projection, and sky fallback through Unity GI | Implemented; probe scene gate open |
| Fog, instancing, and stereo | Pass pragmas and transfer data connected | Import-verified; Frame Debugger gate open |
| Face, cutoff, outline, and shadow policy | Shared `VFACE` convention, manual ShadowCaster culling, zero double-sided normal offset, common alpha clip | Implemented; thin/negative-scale gate open |
| Placeable SH volume provider | Receiver remains in this shader; authoring and runtime provider live in `tom.lightingprobes` | External probe-track evidence required |

### Automated Verification Status

- Unity 2019.4.9f1 completed a forced AssetDatabase refresh through CodexBridge with no final `tom/MainOpaqueX`, `TomToonLighting`, or `TomToonBRDF` shader error or warning.
- `manifest.xml` parses successfully and all 65 `tom/MainOpaqueX` shader properties match the 65 manifest properties.
- ForwardBase, ForwardAdd, Outline, and ShadowCaster source contracts are present; fixed `Offset 1, 1` is removed.
- Shared `KKPPBRBRDF.cginc` remains unchanged. The production GGX implementation is isolated to the Tom library.
- No visual, bake, Frame Debugger, RenderDoc, or GPU timing acceptance has been recorded yet.

### Controlled Acceptance Setup

- Use KKS Unity 2019.4.9f1 Built-in Forward and the project `Fantastic` quality level: Pixel Light Count 4, four cascades, 150 m shadow distance, and realtime Reflection Probes enabled.
- Use a fixed 1920x1080 Game camera, VSync off, fixed exposure, and no post-processing for image or timing comparisons. The project's `Fantastic` quality entry defaults to VSync 1, so explicitly override it to 0 for timing runs.
- For isolated direct-light tests set Ambient and Indirect Intensity to zero, Vertex Light Intensity to zero, Reflection Mode off, Rim/Emission/Outline off, Toon Ramp off, and `_GGXSpecularBlend = 1`.
- Warm up 120 frames, record 300 frames, and report median plus P95 GPU frame time. Record GPU, driver, resolution, quality level, and visible renderer/light counts.
- Use lossless screenshots or HDR captures. An LDR equality tolerance of `1/255`, silhouette tolerance of one display pixel, and shadow tolerance of two shadow-map texels are the default limits.

### Stage One Acceptance Matrix

| ID | Test | Pass metric |
| --- | --- | --- |
| C01 | Import and property contract | No X-series shader error/warning; XML valid; shader and manifest property sets identical |
| L01 | Directional, point, and spot parity | Each light produces diffuse and GGX specular through the same controls; in an unclipped isolated capture, changing its applicable intensity from 1.0 to 0.5 gives an RGB/luminance ratio of `0.50 +/- 0.03` |
| L02 | Additional-light full shadows | A fully occluded sample loses at least 90% of its isolated ForwardAdd direct contribution; enabling MatCap, IBL, rim, emission, or indirect light changes the isolated Add output by at most `1/255` |
| L03 | Distance and cookies | Point/spot intensity decreases monotonically with distance; outside a spot cone or black cookie the isolated direct delta is at most `1/255` |
| L04 | Geometric horizon | With the light behind the geometric surface, isolated direct diffuse and specular deltas are at most `1/255` |
| V01 | Vertex-light demotion | Lights beyond Pixel Light Count add no ForwardAdd draw, GGX, cookie, or realtime shadow; `_VertexLightIntensity = 0` removes their delta within `1/255` |
| G01 | Lightmap and Mixed Lighting | Realtime, Baked Indirect, Subtractive, Shadowmask, and Distance Shadowmask contain one direct-light owner; fade/transition luminance discontinuity stays below 5% |
| G02 | AO ownership | Black AO changes isolated direct light by at most `1/255`; indirect diffuse and probe specular darken monotonically with AO strength |
| G03 | SH/LPPV receiver | Dynamic objects follow spatial probe changes without direct-light duplication; evidence may be imported from the probe-track project |
| R01 | Reflection Probe matrix | No-probe/sky, one probe, two-probe blend, and box projection remain finite; 20 equal movement steps through a blend region have less than 5% adjacent-sample luminance jumps unless crossing a deliberate hard boundary |
| F01 | Fog composition | Base, Add, and Outline converge at the same depth without colored additive halos; visible edge mismatch is at most one pixel |
| I01 | GPU instancing | Repeated identical mesh/material instances form one instanced draw per applicable pass in Frame Debugger and match non-instanced output within `1/255` |
| S01 | Thin and double-sided shadows | Quad, thin shell, and hair card obey Cull Back/Front/Off; Cull Off casts both sides without normal-bias separation or discontinuous gaps |
| S02 | Negative-scale consistency | `(1,1,1)`, `(-1,1,1)`, and `(-1,-1,1)` retain matching body, Outline, cutoff, and cast-shadow face conventions; screen-space Outline width differs by at most one pixel |
| S03 | Cutoff and offset | Body/Outline edge mismatch is at most one display pixel and body/shadow mismatch at most two shadow texels; default material raster Offset is zero |
| B01 | Production GGX stability | Roughness `0.04/0.1/0.5/1`, Metallic `0/1`, IOR `1/1.5/2.5`, and views through 89 degrees produce no NaN, Inf, full-surface flash, or back-lit highlight |
| B02 | GGX response and energy | As roughness rises, peak decreases and highlight width increases; with Metallic 1 and a black Specular Mask, isolated diffuse is at most `1/255` |
| B03 | Legacy compatibility | Blend 1 is the acceptance path, Blend 0 remains finite Legacy Blinn, and intermediate values remain finite but have no energy-conservation requirement |
| P01 | Draw and sample contract | Base has one ForwardBase draw; each pixel additional light adds one ForwardAdd draw per affected renderer; a shadowed point light may add six shadow faces; ForwardAdd keeps 7 material plus 1 ramp sample before light textures |
| P02 | Relative GPU cost | Repeated lights with the same type, screen coverage, cookie state, and shadow state have incremental median costs within 20% of each other; P95/median stays below 1.25; Base-only feature toggles change measured ForwardAdd pass time by no more than 5% |

### ForwardAdd Cost Baseline

- Base material path: 10 common material samples (`MainTex`, `AlphaMask`, two normals, metallic, roughness, specular mask, AO, reflection mask, and emission mask) plus one ramp sample. MatCap and one/two Reflection Probe samples are conditional Base-only work.
- Direct material path: 7 material samples (`MainTex`, `AlphaMask`, main normal, detail normal, metallic, roughness, and specular mask).
- Toon transfer: 1 ramp sample.
- Light visibility: Unity shadow sampling plus zero or more attenuation/cookie samples according to light type.
- Explicitly absent from ForwardAdd: occlusion map, reflection mask, emission mask, MatCap, Reflection Probe IBL, rim, indirect diffuse, and emission composition.
- Static sample ownership is verified. Compiled instruction counts and GPU timings remain part of P01/P02; compile-time feature variants and optional texture packing remain Stage Two optimization work.

### Non-Goals

- Do not restore V+ color masks, liquid, KKP rim, or specialized character behavior during this stage.
- Do not add transparent rendering during this stage.
- Do not create Reflect, Studio, or Tess combinations.
- Do not implement a custom render pipeline or replace Unity's shadow-map system.

### Exit Criteria

- Code gate: passed when C01 remains green.
- Lighting and material gate: L01-L04, V01, B01-B03 pass.
- GI and integration gate: G01-G03, R01, F01, and I01 pass. G03 may use evidence produced by the independent probe track.
- Geometry and shadow gate: S01-S03 pass.
- Performance gate: P01 and P02 are recorded with reproducible environment details and pass.
- Stage One is accepted only after every gate above passes or an explicit exception is documented in the progress record. Compilation alone does not close the stage.

### Stage Deliverable

A stable `tom/MainOpaqueX` lighting reference that can serve as the shared foundation for later X-series shaders.

---

## Stage Two: Toon Art Direction and Hybrid Material Model

### Goal

Define the final X-series visual identity: xukmi-inspired toon composition with modern, physically coherent material response. This stage decides which artistic controls are worth keeping and how they interact.

### Required Work

- Redesign the primary toon ramp as a documented light-transfer function.
- Keep real-time shadow visibility separate from ramp evaluation, with an explicit option to feed shadow visibility into the toon shaping stage.
- Replace the current Blinn/GGX blend with GGX-based stylized specular shaping.
- Add specular threshold, softness, banding, and intensity controls after GGX evaluation rather than maintaining two unrelated BRDFs.
- Define stable metallic, roughness, IOR, Fresnel, and energy-conservation semantics.
- Redesign `Another Ramp` as an optional Secondary Tone Ramp or regional ramp blend.
- Give the secondary ramp a clear strength and mask; do not preserve unexplained Fresnel coupling from old shaders.
- Replace KKP Rim with a compact Fresnel-based stylized rim.
- Give rim an explicit mask, shadow influence, and optional light-direction influence.
- Keep MatCap as an optional Base Pass art layer.
- Support clear MatCap Add, Multiply, and optionally Overlay semantics.
- Allow MatCap to choose geometric normal, main normal, or combined detail normal where artistically useful.
- Avoid MatCap and environment texture sampling when those features are disabled, using suitable shader variants or keywords.
- Improve the inverted-hull outline with stable screen/object-space width, depth offset, width mask, and optional smoothed-normal input.
- Finalize emission behavior, including whether a simple Keep Base Color mode is useful in the new composition.
- Define the production texture layout and channel packing.
- Define defaults that produce a usable toon material without assigning optional textures.
- Organize MaterialEditor categories and tooltips around the new semantics rather than V+ terminology.

### Feature Decisions

- `KKP Rim`: remove.
- Old `Another Ramp`: replace with a documented secondary tone system.
- Separate `Reflect` shaders: remove.
- Separate `Studio` shaders: remove.
- Legacy liquid and overtex controls: exclude from the common material model.
- MatCap: retain as a deliberately non-physical art layer.
- Outline: retain and modernize.
- Clear coat was deferred from Stage Two and subsequently implemented across the seven-entry family in 0.2.0. See `XSeriesCoatEyeProgress.md` for zero-weight compatibility evidence and separate release acceptance.

### Exit Criteria

- The shader remains recognizably toon at default settings.
- Metallic, roughness, and probe reflections add material character without washing out the toon light bands.
- Toon diffuse, stylized GGX, rim, MatCap, reflection, and emission have a documented and deterministic composition order.
- Each visible control has a clear purpose and produces a measurable visual change.
- Disabled features do not continue paying their full texture-sampling cost.
- A small reference material set demonstrates cloth, plastic, skin-like dielectric, painted metal, bare metal, and glossy stylized surfaces.
- The final property names and packed-map channel definitions are frozen for downstream variants.

### Stage Deliverable

The production visual and material specification for the X series, implemented in `tom/MainOpaqueX` and documented with reference materials and test scenes.

---

## Stage Three: Specialized Materials, Integration, and Release Hardening

### Goal

Extend the shared X-series core only where a different render state or material model is genuinely required, then validate the family for practical KKS use.

EyeWX/EyeX v1 feature scope is closed at package 0.2.1; see
`XSeriesEyeClosure.md` for evidence. On 2026-10-04 the user confirmed all scoped
non-performance KKS game acceptance complete; performance qualification is the
only remaining acceptance gate for this eye scope. This does not close unrelated
family hardening or certify Linear, VR/stereo or KK.
The [local GPU baseline](XSeriesGpuBaseline.md) now measures 217 cases and 13,020
samples against local V+ LTS/lilToon. It identifies EyeX multi-light optical work
as an optimization candidate, without changing shipping shaders or certifying
full-character target-game performance.
True cavity self-occlusion is a conditional POM/first-hit research trigger, not
a prerequisite for this eye baseline. The eyebrow/eyeline split stays deferred.

### Planned Shader Family

- `tom/MainOpaqueX`: general opaque and cutout materials.
- `tom/MainAlphaX`: semi-transparent materials with optional DepthPrepass, plus explicit blending, cutoff, fog, and shadow policies.
- `tom/MainAlphaXBackFront`: primary two-color-layer shader, back/front compositing for accumulating coverage; no depth-only prepass or triangle sorting.
- `tom/MainAlphaX2Pass`: legacy depth-prepass compatibility entry only, not another primary development branch or the back/front variant.
- `tom/HairX`: two-sided hair cards, anisotropic specular, stable self-shadow behavior, and optional HairFront compatibility.
- `tom/SkinX`: skin-oriented diffuse wrap or lightweight subsurface approximation, while sharing the common light and probe systems.
- `tom/EyeWX`: sclera and cutout eyeline/brow roles with shared X lighting and explicit Ref 2 coverage.
- `tom/EyeX`: iris/pupil and corneal lighting, KKS eye bindings, explicit Ref 2 coverage and optional local optical effects.
- Special displacement or overlay shaders only when a concrete effect requires them.

### Required Work

- Extract stable common libraries from the completed MainOpaqueX implementation.
- Keep game-specific stencil, render queue, and depth behavior in small adapter modules.
- Design transparent lighting separately rather than enabling transparency with a single blend toggle.
- Give HairX an explicit two-sided normal and two-sided shadow strategy.
- Use anisotropic GGX or another documented hair lobe instead of copying legacy Hair Gloss behavior.
- Give SkinX a restrained skin response rather than copying every SkinPlus body option.
- Keep EyeX focused on eye material behavior and required hair-front interaction.
- Add quality tiers or compile-time feature sets where they provide meaningful performance savings.
- Audit shader variant count and remove unused combinations.
- Profile Base, Add, ShadowCaster, Outline, and transparent passes on representative character scenes.
- Test directional, point, and spot lights with hard and soft shadows.
- Test no-probe, Light Probe, LPPV, Reflection Probe, lightmapped, and mixed-lighting scenes.
- Test alpha cards, mirrored meshes, negative scale, thin geometry, and overlapping hair cards.
- Build visual regression scenes and record expected screenshots/settings.
- Complete MaterialEditor categories, tooltips, example materials, asset bindings, and migration documentation.
- Backport compatible functionality to KK/Unity 5.6 after the KKS implementation is stable.

### Optional Research, Not Release Requirements

- Further coat models beyond the implemented shared Clearcoat extension, such as independently stylized or anisotropic coating; see `XSeriesClearcoatDesign.md` for the first-version design.
- Sheen for cloth.
- Improved skin scattering.
- Transmission or refraction.
- Iridescence.
- Parallax or tessellated displacement.
- Command-buffer-based contact shadows or other pipeline extensions.

These features require a demonstrated use case, acceptable performance, and a composition rule that does not destabilize the core shader.

### Exit Criteria

- Specialized shaders share the same material/light architecture and do not duplicate the full lighting implementation.
- No Reflect, Studio, TessReflect, or similar combinatorial shader explosion is introduced.
- KKS reference assets compile and render without missing properties or invalid passes.
- Performance targets and variant budgets are documented.
- Known Unity Built-in limitations are documented separately from shader defects.
- The X series has a stable public property contract and can begin normal feature maintenance rather than architectural experimentation.

### Stage Deliverable

A release-ready KKS X-series shader family, with KK-compatible subsets where practical, reference assets, documentation, and repeatable validation scenes.

---

## 4. Decision Gate Between Stages

Do not begin the next stage merely because the previous shader compiles.

- Stage One to Stage Two requires correct lighting data flow and acceptable ForwardAdd cost.
- Stage Two to Stage Three requires frozen material semantics and a stable composition order.
- New specialized shaders must consume the shared core instead of forking an unfinished prototype.
- Optional advanced effects must not delay completion of the common opaque, transparent, hair, skin, and eye use cases.

## 5. Definition of Success

The X series succeeds when it provides a recognizable Koikatsu-style toon image while behaving predictably under modern scene lighting. It should be easier to understand than V+, more consistent across light types and environments, and more selective about non-physical features.

Success is not measured by reproducing every historical xukmi option. It is measured by coherent lighting, useful art direction, maintainable code, stable performance, and material behavior that remains understandable across different scenes.
