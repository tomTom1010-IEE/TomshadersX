# Tom Shaders X Development Guide

Independent KKS / Unity 2019.4.9f1 Built-in shader project, separated from the
Vanilla Plus LTS fork. Toon-first materials with controllable GGX highlights,
per-pixel additional lights, full light shadows and probe-aware indirect lighting.

## User Manual And Closing Audit

Read the [Chinese user manual](TomShadersX-UserManual.zh-CN.md) for shader
selection, common ME controls, packed maps, eye depth authoring, Skin texture
contracts and probe refresh. The [2026-10-04 closing audit](XSeriesClosingAudit.md)
records the code/Unity/ME-metadata scope, repaired help text, test evidence and
deferred items. It does not claim a new in-game audit or performance certification.

Bridge `renderxshowcase` produces 96 real GPU sphere captures in 24 feature groups.
Run `Tests/Export-XShowcase.ps1 -ReportDirectory <returned report directory>` with
PowerShell 7 to export the 2048px comparison boards, overview and offline HTML
gallery/manual. Open `Gallery.html` directly. Camera, lights and all material
properties are recorded; demonstration settings do not replace shipped defaults.
Run `Tests/Test-XSeriesMetadata.ps1` after changing any ME registration or tooltip.

## Shader Family

- tom/MainOpaqueX: opaque/cutout baseline.
- tom/MainAlphaX: standard alpha with optional DepthPrepass (default off).
- tom/MainAlphaXBackFront: the primary two-color-layer alpha shader, with back/front coverage accumulation.
- tom/MainAlphaX2Pass: legacy depth-prepass compatibility entry; unchanged depth/alpha defaults, not the back/front shader.
- tom/HairX: unified cutout hair with anisotropic Toon highlights and optional single-layer HairFront. Feather needs the optional runtime provider.
- tom/EyeWX: cutout eye-area surface, explicit stencil coverage, shared lighting and optional coat. One role-independent default.
- tom/EyeX: iris/pupil with game texture aliases, shared coat and optional local refraction/RGB dispersion on the existing mesh.
- tom/SkinX: shared face/body cutout skin, KKS texture layers and clothing masks, regional gloss, optional soft/warm diffuse and localized liquid/wet coat.

For new materials, choose MainAlphaX or MainAlphaXBackFront. On MainAlphaX,
DepthPrepass=1 and AlphaOptionZWrite=0 reproduce the legacy depth-prepass mode.
Both options default to 0; color-pass ZWrite remains independent. Existing
materials/prefabs and shader names are not renamed or automatically migrated.

Install this folder at Assets/Mods/TomShadersX in a KKS Modding Tools Unity project.
Keep the supplied .meta, materials, prefabs and reference assets so bindings survive.
This repository is a mod/source package, not a second complete Unity Editor project.
It does not require the Vanilla Plus source folder to compile or render.

## Packaging

- Manifest GUID: tom.Shaders.X.
- AssetBundle: chara/tom/shaders/tomx.unity3d.
- Bound prefabs: a_TomMainOpaqueX, a_TomMainAlphaX, a_TomMainAlphaX2Pass,
  a_TomMainAlphaXBackFront, a_TomHairX, a_TomEyeWX, a_TomEyeX, a_TomSkinX.
- Material names have the corresponding m_Tom prefix.
- tom_x_tooltips is included in the same bundle.

Build this bundle, then package this folder's manifest separately from Vanilla Plus.
The live shader names, property names, prefab names and Unity GUIDs are preserved.
Do not install an older combined V+ package that still registers the same tom/*
shaders alongside this package; use the separated LTS build.
The package GUID and bundle path are intentionally new. Already-built old bundles
are not rewritten by source migration and must be rebuilt before release.

Shaders/KKPDeclarations.cginc is an independent copy of the MIT-licensed shared
macros. Vanilla Plus keeps its own unchanged copy. No symlinks, submodules or
cross-package shader includes are used. Optional external probe providers retain
their shader-global interface but are not required dependencies.

## Development and Tests

See Documents/XSeriesDevelopmentRoadmap.md and Documents/XSeriesAlphaContract.md.
See Documents/XSeriesBackFrontComparison.md for the three transparent strategies.
See Documents/XSeriesAlphaOverlapTest.md for the rendered twisted open-cube comparison.
See Documents/XSeriesCrossMeshAlphaTest.md for transparent rear objects and queue-order controls.
See Documents/XSeriesHairContract.md for hair direction, stencil/depth policy,
optional feather packaging, known proxy limitations and the pending performance gate.
See Documents/XSeriesHairClosure.md for the frozen HairX v1 feature scope and the
resolved supported-sclera case. Release and GPU acceptance remain separate.
The [shared Clearcoat](XSeriesClearcoatDesign.md) extension is implemented
across all eight entries, including [EyeWX/EyeX](XSeriesEyeDesign.md).
The eye pair reuses that coat; local refraction/dispersion remains EyeX-only.
EyeWX has one role-independent default, with no automatic mesh-based presets.
See Documents/XSeriesEyeOpticsResearch.md for algorithm research and sources.
Coat and optics default off. See [implementation and rendering evidence](XSeriesCoatEyeProgress.md)
for independent controls, shader-only limitations, regression captures and acceptance status.
Main package version is 0.3.1; X eye feather writers require optional adapter 0.3.0.
All eight shaders and carrier presets now default IndirectDiffuseIntensity to
0.25 (formerly 1), at the user's request after bright Maker indirect lighting
was mistaken for lost skin color. This changes only the indirect diffuse gain,
not direct lights, probe reflections or Clearcoat. Explicit saved values remain
unchanged; materials without a stored override use the new default. No ME XML
default forces existing values, and no game-wide lighting setting is changed.
The property names/types/ranges and skin color/texture composition are unchanged.
See [EyeX surface maps and independent IOR](XSeriesEyeSurface.md) for
grayscale-height/R-depth-A-region authoring, shared depth gain and old-material migration.
See [Eye series feature closure](XSeriesEyeClosure.md) for the frozen
v1 scope, user-confirmed KKS game acceptance, the remaining performance gate and
the conditional POM/first-hit reminder.
See [GPU performance baseline](XSeriesGpuBaseline.md) for measured
Clearcoat/optics increments, 1/4/8-light and eye-pair cases, local V+ LTS/lilToon
comparisons, limitations and the benchmarkxgpu Bridge reproduction workflow.
EyeX optimization is deferred by user decision: retain current effects; it does
not block current closure. The approved [SkinX design](XSeriesSkinDesign.md)
is implemented as one face/body entry. See the [material contract and migration](XSeriesSkinContract.md),
[source texture audit](XSeriesSkinTextureContract.md) and
[implementation evidence](XSeriesSkinProgress.md). SkinX remains experimental
pending actual KKS face/body acceptance; eye acceptance does not certify skin.
Run Tests/Test-XAlpha.ps1 and Tests/Test-ProjectIsolation.ps1 for static checks.
Run Tests/Test-XHair.ps1 and Bridge renderxhair for the HairX prototype.
Run Tests/Test-XCoatEye.ps1 and Bridge renderxcoateye for coat/eye checks and comparison PNGs.
Run Tests/Test-XEyeSurface.ps1 for the independent-IOR and shallow-field contracts.
Run Tests/Test-XEyeMetadata.ps1 for isolated metadata regeneration/version checks.
EyeWX/EyeX property snapshots are checked by Test-XCoatEye.ps1; do not silently
regenerate them when changing the public material contract.
Bridge preparexeyes creates missing eye carriers through Unity APIs without resetting existing materials.
For current eight-shader builds, use Bridge preparexskin and buildxskin, then
Tests/Package-XSkin.ps1. The older buildxcoateye/Package-XCoatEye.ps1 path is the
historical seven-shader 0.2.1 subset and must not package the current manifest.
Run Tests/Test-XSkin.ps1 and Tests/Test-XSkinMetadata.ps1 for the skin contract.
Bridge renderxskin checks the UV/channel and lighting fixtures. benchmarkxskin
uses the existing native D3D11 timer for large-face/body-proxy profiling; wait for
the returned report's status to become complete, not just its start acknowledgement.
Editor fixtures render through CodexBridge. Tests/Install-XStageTwoBridge.ps1 adds
the validation operations to a project's existing bridge.
Reference scenes are in Tests/ReferenceAssets/Scenes.

See Documents/ProjectSeparation.md for migration scope and verification.
The repository is published under AGPL-3.0. Original MIT upstream notices are
preserved in LICENSES/MIT-Upstream.txt; see THIRD_PARTY_NOTICES.md at the repo root.
