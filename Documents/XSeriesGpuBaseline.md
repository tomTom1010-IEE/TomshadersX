# Clearcoat and Eye GPU Baseline

Date: 2026-10-04. Scope: local Unity 2019.4.9f1 Editor, Built-in Forward,
D3D11, Gamma, RTX 4070 Ti SUPER. This is a repeatable shader fixture baseline,
not complete-character KKS performance certification. No shipping shader,
material default, game installation or package version is changed.

**User decision, 2026-10-04:** retain the current appearance and features.
EyeX multi-light optimization is a follow-up, not a blocker for current closure
or SkinX planning. This fixture identifies a cost source, not an established
whole-game bottleneck. Revisit optimization when representative game/device GPU
budgets justify it. Do not remove features, weaken Clearcoat, or introduce
mandatory quality tiers on these results alone. Keep dispersion opt-in.

## Results

The accepted run is `CodexBridge/Reports/XGpuBaseline-20261004-071530`:
217 cases, 13,020 finite timed samples, three randomized rounds, 66 untimed
preview PNGs. `report.json`, `summary.csv`, `summary.md` and `materials.txt`
retain raw evidence and configuration. NVIDIA driver 591.86; spot checks during
the run reported P0, 2655 MHz graphics and 10701 MHz memory. Clocks were not locked.

Close view, one enlarged eye pair, GPU median milliseconds:

| Configuration | 1 light | 4 lights | 8 lights |
| --- | ---: | ---: | ---: |
| EyeX optics/coat off | 0.0471 | 0.1239 | 0.2335 |
| EyeX coat only | 0.0584 | 0.1423 | 0.2621 |
| EyeX procedural flat | 0.0696 | 0.1956 | 0.3697 |
| EyeX procedural bowl | 0.0891 | 0.2642 | 0.5002 |
| EyeX painted bowl | 0.0983 | 0.2918 | 0.5550 |
| EyeX painted bowl + dispersion | 0.1715 | 0.5535 | 1.0665 |
| EyeX painted bowl + dispersion + coat | 0.1818 | 0.5719 | 1.0967 |
| Local V+ EyePlus, reflection on, Add enabled | 0.0246 | 0.0522 | 0.0901 |
| Local lilToon Cutout, reflection off | 0.0563 | 0.1659 | 0.3328 |
| Local lilToon Cutout, reflection on | 0.0635 | 0.1843 | 0.3666 |
| Local lilToon Cutout, simple parallax | 0.0604 | 0.1782 | 0.3564 |

These are different material implementations, not equivalent quality levels.
V+ is cheaper in these selected cases; X with optics off is cheaper than the
selected local lilToon Cutout cases. X's painted-depth refraction and dispersion
do more work than the reference presets. Do not generalize to every family
member, asset, platform or upstream release.

Fixed-size crowd proxies with eight pixel lights:

| Configuration | 1 eye pair | 4 eye pairs | 8 eye pairs |
| --- | ---: | ---: | ---: |
| EyeX optics/coat off | 0.0492 | 0.1915 | 0.3574 |
| EyeX painted bowl | 0.1239 | 0.4332 | 0.8218 |
| EyeX painted bowl + dispersion + coat | 0.2611 | 0.8888 | 1.7060 |
| Local V+ EyePlus, reflection on, Add enabled | 0.0246 | 0.0922 | 0.1618 |
| Local lilToon Cutout, reflection on | 0.0973 | 0.3277 | 0.9462 |

## Interpretation

- Clearcoat alone adds about 0.0113 ms in the close one-light EyeX case and
  0.0287 ms with eight lights. Shared-module checks are similar: EyeWX adds
  0.0082/0.0245 ms and MainOpaqueX adds 0.0092/0.0277 ms for one/eight lights.
  It is not the dominant cost in this matrix.
- In the close eight-light case, painted depth adds 0.3215 ms over optics off;
  dispersion adds another 0.5115 ms over painted depth. Adding coat to the latter
  adds 0.0302 ms. Lowering a nonzero dispersion strength is not a demonstrated
  performance tier; the source selects its extra RGB rays while strength > 0.
- With eight physical lights, the diagnostic copy with Add disabled reduces
  close-view painted+dispersion+coat from 1.0967 to 0.1812 ms. The 0.9155 ms
  difference includes all additional-light work, not optics alone. The same
  subtraction for optics/coat off is 0.1859 ms, leaving approximately 0.7296 ms
  of extra Add-path cost associated with the enabled effects. Disabling Add is
  a diagnostic, not a valid optimization: its lighting contribution is lost.
- Close-view X pixel invocations are 942,912 for stencil+Base versus 4,243,104
  with seven Add passes. All six Add-disabled controls match their one-light
  counters. V+ active-Add cases also show increased pixel invocations after
  correcting the fixture controls.
- Three block medians for close eight-light EyeX full effects span
  1.0952..1.0977 ms; painted depth spans 0.5535..0.5565 ms. Individual samples
  have larger tails, so inspect p10/p90 rather than treating microsecond-scale
  differences as universal rankings. Empty-camera median rounds to zero at
  timestamp resolution; it does not establish zero overhead.
- The dense-mesh pilot illustrates geometry sensitivity: eight eye pairs and
  eight lights with full X effects measured 6.3416 ms at 32,768 triangles/eye,
  versus 1.7060 ms at 2,048 triangles/eye in the main fixture. Geometry and
  rasterization work matter; the dense result should not masquerade as typical
  character cost. The pilot and main run are separate experiments, not a
  synchronized geometry-only causal estimate.

Any future optimization should preserve the same optical mapping in Base and Add.
Candidates include compiled optics-off cost and redundant hit/gradient/texture work;
do not simply disable refraction in Add or assume a shared include caches work
between passes. Keep the current feature defaults and use optics/dispersion
switches already present. An explicit optional quality policy becomes justified
if an agreed target-device multi-character budget still fails after measured
optimization. No such universal budget or cross-device pass is claimed here.

## Measurement Protocol

- A render-thread-only native helper inserts D3D11 timestamp queries around
  `BeforeForwardOpaque` through `AfterEverything`. The disjoint query validates
  the GPU frequency; unavailable/disjoint results fail instead of falling back
  to a CPU stopwatch. Query completion waits and 1-pixel readback happen after
  the end timestamp, outside the measured GPU interval.
- One camera render per Editor update; eight initial warm-up renders per case,
  four on later visits, three seeded-randomized blocks of 20 measured renders.
  The report retains every sample, block and measurement order. Medians describe
  these samples; p10/p90 and between-block median spread expose instability.
- 1920x1080 ARGBHalf plus 24-bit depth/stencil, no MSAA, physical shadows,
  animation, body, hair or postprocessing. An orthographic camera and 25-degree
  rotated curved UV patches stay fixed. Identical 1024-square mipmapped iris
  and linear grayscale bowl data are shared across candidates.
- The main fixture uses 32x32 subdivisions, 2,048 triangles per eye. Two eyes
  represent one proxy character. Crowd layouts have 1/4/8 pairs of fixed-size
  eyes without overlap; adding proxies increases coverage instead of shrinking
  all eyes into the same footprint. The close view uses one enlarged eye pair.
- One directional key plus 0/3/7 point lights, all forced pixel lights. Native
  D3D11 pipeline statistics are collected in separate untimed diagnostics.
  X Eye draws one stencil pass, one Base pass and one Add per additional light.
  Add-disabled copies must reproduce one-light primitive/pixel execution counts.
- Camera.Render wall-time and fence/readback wall-time are stored separately.
  Neither is substituted for GPU time. Camera.Render may itself stall and is
  not a pure CPU-work counter. The GPU interval includes draw/pipeline scheduling,
  not only fragment instructions. GPU clocks/power policies are not modified.

The measurement uses [Microsoft's timestamp/disjoint query contract](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/ne-d3d11-d3d11_query)
and Unity's [render-thread plugin events](https://docs.unity3d.com/2019.4/Documentation/ScriptReference/Rendering.CommandBuffer.IssuePluginEvent.html).
The small DLL is built outside Assets, loaded only for the Editor benchmark,
and unloaded after the render queue is fenced. It is not a game dependency.

## Comparison Controls

Shaders are loaded by explicit AssetDatabase path, avoiding duplicate-name
Shader.Find resolution. Effective properties, pass names, keywords and shader
dependency hashes are written to `materials.txt` / `report.json`.

- V+ LTS is the local `KKShadersPlus-release1.7.1` tree, manifest version 1.7.5.
  EyePlus, EyeWPlus and MainOpaquePlus are measured. Its optional additional-light
  path requires `_UseForwardAddFullShadows=1` and `_DisablePointLights=0`; these
  are explicitly set on temporary benchmark materials. Lights still have no
  shadows, so this is not a shadow-map benchmark.
- The V+ eye Add pass also tests stencil Ref 2. Every candidate receives an
  identical full-target Ref 2 seed before the timed interval, without color/depth
  writes. This supplies the scene dependency instead of timing rejected draws.
  It is not a simulation of every game's stencil boundary or occlusion setup.
- lilToon is the local KKS port, manifest version 1.0.3, not a claim about all
  upstream lilToon releases. Both Opaque and Cutout entries are included.
  The `lil-eye-*` Cutout copies use cutoff 0.001 and ZWrite Off to approximate
  the aperture role. Outline is disabled by its material property, but native
  outline-pass dispatch overhead is retained. Reflection is optional; simple
  parallax uses the same height map, scale 0.08, and POM Off.
- Native material/pass behavior is not rewritten to force matching images.
  In particular lilToon's default ForwardAdd BlendOp is Max, and V+ has a
  separate reflection pass and legacy layer ordering. Their additional lights
  need not produce the same brightness increments as X. Base specular, cutoffs,
  Fresnel, coverage and reflection are not feature-identical. A lower number
  here does not establish an equal-quality winner.
- X optics use depth gain 0.25, iris radii 0.37, refraction IOR 1.376, coat IOR
  1.5 and roughness 0.15. Dispersion is 1 when enabled. The map is a shallow
  grayscale bowl. These are controlled comparison settings, not universal
  recommended material values.

## Pilot Evidence

The initial 128x128 mesh run at
`CodexBridge/Reports/XGpuBaseline-20261004-070236` completed 181 cases and
10,860 samples. It is a dense-geometry stress reference for X only, not the main
cost baseline. V+ had its optional Add flag off and lacked pre-existing stencil;
those multi-light numbers must not be presented as working-Add comparisons.

The intermediate `XGpuBaseline-20261004-070943` and
`XGpuBaseline-20261004-071254` runs were deliberately interrupted to correct
these controls. Their reports record failure/interruption; they are excluded
from final timing tables. A successful Bridge start acknowledgement is not a
completed benchmark.

## Reproduction

Build the helper from the project root with a Windows x64 MSVC/CMake toolchain:

```powershell
cmake -S Assets/Mods/TomShadersX/Tests/GpuTimer -B CodexBridge/Builds/TomXGpuTimer -G "Visual Studio 17 2022" -A x64
cmake --build CodexBridge/Builds/TomXGpuTimer --config Release
```

Refresh through Bridge, then submit a uniquely named Inbox JSON:

```json
{"operation":"benchmarkxgpu","gridX":32}
```

`gridX` selects mesh subdivisions (8..128); it does not change the camera,
resolution or number of proxy characters. Read the start command's result,
then wait for the returned directory's `report.json` to say `complete`.
Assembly reload or cancellation aborts the run and restores the active scene,
light masks, pixel-light budget and render target. Do not edit C# while timing.

```powershell
& Assets/Mods/TomShadersX/Tests/Summarize-XGpuBaseline.ps1 -ReportPath <absolute-report-json-path>
```

The analyzer requires complete finite samples, checks X Add execution ratios
and validates Add-disabled counters. It writes `summary.csv` and `summary.md`
beside the raw report. PNG readback/export is outside timed samples.

## Certification Boundary

The user's non-performance KKS eye acceptance remains complete. This fixture
does not certify a full game frame, target laptop GPU, skinned characters,
hair feathering, body shaders, overdraw between characters, shadowed lights,
VR/stereo, Linear color space, KK, or compiled register/variant budgets. Target
game/device profiling remains a separate release-performance check.
