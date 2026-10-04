# HairX Progress Record

Date: 2026-10-03. Scope: independent KKS X project only.

Current status: [HairX feature scope closed](XSeriesHairClosure.md), with a frozen
110-property v1 baseline. Specific sclera compatibility is resolved below; broader
game/performance release gates remain open. Earlier sections are dated history.

## Completed

- Added tom/HairX, retaining one public hair entry for the prototype.
- Added continuous tangent/flow strand direction, anisotropic continuous/Toon GGX
  direct highlights, finite tangent fallback and two-sided geometric conventions.
- Reused X raw-light/surface-light decomposition, full-shadow ForwardAdd, indirect
  lighting, MatCap, Rim, emission and outline behavior.
- Added complementary read-only stencil groups: cutout outside, optional single
  alpha layer inside, default depth writing. Zero local coverage discards. The
  light-space shadow remains independent of camera HairFront opacity.
- Added an optional half-resolution inward-distance proxy provider. Missing or
  unsupported provider/no eligible writers falls back to Hard. Mixed eligible and
  missing/late sources can still produce an incomplete proxy. No scene-color grab,
  dither, triangle sorting or extra hair depth prepass.
- Added an optional KKS BepInEx adapter and reproducible DLL/hidden-shader-bundle
  build commands. Outputs built successfully; no game files installed or replaced.
- Registered 110 manifest properties and corresponding shader-scoped tooltips.
- Added independent HairFeatherThreshold (0.05..0.95, default 0.5) and
  HairFeatherPower (0.5..4, default 1). Default values retain the original
  smoothstep path. The half-transition stays fixed when Power changes. Distance
  provider, stencil region, light-space shadow and fallback behavior are unchanged.
- Through Unity Bridge, created m_TomHairX and a_TomHairX and bound the latter to
  chara/tom/shaders/tomx.unity3d. No existing assets/GUIDs were migrated.

## Automated Evidence

Reports are relative to the Unity project root, not this package directory.

- Feather-curve update: `CodexBridge/Reports/XHair-20261003-053201` has **71
  checks passed, 92 rendered captures**, plus mask/distance diagnostics. All 70
  pre-existing PNGs (68 captures and two diagnostics) are byte-identical to
  XHair-20261003-051012. Both compilation sweeps report no warnings or errors.
- Curve validation covers the 5 x 4 Threshold/Power matrix, visible midpoint and
  steepness changes, reset-to-default equivalence, unchanged Hard/missing-provider/
  zero-width behavior, unchanged helper target/draw count, zero-interior framebuffer
  depth and Base/ForwardAdd coverage agreement. The multi-light check transfers
  measured unlit per-pixel coverage to lit output, isolating light weighting from
  the provider's half-resolution geometric approximation. Independent ideal
  rectangle-distance comparisons remain in the curve tests.
- CPU curve checks cover 2020 samples: finite and bounded output, monotonicity,
  fixed endpoints/midpoint, complementary symmetry, increasing slope with Power
  and default smoothstep equivalence. Static HairX/Alpha/isolation tests pass.
- `CodexBridge/Reports/XHair-20261003-051012`: **42 checks passed, 68 rendered
  captures**, plus mask/distance diagnostics: the pre-curve baseline.
  Both reports use Unity 2019.4.9f1 / D3D11.
- Hair compilation warmed 13 representative variants before and after rendering;
  shader supported, no reported shader errors. Not exhaustive variant coverage.
- Pixel tests cover Off/Hard equivalence at opacity 1, exact half coverage,
  Base/Add weighting under several lights, zero-coverage framebuffer depth,
  zero texture alpha at cutoff 0, missing/disabled provider, zero width,
  actual stencil gating, CPU rectangle-distance reference, late-writer rejection,
  per-camera global reset, unregistered isolated-writer fallback, perspective-camera
  alignment and full-HD helper scheduling.
- Direction tests cover map/mesh endpoints, an intermediate blend, opposite-axis
  sign alignment, zero-vector fallback and missing mesh tangents.
- Light tests cover backlight specular suppression and real shadow presence;
  HairFront opacity 0/1 gives identical receiver shadows.
- Finite-pixel sweeps cover physical roughness 0.04/0.1/0.5/1 and Toon blends
  0/0.5/1. Rendered visual matrices cover Cull Off/Front/Back, negative scales,
  curved crossing cards, near point lighting and full-shadow directional/spot
  lighting. These matrices still need user appearance acceptance.
- `Test-XHair.ps1`: matching 110 properties/tooltips, curve invariants and
  coverage/pass contracts.
- `Test-ProjectIsolation.ps1`: all five carrier bindings, local includes, independent
  manifest/bundle; V+ not registered or modified as part of HairX.

### Regression Protection

- `CodexBridge/Reports/XAlphaMerged-20261003-045315`: all **161** existing Alpha
  checks pass. All **209** baseline PNGs from XAlphaMerged-20261002-175254 are
  byte-identical; all 39 legacy-versus-merged prepass pairs remain identical.
- `CodexBridge/Reports/XStageTwo-20261003-045317`: all existing opaque pixel
  assertions pass. All **22** PNGs, including the contact sheet, are byte-identical
  to XStageTwo-20261002-160232.
- Frozen non-hair properties and defaults did not change. TOM_HAIR hooks compile
  out of the existing Opaque/Alpha paths. The shared PBR and V+ libraries were not
  changed.
- The curve update changes only HairX-specific shader/coverage, manifest/tooltips,
  tests and documentation. Runtime provider/plugin, other shader sources and
  existing Unity-native assets are unchanged. No new carrier or helper build is
  needed; game testing requires rebuilding the main X shader bundle/zipmod.

### Validation Correction During Development

The first feather test suite was too permissive: a visible boundary change alone
did not prove a correct distance field. Direct image inspection caught a misplaced
spot instead of inward edge feather. The provider now uses explicit clip-space
filter draws and the appropriate command-buffer projection convention. A CPU
rectangle-distance comparison was added; the corrected render passes it. The
earlier XHair-20261003-044751 and -044931 reports are not accepted feather evidence.

## Stylization Render Validation

The separate [stylization render validation](XSeriesHairStyleValidation.md) used
`CodexBridge/Reports/XHairStyle-20261003-063352`: 45 captures, 26 behavior checks
and 90 finite/nonblank checks passed. Seven labeled contact sheets record actual
Unity output with fixed display mapping. Anisotropy, direction blending, Toon
selection, size/threshold/softness/bands and additional-light response are visible.
No production shader, manifest, runtime provider or carrier assets changed.
The test also documents a remaining open-card inverted-hull outline limitation;
closed geometry has a working outline. Weak Rim on facing cards is expected.

## Runtime Feather Investigation (2026-10-03)

The user's game screenshots show EyePlus at queue 2474, HairX at 2475, Mode 2 and
Width 32. The supplied log records plugin 0.1.0 loading without a missing-bundle
warning, but contains no discovery/camera diagnostics. Thus the shown shader name,
queue ordering and mode are valid; the actual game failure is not yet localized.
Do not assert an unsupported writer or high display resolution as the cause.

Adapter 0.1.1 adds successful resource-load reporting and change-only discovery,
camera and fallback diagnostics. Provider gates and rendering math are unchanged.
The DLL was compiled to `CodexBridge/Builds/TomHairFeather/TomHairFeather.dll`, not
installed in the user's game. Its existing hidden-shader bundle remains compatible.
See [runtime diagnosis](XSeriesHairContract.md#runtime-diagnosis-adapter-011).

Unity Bridge report `CodexBridge/Reports/XHair-20261003-081908` passed **79 checks
and 94 captures**. It adds an optional integration check using the installed real
xukmi/EyePlus Forward pass at queue 2474 with HairX at 2475; Hard and Feather are
visibly distinct. It also checks diagnostics for zero width, scheduled work,
partial viewport, missing passes and late writers. All **94 existing PNGs** from
XHair-20261003-053201 (92 captures plus two field diagnostics) are byte-identical.
Static HairX/Alpha/isolation tests pass. Production shader, manifest, native assets
and V+ code were not changed. This Editor fixture does not reproduce the user's
game camera or skinned character; new runtime logs are still required.

## Maker Viewport And World Width (Adapter 0.2.0)

The follow-up game log localizes the earlier failure: non-overview Maker uses
camera rect `(0.33, 0, 1, 1)`, clipped by the render target. Adapter 0.1.1 explicitly
rejected partial viewports. Overview restored a full viewport and therefore
enabled scheduling. This was a provider viewport restriction, not evidence that
a missing camera depth texture disabled feather; the provider does not sample it.

- Support partial and clipped viewports with viewport-local field coordinates.
  On Unity 2019, switching private targets inside BeforeForwardOpaque disturbed
  the scene viewport even with explicit restoration. Partial cameras therefore
  generate their private field in OnPreCull, before native camera setup, and only
  publish globals at BeforeForwardOpaque. Full cameras retain the legacy path.
- Add HairFeatherWidthMode (0 Pixels, default; 1 World Units) and
  HairFeatherWorldWidth (0..0.05, default 0.005). Existing HairFeatherWidth and
  materials retain pixel semantics. Selected width zero falls back to Hard.
- Project world width using fragment view depth, projection scale and viewport
  height, with an orthographic path. This is projected scene-unit width, not a
  surface/geodesic distance or character-relative measurement. Base/Add share
  coverage; Outline uses its expanded screen location and underlying body depth.
- Preserve stencil, opacity, curve and shadow contracts. World search budget is
  conservative and capped at 256 pixels; extreme close-ups can hit that cap and
  distant subpixel features remain resolution limited. Half-resolution filtering
  uses seven draws for legacy Pixels, up to ten for the maximum World radius;
  target count remains four. This is not a GPU cost measurement.

### Verification And Delivery

- `CodexBridge/Reports/XHairWidth-20261003-090053`: **154 checks passed, 108
  captures**. Tests cover distance 1.5/3/6, FOV 20/40/60, resolutions 256/512/768,
  orthographic size changes, full/partial/clipped/offset viewport switching,
  HDR/LDR, real EyePlus integration, and a moved skinned writer on its first
  captured pose. Also covers width-budget saturation and zero selected width.
- Base plus actual ForwardAdd and Outline compare World output to independently
  projected Pixel references. Curve checks use non-default threshold/power.
  Analytical rectangle comparisons allow two display pixels for half-resolution
  raster uncertainty; the World/Pixel reference comparison uses 1e-5 tolerance.
  Finite output and hard-stencil containment are checked separately.
- `CodexBridge/Reports/XHair-20261003-090409`: **79 checks passed, 94 captures**;
  all **96 PNGs**, including field diagnostics, match the pre-update
  XHair-20261003-081908 baseline byte-for-byte. Both suites warmed 13 representative
  variants before/after rendering with no reported shader errors. Static HairX,
  Alpha and isolation checks pass; the full Alpha render suite was not rerun here.
- DLL 0.2.0 and field-contract-v2 `hairx-feather.unity3d` were built through the
  compile script and Unity Bridge into `CodexBridge/Builds/TomHairFeather`.
  The plugin rejects an outdated helper bundle with a diagnostic and Hard fallback.
  Replace BOTH adjacent helper files and rebuild the main X shader zipmod with
  shader/manifest/tooltips. The main zipmod was not built or installed in this
  update. Existing material/prefab carriers need no rebinding.
- No V+, KK-port or production native assets were modified. Unity generated source
  metadata normally. Test scenes/meshes/materials were transient, not saved assets.
  Actual game Maker switching and GPU profiling remain user acceptance tasks.

## Internal Feather Edge Investigation (2026-10-03)

User confirms internal eye/eyeline artifacts disappear in HairFrontMode 1. The
0.2.0 runtime log reports four registered writers: cf_Ohitomi_L02/R02 (EyePlus)
and cf_O_eyeline / cf_O_eyeline_low (EyeWPlus), all at queue 2474. Hair is 2475;
visible/source draws are four, late writers and missing passes are zero. No
separate sclera renderer appears in discovery. This makes an unregistered sclera
writer the leading hypothesis, not a confirmed identification of its shader.
Do not interpret missingPass=0 as proof that all actual stencil writers were found.

Expanded `renderxhairwidth` with a ring-shaped sclera, overlapping iris and upper
eyeline, compared against a single filled union. Both fixture and real EyePlus /
EyeWPlus passes are tested at full and half field resolution, full and Maker-style
partial viewports, and reversed source order. Complete sources produce identical
union masks and distances, without internal feather edges. Negative controls omit
the iris or sclera ONLY from the provider while retaining its real stencil draw:
they produce internal Feather bands while Hard output stays exactly unchanged.
These controls reproduce the failure mechanism, not the user's exact character.

Report `CodexBridge/Reports/XHairWidth-20261003-094651`: **198 checks passed**, with
**208 images (152 camera captures and 56 field diagnostics)**. All 108 prior width
suite PNGs match XHairWidth-20261003-090053 byte-for-byte. Compilation sweeps report
no shader errors. This investigation changes only Editor validation and this
record, not runtime/shader behavior, material properties or production assets.

### User Confirmation And Resolution

The user subsequently replaced `cf_m_sirome_00` with `xukmi/EyeWPlus` at queue
**2472** and reported immediate improvement, with screenshots of the corrected
eye. This closes the reported case for the supported combination and supports
the missing-sclera proxy diagnosis. No feather/stencil/opacity/curve algorithm
change is needed. The original sclera shader remains unidentified; its adapter
is deferred, not guessed. This does not prove its own stencil was defective.

Do not globally fill proxy holes: legitimate eye/brow gaps must remain intact.
The compatibility documentation and HairFrontMode tooltip now explain incomplete
mixed-writer fields. No eligible writers gives Hard; some eligible writers does
not guarantee that every real stencil writer was discovered.

## Cost Observations, Not A GPU Benchmark

At 1920x1080, six curved-card renderers with six eye writers were measured using
nine synchronous Camera.Render CPU samples after four warmup renders. These are
small test fixtures, **not six complete characters or GPU frame times**.

| Mode | 1 light CPU median ms | 4 lights CPU median ms | Proxy writer draws | Filter draws |
| --- | ---: | ---: | ---: | ---: |
| MainOpaqueX comparison | 0.09 | 0.19 | 0 | 0 |
| HairX Off | 0.09 | 0.29 | 0 | 0 |
| HairX Hard | 0.10 | 0.29 | 0 | 0 |
| HairX Feather | 0.16 | 0.32 | 6 | 7 |

There is also one stencil-extraction draw when Feather is active. Timing noise
and asynchronous GPU execution explain why a more expensive mode need not show a
higher Camera.Render CPU median in every column. Do not infer negligible GPU
cost, or a performance win, from these numbers. Off/Hard have no auxiliary targets
or filter work, but the unified shader still submits both complementary hair
pass groups. Keep the single-entry decision provisional until real KKS profiling.

## Enhancement Backlog (2026-10-03)

User direction: retain all four enhancements below for future HairX development.
**Dual specular lobes have the highest enhancement priority**, specifically for
Toon art direction, not just physical realism. This supersedes the earlier
suggestion to prioritize backlight transmission. These are recorded intentions,
not implemented features or additional requirements for closing the current
HairX feature scope. Existing game, geometry and performance acceptance remains
open; recording this backlog does not pass those gates or delay SkinX/EyeX.

### First Priority: Stylized Dual Specular Lobes

- Combine a primary highlight with an independently shaped secondary sheen, for
  example a narrow bright band and a broader tinted band along the same strand
  field. The goal is controllable layered hair highlights and richer color design.
- Evaluate independent strength, color, width and longitudinal shift, plus Toon
  threshold/softness shaping. These are design candidates, not frozen property
  names, ranges or a requirement to expose every control twice.
- Reuse the continuous tangent/flow-map direction and the existing per-light
  visibility, geometric horizon and shadow contracts for both lobes. Keep the
  secondary lobe a HairX-local extension rather than another public shader family.
- Existing SpecularBands quantizes one lobe; it is not a substitute for a second
  lobe with its own position and shape.
- Default secondary contribution to zero so current materials retain their
  appearance. Verify disabled-path cost and added Base/ForwardAdd cost before
  deciding runtime branches versus variants; do not assume disabled means free.
- Acceptance should show independently placed/shaped/tinted lobes, unchanged
  baseline when disabled, correct multi-light shadows, and bounded artistic
  composition without broad unintended whitening of the Toon body color.

### Other Retained Enhancements

| Enhancement | Intended benefit and design boundary | Status |
| --- | --- | --- |
| Backlight transmission | A separately masked/tinted thin-hair or tip response to backlighting, distinct from view-driven Rim. Retain cutout coverage and define its own light/shadow visibility; do not weaken ordinary reflection NdotL or geometric-horizon guards. | Deferred; evaluate after dual lobes according to visual need. |
| Anisotropic Probe IBL | Align environment sheen with the strand direction. Current probe specular remains isotropic; evaluate a bent-reflection approximation before more expensive filtering, with probe-only visual and GPU comparisons. Do not reintroduce broad environment color washout. | Deferred enhancement, not a current lighting defect. |
| Internal hair scattering | Approximate transport within strands/bundles, with explicit thickness/density/occlusion assumptions and a restrained effect on Toon light/shadow grouping. Not simply stronger ambient or a copied skin SSS control. | Longer-term research; no current implementation commitment. |

No shader, manifest property, material or prefab is added by this decision.

## Still Open

Feather width, midpoint and curve steepness are now independently adjustable.
HairFrontOpacity still sets interior coverage, not hardness. High-power transitions
on narrow/subpixel eye features require moving-camera acceptance in game.

1. User acceptance on real KKS hair meshes, actual packed normal textures, animated
   skinned meshes, authored flow maps, eye writers and material render queues.
2. Proxy limitations: occlusion-created stencil boundaries and overlapping-character
   ownership are not reconstructed. Unknown eye shader writers require integration.
3. Packaged main X bundle testing, optional-plugin game lifecycle/config testing,
   real GPU/frame profiling, multi-camera/reflection interactions and VR support.
4. MSAA, camera-depth-dependent DOF/SSAO and actual framebuffer versus generated
   _CameraDepthTexture behavior. Light-space hair shadows intentionally do not fade
   with HairFront; this may differ from the post-process depth users expect.
5. Variant/quality budgets and the final keep-one-versus-split-HairFront decision.
6. SkinX and EyeX, plus wider Stage Three release hardening. No KK backport yet.
7. Hair-card outline policy: the current back-face inverted hull cannot guarantee
   outlines for front-facing open cards. Decide whether to retain this documented
   geometry requirement or add a separate hair-specific method after user review.

See [the detailed contract and installation boundary](XSeriesHairContract.md).
