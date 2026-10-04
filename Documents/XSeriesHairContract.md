# HairX v1 Material Contract

Status: KKS / Unity 2019.4.9f1 Built-in Forward feature baseline, 2026-10-03.
One public shader: `tom/HairX`. No V+ or KK changes. The reported sclera case is
resolved with a supported writer; broader game acceptance and GPU performance
remain open. See [feature closure](XSeriesHairClosure.md); this is not a release.

## Material And Direction

- Inherits the frozen 98-property X material contract, plus twelve hair properties.
- The full 110 declarations are frozen in Tests/HairX-v1.properties.txt and checked
  by Test-XHair.ps1. Do not silently change defaults while extending eye support.
- MainTex RGB times BaseColor is the body. MainTex alpha times AlphaMask R is
  shared texture coverage. Zero coverage always discards, even at Cutoff 0.
- Outside the eye stencil, coverage surviving Cutoff is opaque and writes depth.
- NormalMap and NormalMapDetail still use Unity `UnpackScaleNormal` and the X
  tangent-frame normal combination. Flow maps do not replace normal maps.
- StrandAngle chooses the mesh tangent-plane axis: U = 0 degrees, V = 90.
- StrandDirectionMap stores linear RG: `direction = RG * 2 - 1`. Import as a
  linear Default texture, NOT as a Unity Normal Map. B/A are unused.
- StrandDirectionBlend is a continuous 0..1 float, not a feature toggle. Mesh
  and map axes are sign-aligned before interpolation. Neutral/zero flow falls
  back to StrandAngle; missing tangents get a finite geometric fallback, not a
  claim of correct authored hair orientation. Supply proper mesh tangents.
- HairAnisotropy stretches the direct highlight across the strand. Existing
  SpecularSize/Threshold/Softness/Bands/AA control its Toon shape. SpecularToonBlend
  selects continuous anisotropic GGX or the shaped endpoint. No Legacy Blinn path.
- NdotL, NdotV, safe half vectors and geometric horizon limits remain enforced.
- Each additional light uses the shared full-shadow ForwardAdd lighting path.
- Indirect diffuse, SH/custom SH, probe IBL, AO, MatCap, Rim, emission and outline
  retain the X layer ownership. Probe prefiltering is still isotropic; this is not
  a full strand-scattering, multiple-scattering or anisotropic IBL model.
- Tangent handedness applies the negative-scale determinant once. Raster VFACE
  determines backface orientation. Default Cull Off supports double-sided cards.

The new direct lobe is in TomHairBRDF.cginc; TOM_HAIR selects small hooks in
TomToonLighting.cginc. The full lighting implementation is not duplicated.
TomHairCoverage, TomHairForward, TomHairOutline and TomHairShadow isolate the
hair-specific coverage and render-state adapters.

## HairFront

| Property | Contract | Default |
| --- | --- | --- |
| HairFrontMode | 0 Off, 1 Hard, 2 optional inward Feather | 0 |
| HairFrontOpacity | 1 opaque for fully covered texels; 0 invisible inside the hard region | 0.5 |
| HairFeatherWidthMode | 0 legacy Pixels, 1 projected World Units | 0 |
| HairFeatherWidth | Inward transition in screen pixels, used only in Pixels mode; zero means Hard | 8 |
| HairFeatherWorldWidth | Inward width in scene units, 0..0.05, used only in World Units mode; zero means Hard | 0.005 |
| HairFeatherThreshold | Half-transition position within the width, 0.05..0.95; not a stencil cutoff | 0.5 |
| HairFeatherPower | Midpoint steepness, 0.5..4; above 1 harder, below 1 softer | 1 |
| HairFrontZWrite | Depth writes for stencil-equal body and outline | 1 |

Stencil reference is **2**, compatible with the existing xukmi eye writers.
HairX reads the stencil and never changes it. The eye writer must render before
the hair's actual material render queue; default HairX queue is 2475. Importing
an eye shader does not by itself establish the game's material queue ordering.

Ordinary mode ignores the distinction visually: both regions remain cutout.
In Hard mode the inside region has `alpha = textureCoverage * HairFrontOpacity`
after the same global texture cutoff. All Base art layers and Outline follow
this coverage. Add contributes `newLight * alpha` with One/One; it does not
multiply the background a second time. Alpha zero discards before depth writes.
Keep HairFrontZWrite at 1 for ordinary cutout hair; it also controls this region
when Mode is Off. Turning it off is an explicit advanced depth-policy change.

This is a single-layer HairFront strategy, not sorted translucent hair. Crossing
semi-transparent sheets within a mesh are not order-independent. Mod authors
must separate and order hair materials where necessary. No hair BackFront,
depth-only prepass, screen-color refraction, dither or automatic sorting is added.

ShadowCaster always uses hair-texture cutoff and the X Cull/bias contract.
It never reads the camera stencil, HairFrontOpacity or feather field. Revealing
an eyebrow does not punch a corresponding light-space hole in the hair shadow.
This deliberately differs from globally fading an entire Alpha material.
Camera framebuffer depth and Unity's separately rendered _CameraDepthTexture
are different. The latter may reuse this stencil-independent ShadowCaster and
therefore retain cutout hair in the exposed-eye area. DOF/SSAO agreement there
is not certified by the framebuffer-depth tests and needs a separate system
decision; changing it must not silently remove the real light-space shadow.

## Optional Feather Provider

Hardware stencil alone does not provide a sampleable edge distance. The optional
TomHairFeatherCamera component runs before opaque hair. It replays explicitly
registered eye stencil-writer passes into a private binary proxy, extracts stencil
2, and generates a bounded approximate inward distance field using jump flooding.
At half resolution Pixels mode performs one stencil extraction and seven filter
draws (seed, five jumps, distance) plus the registered writer draws, once per
active camera, not once per light or hair material. Hair samples one distance per
inside fragment; Base/Add/Outline use the same formula:

`alpha = lerp(1, textureCoverage * HairFrontOpacity, FeatherWeight(distance))`

### Viewport And Width Units (0.2.0)

Pixels remains the default and retains the original 0..32 range. World Units uses
the positive view-space depth of the hair fragment, not the camera depth texture:

`widthPixels = worldWidth * viewportHeight * abs(projection.m11) / (2 * viewDepth)`

Orthographic projection replaces viewDepth with 1. Outline uses its expanded screen
position but the underlying surface depth, so shell width/depth offset does not
change the material's world-width convention. This is a world-calibrated projected
width, not a scalp geodesic or true 3D distance to an eye boundary. Character scale
does not automatically scale a width expressed in scene units.

The provider conservatively estimates the largest requested projected width from
visible renderer bounds. The search radius is rounded up to a power of two from
32 to 256 pixels. Shader width and distance output share that cap; diagnostics
report the radius and whether the conservative request exceeded the budget.
At half resolution the maximum is ten filter draws rather than seven. The same
four render targets are reused; a larger radius does not allocate a second field.
Close-ups reaching the cap and subpixel distant features are explicit limits.

Maker's non-full viewports are supported without changing the Camera rect. In
Unity 2019, switching render targets within BeforeForwardOpaque can disturb the
partial viewport for subsequent scene draws. Partial-camera fields are therefore
generated in OnPreCull using a private command buffer, before native camera setup;
the BeforeForwardOpaque buffer only publishes that field's globals. Full-view
cameras retain the original deferred command-buffer drawing path. Each private
target has an explicit local viewport; partial-camera sampling derives local UV
from clip coordinates, including the expanded Outline position. No scene-color
capture or camera-depth sample is introduced. Both paths reset availability after
the camera finishes, and Off/Hard still release auxiliary resources.

Width sets the distance; Threshold positions 50% transition progress within it;
Power adjusts steepness without moving that midpoint or either endpoint.
At width 8 and thresholds 0.25/0.5/0.75, half progress lies 2/4/6 pixels inward.
Half progress is not necessarily alpha 0.5: an interior coverage of 0.2 gives
alpha 0.6 at the midpoint. HairFrontOpacity controls that interior coverage.

The default Threshold 0.5 / Power 1 executes the original
`smoothstep(0, width, distance)` directly. Otherwise, with `u = saturate(distance / width)`,
`t = clamp(Threshold, 0.05, 0.95)` and `p = clamp(Power, 0.5, 4)`:

```text
b = u * (1 - t) / (u * (1 - t) + (1 - u) * t)
q = b^p / (b^p + (1 - b)^p)
weight = smoothstep(0, 1, q)
```

Balanced powers steepen around the fixed half-transition, unlike a plain `u^p`.
The bounded parameters keep denominators positive. Shader-side clamping also
handles finite values set outside the UI range. Only active Feather uses these
controls: Hard, zero-width and missing-provider behavior is unchanged. Curve
adjustment adds arithmetic in shaded fragments, not new masks, passes, textures
or provider work. High Power can make half-resolution/subpixel edges less stable;
it is an art control, not a higher-resolution distance reconstruction.

Only pixels that already pass the actual camera stencil test can use feather.
The feather does not extend the hard eye region or recover the original eyebrow
alpha. Opacity zero leaves a transition near the boundary, but the fully faded
interior writes neither camera color nor depth.

Important limits:

- This is a **proxy** of registered eye coverage, not an exact copy of the camera
  depth/stencil after all opaque geometry. It does not reconstruct occlusion-created
  boundaries. Overlapping eye writers can join the proxy distance field. Actual
  stencil gating prevents opening a region outside the camera stencil, but it does
  not guarantee owner-separated feather in overlapping characters.
- Half-resolution distances approximate narrow/subpixel features. Pixel width is
  0..32; World Units has a 256-pixel projected cap. Test motion and tiny eyelashes.
- Current provider is limited to D3D11, non-stereo Forward cameras.
  Unsupported/missing provider, zero width, or no earlier registered writers falls
  back to Hard. Camera view/target-size checks reject another camera's field.
- Disabling feather releases auxiliary render targets and removes all helper command
  buffers. Off/Hard do not render or filter a proxy mask. The optional plugin still
  does a periodic CPU renderer discovery to detect later material changes.
- No scene color, GrabPass or camera-depth texture is sampled. The distance field
  does not require the public hair queue to move above 2500.

### KKS Adapter And Packaging

Hard HairFront needs no runtime plugin. Feather's optional BepInEx adapter is
Runtime/TomHairFeatherPlugin.cs; it discovers HairX receivers and six known
xukmi eye writer names (Eye/EyeW Plus, AlphaPlus and PlusTess). Other writer
shaders must be explicitly integrated. With no eligible registered writers the
provider falls back to Hard. Mixing supported and unsupported writers can instead
produce a partial proxy and internal Feather seams; there is no automatic
per-writer fallback. Include sclera as well as iris, eyelines and intended brows.
The adapter replays `StencilMask` when available, otherwise `Forward`. This is
runtime compatibility, not a source-code or asset dependency on the V+ package.

The user's 2026-10-03 sclera switch to xukmi/EyeWPlus at queue 2472 improved the
reported internal edges with HairX at 2475. The original shader is unidentified,
so its integration remains deferred. No feather algorithm patch is required for
that now-supported combination. Complete-source overlap tests are recorded in
[the progress record](XSeriesHairProgress.md#internal-feather-edge-investigation-2026-10-03).

Build with Tests/Build-HairFeatherPlugin.ps1 and Bridge operation
`buildxhairfeather`. Outputs are in the project-level directory
`CodexBridge/Builds/TomHairFeather`. For an optional game test, deploy
TomHairFeather.dll and hairx-feather.unity3d together in a dedicated BepInEx/plugins
subfolder. They are not installed automatically and are not bundled into the
shader-carrier prefab. The main X mod still needs its ordinary bundle/zipmod
rebuild. A shader asset bundle cannot supply new executable C# by itself.

### Runtime Diagnosis (Adapter 0.1.1)

The optional adapter now logs successful hidden-shader loading and supports
`[Diagnostics] LogStateChanges = true` (default) in its BepInEx configuration.
Discovery and per-camera reports have the `[HairFeather]` prefix. They are checked
once per second and logged only when the report changes, not on every frame.
Disable the option after troubleshooting if it is not needed.

The report lists HairX material queues/modes/widths, recognized eye shader names,
writer pass names/indices, and camera path/viewport/target dimensions. Per-camera
fallback reasons distinguish no visible requesting hair, missing eye sources,
unsupported camera/API/texture formats, late writers and missing shader passes.
Counts include visible writers, queue-rejected writers and submitted proxy draws.
`Scheduled` means command buffers were prepared, not that the mask contains the
correct pixels or that the material consumed the field successfully. Remaining
GPU/geometry/alignment failures still require render evidence.

For a 0.1.0 installation, exit the game and replace the existing
`TomHairFeather.dll` with the 0.1.1 build; keep the existing adjacent
`hairx-feather.unity3d`. Do not install a duplicate DLL in another plugin folder.
No main shader zipmod rebuild is required for this diagnostics-only update.
Reproduce the problem with Mode 2 and Width greater than zero, wait a few seconds,
then collect BepInEx/LogOutput.log including the multiline HairFeather reports.

For **0.2.0**, replace both DLL and adjacent helper bundle, and rebuild the main X
shader zipmod with the new shader, manifest and tooltip catalog. The helper shader
has field-contract version 2; an older helper bundle is explicitly rejected by
the adapter. Existing carrier materials/prefabs do not need rebinding. No files
are automatically installed in the game. The 0.1.1 DLL-only instruction above
applies to that historical diagnostics update, not to 0.2.0.

## Render Cost And Consolidation Gate

HairX has seven declared passes: outside Outline/Base/Add, inside Outline/Base/Add,
and ShadowCaster. The two stencil tests are complementary, so they do not both
shade the same framebuffer sample, but geometry submission and vertex work remain.
Off is **not** a one-pass ordinary hair shader. Additional lights repeat both Add
groups. Disabling Outline clips it but does not remove its declared pass.

At 1920x1080 with half-resolution feather, private textures use approximately
13 MiB before driver overhead (binary color+depth, two RGFloat seeds, RHalf result).
The five nine-tap jump passes alone read about 23 million samples per active
camera. Actual performance depends on hardware, writer complexity and visibility.
CPU Camera.Render timing is recorded by tests but is not GPU frame time.

Keep the single public shader for the prototype. Final consolidation requires
actual KKS GPU/frame profiling with several characters, 1/4/8 pixel lights,
shadowed hair and feather toggled. Do not close that gate from small-fixture CPU
timings. Split Hair/HairFront later only if measured ordinary-hair overhead or
feather integration makes that tradeoff worthwhile.

## Assets And Acceptance

Manifest: tom/HairX -> a_TomHairX in chara/tom/shaders/tomx.unity3d.
Unity Bridge creates Material/m_TomHairX.mat and Prefab/a_TomHairX.prefab and
assigns the bundle on the prefab. Existing X/V+ assets are not migrated.
All 110 material properties have matching manifest entries and scoped tooltips.

Run Test-XHair.ps1 and Test-ProjectIsolation.ps1. Bridge `renderxhair` warms
13 representative shader variants and renders transient fixtures without saving
a scene. Tests cover stencil coverage, actual depth rejection, Add weighting,
proxy distance against a CPU edge reference, disabling auxiliary work, direction
blend/fallback, extreme roughness, shadow invariance and negative-scale cards.
Finite-image checks on cards/negative scales are not full visual acceptance.

Manual KKS acceptance still required:

1. Real UV/tangent hair meshes, KK normal textures and direction maps; verify U/V
   alignment and continuous blends at 0, 0.25, 0.5, 0.75, 1.
2. Front/back/crossed cards under directional, near point and spot lights; hard
   and soft shadows, Cull Off/Front/Back, mirrored UVs and negative scales.
3. Eye writers and actual queues; no invisible depth wall at opacity 0; self-shadow
   remains when eyebrows are exposed. Alpha holes agree across color/outline/shadow.
4. Hard versus Feather near eyebrow boundaries, moving camera, multiple characters,
   reflection captures and post-processing. Proxy occlusion limits stay explicit.
   Sweep Threshold 0.05/0.25/0.5/0.75/0.95 and Power 0.5/1/2/4. Defaults must retain
   the previous transition; the midpoint must not move when only Power changes.
5. GPU/frame timing at target resolution, feather off/on and 1/4/8 lights. Confirm
   optional-plugin disable stops helper draws. Test low resolutions and MSAA.

Algorithm references: [Filament anisotropic GGX](https://google.github.io/filament/main/filament.html),
[Unity 2019 camera events](https://docs.unity3d.com/2019.4/Documentation/ScriptReference/Rendering.CameraEvent.html),
[Unity stencil](https://docs.unity3d.com/2019.4/Documentation/Manual/SL-Stencil.html).
