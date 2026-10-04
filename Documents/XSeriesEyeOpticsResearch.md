# Eye Optics Research and Algorithm Selection

Reviewed: 2026-10-03. Purpose: choose practical angle-dependent iris/pupil and
reflection effects for the planned KKS EyeX, not reproduce an anatomical eye or
perform wave-optics simulation. No optical shader has been implemented or timed.

## Recommendation

Use **local analytic iris refraction with a separate corneal reflection normal**
as the main optical prototype. Retain a cheap parallax reference for comparison.
Add **three-channel geometric dispersion** only after the baseline is stable,
default off. EyeWX does not need iris optics. Surface reflection belongs to the
[shared X Clearcoat baseline](XSeriesClearcoatDesign.md), implemented before eyes,
not an eye-only layer or an extra lobe stacked onto the planned cornea.
All work stays on existing geometry; additional corneal/tear meshes are excluded.
The primary target is the user's continuous, sphere-like iris UV layout.
This is our engineering selection
for this repository, not a performance result reported by the sources below.

The desired cues are different: interior texture movement suggests depth;
corneal glints/reflections move on the outer surface; optional dispersion gives
a small wavelength-dependent separation inside the lens. Moving a painted
highlight alone cannot establish all three.

## Primary Source Findings

| Source | What it establishes | Consequence for our design |
| --- | --- | --- |
| [Unity HDRP 10.5 eye documentation](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@10.5/manual/eye-shader.html) | Distinct iris/sclera inputs, corneal refraction, pupil calibration and an invalid-refraction fill control. | Useful authoring concepts. HDRP Shader Graph is not a Built-in 2019 drop-in shader. |
| [Unity Graphics eye implementation](https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Eye/Eye.hlsl) | Separate diffuse and specular normals/layers. Its iris-plane caustic lookup uses a refracted view ray and a plane intersection. | Supports separating surface reflection from interior shading. This is current reference code, not the 2019 Built-in implementation or a complete portable UV algorithm. |
| [Epic Digital Humans, UE 4.27](https://dev.epicgames.com/documentation/en-us/unreal-engine/digital-humans?application_version=4.27) | A single-surface eye can model refraction without a second visible shell. Depth, IOR, bulge and UV/mesh calibration matter; the supplied setup is interdependent. | Do not promise an exact lens on arbitrary KKS skinned meshes or use a shared renderer origin as both eye centers. |
| [Tatarchuk, Practical Dynamic Parallax Occlusion Mapping](https://cgg.mff.cuni.cz/~pepca/lectures/pdf/Tatarchuk-ParallaxOcclusionMapping-Sketch-print.pdf) | Iterative ray/height-field intersections, optional self-shadow tracing and view-dependent sampling address detailed relief. | Real surface relief is a different requirement from a simple recessed iris; begin without a height march. |
| [Khronos KHR_materials_dispersion](https://github.com/KhronosGroup/glTF/blob/main/extensions/2.0/Khronos/KHR_materials_dispersion/README.md) | Wavelength-dependent IOR, Abbe/Cauchy parameterization, zero-disabled strength and an RGB transmission approximation. | Geometric color separation is feasible without wave simulation. Adapt to the iris texture, not a scene-color transmission buffer. |
| [Khronos sample renderer IBL code](https://github.com/KhronosGroup/glTF-Sample-Renderer/blob/main/source/Renderer/shaders/ibl.glsl) | Its dispersion branch traces three IOR-dependent rays and samples a transmission framebuffer. | Borrow the channel separation concept only. Importing that framebuffer path would violate this project's camera-independent baseline. |
| [Microsoft HLSL refract](https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/dx-graphics-hlsl-refract) | A refraction direction intrinsic is available; an invalid transmission can return a zero vector. | No ray-tracing API is required. Validate the ray before division or normalization. |
| [Unity 2019 GrabPass documentation](https://docs.unity3d.com/2019.4/Documentation/Manual/SL-GrabPass.html) | Copies the current framebuffer color into a texture for later passes; can increase CPU/GPU frame time. | It is a source-image capture, not a prerequisite for geometric refraction of an existing iris texture. |
| [Filament clearcoat model](https://google.github.io/filament/main/filament.html#materialsystem/clearcoatmodel) | A dielectric specular layer with layer weighting; its simplified coat does not simulate internal refraction. | Reflection and interior image displacement are independent concerns. |
| [Khronos clearcoat extension](https://github.com/KhronosGroup/glTF/blob/main/extensions/2.0/Khronos/KHR_materials_clearcoat/README.md) | Separate coat weight, roughness and normal; a thin-layer BRDF, not a volume-refraction algorithm. | Use independent smooth wet normals without duplicating the existing corneal lobe. |

Sources describe their own engines/materials. The proposed KKS equations, fallbacks,
integration and cost gates below are our adaptation. Do not copy HDRP/UE lighting
infrastructure into the X package. If any implementation code is later copied,
review its license and retain required attribution separately.

## Alternatives

Costs below are structural estimates per shaded fragment, not GPU measurements.
Existing coverage, normal, overlay and material samples are additional costs.

| Technique | Visual benefit | Main constraint | Decision |
| --- | --- | --- | --- |
| Tangent-space offset parallax | Interior moves relative to the outer surface; one iris RGB fetch. | No bending through a curved interface; large grazing offsets need bounds. | Cheap reference; possible low-cost fallback. |
| Analytic refraction to recessed iris layer | View-dependent iris position and distortion with a lens normal; one RGB fetch plus bounded arithmetic. | Needs chart/depth/normal calibration; the selected local differential chart is not a global anatomical lens. | Recommended E1; flat-plane math below is the reference fixture. |
| Analytic bowl / iris relief | More concave interior and spatially varying depth. | Adds shape and intersection assumptions on already varied headmods. | Later only if plane reference looks insufficient. |
| POM / relief mapping | Iris microrelief with height occlusion. | Height authoring, iterative samples and temporal/LOD work, repeated in Add passes. | Defer; not needed for the main depth cue. |
| Two physical eye shells | Explicit outer cornea and interior geometry. | Requires an asset/mesh workflow beyond the user's shader-only scope. | Excluded, including as a future fallback for this plan. |
| GrabPass / screen refraction | Refracts already rendered scene color. | Additional capture, screen edges, scene order and camera viewport coupling. | Reject for this eye-local effect. |
| RGB local dispersion | Small angle-dependent chromatic separation of interior features. | Three interior RGB samples and calibration; strong settings can look like fringing. | Optional E2, disabled by default. |
| Full spectral / wave simulation | Much broader optical phenomena. | Different renderer and asset scope; unnecessary for requested stylization. | Out of scope. |

POM means Parallax Occlusion Mapping: it searches a height field along the view
ray to find the visible relief surface. Our simpler model intersects a known iris
plane analytically instead. Avoiding that height-field search does not remove
view dependence: changing the view ray still changes the sampled iris position.
The tradeoff is missing arbitrary height-detail occlusion, not losing all depth.

GrabPass supplies an already-rendered screen image, useful for effects that warp
the visible background. EyeX already has the iris image to sample. It can bend
the local view ray, compute its iris UV and sample that texture directly, with no
screen capture. Probe reflections are a separate source again; none requires an
extra corneal mesh.

## Reference Ray Model

The following is a flat-chart reference, not executable shader code or a claim
that the eye center/frame can be inferred from a head renderer's origin. The
selected production prototype uses the differential UV-to-world mapping in
[the eye design](XSeriesEyeDesign.md#local-analytic-refraction). Its chart is in
final MainTex UV after game rotation/ST. Since package 0.2.1 its effective ray
IOR is EyeRefractionIOR, independent of ClearCoatIOR. The optional
[shallow depth-field extension](XSeriesEyeSurface.md) retains this local chart.
Use this simpler fixture to test ray sign, depth and RGB separation independently.
Define an orthonormal frame with +Z facing outward, a local surface point `P`,
outward smooth cornea normal `Nc`, and normalized surface-to-camera vector `V`.
All chart distances use the same normalized iris-radius units. An iris plane lies
at `z = -d` relative to the chart surface reference; `d >= 0`.

```text
incident direction I = -V
parallax direction T = I
refracted direction T = refract(I, Nc, nOutside / nInside)

t = (-d - P.z) / T.z
iris hit Q = P + t * T
iris sample UV = chart_to_texture(Q.xy)
```

Accept only finite inward rays with `T.z < -epsilon` and `t >= 0`. Set a bounded
maximum chart displacement; blend to the original UV at invalid/grazing cases.
Do not divide by `max(T.z, epsilon)`, which loses the inward-ray sign. For a flat
local reference (`P.z=0`) the unbent offset is `-d * V.xy / V.z`, before the
calibrated chart-to-texture scale/rotation. This is an analytical ray/plane hit,
not a screen-distance offset and not an iterative ray marcher.

IOR 1 removes **bending**, not parallax caused by nonzero depth. An explicit Off
mode/zero optical strength must return the original UV. A zero-depth check only
implies zero offset when the hit plane coincides with the chosen surface point;
a virtual dome with nonzero height must not violate this test by accident.

For a flat test patch, a trial dome can be a bounded radial height field
`h(x,y)` with `Nc = normalize(-dh/dx, -dh/dy, 1)`. Its physical and UV scales must
agree; simply using a unit sphere normal with unrelated depth produces arbitrary
magnification. Production work prioritizes the user's existing sphere-like
surface with continuous UVs and its smooth normals; virtual bulge is not required
in v1 and must not add curvature a second time. Use modest depth and ordinary
center/radius calibration. The chart remains an eye-local approximation,
not an anatomically exact multi-interface lens reconstructed from a renderer origin.

Keep surface and interior normals separate. The smooth corneal normal controls
reflection and the refracted view ray. Iris texture normal detail controls interior
diffuse response at the hit UV. No claim is made that light rays are refracted
through multiple ocular interfaces or that physical caustics are solved.

## Reflection Versus Painted Highlights

For the outer layer use `reflect(-V, Nc)` for the existing X probe lookup and the
same normal for the shared coat's direct GGX glint. This is angle-dependent surface
reflection, separate from the interior hit UV. The X implementation already has
probe HDR decoding, roughness selection, blending/box projection and artistic
bright-region control; reuse it rather than adding a screen reflection system.

Keep probe/reflection color restrained over the iris. Probe spatial capture and
update policy still limit what scenery it can reflect: local UV refraction does
not magically provide a live reflection of nearby characters. No-probe cases use
the existing environment fallback; direct glints and optional painted highlights
remain usable independently.

Legacy highlight textures are an intentional art layer. Keep their game controls
and default surface UVs. Warping them with the iris can make the wet surface look
attached to the interior. Already-painted glints in MainTex cannot be separated
automatically; provide manual on/off comparisons and recommend neutral iris artwork for
an optics-focused result. Do not automatically multiply Base-only glints for every
additional light.

## Clearcoat and the Wet Surface

Clearcoat is a thin dielectric reflection layer. It controls the outer highlight,
surface roughness and angular reflectance; it does not by itself move the iris
image. Therefore it complements parallax/refraction, but overlaps the role of
the corneal reflection already proposed. Model that corneal response as the coat
rather than counting the same wet interface twice. This is our proposed eye
adaptation, not a claim that a generic varnish model reproduces ocular optics.

| Module | Controls in this design | Can operate independently? |
| --- | --- | --- |
| Wet-surface coat | Outer direct/probe highlight and smooth surface normal | Yes, with iris optics off. |
| Parallax/refraction | Interior iris/pupil sample location | Yes, with wet reflection weight zero. |
| Dispersion | Channel-dependent interior sample location | Requires the refraction path, not an extra reflective coat. |

EyeX can use one dominant outer wet lobe over diffuse-dominant iris art. EyeWX uses
the same optional module without knowing whether a mesh is sclera, line or brow.
Both start with coat disabled; the user selects the amount. Role-specific presets
or automatic role inference are not delivery requirements. Layer weight, roughness
and normal ownership are explicit in the shared contract. Do not demand extra
textures or a second mesh just to make the surface glossy.

In the physical weighting option, account for coat reflection reducing the
underlying response. The Toon option retains deliberate body-color control and
is labeled approximate. Neither is implemented by simply summing duplicate
full-strength corneal lobes. Implement the composition once in the shared X coat
module, with zero weight preserving old X appearance. The eye adapter supplies
inputs instead of owning another BRDF. Both remain shading computations within
the existing color passes, with unchanged alpha, depth and stencil.

## Approximate Dispersion

Khronos relates strength to inverse Abbe number and describes independent RGB
transmission rays. For our trial, let `s` be that nonnegative strength and `n` the
central effective IOR. A symmetric RGB approximation is:

```text
spread = (n - 1) * s / 40
nRGB = (max(1, n - spread), n, n + spread)
C = (Iris(Qr).r, Iris(Qg).g, Iris(Qb).b)
```

Each `Q` uses the same ray model with its channel IOR. This is a three-channel
approximation, not spectral integration; RGB channels are not single physical
wavelengths. Cauchy's wavelength model is an alternative if later validation
needs more spectral control, not an additional requirement. At `s=0`, take the
single-ray path rather than performing three identical fetches.

Our eye-specific constraints:

- Split only the interior RGB lookup initially. Use a common central normal,
  coverage and shading evaluation; do not run the entire lighting shader three
  times or assign three different stencil tests.
- Retain the non-optical aperture/legacy coverage. Do not combine shifted RGB
  alphas with max/min and create colored eye holes or new HairFront boundaries.
- Keep the outer reflection neutral to this control initially. Dispersion in
  transmission is not thin-film iridescence, a rainbow Fresnel tint, or a global
  screen chromatic-aberration filter. It will not create rainbow highlights on
  uniform iris colors simply because a camera moves.
- Bound all three UVs to the authored eye region with border-safe sampling and
  suitable fill/fade. Clamping to texture 0..1 alone is insufficient for an atlas.
  Keep a common validity policy so one invalid channel cannot flash a colored seam.
- Filter minified/high-contrast iris detail. Compute derivatives before divergent
  fallback branches; compare explicit gradients where needed. Do not claim simple
  RGB splitting eliminates aliasing.
- Label strong color separation as stylized. Start at zero with a subtle demo;
  this is not a measured prediction of how a human eye should look externally.

No wave-optics implementation is needed for this geometric approximation.
Thin-film interference, diffraction, internal multiple reflections and rendered
rainbow caustics remain outside this feature.

## Numerical Sanity Check

A double-precision CPU evaluation of the flat-chart equations used depth 0.1 iris
radii, central IOR 1.35, dispersion strength 0.2 and an outward plane normal.
These are trial art parameters, not measurements of a human eye. Values below
are chart distances, not screen pixels or GPU render observations.

| View angle | Unbent offset | Refracted offset | Red-blue separation |
| --- | ---: | ---: | ---: |
| 0 degrees | 0 | 0 | 0 |
| 15 degrees | -0.026795 | -0.019534 | 0.000053 |
| 30 degrees | -0.057735 | -0.039873 | 0.000120 |
| 60 degrees | -0.173205 | -0.083624 | 0.000368 |
| 80 degrees | -0.567128 | -0.106651 | 0.000591 |

Zero depth returned zero offset and IOR 1 matched the unbent parallax equation at
all five angles, within 1e-12. The RGB split is much smaller than the interior
motion in this example: at 60 degrees it is about 0.037% of the iris radius.
This supports prioritizing depth and reflection over dispersion. Noticeable color
separation may require a different lens/depth calibration or deliberately stronger
art direction; do not promise dramatic rainbows from subtle settings. A domed
surface, texture derivatives and actual skinned UVs still need rendered validation.

### Differential Chart Algebra Check

During the two-step design update, 181 double-precision CPU comparisons checked
the selected local-chart equations: zero depth, uniform world scaling and recovery
of the same chart under different derivative-basis densities, rotations and signs.
The maximum absolute displacement difference was 8.33e-17 chart units.

The affine fixture used Ju=(1.2,0.3,0.1), Jv=(0.1,0.8,0.15), Ng=normalize(Ju cross
Jv) and V=normalize(0.85*Ng + 0.52*normalize(Ju)). It covered IOR 1/1.35/1.5/2.5,
depth 0/0.08/0.5 and uniform scales 0.1/1/10. Derivative row pairs were
(0.01,0)/(0,0.01), (0.002,0)/(0,0.004), (0,0.007)/(-0.006,0), and
(-0.01,0.003)/(0.002,0.02). These are algebra checks only: they do not validate
finite GPU derivatives, curved/skinned geometry, mip selection, temporal stability
or actual FOV/viewport screenshots. Those remain E1 gates.

## Cost and Acceptance

An analytic one-ray path needs fixed arithmetic and one interior RGB sample; RGB
dispersion raises that sample count to three. Coverage may require its own
unshifted sample. This does **not** mean the whole shader is exactly three times
as expensive. Conversely, Built-in ForwardAdd re-evaluates the material for each
pixel light, so UV work and iris fetches repeat unless a measured optimization
changes that design. No cross-pass pixel cache is assumed.

Keep the optical path out of the dedicated StencilMask when coverage permits it.
Legacy expression alpha work can still be necessary there. Compare actual GPU
cost with optics Off / analytic / dispersion at 1, 4 and 8 lights, close-up and
ordinary framing, and multiple complete characters. Inspect compiled branches
and build-time variants; a material toggle alone does not prove zero disabled cost.

Accept the effect only if an orbit shows interior motion distinct from corneal
reflection, Off reproduces the baseline, dispersion zero reproduces the single
ray, both eyes stay coherent through blink/gaze, and color/stencil boundaries stay
consistent across full/partial Maker viewports. Remaining integration details and
the full test matrix are in [EyeWX and EyeX design](XSeriesEyeDesign.md).
