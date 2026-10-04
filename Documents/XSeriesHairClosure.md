# HairX Feature Closure

Date: 2026-10-03. Scope: independent KKS X package, Unity 2019.4.9f1 Built-in.

Historical v1 closure below is retained. The subsequent
[coat/eye extension](XSeriesCoatEyeProgress.md) appends ten disabled-by-default
coat properties without changing the 110-property snapshot. Main package is now
0.2.0 and adapter 0.3.0 adds X eye mask writers and known-missing-pass fallback.

## Decision

Close the current HairX feature scope and preserve it as the v1 material baseline.
Proceed to shared Clearcoat, then EyeWX / EyeX design. This is a feature freeze, not a production release
or certification of every game, geometry and performance case. The main package
manifest remains 0.1.0; the optional feather adapter remains experimental 0.2.0.

No change to stencil, transparency, feather curves, lighting formulas or native
material/prefab assets is needed for this closure. The single public shader stays
`tom/HairX`. All 110 property declarations, including attributes, labels, types,
ranges and defaults, are now captured in `Tests/HairX-v1.properties.txt` and checked
by `Test-XHair.ps1`. Intentional future extensions require a reviewed baseline
update; the snapshot must not be regenerated merely to silence a failing test.

## Resolved Sclera Case

The user's final test replaces the sclera material `cf_m_sirome_00` with
`xukmi/EyeWPlus` at queue **2472** and reports that the internal eye artifacts
improve immediately. Earlier logs had recognized iris and eyeline writers at 2474
but no separate sclera writer; HairX was at 2475. Editor negative controls already
reproduced internal Feather bands by omitting only sclera from the proxy while
keeping its actual stencil draw. Complete source unions had no internal seams.

Together these support closing this reported case as a **writer compatibility
gap resolved by using the supported sclera shader**, not a reason to modify the
distance or opacity formula. The original sclera shader name is still unknown:
its own stencil is not proven defective, and no guessed adapter is added.
Hard mode remains a workaround for an unsupported mixed-material setup.

The screenshots and user report establish this specific in-game improvement.
They do not certify World Units mode, every Maker transition or GPU cost.

## Frozen Behavior

- Cutout cards with tangent/flow-map direction and a single anisotropic Toon lobe.
- Shared X lighting, full-shadow additional lights, indirect/probe/art layers.
- Ref 2 read-only HairFront: Off, Hard, optional inward Feather; default depth
  writing and unchanged stencil-independent light-space shadows.
- Pixels is the default for old materials. World Units is a projected width at
  the hair surface depth, not a distance along the scalp or a character scale.
- Base, Add and Outline share feather coverage; midpoint/power remain independent.
- Maker partial viewport support in adapter 0.2.0, without camera depth/color reads.
- Existing carrier identities, bundle path and 110-property interface preserved.

## Evidence Ledger

Report locations below are relative to the Unity project root. They are existing
render evidence, not newly rerun GPU tests during this documentation closure.

| Scope | Evidence | Result / boundary |
| --- | --- | --- |
| Legacy HairX and curve regression | `CodexBridge/Reports/XHair-20261003-090409` | 79 checks; 94 captures plus 2 diagnostics. All 96 PNGs match the pre-0.2 baseline. |
| Viewport, World/Pixels, multiwriter union | `CodexBridge/Reports/XHairWidth-20261003-094651` | 198 checks; 208 images: 152 camera captures and 56 field diagnostics. Prior 108 width images unchanged. |
| Anisotropy and Toon art controls | `CodexBridge/Reports/XHairStyle-20261003-063352` | 45 captures; 26 behavior and 90 finite/nonblank checks. Appearance coverage is not exhaustive. |
| Compilation | Hair and width report sweeps | 13 representative variants before/after, no reported shader errors; not every variant. |
| Specific game stencil issue | User report and screenshots, 2026-10-03 | Supported EyeWPlus sclera resolves the reported mismatch; original shader not identified. |
| Closure static regression | `Test-XHair.ps1`, `Test-XAlpha.ps1`, `Test-ProjectIsolation.ps1` | All passed after this change: 110 frozen HairX properties, 2020 curve samples, unchanged Alpha contracts and 54 local includes. No rendering formula changed. |

## Supported Setup

1. Put every intended Ref 2 writer before the earliest relevant HairX material.
   The observed setup is sclera 2472, iris/eyeline 2474, HairX 2475. These are
   compatible example queues, not values to force onto every imported character.
2. For Feather, use registered shaders for **all** relevant sclera, iris, eyeline
   and brow materials. Adapter 0.2.0 recognizes the six xukmi Eye/EyeW Plus,
   AlphaPlus and PlusTess names. A shader name outside that set needs verification.
3. Check discovery by renderer/material, not only a fixed writer count. Headmods
   can combine meshes, and `missingPass=0` does not detect unrecognized shaders.
4. With no eligible sources, the provider falls back to Hard. With some recognized
   sources and some unrecognized sources, it can still generate an incomplete
   field. There is no automatic complete-source validation or per-material fallback.
5. Use `HairFrontMode=2` and a positive selected width to test Feather. Enter the
   integer Mode value explicitly; fractional slider values are not extra modes.

## Delivery Boundary

Hard needs only the main shader mod. Feather 0.2.0 additionally needs matching
`TomHairFeather.dll` and adjacent `hairx-feather.unity3d` (field contract v2), from
`CodexBridge/Builds/TomHairFeather`. Install one copy, not several plugin versions.
Rebuild the main X zipmod for the updated shader/manifest/tooltips. Updating just
the DLL cannot add shader properties or update the tooltip catalog.

The earlier DLL/helper build succeeded. This closure does not build or install a
new zipmod, replace game files, rebind carriers, bump versions, publish a release,
or backport anything to KK. The tooltip clarification will ship with the next
main bundle build. See [the contract](XSeriesHairContract.md) for build operations.

## Explicit Remaining Gates

- Real game World Units camera motion, FOV/resolution/viewport switching, loaded
  cards, skinned animation, packed normal textures and authored flow maps.
- Multi-character ownership and occlusion-created stencil boundaries remain proxy
  limitations; completeness of writers alone does not solve them.
- Open hair-card inverted-hull outlines are geometry-limited. Camera-depth-based
  DOF/SSAO, MSAA, reflection cameras, plugin lifecycle and VR are not certified.
- Actual KKS GPU/frame profiling with 1/4/8 lights and multiple full characters.
  CPU Camera.Render fixtures do not pass this gate or prove the seven-pass layout
  is inexpensive. A later measured layout change must retain the public identity.
- Final packaged release acceptance. See the detailed [progress record](XSeriesHairProgress.md).

These are release-hardening tasks, not reasons to keep adding HairX features now.
The retained enhancement backlog starts with **stylized dual specular lobes**,
then separately evaluates transmission, anisotropic probe IBL and scattering.
None is silently removed or made a prerequisite for the eye work.

Next: [shared Clearcoat extension](XSeriesClearcoatDesign.md), then
[EyeWX and EyeX design](XSeriesEyeDesign.md), supported by the
[eye optics research](XSeriesEyeOpticsResearch.md). These future extensions keep
the v1 declaration snapshot as a compatibility reference, not an editable target.
