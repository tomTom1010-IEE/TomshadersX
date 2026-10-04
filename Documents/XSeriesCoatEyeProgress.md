# Clearcoat and Eye Implementation

Date: 2026-10-03. Experimental package 0.2.1; optional HairFeather adapter 0.3.0.
Target: KKS, Unity 2019.4.9f1, Built-in Forward, D3D11. This records implemented
behavior, actual Editor evidence and separately identified user game acceptance.

Update 2026-10-04: [EyeWX/EyeX v1 feature closure](XSeriesEyeClosure.md) records
the user's working painted-depth/angle response, a repaired metadata synchronizer,
frozen property contracts and a fresh 266-check GPU run. Its 180 PNGs match the
preceding 0.2.1 report exactly. No runtime shader or built package changed.
True height-field self-occlusion remains a conditional POM/first-hit follow-up,
not a missing v1 feature. The user subsequently confirmed all scoped
non-performance KKS game acceptance complete on 2026-10-04; only the performance
gate below remains open for this eye scope.

## Delivered Scope

- One `TomClearcoat.cginc` implementation feeds all seven public X shaders.
- The five older entries keep their existing properties and defaults, with ten
  identical coat properties appended. Their frozen baseline snapshots were not
  regenerated. No old material is automatically migrated or enabled.
- `tom/EyeWX` provides eye-area coverage, X lighting and optional coat. There is
  one role-independent configuration, not automatic sclera/eyeline/brow presets.
- `tom/EyeX` adds game iris/highlight/expression inputs and optional local iris
  refraction, with optional RGB dispersion. Pupil art stays in MainTex.
- Coat, eye optics and dispersion are disabled by default. No POM, GrabPass,
  camera depth, extra corneal mesh or optics plugin is used.
- Main package GUID and bundle path remain `tom.Shaders.X` and
  `chara/tom/shaders/tomx.unity3d`. The optional feather plugin remains separate.

## Shared Coat

`TomClearcoat.cginc` owns neutral dielectric GGX, its roughness, interface normal,
Fresnel and layer weights. `TomToonLighting.cginc` integrates the layer into the
existing light/probe path rather than copying it into individual shaders.

The ten properties are `_ClearCoat`, `_ClearCoatRoughness`, `_ClearCoatIOR`,
`_ClearCoatMap`, `_ClearCoatNormalSource`, `_ClearCoatNormalMap`,
`_ClearCoatNormalScale`, `_ClearCoatEnvironmentStrength`, `_ClearCoatEnergyBlend`
and `_ClearCoatDebugView`. Full defaults are in the [design](XSeriesClearcoatDesign.md)
and `Tests/Clearcoat-v1.properties.txt`.

Base evaluates direct and environment coat. Each Add pass evaluates only that
light's direct coat, including shadow, distance and cookie attenuation. Coat
environment uses the existing Unity probe lookup independently of substrate
ReflectionMode, tint and reflection mask. IOR 1 produces no coat reflection.

The substrate direct specular uses incoming/outgoing transmission weights;
diffuse attenuation is blended by EnergyBlend. Indirect attenuation is an
approximation, and emission receives outgoing transmission. Existing additive
Matcap and Rim remain art layers. This is not exact layered microfacet transport,
spectral rendering or a physical wet-hair fiber model.

Coat normal selection uses the original surface inputs. In EyeX, refracted iris
normals do not feed back into the interface or coat. Weight 0 bypasses coat
lighting and coat-map fetches; optics can still require the interface normal.
No new color pass, alpha operation or HairX coverage multiplication is added.

`_ClearCoatMap` has independent ST; R is weight and G is roughness. It shares the
MainTex sampler state. The independent coat normal has its own ST and scale, but
shares the NormalMap sampler. These deliberate sampler-budget constraints mean
independent filtering/wrap settings on those two coat textures are not honored.
Import the control map as linear data and the normal map as a Unity normal map.

## Eye Inputs and Coverage

The eye-specific modules are `TomEyeInput`, `TomEyeCoverage`, `TomEyeOptics` and
`TomEyeForward`, with small shared-core hooks. Original UV1/UV2 highlight inputs
are packed into an eye-only varying; existing family varyings are unaffected.

The local EyePlus/EyeWPlus sources were inspected for input behavior, without
adding a runtime include/dependency on V+:

- `_rotation` is a clockwise normalized turn about UV 0.5, before MainTex ST.
- `_overtex1/2`, `_overcolor1/2` and `_isHighLight` retain separate original
  UV1/UV2 highlight inputs and screen-style composition. They are Base-only
  artwork; Add is attenuated by overlay coverage and cannot repeat that artwork.
- Expression retains its legacy view shift, MainTex transform, size and offset;
  `_expression_ST` is intentionally unused, matching that source path.
- EyeWX `_Color` is neutral at 0.5 and is applied as `2 * _Color` under X lighting.
  This normalization is an X policy, not a promise of pixel-identical V+ lighting.
- Unwarped composite alpha controls color and stencil. Refraction/dispersion
  move interior RGB/normal sampling only; coat reflection cannot fill alpha holes.

| Entry | Default queue | Color behavior | Stencil behavior |
| --- | --- | --- | --- |
| EyeWX | 2472 | Cutout, cutoff 0.5, Base unblended; additive lights RGB One/One | Named `StencilMask`, Ref 2 Replace, shared Cull and ZTest LEqual |
| EyeX | 2474 | Cutoff 0, SrcAlpha/OneMinusSrcAlpha Base; SrcAlpha/One RGB Add | Same dedicated mask, default StencilCutoff 0.5 |

Both have ZWrite Off, color-pass stencil Keep, and no Outline or ShadowCaster.
StencilMask is a separate ColorMask 0 Always pass. Queue/cutoff remain explicit
material controls; existing character materials are not rewritten.

The adapter recognizes `tom/EyeWX` and `tom/EyeX` and replays only their named
mask pass. The original six registered xukmi names remain supported. If a known
visible registered source lacks its required pass, the whole affected provider
falls back to Hard instead of feathering an incomplete known union. Unknown,
unregistered writers are still not detectable from this registry: a mixed set
can remain incomplete. All intended writers must precede the relevant HairX.

## Refraction and Dispersion

EyeX reconstructs a local world/UV differential chart from surface derivatives,
refracts the view ray at the original interface normal, and intersects a virtual
recessed tangent plane. IrisDepth is in local iris-radius units, not screen
pixels or absolute meters. Uniform world scaling cancels in the chart. This is
a local single-interface approximation on the existing curved surface, not an
anatomical lens or a new visible corneal shell.

Since 0.2.1, `_EyeRefractionIOR` owns rays and dispersion independently of
`_ClearCoatIOR`; the unwarped interface normal is still shared. Refraction IOR 1
removes bending but retains depth parallax. Non-default old enabled-optics
materials need their former coat IOR copied once into Eye Refraction IOR.
Zero mode, strength or depth preserves original sampling. Degenerate charts and
invalid safe UV bounds fall back to the original image.

The default flat hit is limited by a maximum offset, grazing fade, iris-edge fade and safe
atlas rectangle. GPU inspection exposed white interior crescents at large depth
and oblique angles; an additional radial aperture bound now keeps the displaced
interior within its own iris, not merely within the texture rectangle.

Package 0.2.1 adds optional shallow cone/bowl profiles and painted height/depth
regions. Both use the existing IrisDepth as global gain. Map mode replaces the
procedural ellipse/edge cap and follows final iris UV; the original flat path
remains default. See [surface map contract and limits](XSeriesEyeSurface.md).

Sampling uses explicit gradients. The local chart is held constant while
view/normal/UV derivatives are propagated through the ray; the shader does not
take derivatives of a hit already constructed from derivatives. With dispersion,
red and blue gradients are propagated too. Their maximum stretch relative to the
green-ray ellipse determines one shared footprint, retaining anisotropy and using
an isotropic fallback only for a singular green Jacobian. This avoids an abrupt
blur when enabling a small dispersion value, but costs more ALU than the
single-ray mode. Main/detail interior
normals follow the green-ray UV with their own ST; coverage remains unshifted.

Dispersion adjusts RGB IORs around the independent refraction IOR, using a small
`(IOR - 1) * strength / 40` spread. It uses three interior color fetches and one
lighting evaluation, not three full shaded eyes. It is approximate RGB color
separation, not spectral or wave-optics simulation; default strength is 0.

## Demonstration Controls

To test coat alone, raise Clearcoat Weight with optics disabled. Start near
roughness 0.1; environment requires a useful reflection probe. Substrate
SpecularStrength/ReflectionMode do not need to be enabled for coat to work.

To test refraction alone, enable Eye Optics and leave Clearcoat Weight at 0.
Set Iris Center/Radius in final MainTex UV space and start with depth 0.08.
Set a safe rectangle for atlas textures. Enable coat separately when the interior
motion is satisfactory, then increase dispersion gently if desired.

The synthetic fixture deliberately uses IrisDepth 0.25 and IOR 1.376 to make
angle-dependent movement legible. These are demonstration values, not installed
defaults or an automatic eye preset. The public defaults are depth 0.08 and IOR
1.5, with the relevant effects disabled.

## Surface Extension Evidence

Final 0.2.1 report: `CodexBridge/Reports/XCoatEye-20261003-142132/report.json`.
266 checks passed, 167 GPU captures, six contact sheets and seven raw data-map
templates (180 PNG files total). All float captures were finite. The seven-entry
representative compilation sweep reported no shader errors or warnings. The
Editor enum drawer was also checked after replacing the overlong inline enum
with an Editor-only named enum; no new runtime dependency was added.

- Coat-IOR changes produced exactly zero UV-shift error in flat, cone, bowl and
  mapped modes. Dispersed iris RGB with coat disabled was also unchanged.
- Raising refraction IOR reduced central parallax in all four modes; changing
  it left the isolated coat reflection unchanged.
- Painted/procedural interior shift mean error was 0 for flat, 0.000188 for cone
  and 0.000116 for bowl. Quantized templates and procedural edge guards are not
  promised identical over the whole iris boundary.
- Map alignment under rotation plus nonuniform/mirrored MainTex ST passed.
  Moving the procedural ellipse away from the custom mask changed no mapped UV.
- Both sources share IrisDepth; 0.06 to 0.12 doubled central flat displacement.
  Zero gain/strength, white height and zero A preserve unwarped RGB.
- Original alpha and actual stencil were unchanged with painted optics enabled.
  Nested X writer/feather-union checks and missing-pass fallback still passed.
- Map-mode Base vs matching Add light mean pixel error: 2.64e-11. Dispersion
  strength 0.00001 vs zero error: 5.25e-8.
- Painted shift half/double-resolution errors: 0.002429 / 0.000614; doubled-world
  scale error: 1.77e-9. Same central-ray distance/FOV errors: 0.000582 / 0.000639.
  Orthographic distance, Maker-style viewport/restore and grazing views passed.

The strict 0.2.0 PNG comparator reports 80/81 images byte-identical. The one
exception is `eye-256-shift.png`, with one of 262144 pixels differing by one
8-bit level. Its decoded pixel difference was inspected; the strict test was
not weakened or the baseline regenerated. All old final-color comparison PNGs
are identical. The fixture explicitly copies its old 1.376 optical IOR into
EyeRefractionIOR, as required by the migration note.

Static CoatEye, EyeSurface, Alpha, Hair and ProjectIsolation suites passed.
675 shallow CPU root-reference cases passed, with worst error 6.44e-8; 72 local
shader includes stay within this package. This does not replace game acceptance.

New report images:

- `surface-procedural-shapes.png`: columns flat/cone/bowl; rows -40/0/+40 degrees.
- `surface-painted-shapes.png`: the same layout using grayscale height maps.
- `surface-custom-comparison.png`: optics off / asymmetric painted region /
  painted region plus clearcoat.
- `template-*-height.png`, `template-*-depthRA.png`: raw linear-data starters,
  not tone-mapped screenshots; see `XSeriesEyeSurface.md` before importing.

Bridge `buildxcoateye` succeeded in StrictMode for the 0.2.1 bundle. Existing
AssetBundle Browser duplicate-GUID import errors elsewhere in the project still
appear during refresh/build; they were not changed and did not fail this build.

## Initial Unity Evidence

All paths below are relative to the Unity project root. Initial 0.2.0 coat/eye report:
`CodexBridge/Reports/XCoatEye-20261003-124639/report.json`.

- 132 checks passed, 78 GPU captures. All float outputs were finite.
- Seven shaders compiled and were supported, with no reported compiler warnings
  or errors in the representative validation sweep. This is not all variants.
- Tested independent coat controls, zero weight/map, IOR 1, Base/Add ownership,
  direct shadows/cookies, oblique refraction, dispersion and original coverage.
- Dispersion strength 0.00001 converged to its zero baseline with mean pixel
  difference 2.08e-8, guarding against a filter/mip discontinuity at enable time.
- Actual stencil output, nested EyeX/EyeWX union and missing-mask Hard fallback
  were rendered. A nested iris did not introduce an internal feather boundary.
- Tested orthographic near/far, 256/512/1024 resolutions, uniform world scale,
  central rays across FOV changes, partial viewport and restoration.
- Normalized UV-shift image error was 0.002618 at half resolution and 0.0006434
  at double resolution; uniform-scale error was 3.14e-9. Same central-ray FOV
  error was 0.0004937. These are fixture image metrics, not optical accuracy.

| Legacy suite | Pre-change baseline | Current comparison | PNG SHA256 result |
| --- | --- | --- | --- |
| Opaque | `XStageTwo-20261003-115623` | `XStageTwo-20261003-122325` | 22/22 identical |
| Alpha family | `XAlphaMerged-20261003-115720` | `XAlphaMerged-20261003-122315` | 209/209 identical |
| Hair | `XHair-20261003-115721` | `XHair-20261003-123045` | 96/96 identical |

The 327 PNGs include contact/diagnostic images. Equality is exact for these
fixtures with coat off, not a proof over all possible materials and scenes.
`Compare-XCoatBaseline.ps1` writes the comparisons into the candidate report
directories. Alpha's existing merged/prepass comparison also passed.

`CodexBridge/Reports/XHairWidth-20261003-123046` passed 198 checks with 208 images,
covering World/Pixels widths, camera/FOV/resolution/viewport changes, shared
Base/Add/Outline coverage and source unions. Static Alpha, Hair, CoatEye and
ProjectIsolation tests passed; 71 shader includes remain inside this package.

The rendering machine was Unity 2019.4.9f1 / D3D11 / RTX 4070 Ti SUPER, with the
project in Gamma color space. PNGs share float readback, x1.5 exposure, Reinhard
and display-gamma processing, with no per-shot normalization. They demonstrate
relative behavior, not calibrated KKS color. Metrics use unmodified float pixels.

In the final report folder:

- `clearcoat-comparison.png`: left off, center roughness 0.08, right 0.35.
- `eye-angle-comparison.png`: rows -40/0/+40 degrees; columns original,
  refraction only, refraction plus coat. One curved continuous-UV surface, no
  extra corneal geometry. Procedural iris art and synthetic HDR softboxes.
- `dispersion-comparison.png`: strength 0 and 1 at the same angle/settings.
- `eye-final-perspective.png`: combined result from a perspective camera.

These are actual Editor renders through CodexBridge, not generated mockups.
The fixture creates a temporary additive scene and restores prior scene/light/
quality/render-target state on completion. It does not edit the user's scene.

## Build and Reproduce

Bridge operations: `validatexcoateye`, `preparexeyes`, `renderxcoateye`,
`buildxcoateye`. Run `refresh` after source edits and wait for script reload before
issuing the operation. `preparexeyes` creates missing native carriers through
AssetDatabase/PrefabUtility and preserves existing ones.

`buildxcoateye` builds only the seven X carriers and tooltip catalog, with a
cross-package dependency guard. All use actual custom shaders, so no legacy
preview-shader remapping/postprocess is needed. Output is
`CodexBridge/Builds/TomX-0.2.1/abdata/chara/tom/shaders/tomx.unity3d`.
`Tests/Package-XCoatEye.ps1` adds manifest.xml to make a timestamped 0.2.1 zipmod.

The optional provider DLL was compiled as 0.3.0 and its helper bundle rebuilt in
`CodexBridge/Builds/TomHairFeather`. Install the DLL together with adjacent
`hairx-feather.unity3d` only when Feather is needed; keep one version, not duplicate
providers. Hard mode, coat and eye optics do not require it.

Nothing was installed into the game, committed, pushed or backported to KK.
V+ sources were read for compatibility only and not modified.

## Game Acceptance Update (2026-10-04)

The user confirmed all scoped non-performance in-game acceptance complete.
The earlier material/card lifecycle, eye bindings/animation and eye-to-hair
integration checks are closed for their tested KKS setups. This is user-reported
acceptance, not a new automated test run or universal head-mod certification.
No mesh-role classification, automatic optical-center calibration or support
for previously unsupported writers is implied.

## Remaining Performance Acceptance

- Local Editor timing and multi-light/proxy-count baseline completed on
  2026-10-04: [measurement, controls and recommendations](XSeriesGpuBaseline.md).
  The accepted run has 217 cases and 13,020 GPU timestamp samples.
- Full-character target-game/device cost and variant/register/sampler budgets
  remain unqualified. Disabled image equality does not prove zero instruction cost.

Linear color space, VR/stereo and KK remain outside the accepted KKS scope,
not additional blockers for this closure. Feature and scoped game acceptance
are complete; the package remains experimental pending performance qualification.

## Optimization Priority Decision 2026-10-04

The user chose to retain the current appearance and effects. EyeX multi-light
optimization stays on the follow-up list and does not block current closure or
SkinX planning. The synthetic benchmark is diagnostic evidence, not proof that
eyes dominate a typical game frame. No quality reduction or shader edit follows
from this decision. Whole-game/device certification remains separate.
