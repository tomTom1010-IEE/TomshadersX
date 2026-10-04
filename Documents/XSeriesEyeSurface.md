# EyeX Surface Maps and Independent IOR

Experimental package 0.2.1, KKS / Unity 2019.4.9f1 / Built-in Forward.
This extends EyeX only. HairX, EyeWX, shared clearcoat, alpha and stencil contracts
are unchanged. No extra corneal mesh, screen capture, depth texture or plugin is
required. The eyebrow/eyeline shader split remains deferred.

## Independent Controls

`_ClearCoatIOR` now controls only coat reflection and layer energy.
`_EyeRefractionIOR` controls all interior rays, their gradients and RGB dispersion.
It defaults to 1.5, with range 1..2.5. Raising it reduces unclamped lateral
parallax; at 1 the ray does not bend but a recessed surface still has parallax.
Changing coat IOR can still change the visibility of iris details through
reflection/energy, but cannot move the optical UV. Both effects continue to use
the unwarped interface normal selected by Clearcoat Normal Source.

There is deliberately no live IOR link. Existing optics-off materials are
unchanged. To reproduce an old enabled-optics material whose Clearcoat IOR was
not 1.5, copy that old value to Eye Refraction IOR once. There is no automatic
material/card rewrite. This decoupling is an art control, not a more physically
exact single-interface model.

## Procedural Shapes

Enable Eye Optics, leave Use Eye Surface Mask off, and choose Procedural Recess
Shape. The default remains 0, the exact legacy flat/ellipse construction.

| Value | Shape | Normalized depth inside the iris |
| --- | --- | --- |
| 0 | Flat | Original constant recess with original edge fade and radial offset cap |
| 1 | Shallow cone | `max(0, 1-r)` |
| 2 | Shallow bowl | `max(0, 1-r*r)` |

`r` is radius in the ellipse defined by Iris Center X/Y and Iris Radius X/Y.
Cone and bowl are local UV depth profiles under the existing curved mesh, not
global anatomical surfaces or visible displaced geometry. The cone tip is the
ideal radial profile; a painted rounded tip can make it smoother.

**Iris Depth (Global Gain)** is the existing `_IrisDepth` property, not a second
gain. All procedural shapes and all map encodings multiply this same value.
Default 0.08, range 0..0.5, zero disables displacement. Units are local iris-radius
units, not meters or pixels. Optics Strength remains the existing overall effect
control, independently of the physical-looking recess amplitude.

## Painted Surface Maps

Enable **Use Eye Surface Mask**, assign **Eye Surface Map**, and choose encoding:

| Encoding | R channel | A channel | Empty region |
| --- | --- | --- | --- |
| 0 GrayscaleHeight, default | Height: white at the outer surface, black at maximum recess; `depth=1-R` | Ignored | White |
| 1 DepthRMaskA | Depth: black zero, white maximum recess | Optical region: black outside, white inside, smooth gray transition | A=0 |

In packed mode, the local depth field is `R*A`. The entry coverage also fades the
ray displacement at the region edge. This is an optical transition, **not alpha
or stencil coverage**. A black-depth area can be inside the mask but have no
recess. Gray height maps need no alpha channel; their height returning to white
supplies the edge transition. Do not reverse height/depth polarity when switching
encodings. The default white map in default encoding produces no parallax.

Map mode replaces the procedural center, shape, edge fade AND radial aperture
cap. It is not clipped by the old ellipse. Iris Radius X/Y remain reference-unit
calibration for the common depth gain and maximum offset, not painted-region
bounds. Safe UV Min/Max, maximum offset, grazing and invalid-chart guards remain
active in both modes.

Maps use **final MainTex UV**, after game iris rotation and MainTex scale/offset.
Paint over the iris texture at the same coordinates. No independent map ST is
applied, including any generic ST fields a material editor happens to display.
The surface map shares MainTex's sampler/filter/wrap state to stay within the
existing sampler budget. Independent map sampler settings are not honored.

Import as Default/data, with **sRGB off** and **Alpha Is Transparency off**; do
not import as a normal map or premultiply its channels. Prefer uncompressed data
while authoring, adequate resolution and smooth transitions. Color decoding
would change numeric heights, as explained in [Unity's linear texture guidance](https://docs.unity3d.com/2019.4/Documentation/Manual/LinearRendering-LinearTextures.html).
Runtime MaterialEditor imports still need game-side acceptance; these Editor
fixtures construct their data textures explicitly as linear.

## Solver and Limits

The legacy flat mode keeps its old computation and guards. New profiles compute
the full-depth local ray travel, then solve `t = depth(uv + travel*t)` on [0,1]
with six bracketed secant refinements. Entry-region coverage, strength, grazing
and maximum offset bound travel. Non-converged residuals fade to the original
sampling. The field is evaluated at the candidate hit, not simply sampled once
at the original UV and multiplied into a displacement.

This solver assumes a shallow, smooth, locally single-intersection field. A
single-valued height map can still intersect an oblique ray multiple times.
There is no first-hit search, self-shadowing, silhouette change, depth write,
overhang support or multiple internal reflection. Deep steps/noisy maps are
outside the supported authoring regime; convergence alone does not prove the
first visible intersection. [GPU Gems' relief-mapping discussion](https://developer.nvidia.com/gpugems/gpugems3/part-iii-rendering/chapter-18-relaxed-cone-stepping-relief-mapping)
explains why general self-occlusion requires a suitable search strategy.

The local chart stays fixed while UV/view/normal differentials propagate through
the same solver. Map fetches use explicit source gradients; no derivatives of a
derivative-built hit are taken. Existing shared chromatic footprint, atlas
fallback, refracted interior normals and unwarped coat/coverage are preserved.
Depth alone changes intersection position, not the interface normal or a new
automatically generated cavity-lighting model.

Map mode costs up to eight depth-map samples per evaluated ray, or 24 for a
green hit plus its two gradient evaluations; dispersion can raise that bound to
72. Compiler reuse/early exits can reduce it. Each ForwardAdd repeats its optical
material evaluation. These are source-level upper bounds, not GPU timing claims.
Procedural profiles need no surface texture fetches, and default flat skips the
new solver. Performance on real KKS characters remains an acceptance item.

## Diagnostics and Templates

Existing Eye Debug values retain their indices. 7 displays normalized surface
depth (white deep), and 8 the optical region. Neither view displays or changes
stencil. View 4 is the geometric UV shift before the atlas-validity blend, so
combine it with view 6 and final color when checking fallback behavior.

Bridge `renderxcoateye` writes raw linear-data starter PNGs alongside captures:

- `template-flat-height.png`, `template-cone-height.png`, `template-bowl-height.png`
- `template-flat-depthRA.png`, `template-cone-depthRA.png`, `template-bowl-depthRA.png`
- `template-asymmetric-depthRA.png`

The circular examples use center (0.5,0.5), radius 0.37; adapt them to the actual
iris art. They are not automatically assigned to materials or included as new
required asset dependencies. Rendered previews use the fixture display transform;
the template PNGs do not. Do not paint from the tone-mapped previews.

## Weak Angle Response

A working depth slider does not establish the eye's angle relative to the view:
gaze tracking can keep it facing a moving camera. Fix the eye direction before
comparing front and left/right oblique views. Isolate optics by temporarily
disabling coat and dispersion; do not use changing reflections as a UV test.

The Editor demonstration uses depth 0.25, refraction IOR 1.376, strength 1 and
angles -40/0/+40 degrees. Default depth 0.08 and IOR 1.5 are more conservative.
Use demonstration settings only as a controlled comparison, not a new default
or a guarantee of safe large depth on every authored map. Iris Radius X/Y still
calibrate depth/offset units in map mode; they are not inferred from the texture.

Depth distribution matters as well as its maximum. A center-heavy gradient
that gets shallow early can move little outer-ring detail; a broad recessed
floor with a smooth transition retains depth across more of the iris. Symmetric
rings and baked highlights can make motion harder to read than fine iris fibers.
Inspect Debug 7 for decoded depth, 4 for geometric UV shift, and 6 for fallback;
Debug 4 precedes atlas fallback and is not final visible displacement by itself.

If raising depth stops adding motion, inspect Optics Maximum Offset, grazing
and atlas validity instead of continuing to increase gain. The depth field does
not automatically create cavity shading/normals or change the visible silhouette.
The generated visual template is approximate, not a precision regression map;
use numeric linear-data templates to test exact empty regions and boundaries.

Use GrayscaleHeight for ordinary opaque grayscale height PNGs. DepthRMaskA
requires the intended depth polarity and alpha region; an unassigned white map
in that encoding means full depth/coverage, not the grayscale mode's no-op.

The user confirmed visible angle response after this diagnostic comparison on
2026-10-04. This does not require a new view-response function. If the desired
effect later becomes near-wall occlusion of far content, follow the
[conditional POM / first-hit reminder](XSeriesEyeClosure.md#conditional-pom-reminder).

## Validation

`Tests/Test-XEyeSurface.ps1` checks the independent IOR/default/coverage/UV
contracts and compares 675 shallow plane/cone/bowl secant solutions against
48-step CPU bisection. Maximum normalized root error: 6.44e-8. This is reference
algebra, separate from GPU evidence and not a general first-hit proof.

The extended `renderxcoateye` fixture checks coat/refraction independence, shape
differences, height/packed agreement, rotated/scaled/mirrored UV alignment,
custom regions outside the ellipse, shared gain, zero/missing/invalid inputs,
alpha and actual stencil, dispersion continuity, camera/scale/viewport changes,
and matching Base/Add lighting. GPU run identifiers and final counts are recorded
in `XSeriesCoatEyeProgress.md` after verification.

On 2026-10-04 the user confirmed all scoped non-performance KKS game acceptance
complete, closing the prior card, blink/gaze, MaterialEditor and authored-profile
checks for their tested setups. The local Editor GPU baseline is now measured;
full-game/device cost remains unqualified. See [performance evidence](XSeriesGpuBaseline.md)
and [the closure record](XSeriesEyeClosure.md). Nothing is installed into the game
by the build or validation operations.
