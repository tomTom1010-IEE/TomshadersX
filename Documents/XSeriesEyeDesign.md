# EyeWX and EyeX Design

Date: 2026-10-03. Status: implemented experimental eye pair, not a frozen eye ABI
or completed game acceptance. Target: KKS, Unity 2019.4.9f1, Built-in Forward, D3D11.
See [implementation evidence and deviations](XSeriesCoatEyeProgress.md) for current
rendered results, sampler/gradient policy and the remaining game validation gates.

## Two Step Dependency

Step one is [shared Clearcoat](XSeriesClearcoatDesign.md): add one optional outer
reflection layer to the X baseline and all five existing public shaders, retaining
their disabled appearance. Step two is this eye pair, built on the validated shared
layer. Do not ship an eye-only coat and later migrate another implementation into
the core. [HairX v1](XSeriesHairClosure.md) remains the prior accepted feature scope.

Eye delivery starts with KKS input/coverage compatibility, then adds local analytic
iris refraction, followed by optional RGB dispersion. [Research](XSeriesEyeOpticsResearch.md)
records algorithm alternatives and limitations. No extra corneal/tear mesh, POM,
GrabPass, camera-depth texture or optics runtime plugin is required. This is
shader-only development on existing geometry, including the user's primary case
of continuous iris UVs on approximately spherical surfaces.

The design does not identify a material's role from a mesh name, renderer name,
normal distribution or texture color. One EyeWX default serves all its uses.
Separate sclera/eyeline/brow presets, automatic application and a role classifier
are not requirements. The user controls gloss, cutoff and render queue explicitly.

Implementation is isolated to the X family; V+ LTS, KK and the feather formula
remain unchanged. Eye color/optics and HairFront Hard work without a helper;
only HairFront Feather needs the optional provider and its writer registration.

## Public Family and Defaults

| Entry | Responsibility | Default and boundary |
| --- | --- | --- |
| `tom/EyeWX` | Eye-area cutout color/normal, X lighting, shared optional Clearcoat and explicit stencil coverage; usable on sclera, lines or brows. | One role-independent configuration, ClearCoat=0. No iris optical properties or automatic role switches. |
| `tom/EyeX` | Iris/pupil artwork and game overlays, shared optional Clearcoat, bounded local refraction and optional dispersion. | ClearCoat=0, optics Off, dispersion 0. No separate PupilX, ReflectX or StudioX shader. |

Use metallic 0 and a diffuse-dominant new-eye baseline: substrate SpecularStrength
0 and ReflectionMode Off are the defaults for both new entries. Legacy painted
highlights remain controlled by the game. Users can enable shared coat or substrate
reflection deliberately; switching on coat must not automatically rewrite those
other saved controls. These are new shader-wide defaults, not role-dependent presets.

All ten Clearcoat declarations and defaults match step one exactly. A wet EyeX
uses that layer as its corneal response, not as a third reflection layer. Iris
details remain in the substrate. Pupil artwork stays in the existing iris texture;
v1 adds no competing pupil-size controller or automatic light-driven dilation.

EyeWX works without EyeX. EyeX works with recognized xukmi sclera/eyelines. A mixed
family is a supported configuration, not a mandatory whole-character migration.
Input compatibility means preserving game controls, not pixel-identical V+ lighting.

## KKS Compatibility Before Optics

The existing local EyePlus/EyeWPlus shaders provide compatibility evidence, not
code to include from the V+ package. Preserve the following behavior where the
game or installed integrations depend on it; confirm actual runtime setters in
the first implementation milestone before freezing aliases and defaults.

| Observed input | Required investigation / preservation |
| --- | --- |
| `_MainTex` and ST | Maker-generated iris/sclera textures, offset and size. Do not require users to repaint existing eyes. |
| `_rotation` | EyePlus rotates UV0 about 0.5 before MainTex ST; retain direction and normalized-turn units for this binding. |
| `_overtex1`, `_overtex2`, `_overcolor1`, `_overcolor2`, `_isHighLight` | Two legacy highlight layers use separate UV1/UV2. Keep their runtime visibility/tint control; do not replace them with probes. |
| `_expression`, `_exppower`, `_ExpressionSize`, `_ExpressionDepth` | Expression overlays and their legacy mapping. Avoid applying the old expression offset and new iris refraction twice. |
| EyeW `_Color` | Preserve game-driven color independently of author BaseColor; verify color-space and neutral defaults, avoiding double tint/gamma conversion. |
| Existing texture alpha | Preserve visible holes and source coverage. A bright RGB reflection must not turn a transparent texel into a stencil writer. |

Audit base-game material setters, MaterialEditor, eye overlays, card reloads,
heterochromia, gaze and blink before claiming drop-in compatibility. Legacy
lighting globals are not automatically required bindings: keep X light/shadow
ownership instead of inheriting every old ambient or reflection control.

Extend eye varyings deliberately: `TomVert` currently passes UV0 and uses UV1/UV2
for GI/lightmap data, not two raw eye-highlight UVs. Eye-specific varyings must
preserve those game UVs without overwriting GI coordinates or silently changing
Opaque/Alpha/Hair varyings. Shader sampling compatibility alone is insufficient.

Pack the two raw overlay UVs in one additional float4 where the compiled
interpolator budget permits; compile all lighting/shadow variants before freezing
the layout. Do not consume UV4 if a future optional Outline uses that channel.

The locally inspected references are
`Assets/Shaders/Custom/MaterialEditor/xukmi/xukmiEyePlus.shader` and
`xukmiEyeWPlus.shader`. Their property blocks/render states are evidence only.
The game-setter audit must settle EyeW color normalization, expression composition,
overlay alpha and texture lifetime before a compatibility claim. Record the actual
methods/properties tested; do not infer runtime behavior solely from a label.

## Composition and Shared Architecture

Reuse the X `TomRawLightData`, shadow visibility, Toon transfer, BRDF and probe
utilities. Add small eye input/coverage/optics/forward adapters. Do not fork the
whole `TomToonLighting.cginc` or compile against legacy eye includes.

| Planned module | Ownership |
| --- | --- |
| `TomEyeInput.cginc` | Game aliases, overlay varyings, eye parameters and runtime-neutral defaults. |
| `TomEyeCoverage.cginc` | Unwarped game composition/alpha and common color/stencil visibility. No new optical ray or probe lookup. |
| `TomEyeOptics.cginc` | Chart mapping, local refracted ray, validity/bounds and optional RGB separation. EyeX only. |
| `TomEyeForward.cginc` | Material assembly, Base/Add entry points and Base-only legacy highlight composition; calls shared lighting and coat. |
| `TomClearcoat.cginc` | Step-one shared outer reflection, roughness, normal and energy weighting; no eye fork. |
| Optional provider writer table | Exact X shader names and StencilMask pass; legacy names preserved. |

Refactor common functions into parameterized helpers only as needed. Before each
such refactor, keep a coat-off and coat-on regression image set from step one.
Use separate substrate and coat records. Evaluate raw light and physical shadow
once per light, then responses with their respective normals and horizon guards.

| Color contribution | Base | Add |
| --- | --- | --- |
| Iris/EyeWX direct body and optional substrate specular | Main light | Current additional pixel light |
| Shared coat direct reflection | Main light | Current additional pixel light |
| Substrate indirect/vertex diffuse, substrate probe and coat probe | Once, with shared ownership/retention | Never |
| Diffuse MatCap Multiply | Same substrate tint | Same substrate tint |
| Additive MatCap, Rim, emission, game painted highlights | Once in their documented composition order | Never repeated |

Treat legacy highlight overlays as explicit surface art after the shared layer
composition, before final alpha/fog. Their game alpha still participates in the
unwarped coverage record where the legacy contract requires it. Expression color
belongs to the interior/game composition, not an additional coat reflection.
Keep optional reflection and legacy glints independently controllable; do not
erase a game texture to avoid a double glint. Baked glints already in MainTex
cannot be removed automatically.

### Shared Coat and Local Refraction

Use all step-one mask, energy, normal and probe rules without reinterpreting them.
Shared Specular/Roughness/Reflection controls still describe the substrate;
ClearCoat controls describe the outer interface. This supersedes the earlier
idea of reusing substrate specular controls for the cornea.

Use the resolved coat normal for the simplified EyeX interface. Since package
0.2.1, interior rays/dispersion use independent `_EyeRefractionIOR`, while
`_ClearCoatIOR` owns reflection/energy. This art control is not a model of tear
film, cornea and aqueous humor separately. See [surface-map extension](XSeriesEyeSurface.md)
for its controls and migration from the originally linked IOR. A demonstration
IOR near 1.35 is not a measured-anatomy claim or an automatic material preset.

Reflection weight does not control refraction strength. Coat=0 plus optics On
still resolves the interface normal/IOR but skips the reflective BRDF/probe.
Optics Off plus coat On remains a valid glossy eye. The coat mask affects only
reflection, not the aperture or optical-depth mask. EyeWX has no optical work.

The two shading layers remain inside existing color passes on existing geometry.
No added transparent shell, no extra coat pass, and no new alpha/stencil behavior.

## Render State and HairFront Contract

Proposed v1 states follow the existing eye layering, with one explicit writer:

| Pass / material | Proposed state / responsibility |
| --- | --- |
| EyeWX Base / Add | Cutout, ZTest LEqual, ZWrite Off. Base unblended; Add One One for RGB with ColorMask RGB. |
| EyeX Base | Straight alpha, ZTest LEqual, ZWrite Off; SrcAlpha OneMinusSrcAlpha for RGB. Preserve legacy destination-alpha behavior until the E0 audit confirms the required convention. |
| EyeX Add | SrcAlpha One for RGB, ColorMask RGB; same source alpha as Base, not another premultiplication in shader. |
| `StencilMask` on both | Named unlit `LightMode=Always` pass, ColorMask 0, ZTest LEqual, ZWrite Off, Ref 2 / Always / Replace, existing full masks; same Cull and skinned geometry as color. |
| Other color passes | Stencil Keep; no second writer at a different alpha threshold. |
| Outline / ShadowCaster | Excluded from the first eye release; ordinary eye/line geometry supplies its visible contour. No fallback shader that silently introduces these passes. |

The intended StencilMask runs once before Base in the normal eye draw sequence
and is the only pass replayed by the proxy. It adds a real geometry submission;
it is not free. Validate normal execution exactly once with frame capture and
pixel tests before accepting the pass layout. Its LightMode must execute in
Built-in. The provider may explicitly draw it even if the game does not, so a
working proxy alone is not proof of Hard-mode correctness.

Shared coverage record `a` is the established unwarped eye coverage, independent
of new optical RGB. For EyeWX it derives from MainTex alpha and AlphaMask. For
EyeX it derives from compatible iris/expression/highlight alpha plus AlphaMask.
Both receive a material `_Alpha` multiplier defaulting to 1; zero is discarded
in every pass. EyeWX remains cutout, so lowering this multiplier changes survival,
not partial color blending. Use the same runtime material/property-block values
in the ordinary and proxy draws.
Both actual and proxy call the same eye coverage function:

```text
color survives:   a > epsilon and a >= Cutoff
stencil survives: color survives and a >= StencilCutoff
```

StencilMask must include required game UVs and view-dependent *legacy* expression
mapping if that contributes alpha. Do not approximate it as just MainTex alpha.
New refraction, dispersion, probe intensity and corneal gloss do not alter `a`.
This keeps the new optical effect from moving HairFront boundaries; gaze, blink,
game expression changes and real mesh motion can still change coverage normally.

If a calibrated optical asset needs a larger independent aperture than its
painted iris alpha, design an explicit coverage texture mode with matching color
and stencil, not a global fill of transparent holes. That is a separate asset
workflow; legacy cards keep their coverage by default.

Use Ref 2 and existing masks, not a new stencil bit layout. Proposed shader-wide
defaults: EyeWX 2472, EyeX 2474, existing HairX 2475. They provide an initial order,
not automatic role recognition. A user applying EyeWX to an eyeline may need a
material queue override such as 2474; no shader can infer this reliably from the
mesh. Keep every intended writer strictly before its HairX receiver, and warn
about unsupported ordering rather than rewrite it. Existing tests used sclera
2472 and iris/eyeline 2474; overlap correctness still needs real-card validation.

Proposed cutoffs: EyeWX `_Cutoff=0.5`, EyeX `_Cutoff=0`, both
`_StencilCutoff=0.5`, with a shared epsilon discard. Thus EyeX can retain a smooth
visible alpha fringe without treating every faint texel as a stencil writer.
Use max(color cutoff, stencil cutoff) for stencil survival; these thresholds are
material controls, not inferred roles. E0 must compare the default fringe against
the reference game shaders and document any final pre-freeze adjustment.

Register `tom/EyeWX` and `tom/EyeX` in the optional runtime adapter only when their
passes exist. Keep the six legacy xukmi names. Bump/build the adapter for that
integration rather than pretending 0.2.0 already discovers X eyes. Add regression
for mixed X/xukmi sources and missing sclera. Shared coverage improves source
agreement, not the existing proxy's occlusion or cross-character ownership limits.

## Optical Coordinate Contract

The public chart is in final MainTex UV after the preserved game rotation and ST,
not screen UV and not object coordinates. This supersedes the earlier pre-ST chart
candidate and keeps calibration tied to the actual iris artwork.

```text
u = M * rawUV0 + b                 // existing rotation then MainTex ST
q = (u - IrisCenter) / IrisRadiusXY
```

IrisCenter and RadiusXY are UV texture coordinates; `q` is dimensionless and its
unit circle bounds the optical region. Preserve negative ST and rotation signs.
After computing an optical displacement `dq`, sample MainTex RGB at
`uHit = u + IrisRadiusXY * dq`, without applying ST again. For an interior normal
map with its own ST, invert M to recover hit rawUV0, then apply that map's ST once.
Legacy highlight UV1/UV2, surface masks and coat maps retain their own coordinates.

Maintain a separate unwarped composition for coverage and a warped interior RGB
composition. Required legacy expression view mapping remains in both where its
contract requires it; do not apply its old offset twice. Refraction initially
moves iris RGB and optional iris normal detail only. Non-circular expression
overlays keep their existing mapping rather than being forced into radial artwork.

## Local Analytic Refraction

Use a locally recessed iris layer on the existing curved mesh, with a bounded
differential chart. It approximates interior depth without pretending that a
shared head renderer origin identifies two eye centers. It is not a reconstructed
global anatomical lens or a ray search through a height field.

For the primary prototype, recover local world-space chart directions from the
interpolated world position P and q. Compute derivatives before divergent branches:

```text
D = qx.x * qy.y - qx.y * qy.x
Ju = (Px * qy.y - Py * qx.y) / D
Jv = (Py * qx.x - Px * qy.x) / D
```

Here Px/Py mean ddx(P)/ddy(P), and qx/qy mean ddx(q)/ddy(q). Project Ju/Jv
into the tangent plane of the oriented smooth mesh normal Ng. Let J have these
two projected columns and G = transpose(J) * J. The local reference iris radius
is `sqrt(length(Ju) * length(Jv))`; set dWorld = IrisDepth times this radius.

```text
V = normalized surface-to-camera direction (parallel camera forward for ortho)
T = refract(-V, coatInterfaceNormal, 1 / EyeRefractionIOR)
t = -dWorld / dot(T, Ng)
deltaWorld = t * (T - Ng * dot(T, Ng))
dq = inverse(G) * transpose(J) * deltaWorld
```

Accept inward, nonzero finite T, finite t>=0, and a well-conditioned D/G. Preserve
the denominator sign. Use scale-relative degeneracy checks, not a fixed derivative
threshold that disables the effect when resolution changes. Regularize or fade
bad conditioning; never normalize a zero vector. This construction supplies the
UV-to-world scale explicitly instead of multiplying a UV radius by unrelated
world-space components. Uniform mesh scaling cancels from dq.

Bound dq to EyeOpticsMaxOffset, fade its influence near the unit-circle edge,
then blend by EyeOpticsStrength. Use a smooth grazing fallback before the inward
denominator becomes unstable. Invalid charts/rays return the original RGB mapping
with a diagnostic color available, not NaNs, texture wrapping or a guessed lens.

This is still a local first-order approximation: coarse/skewed UVs and large
offsets can expose faceting. E1 compares orbit captures against the cheap flat
parallax fixture; lower the supported depth range or reject the prototype if its
temporal behavior is poor. Do not silently substitute screen-space offsets or
require another mesh. Optional virtual bulge and a global iris-bowl intersection
are not v1 requirements; existing sphere-like geometry supplies the surface shape.

Depth is measured in local iris-radius units, not pixels or anatomical millimeters.
For the same surface point and view ray, FOV/resolution/viewport changes must not
change the mathematical mapping; finite-difference/filtering errors are measured
separately. Camera movement legitimately changes the ray. IOR=1 removes bending,
not depth parallax. Depth=0, strength=0 or optics Off must give the original UV.

## Bounds Filtering and Dispersion

Use a user-visible safe texture rectangle, default 0..1, to protect atlas layouts.
Inset it for the actual filter footprint and mip; validate bounds and radii before
sampling. A full-texture clamp does not protect an interior atlas island. If the
requested footprint cannot fit, fade to the original mapping and report it in
debug rather than sample a neighboring eye. Clamp the actual fetched address as
well as fading the result. The unwarped source remains the compatibility fallback.

Use explicit gradients for optical samples. During E1, propagate first derivatives
of the input UV, view vector and normal through a locally linearized ray mapping,
holding J constant within that local estimate. Do not blindly take ddx/ddy of a
hit UV that already depends on derivatives, or of a divergent fallback result.
A conservative source-gradient footprint is the bounded fallback when the optical
gradient is ill-conditioned. All three dispersion samples use a shared conservative
footprint. Grazing views, minified eyes and changing skin deformation are
acceptance cases; optics Off retains the original sampling path.

Optional E2 dispersion varies the effective interface IOR for three interior
RGB rays. Keep the central IOR/normal for one shading evaluation and all alpha:

```text
spread = (EyeRefractionIOR - 1) * EyeDispersion / 40
nRGB = (max(1, IOR-spread), IOR, IOR+spread)
C = (IrisRGB(uRed).r, IrisRGB(uGreen).g, IrisRGB(uBlue).b)
```

Use a common validity/edge fade for all channels so one invalid ray cannot create
a colored seam. Zero dispersion takes the single-ray path. It is a geometrical
RGB approximation, not a wavelength simulation, rainbow highlight, screen-wide
chromatic aberration or thin-film model. It may be subtle on normal iris textures.
Neither shifted RGB alpha nor a bright reflection may expand the stencil aperture.

## Proposed Eye Controls

The table records the initial design. The current [0.2.1 extension](XSeriesEyeSurface.md)
adds independent IOR, optional height/depth maps, cone/bowl profiles and debug 7/8.
Numeric defaults are conservative values pending real-game acceptance.
Clearcoat uses the identical step-one group; no role-specific default overrides.

| Property | Initial default / range | Ownership |
| --- | --- | --- |
| `_Alpha` | 1, 0..1 | Both eyes; scales common coverage, not a separate coat opacity. |
| `_Cutoff` | EyeWX 0.5, EyeX 0; 0..1 | Both color and effective stencil survival. |
| `_StencilCutoff` | 0.5, 0..1 | Both eyes; cannot admit color-discarded texels. |
| `_CullOption` | Back | Same setting in all eye passes and proxy. |
| `_EyeOpticsMode` | 0 Off, 1 Refraction; default 0 | EyeX only. Round/clamp enum values consistently. Cheap parallax is a test reference, not a third public mode. |
| `_EyeOpticsStrength` | 1, 0..1 | EyeX optical RGB blend; mode Off remains the default. |
| `_IrisCenterX`, `_IrisCenterY` | 0.5 each, float UV coordinates | Final MainTex UV chart center. Separate floats work with MaterialEditor. |
| `_IrisRadiusX`, `_IrisRadiusY` | 0.25 each, 0.001..1 | Final MainTex UV radii, not pupil radius or world scale. |
| `_IrisDepth` | 0.08, 0..0.5 | Virtual recess in local iris-radius units; test range, not anatomy. |
| `_EyeOpticsEdgeFade` | 0.08, 0.01..0.5 | Fade width inside the normalized iris edge. |
| `_EyeOpticsMaxOffset` | 0.25, 0..1 | Maximum normalized-chart displacement. |
| `_EyeUVMinX/Y`, `_EyeUVMaxX/Y` | 0/0 and 1/1; 0..1 | Explicit safe sampling rectangle; advanced floats, no mesh-role inference. |
| `_EyeDispersion` | 0, 0..1 | Optional E2 RGB separation; no work with optics Off. |
| `_EyeDebugView` | EyeWX 0..2, EyeX 0..6; default 0 | 0 off, 1 source alpha, 2 stencil survival; EyeX adds 3 chart, 4 UV shift, 5 optical interface normal, 6 invalid/bounded hits. |

Keep existing X DebugView indices intact. Priority is EyeDebugView, then
ClearCoatDebugView, then existing DebugView; diagnostics must not stack across
lights. Alpha/chart/normal/weight diagnostics are Base-only; direct-only lighting
diagnostics include the matching Add contribution, environment-only never does.
Document diagnostics as RGB views with ordinary coverage still applied.

Shared map meanings remain stable: normal maps describe substrate detail; coat
maps remain in the surface domain. The EyeX adapter alone supplies shifted interior
sampling coordinates. No mandatory height, depth, normal or mask texture is added.
No automatic pupil dilation, SSS, caustic LUT, multiple internal refractions,
additional mesh or screen capture enters this first release.

## Milestones and Exit Gates

| Stage | Deliverable | Must pass before moving on |
| --- | --- | --- |
| Prerequisite | Shared Clearcoat C0-C3 evidence and versioned API | All existing X entries verified, not just an opaque sphere. |
| E0: compatibility baseline | EyeWX, EyeX with shared coat but optics disabled; KKS alias audit, common coverage, StencilMask, X writer registration and tooltips/carriers via Bridge | Real card/blink/gaze/overlay checks; complete union has no internal feather edges; missing-source negative control; Hard works without plugin. No mesh-role classifier or role presets required. |
| E1: local optics | Analytic local-chart refraction, game mapping/inverse mapping, safe bounds/gradients and diagnostics | Same-ray resolution/FOV independence within measured filtering tolerance; independent optics/coat Off tests; finite grazing output; no stencil motion from optical controls. |
| E2: optional dispersion | Three interior RGB rays, common alpha and shared lighting, default 0 | Zero equals E1 baseline; small strength gives subtle angle-dependent separation; no atlas bleed or colored silhouette; measured GPU cost acceptable. |
| E3: delivery | Compatibility table, manual parameter examples, bundles and adapter built as a matched set | In-game package/lifecycle tests, baseline image regression and documented performance/limitations. An unvalidated E2 is excluded, not advertised as working. |

E1 is the core view-dependent eye deliverable. E2 is in the design but not allowed
to delay a stable single-ray release indefinitely: ship it only with its own
evidence, otherwise explicitly defer the feature/property registration. SkinX can
follow the stable eye baseline. No shader placeholders or native assets are
created by this planning task.

## Packaging and Lifecycle

- Retain manifest GUID `tom.Shaders.X` and bundle `chara/tom/shaders/tomx.unity3d`.
  Add entries `tom/EyeWX` and `tom/EyeX` only when compiled and validated.
- Proposed carriers are `m_TomEyeWX`, `m_TomEyeX`, `a_TomEyeWX`, `a_TomEyeX`, created
  via Bridge/AssetDatabase with matching tooltip records and bundle assignment.
  They carry the shader-wide defaults, not guessed renderer-role presets.
- MaterialEditor groups: existing shared groups, Clearcoat, Eye Inputs,
  Eye Coverage, Eye Optics, advanced UV Bounds and Debug. No eye optical properties
  on EyeWX. Runtime game aliases are preserved, not advertised as independent
  artistic controls when the game overwrites them.
- Keep the six recognized xukmi writers. Register X entries by exact name and
  require StencilMask for them; a missing X pass is a diagnostic error, not a
  fallback that replays expensive color lighting and silently different alpha.
- Version and build the optional adapter when its recognition table changes.
  The existing 0.2.0 adapter recognizes legacy names only. Without the new adapter,
  mixed recognized/unrecognized writers can yield an incomplete feather union;
  do not describe that situation as a guaranteed automatic Hard fallback.
- Save/reload every new property and check shader swapping, reset, left/right
  heterochromia, texture regeneration, coordinate changes and material copies.
  Never mutate queues or assign optical settings in a background discovery scan.
- Validate Maker and Studio after restarting the game with the built bundle and
  matched adapter, not only with Editor materials. No V+ change, KK backport,
  automatic installation, public release or user-card migration is implied here.

## Validation Matrix

- Geometry: prioritize existing sphere-like iris patches with continuous UVs;
  skinned motion, independent/mirrored eye UVs and ordinary texture ST. Keep flat
  fixtures, negative scale and invalid tangent/ST cases as bounded fallback tests,
  not new mesh-authoring requirements.
- Game: left/right textures, color, pupil size/rotation, highlight visibility,
  expression transitions, closed/half-open eyes, card save/reload and Maker/Studio.
- Coverage: actual/proxy union, internal eye overlaps and legitimate gaps; queue
  reversal, hidden writers, missing/disabled helper, all supported mixed families.
- Camera: matched-ray renders at several FOVs/resolutions, perspective/orthographic,
  full/partial/clipped Maker viewports; continuous orbit at 0/15/30/60/80 degrees.
- Optics: neutral chart, depth/IOR extremes, invalid ray, bounded atlas edges,
  equal-channel white input, zero dispersion, pupil edge and minified-eye motion;
  derivative Jacobian under mirrored/rotated/nonuniform ST, uniform world scaling,
  mesh triangle boundaries and changing skinned normals.
- Lighting: no probe, sky fallback, blended/box-projected probes, 1/4/8 pixel lights,
  hard/soft shadows, saturated iris colors and legacy-glint on/off comparisons.
- Wet surface: optics off with coat on, coat off with optics on, then both on;
  independent surface/interior normals, no duplicate corneal highlights, and
  manually selected coat values on the same EyeWX shader across different meshes.
  Changing renderer/material names must not change defaults, queues or appearance.
- Pass agreement: identical iris UV/coverage across Base/Add; no base-only layers
  in Add; StencilMask has no new lens work; existing HairX Outline coverage remains
  consistent. Eye shaders must not inherit unintended Outline/ShadowCaster passes.
- Regression: all existing X static tests plus Opaque/Alpha/Hair baseline images
  when changing common modules. EyeWX controls must not acquire EyeX lens behavior.
- Cost: GPU milliseconds, pixel-light pass count, texture fetches/variants and
  helper writer work at target resolution with multiple full characters. Include
  an extreme close-up. No performance claim from compiler success or CPU timing.

Hard gates: no shader/compiler/resource errors, finite outputs, optics/dispersion
zero equivalence, unchanged actual/proxy coverage for all new RGB controls, and
no regression in the five existing X shaders. Same-ray UV tests use a float target
and a declared error tolerance; final screenshots may differ because of filtering.
Do not turn a mathematical invariance goal into an unsupported claim of identical
resampled screenshots at every resolution.

Open validation items are runtime alias/color/alpha details, final new-eye cutoff
defaults, chart quality across representative existing headmods and GPU cost.
These have E0/E1 owners and gates above; none authorizes extra geometry, automatic
role inference, POM or GrabPass. Outline, virtual bulge, pupil dilation and a public
cheap-parallax mode are outside v1, not unresolved mandatory features.
