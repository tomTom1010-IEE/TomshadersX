# X Alpha Coverage Contract

## Scope and Status (2026-10-03)

KKS / Unity 2019.4.9f1 only. All Alpha shaders consume the accepted 98-property
MainOpaqueX material core. The primary entries are MainAlphaX (six transparent
controls plus DepthPrepass) and MainAlphaXBackFront (six plus BackfaceZWrite).
MainAlphaX2Pass retains its six original controls as a legacy compatibility entry.
Its name, defaults, material references and behavior are not repurposed.
This is the first Stage Three implementation, not Stage Three completion.
No V+ or KK changes, refraction, screen-color sampling, glass transmission, OIT,
or three-pass color variant are included.

## Two Primary Entries, Three Render Strategies

| Shader | Default | Intended use |
| --- | --- | --- |
| tom/MainAlphaX | DepthPrepass off, color ZWrite off | Standard alpha; optionally suppress rear self-overlap using a nearest-qualifying-depth prepass |
| tom/MainAlphaXBackFront | Back then front color; both depth writes on | Primary two-color-layer variant; preserve overlapping coverage, including dark fabric accumulation |
| tom/MainAlphaX2Pass (legacy) | Depth prepass on; color ZWrite off | Compatibility only; use MainAlphaX with DepthPrepass=1 and AlphaOptionZWrite=0 for new materials |

The historical MainAlphaX2Pass name still means depth plus color; it is not
renamed to mean BackFront. The primary "two-pass" design means BackFront's
two color groups. Neither term describes the total GPU draw count.
All three shaders also have optional Outline, per-light ForwardAdd and ShadowCaster.
ForwardAdd can run once per additional pixel light. The prepass is local to each
renderer, not a camera-wide prepass that sorts every transparent renderer.

In the original Alpha/2Pass there is no separate front/back color draw: Cull Off renders a single card once
at a covered sample. It does not duplicate the same sheet to darken its coverage.
A closed shell can still contribute front and rear geometry in standard Alpha.
The prepass variant intentionally removes qualifying rear surfaces instead of
faithfully transmitting every shell layer.

BackFront is the separate two-color-layer strategy. See
[its pass order, defaults and limitations](XSeriesBackFrontComparison.md).

## Coverage, Clipping and Shadows

Final coverage is saturate(MainTex.a * AlphaMask.r * Alpha).
BaseColor.a is not another multiplier. All color contributions, including
specular, environment, MatCap, Rim and emission, use this coverage once.
Outline uses this coverage times OutlineColor.a.

| Control | Default | Contract |
| --- | --- | --- |
| Alpha | 1 | Global coverage, not optical transmission |
| AlphaBlendMode | 5 | 5 = Straight; 1 = Premultiplied output; no other values supported |
| AlphaOptionCutoff | 0 | Enable global Cutoff in color, Outline, depth and shadows |
| Cutoff (inherited) | 0.5 | Threshold on final coverage when global cutoff is enabled |
| AlphaOptionZWrite | 0 / 1 / 1 | MainAlphaX color depth / legacy 2Pass prepass / BackFront main-face color depth |
| DepthPrepass (MainAlphaX only) | 0 | Independent depth-only pass before Outline and color; not controlled by color ZWrite |
| BackfaceZWrite (BackFront only) | 1 | Complementary-face color depth, no depth-only prepass |
| DepthShadowCutoff | 0.01 | Shared depth/shadow threshold, after global cutoff |
| CastShadows | 1 | Enable binary cutout ShadowCaster; independent of received shadows and ZWrite |

Coverage zero is always discarded, including when both thresholds are zero.
Globally clipped fragments cannot write depth or cast a shadow.
Depth and shadows both require coverage >= DepthShadowCutoff; equal-to-threshold
fragments survive. Turning off ZWrite does not turn off shadows: CastShadows
controls that independently. Shadows are binary, not fractional glass shadows.

With standard Alpha and ZWrite enabled, the depth/shadow threshold must also
discard color and Outline: a surviving color fragment would otherwise write
depth in that same pass. Use MainAlphaX with DepthPrepass=1 and
AlphaOptionZWrite=0 to keep faint color below the depth threshold. The legacy
2Pass has the same separation. Faint color writes no depth but still tests
against existing depth.
Such fragments can retain the ordinary sorting limitations; the prepass is not OIT.
BackFront applies the same color/depth coupling separately for each enabled
face depth write. Neither face renders clipped coverage as a hidden occluder.

### MainAlphaX Depth Options

| DepthPrepass | AlphaOptionZWrite | Behavior |
| --- | --- | --- |
| 0 | 0 | Default ordinary alpha, no framebuffer depth writes |
| 0 | 1 | Original single-pass color/depth behavior, including color threshold clipping |
| 1 | 0 | Recommended prepass mode, identical to the enabled legacy depth strategy |
| 1 | 1 | Both passes write depth; color and Outline still apply the stricter depth threshold; not the recommended prepass preset |

These are ordinary Float controls and do not require shader keywords, a custom
Inspector, hidden synchronized properties or an ME runtime plugin. Both switches
remain explicit; enabling DepthPrepass does not silently change color ZWrite.
Off rejects the depth fragment before alpha texture reads. The pass is still
submitted, including vertex/raster work; no zero-cost or performance claim is made.
The prepass never duplicates lighting, emission or Outline. Shadow coverage
still uses the existing shared threshold independently of the two depth switches.

### Compatibility and Migration

Keep old MainAlphaX2Pass materials and carrier assets unchanged. To manually
migrate a material to MainAlphaX, preserve textures, tiling, render queue and all
shared properties, copy old AlphaOptionZWrite to DepthPrepass, then set new
AlphaOptionZWrite=0. Do not rename the historical shader or reuse its GUID for
BackFront. No automatic saved-material/card migration is performed.

## Straight and Premultiplied Output

Source textures remain straight RGB in both modes. The shader premultiplies its
final fogged color internally only for mode 1.

- Base/Outline RGB: source factor is SrcAlpha (5) or One (1), destination OneMinusSrcAlpha.
- Base/Outline alpha: One, OneMinusSrcAlpha.
- Add RGB: source factor is SrcAlpha or One, destination One.
- Add never writes target alpha; its result is coverage-weighted exactly once.

Both output modes intentionally produce the same appearance for the same inputs.
This option is NOT an additive highlight mode, a black-key operation or an input
unpremultiplication switch. A black-background RGB image with alpha=1 remains
opaque coverage. Use a proper alpha/mask or prepare that source separately.

## Integration and Known Limits

- Shared lighting is unchanged except for TOM_ALPHA-guarded coverage/output hooks.
  Opaque does not define these macros.
- All three Alpha shaders share TomAlphaCommon, TomAlphaOutline and TomAlphaShadow.
  BackFront adds a thin face-selection adapter, not a second lighting implementation.
- Standard Alpha and the legacy depth shader share TomAlphaDepth. Their depth
  switches differ intentionally for compatibility; the actual coverage and
  depth-fragment implementation are shared. MainAlphaX keeps its original
  conditional color clipping rather than defining the legacy TOM_ALPHA_PREPASS
  macro on every color pass.
- Existing VFACE, normal basis, normal/detail normal, light/probe and GGX/Toon logic
  remain in the X libraries. No legacy xukmi alpha-mask processing is imported.
- Standard Alpha relies on renderer sorting, not triangle sorting. Self-overlap
  can accumulate rear ForwardAdd after front Base; the prepass variant suppresses
  rear fragments only where a qualifying nearest depth exists.
- Inverted-hull Outline is off by default. A transparent body can reveal its rear
  outline through the body; this is not a screen-space edge outline.
- Transparent queue is 3000. Writing framebuffer depth does not establish that a
  particular DOF/SSAO effect uses that depth. Camera depth generation and game
  post-processing must be tested separately; no DOF compatibility claim is made.
- No existing scene, material, prefab or AssetBundle binding was changed.
  Unity may generate import .meta files for new source files.

## MaterialEditor and Bound Assets

All entries inherit the Opaque categories and append transparent controls in Render
Options (seven for Alpha/BackFront, six for legacy 2Pass). Scoped hints explain defaults, the valid blend-mode values, depth
threshold coupling, and sorting limitations.

Created and bound through CodexBridge on 2026-10-02, using shader defaults:

| Shader | Material | Prefab asset |
| --- | --- | --- |
| tom/MainAlphaX | m_TomMainAlphaX | a_TomMainAlphaX |
| tom/MainAlphaX2Pass | m_TomMainAlphaX2Pass | a_TomMainAlphaX2Pass |
| tom/MainAlphaXBackFront (added 2026-10-03) | m_TomMainAlphaXBackFront | a_TomMainAlphaXBackFront |

These are sphere shader-carrier prefabs, matching a_TomMainOpaqueX (MeshFilter
and MeshRenderer, no Studio ItemComponent). They are not standalone Studio items.
All three prefabs are assigned to chara/tom/shaders/tomx.unity3d, matching
their existing manifest entries. Materials are referenced dependencies, without
a separate AssetBundle assignment. AssetBundle build and game acceptance remain pending.
Keep the existing tooltip catalog bundle binding. Native assets must be created
through Unity Editor or CodexBridge, never by hand-writing serialized files.

## Automated Evidence

### Consolidation Regression (2026-10-03)

- Before edits, renderxbackfront passed 114 checks in
  `CodexBridge/Reports/XBackFront-20261002-174530`.
- validatexalpha warmed 13 representative variants for each of MainAlphaX and
  legacy MainAlphaX2Pass, with no reported shader errors.
- renderxalphamerged passed 161 checks in
  `CodexBridge/Reports/XAlphaMerged-20261002-175254`. It retains the 114 checks,
  repeats the 32 shared checks using MainAlphaX's new prepass option, and adds
  12 legacy-versus-merged Outline comparisons plus three combined-switch checks.
  Outline comparisons include Cull Off/Front/Back, prepass on/off, depth thresholds
  above/below visible coverage, straight/premultiplied output and mirrored scale.
- All 142 original PNGs byte-match the pre-edit baseline. All 39 corresponding
  merged-prepass PNGs byte-match the legacy depth shader's PNGs. Reproduce with
  `Tests/Compare-XAlphaMerge.ps1`; see [hash results](Evidence/AlphaMerge-20261003-images.json).
- renderxcrossmeshalphamerged uses background/rear/front queues 2450/2451/2452,
  with MainAlphaX + DepthPrepass in the center column. All ten cross-mesh checks
  passed; all five primary/control image object regions match the previous
  low-queue experiment. See [region hashes](Evidence/AlphaMerge-20261003-crossmesh-images.json).
- [Pixel report](Evidence/AlphaMerge-20261003.report.json) and
  [cross-mesh report](Evidence/AlphaMerge-20261003-crossmesh.report.json).

The merged comparison scene is
`Tests/ReferenceAssets/CrossMeshAlphaMerged-20261002-175256-402/CrossMeshAlphaComparison.unity`.
Only a new test scene/material set was saved through Unity; production carriers
and previous reference scenes were not modified. This is not camera-depth,
DOF/SSAO, performance or packaged-game acceptance. The bundle was not rebuilt.

### Earlier Alpha Evidence

- Tests/Test-XAlpha.ps1: 105/104/105 shader/manifest/tooltip properties;
  render-state/default/coverage checks; inherited Opaque contract unchanged.
- validatexalpha: 13 representative variants per shader warmed successfully,
  including point/spot shadow and fog variants. Not exhaustive variant coverage.
- renderxalpha: 64 pixel checks passed on Direct3D11 / Unity 2019.4.9f1.
  These include intentionally observing standard Alpha's self-overlap limitation.
- Fixtures verify alpha 0/0.25/0.5/1, straight/premult equivalence, the full
  coverage product, global cutoff, framebuffer depth, separate-renderer Base/Add
  compositing, 2Pass merged-mesh occlusion, zero-coverage art/Outline, and binary
  directional/point/spot shadow coverage.
- Opaque regression: all 21 captures passed; all 21 images plus the contact sheet
  byte-match the accepted Stage Two run. There are no Opaque appearance changes
  in those fixtures.

The bridge renderer creates only transient objects in an isolated additive scene,
restores quality/light-mask/active-scene state, and closes it without saving.
It uses Camera.Render, not computer-use automation.

Report: [Alpha pixel checks](Evidence/Alpha-20261002/report.json).
Full local images: CodexBridge/Reports/XAlpha-20261002-141828 under the Unity project.
Opaque rerun: CodexBridge/Reports/XStageTwo-20261002-141829.
BackFront follow-up: 114 checks in CodexBridge/Reports/XBackFront-20261002-160135
(UTC folder stamp, 2026-10-03 local); all original 80 Alpha images byte-match the
post-separation baseline. Detailed scope is recorded in XSeriesBackFrontComparison.md.

## Remaining Acceptance

1. Build the bound assets and inspect actual KKS materials, especially skinned meshes and eyebrow edges.
2. Inspect closed shells, hair-card intersections, layered clothing and adjacent
   transparent renderers while moving the camera. Compare prepass on/off.
3. Test Cull Back/Front/Off and negative/non-uniform scale; inherited Opaque rules
   are not a substitute for Alpha-specific visual acceptance.
4. Inspect nonzero-opacity Outline on partial-alpha silhouettes. Keep it optional.
5. Check game fog, soft shadows, probe-filled environments, DOF and SSAO separately.
6. Profile overdraw and many shadowed pixel lights on a representative character.
7. Evaluate threshold popping during global fades. The current depth/shadow policy
   intentionally uses binary thresholds, not temporal dithering.

Glass with retained surface reflection and any three-pass color variant still
require separate semantics and evidence before adding another public shader.
