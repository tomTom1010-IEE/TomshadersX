# X Series Closing Audit

Date: 2026-10-04. Package: Tom Shaders X 0.3.1.

## Scope And Result

The requested code, Unity and MaterialEditor metadata audit is complete. Ten
static suites pass, the eight existing Unity regression suites pass 1,132 checks,
and the new standard-sphere showcase passes 109 additional checks. No production
shading algorithm was changed by this audit.

This is not a new in-game audit, a whole-character GPU benchmark, or certification
of every MaterialEditor version. ME usability was checked through the registration
and tooltip schemas, regeneration tests and Unity material contracts, not by
driving the game's ME UI. Prior user-confirmed game acceptance remains separate.

## Findings And Repairs

| Finding | Repair | Verification |
| --- | --- | --- |
| EyeX inherited generic cutout-only MainTex help despite blending surviving coverage | Describe source alpha, clipping and alpha blending; separate optical region from coverage | Eye metadata and series metadata tests; coat/eye rendering |
| EyeWX help omitted its game color alias and precise coverage behavior | Clarify the game alias and cutout/stencil roles without renaming properties | Eye contract and metadata tests |
| SkinX Cutoff help implied the generic MainTex/AlphaMask rule | Document fixed clothing-mask threshold separately from optional main-alpha clipping | Skin contract, metadata and rendering tests |
| Skin metadata regeneration replaced six useful shared mask descriptions with a generic sampler note | Retain inherited channel descriptions and append the Skin sampler constraint | Regeneration/idempotence and channel-help regression checks |
| Six Skin-specific categories lacked category help | Add help for KKSColorLayers, KKSClothingCoverage, KKSGloss, SkinResponse, KKSLiquid and SkinWetSurface | All registered categories covered by series metadata test |
| The shallow-eye CPU test assigned to PowerShell's read-only `$Error` variable | Rename the local numerical error to `$rootError`; keep the solver and tolerance unchanged | Rerun all ten scripts in separate PowerShell 7 processes |

All eight shaders expose 973 registered controls in total. The new
`Tests/Test-XSeriesMetadata.ps1` checks source/manifest/tooltip coverage, duplicates,
property types, exact ranges, category help, shared schemas and no forced XML
defaults. Intentional game aliases and family-specific coverage are not flattened
into an incorrect one-size-fits-all interface. Enumeration help retains integer
values for ME versions that display them as numeric controls.

Public shader/property names, stencil and render-queue contracts, alpha policies,
curve controls, saved-material values and the approved 0.25 indirect diffuse
default remain unchanged. No runtime plugin DLL was modified or installed.

## Verification Evidence

Unity 2019.4.9f1, Built-in Forward, Direct3D11, Gamma project; local GPU:
NVIDIA GeForce RTX 4070 Ti SUPER. The paths below are under the Unity project
root's `CodexBridge/Reports/`; each directory retains its machine-readable report
and image evidence. Counts are assertions, not distinct material combinations.

| Suite | Checks | Report directory |
| --- | ---: | --- |
| Shared baseline | 5 | XStageTwo-20261004-135324 |
| Alpha strategies | 161 | XAlphaMerged-20261004-135324 |
| Hair coverage and lighting | 79 | XHair-20261004-135325 |
| Hair feather distance/FOV/resolution/viewport | 198 | XHairWidth-20261004-135326 |
| Hair anisotropy and Toon styling | 116 | XHairStyle-20261004-135333 |
| Clearcoat and eye optics | 266 | XCoatEye-20261004-135334 |
| Skin texture/UV/lighting contracts | 254 | XSkin-20261004-135346 |
| Probe runtime lifecycle | 53 | TomProbes-20261004-215423 |
| Standard-sphere showcase | 109 | XShowcase-20261004-140502 |

Probe validation waited for `complete=true`, `passed=true`, not just the start
acknowledgement. It covers actual captures, layer/near-clip behavior, world-aligned
rotation, shared refresh scheduling, material replacement, cleanup, and preserving
the previous SH receiver/atlas until a complete matching replacement is ready.

Static suites: Test-XStageTwo, Test-XAlpha, Test-XHair, Test-XCoatEye,
Test-XEyeSurface, Test-XEyeMetadata, Test-XSkin, Test-XSkinMetadata,
Test-XSeriesMetadata and Test-ProjectIsolation. Additional numerical checks include
2,020 Hair samples, 192 coat-layer cases, 675 shallow-eye cases and 144 Skin algebra
cases. All 84 local shader includes resolve inside the independent X package.

The pre-existing duplicate AssetBundleBrowser GUID/import messages concern the
package/Assets copies outside X; they were not removed as part of this work.
One initial showcase dispatch preceded the new Editor assembly reload and returned
an unknown-operation result. The subsequent `20261004-closure-12-showcase` result
is successful; the failed attempt is retained rather than relabeled as a pass.
No fresh GPU timing benchmark was performed; the measured baseline in
`XSeriesGpuBaseline.md` remains the performance reference.

Bridge `20261004-closure-14-build` successfully rebuilt and reloaded all eight
carriers and updated tooltips. Twelve representative Skin variants warmed
successfully, with no reported shader errors. This is not exhaustive variant
coverage. Fresh and carrier indirect diffuse defaults were checked at 0.25.

## Showcase And Manual

`Editor/TomXShowcase.cs` renders the real X shaders, not artwork or a separate
approximate renderer. All 96 subjects use the same 128x64 UV sphere, diameter 1,
with consistent UV sets, vertex color and tangent data. There are 24 groups:
diffuse, shadow, specular, metallic/roughness, SH, environment, MatCap, coat, art
layers, normals, multiple lights, anisotropy, Hair Toon, Skin response, Skin masks
and liquid, EyeWX, procedural eye shapes, painted-depth angles, dispersion, IOR
isolation, same-angle optics controls, alpha, HairFront and combined materials.

Raw captures are 768x768. Exposure is fixed at 1.5, with one shared Reinhard then
display-gamma conversion, black background and no per-image normalization or
retouching. The source, camera angles, light descriptions, all material properties
and generated texture inputs are retained. Every image was included in the visual
contact-sheet review, with representative full-size boards additionally inspected.

SH display uses a known coefficient field to isolate directional diffuse, not
captured Studio GI. Shadow groups add a shadow-only occluder; alpha groups add a
checker background; HairFront adds a stencil writer. These fixture differences
are documented and are not evidence of additional subject geometry for eye optics.
Sphere images demonstrate features, not head-model fit or performance.

The portable gallery contains 24 four-panel 2048px boards, a 16-image overview,
four complete contact sheets, the 96 raw images, texture inputs, `report.json`,
offline `Gallery.html`, `UserManual.html` and a checksum receipt. Its material
settings are demonstration choices, not replacements for the shipped defaults.
The Chinese source manual is `TomShadersX-UserManual.zh-CN.md`.

## Reproduction

Run the ten PowerShell test scripts above. After Editor code changes, request
Bridge `refresh` and wait for compilation/domain reload before dispatching
`renderxshowcase`. The existing validation operations are `renderxstage2closure`,
`renderxalphamerged`, `renderxhair`, `renderxhairwidth`, `renderxhairstyle`,
`renderxcoateye`, `renderxskin` and `rendertomprobes`.

```powershell
& Assets/Mods/TomShadersX/Tests/Export-XShowcase.ps1 -ReportDirectory CodexBridge/Reports/XShowcase-20261004-140502
```

Export requires PowerShell 7 (`ConvertFrom-Markdown`) on Windows and System.Drawing.
It does not run the renderer. Open `Gallery.html` or `UserManual.html` directly;
there is no server or external CDN dependency.

For the eight-shader bundle use Bridge `buildxskin`, read the successful build and
reload result, then run `Tests/Package-XSkin.ps1`. The closing build retains version
0.3.1 because the audit only repairs metadata, documentation and Editor tooling.
The timestamped archive distinguishes it from earlier 0.3.1 builds. Do not install
multiple copies of the same package GUID together. No game install is updated by
this workflow. Unity native asset changes are made only through Editor APIs.

## Follow-Ups, Not Closing Blockers

- Preserve the current EyeX effects. Revisit repeated eye-optics work in ForwardAdd
  only when representative scene/device budgets justify an optimization; no
  iteration reduction or dispersion removal was made here.
- If true near-wall/far-content self-occlusion is requested, explicitly consider
  POM / first-visible-hit search. The current shallow single-valued solver does
  not promise it. Weak angle response alone is not sufficient reason to upgrade.
- HairX dual specular lobes remain the preferred future hair feature. Internal
  scattering, backlight transmission and anisotropic environment reflection are
  separate additions, not features silently implied by this showcase.
- Splitting EyeWX into anisotropic brow/eyeline and coated sclera entries remains
  deferred. Existing role-independent defaults are retained.
- In-game full-character/multiple-character GPU cost, SH capture stalls, other
  graphics backends/VR, arbitrary transparent intersections and unsupported stencil
  writers remain outside this audit. No new in-game audit was performed.
