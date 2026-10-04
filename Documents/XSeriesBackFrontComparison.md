# Alpha Back/Front Comparison

## Decision (2026-10-03)

Current decision: MainAlphaX now includes optional DepthPrepass, and BackFront
is the primary two-color-layer variant. MainAlphaX2Pass remains a legacy
compatibility entry without renaming or changing existing material behavior.
The three-way experiment below records the original comparison; its depth
column can now be reproduced with MainAlphaX, DepthPrepass=1, AlphaOptionZWrite=0.

The requested local lilToon "2pass" means two visible color layers, not a
nearest-surface depth prepass. Preserve the existing two X variants and add
tom/MainAlphaXBackFront for side-by-side KKS evaluation.

| Variant | Core strategy | Overlap behavior |
| --- | --- | --- |
| MainAlphaX | One Base group, default ZWrite off | Multiple triangles accumulate, but order follows the mesh rather than a triangle depth sort |
| MainAlphaX2Pass | Depth-only prepass, then color | Qualifying nearest surface rejects rear layers, intentionally preventing their accumulation |
| MainAlphaXBackFront | Back color/Base+Add, then front color/Base+Add | Both orientation groups can contribute coverage; dark fabric overlaps naturally darken |

For two surfaces with coverage 0.5, combined coverage is 0.75, not 1.0.
Darkening is the result of over-compositing dark surface color, not an extra
darkening coefficient. White or brightly lit surfaces need not darken.

## Passes and Controls

Declared order: FORWARD_BACK, FORWARDADD_BACK, OUTLINE, FORWARD, FORWARDADD,
SHADOWCASTER. "Two pass" describes the two color groups, not total draws.
ForwardAdd runs per additional pixel light; shadows and Outline cost extra.
Both groups consume the same X material/light libraries and fade all art layers.

- CullOption=2 (default): complementary pass draws backfaces, main draws frontfaces.
- CullOption=1: swap orientation groups. This is not a general depth-order solution.
- CullOption=0: skip the complementary group, draw both faces once in the main
  group. A thin card is never deliberately rendered twice at the same sample.
- AlphaOptionZWrite=1: main-face color writes depth.
- BackfaceZWrite=1: complementary-face color writes depth.
- AlphaOptionCutoff=0: continuous visible coverage by default; Cutoff remains 0.5.
- DepthShadowCutoff=0.01: shared depth/shadow threshold. A color pass writing
  depth must discard below this threshold too. Disable that pass's depth write
  to retain faint color; zero coverage always discards.
- AlphaBlendMode=5: straight output. Mode 1 premultiplies internally and produces
  equivalent compositing for the same straight input textures.
- ShadowCaster projects both visible sides, uses no normal-direction bias, and
  keeps Unity depth bias plus the material ShadowOffset settings. CastShadows and
  global cutoff retain their existing meanings; shadows are binary.
- Outline is one optional inverted-hull pass, off by default, between groups.

Queue stays Transparent (3000), matching the other X alpha variants. No
GrabPass, screen-color sampling, stochastic alpha, depth peel or OIT is added.

## Local lilToon Reference

The local lilToonTwoPassTransparent.shader has a visible FORWARD_BACK and a
visible front Base pass, with PreCull=1, Cull=2 and both depth writes enabled.
Its LTSKKS transparent color path uses continuous coverage, not dither.
The screenshot's DstBlend/DstBlendAlpha=10 means OneMinusSrcAlpha; its
DstBlendFA/DstBlendAlphaFA=1 means One. These match the X over/add roles.

This is semantic alignment, not a lilToon port. X retains its lighting core,
coverage product, RenderType=Transparent and queue 3000 (the local lilToon file
uses AlphaTest+10). X also gives the rear group its own full-shadow ForwardAdd;
the local file has only the main ForwardAdd group. Output factor settings differ
internally when X uses straight output, while preserving the same over equation.

## Verification

- Unity 2019.4.9f1 / Direct3D11, through CodexBridge Camera.Render only.
- validatexbackfront: 13 representative variants warmed, no reported shader errors.
- renderxbackfront: 114 scoped pixel checks passed. This includes the original
  64 Alpha checks, 32 shared checks for BackFront, and 18 BackFront-specific checks.
- Different front/rear RGB and alpha values matched two separately sorted
  reference renderers with directional, point and spot contributions. Tests
  verify added lights actually affect the result, premultiplied equivalence,
  all four depth-switch combinations, zero coverage and single-card coverage.
- The original 80 Alpha images byte-match the post-separation baseline.
- Opaque rerun passed 21 captures; all 21 PNGs plus the contact sheet byte-match
  the post-separation baseline. The frozen 98-property core is unchanged.
- Static checks cover all four registrations and Unity asset bindings.

Evidence: [pixel report](Evidence/BackFront-20261003.report.json).
Full images: CodexBridge/Reports/XBackFront-20261002-160135 under the Unity project.
Opaque images: CodexBridge/Reports/XStageTwo-20261002-160232.
Report folders use UTC; the local run date is 2026-10-03.

Material m_TomMainAlphaXBackFront and sphere carrier a_TomMainAlphaXBackFront
are created by Unity through CodexBridge. The prefab is assigned to
chara/tom/shaders/tomx.unity3d; its material is a referenced dependency.
Manifest and scoped tooltip entries are registered. No V+, KK, existing user
materials, saved scenes or old variant asset bindings are changed.

## User Comparison and Limits

Follow-up Unity test: [twisted open cube, captures and remaining edge differences](XSeriesAlphaOverlapTest.md).

1. Use the same cloth mesh, textures, colors and Alpha on the three X variants.
   For two-sided cloth use Cull Off on standard/depth variants, and default
   Cull Back on BackFront (its complementary pass supplies the other face).
2. Start with Alpha=0.5, AlphaOptionCutoff=0, DepthShadowCutoff=0.01, Outline off.
   BackFront starts with both depth writes on; standard off, prepass on.
3. Compare single layers and folded front/back overlap against a light background.
   Rotate the camera and move a point/spot light; check rear highlights do not
   appear unattenuated through the front coverage.
4. Try disabling BackfaceZWrite, then both color depth writes. Check nearby
   transparent objects and additional same-facing folds, not just an isolated shell.
5. Test cloth alpha textures, skinned animation, negative scale, soft shadows,
   nonzero Outline and actual KKS post-processing before selecting a final variant.

Back/front orientation is not depth sorting: arbitrary folds, intersecting
meshes, multiple front-facing layers and coplanar cards can still mis-composite.
Enabled depth writes can hide later transparent geometry; disabling them can
expose unsorted layers. The fixture proves scoped layer composition, not all
real-character topology, game visual acceptance, DOF compatibility or performance.
No bundles were built or installed into the game by this change.
