# HairX Stylization Render Validation

Date: 2026-10-03. Scope: KKS HairX, excluding stencil and feather.
Production shader, runtime provider, manifest and carrier assets were not changed.

## Evidence And Reproduction

Unity Bridge operation: `renderxhairstyle`.
Runner: `Editor/TomXHairStyleValidation.cs`.
Report directory, relative to the Unity project root:
`CodexBridge/Reports/XHairStyle-20261003-063352`.

- Unity 2019.4.9f1, D3D11, 384 x 384 ARGBFloat captures.
- 45 captures, 26 behavior assertions and 90 finite/nonblank image checks passed.
- 13 representative HairX shader variants warmed before and after rendering;
  no reported shader warnings or errors. This is not exhaustive variant coverage.
- Temporary additive scene and runtime-only materials/textures/meshes, removed
  after capture. Existing light masks, quality setting and active scene restored.
  No test scene, material or prefab was saved.
- Stencil Off, environment/fog/post effects Off. The isolated fixture is a curved
  patch with known U/V axes; final-layer tests use the existing crossing-card mesh.
- Every PNG uses the same x4 exposure and RGB Reinhard display mapping. There is
  no per-image normalization; brightness differences are retained. Numeric tests
  use unmodified float readback. These are diagnostic previews, not default game
  exposure or recommended material presets.

Create labeled contact sheets with:

```powershell
& 'Assets/Mods/TomShadersX/Tests/New-HairStyleContactSheets.ps1' `
  -ReportDirectory 'CodexBridge/Reports/XHairStyle-20261003-063352'
```

The script only lays out and labels the Unity-rendered PNGs. It does not synthesize
shader output. The JSON report lists individual captions, metrics and assertions.

## Confirmed Responses

| Control | Rendered sweep | Observed behavior |
| --- | --- | --- |
| HairAnisotropy | 0, 0.5, 0.9 | Isotropic spot becomes a cross-strand band |
| StrandAngle | 0, 45, 90 degrees | Rotates the directional highlight with the surface's tangent frame |
| StrandDirectionBlend | 0, 0.5, 1 | Intermediate orientation is distinct; map endpoint matches the corresponding U-axis reference |
| SpecularToonBlend | 0, 0.5, 1 | Broad continuous GGX response becomes a selected Toon band; midpoint matches RGB interpolation |
| SpecularSize | 0.25, 0.55, 0.8 | Visible highlighted area grows monotonically |
| SpecularThreshold | 0.2, 0.5, 0.8 | Higher threshold selects a smaller highlighted area |
| SpecularSoftness | 0.02, 0.2, 0.6 | More intermediate edge pixels, softer transition |
| SpecularBands | 1, 2, 4 | Smooth selection, binary-like selection, then stepped levels |
| Roughness at Toon blend 1 | 0.04, 0.5, 1 | Isolated direct highlight is identical; Toon width is owned by SpecularSize |
| SpecularAA | 0, 1; camera shifts 0, 0.5 pixels | Changes the high-frequency packed-normal response; temporal acceptance remains open |
| Additional lights | Sun; sun + point; sun + spot | Point and spot lights add their own stylized lobe |

For the size sweep, pixels above 20% of each capture's peak increase from 457 to
11486 to 34964. Peak luminance remains approximately 0.160. For the threshold
sweep, the same count falls from 22969 to 9436 to 2260. These are fixture-specific
measurements, not resolution-independent shader guarantees.

Flow maps use linear RG direction data. The AA diagnostic normal is packed as
R=1, G=Y, B=1, A=X for the current Unity/KK unpacking path. No alternate production
normal decoder was added. The high-anisotropy/large-size highlight can reach the
patch boundary; its flat cropped ends are the finite fixture, not an extra cutoff.

## Geometry Findings

### Rim Is View Dependent

Rim width 0.55 barely affects the mostly front-facing crossing cards (mean RGB
difference about 0.0000005). Increasing width to 0.9 makes the art layer obvious,
but can brighten a broad region: this is an exaggerated diagnostic, not a preset.
On a closed sphere, width 0.35 clearly selects the grazing-angle rim. The weak
front-facing result is not a disconnected property.

### Open-Card Outline Limitation

With Cull Off, the current inverted-hull outline selects expanded back faces.
All front-facing open cards in this fixture have no such back-facing hull:
Outline Off and On are pixel-identical. On a closed sphere, the same shader
renders the requested 2-pixel outline, visibly verified with a contrasting color.

Do not interpret this as general hair-card outline support. If planar/open hair
cards must have reliable texture-edge or outer-edge outlines, a hair-specific
policy or geometry-authoring contract still needs design and acceptance. This
test does not change culling or add a screen-space outline as an implicit fix.

The first exploratory report, `XHairStyle-20261003-063014`, failed two over-broad
assertions that assumed any facing cards must show Rim and Outline. The final
suite preserves those geometry observations as negative controls and adds the
closed-surface positive controls; it does not hide the open-card limitation.

## Images

- `directions-sheet.png`: anisotropy, mesh angle and continuous flow blend.
- `models-sheet.png`: continuous/Toon mixing and stepped highlight levels.
- `shape-sheet.png`: size, threshold and softness.
- `antialiasing-sheet.png`: packed-normal AA diagnostic and subpixel movement.
- `lights-sheet.png`: directional, point and spot contributions.
- `layers-sheet.png`: body, direct highlight, wide Rim, MatCap and added lights
  on crossing cutout cards. The Outline comparison intentionally shows no change.
- `geometry-sheet.png`: closed-mesh positive controls for Rim and Outline.

## Not Accepted By This Test

This is not a real-character visual acceptance, a GPU performance measurement,
a dynamic temporal-AA guarantee, or a stencil/feather/DOF/MSAA certification.
Real animated KKS hair, mirrored UVs and extreme normal/lighting combinations
retain their existing acceptance requirements. No production shader was adjusted
to make these particular images look better.
