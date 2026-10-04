# SkinX Implementation And Verification

Date: 2026-10-04. The approved shared face/body `tom/SkinX` is implemented as the
eighth X entry in experimental package 0.3.0. This is an implementation delivery,
not a claim that SkinX has passed live KKS face/body or whole-game acceptance.
EyeX multi-light optimization remains deferred and no eye effects were reduced.

## Implemented Scope

- Narrow `TOM_SKIN` specialization of the common input, lighting and Clearcoat
  core. Skin input, coverage, depth, surface and diffuse modules are local files.
- Audited KKS overlays, four UV sets, vertex-color gates, nipple remap, clothing
  RG masks, Detail/Line channels, regional gloss and five liquid amounts.
- Diffuse-only detail-normal control, opt-in face-art normal softening, restrained
  front-side diffuse wrap/warmth and one shared optional wet Clearcoat.
- 167 frozen property declarations, matching MaterialEditor/tooltips, native
  default textures/material/prefab created by Unity Editor APIs, no stencil write.
- Independent metadata regeneration, eight-carrier isolated build and packaging
  workflow. The old seven-shader build is retained only as a historical subset.

The [material contract](XSeriesSkinContract.md) records defaults, sampling groups,
explicit legacy differences and migration instructions. The source audit and
approved design remain linked as evidence, not substitutes for tests.

## Editor Evidence

Paths below are relative to the Unity project root.

| Evidence | Result |
| --- | --- |
| `CodexBridge/Reports/XSkin-20261004-084400/report.json` | 196 checks passed, 105 float-readback GPU captures plus overview sheet |
| `preparexskin` | Packaged neutral default textures and carrier validated; 12 requested shader variants warmed, supported, no reported errors |
| `Tests/Test-XSkin.ps1` | 167-property snapshot/metadata/pass contracts, 144 CPU algebra samples |
| `Tests/Test-XSkinMetadata.ps1` | Byte-idempotence, seven-shader isolation, version preservation and regeneration |
| `Tests/Test-ProjectIsolation.ps1` | Local dependencies and all eight carrier bindings |
| Existing Alpha/Hair/CoatEye/EyeSurface/EyeMetadata tests | Passed without regenerating their frozen material contracts |

The GPU fixtures cover ordered overlay UVs/ST/color, vertex R/B gates, nipple
remap, runtime-like blush/gloss writes, regional gloss endpoints, painted shade
and lines, moving/stationary Detail R, diffuse/spec normal independence, mirrored
tangent handedness, liquid region amounts 0/1/2 and tiling isolation, coat coverage
and exact-off, Base/Add diffuse-art agreement, Base-only environment, blockers,
spot cookies, Rim/Outline suppression, clothing RG and Main alpha across passes.

Final post-build rerun `CodexBridge/Reports/XSkin-20261004-085153/report.json`
also passes all 196 checks/105 captures. All 106 PNGs including its overview are
SHA256-identical to the earlier Skin run after the named-enum Inspector fix.

The overview is `XSkin-20261004-084400/skin-overview.png`: neutral, softened,
warm-transition, and coated skin spheres from left to right. It is a synthetic
material comparison, not a character screenshot. All display PNGs use one fixed
exposure/tonemap; assertions use unmodified float readbacks.

Existing seven-shader before/after regression:

- Before: `CodexBridge/Reports/XCoatEye-20261004-081411`.
- After: `CodexBridge/Reports/XCoatEye-20261004-084457`.
- Both: 266 checks passed, 167 captures. All 180 PNG files including comparison
  sheets are SHA256-identical. This establishes no change in those tested images,
  not exhaustive equivalence across every keyword/platform/scene.

Unity 2019's inline Enum drawer limit was encountered on the 14 skin debug modes
and fixed with a named Editor enum; it does not add a game plugin dependency.
Unrelated existing AssetBundleBrowser duplicate GUID errors and V+ warnings were
left untouched. The bridge results above are authoritative for these fixtures.

## GPU Baseline

Completed report: `CodexBridge/Reports/XSkinGPU-20261004-084614/report.json`.
Unity 2019.4.9f1, D3D11, NVIDIA GeForce RTX 4070 Ti SUPER, project Gamma color
space, 1920x1080 linear ARGBHalf target, no MSAA. 55 cases, 165 shuffled blocks,
60 timed samples per case (3300 total), plus untimed warmups and statistics.
Every nonempty case had GPU primitives/pixel invocations and a nonblank preview.

Native D3D11 TIMESTAMP/DISJOINT measures BeforeForwardOpaque to AfterEverything,
not CPU Camera.Render or FPS estimates. Readback/polling and pipeline statistics
are outside timed samples. This interval excludes earlier camera setup/clear.
The empty case quantized to 0 ms and is not subtracted from the table.

GPU medians in milliseconds, rounded to four decimals:

| Layout | Lights | MainOpaqueX | Skin dry | Soft | Soft + coat | Soft + liquid | Soft + liquid + coat |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Face close | 1 | 0.0584 | 0.0881 | 0.0881 | 0.1055 | 0.0983 | 0.1157 |
| Face close | 4 | 0.1382 | 0.2171 | 0.2171 | 0.2499 | 0.2509 | 0.2785 |
| Face close | 8 | 0.2437 | 0.3830 | 0.3830 | 0.4403 | 0.4516 | 0.4936 |
| One body | 1 | 0.0348 | 0.0532 | 0.0532 | 0.0614 | 0.0584 | 0.0676 |
| One body | 4 | 0.1004 | 0.1444 | 0.1444 | 0.1587 | 0.1556 | 0.1720 |
| One body | 8 | 0.1628 | 0.2335 | 0.2335 | 0.2550 | 0.2529 | 0.2775 |
| Four bodies | 1 | 0.1290 | 0.1812 | 0.1812 | 0.2140 | 0.1987 | 0.2324 |
| Four bodies | 4 | 0.3164 | 0.4618 | 0.4588 | 0.5202 | 0.5059 | 0.5632 |
| Four bodies | 8 | 0.6103 | 1.0301 | 1.0271 | 1.1264 | 1.1346 | 1.1684 |

The face is a large sphere; bodies are seven-part sphere/capsule proxies at fixed
size, not real KKS meshes. One-body colored coverage is 259195 pixels, four-body
1036780 pixels, and face 748777 pixels. The timed scene has no skinning, hair,
clothes, animation, shadow maps or postprocessing, and uses neutral/simple data
textures. It exercises large surface coverage and repeated per-pixel lights,
but is neither worst-case texture traffic nor a whole-game/device certification.

In the four-body/eight-light case, Skin's adapter adds about 0.420 ms over the
general opaque fixture. Soft+liquid+coat adds about 0.138 ms over dry SkinX.
Individual sample variation is material: dry p10/p90 is 0.924/1.197 ms and
liquid+coat 1.089/1.362 ms. The soft/dry median difference is within noise; this
does not establish zero cost. No feature reduction or quality tier is justified
from this one high-end proxy run alone. Preserve effects and profile the actual
target character scene before prioritizing optimization.

## Package

Bridge `buildxskin` succeeded with strict Windows64 LZ4 build, no external bundle
dependencies, then reloaded all eight carriers/tooltips and checked SkinX shader
support and packaged neutral textures. Package archive size/payload checks pass.

Artifact:
`CodexBridge/Builds/TomX-0.3.0/TomShadersX-0.3.0-20261004-165103.zipmod`

SHA256: `0BF7144C5E1130AA13A8A1273378F5C825FC1754B3BFDDC5EF8F123571CBB6EE`.

This replaces the earlier Tom Shaders X zipmod with the same manifest identity;
do not install duplicate versions side by side. V+ LTS is not replaced. Optional
HairFeather DLL/bundle remains separate and unchanged. Nothing was installed into
either local game folder, and no Git commit/push was made.

## Reproduction

Run the static tests from `Assets/Mods/TomShadersX/Tests`. The bridge installer
registers `preparexskin`, `validatexskin`, `renderxskin`, `benchmarkxskin` and
`buildxskin`. Do not run renders/builds while the asynchronous benchmark is active.
Its start result is not completion; inspect report.status and wait for complete.

`buildxskin` builds only the eight X carriers and tooltip catalog with strict
LZ4 Windows64 output, then reloads and checks the package bindings/defaults.
`Tests/Package-XSkin.ps1` packages the current manifest and bundle, verifies the
archive payload and prints its SHA256. No game installation is modified.

## Skin Color Inheritance Investigation (2026-10-04, Closed)

The user reported white skin after changing to SkinX and suspected the base-color
property name. The user subsequently confirmed the material was correct:
Maker indirect lighting caused the misleading appearance. No color-property
rename or color-composition fix is needed.

Rechecked the local KKS Assembly-CSharp and MaterialEditor source:

- CreateFaceTexture/CreateBodyTexture send skinMainColor to the texture
  compositor's _Color, not directly to the final skin material.
- CustomTextureControl.SetNewCreateTexture assigns the composed RenderTexture
  to the final material's _MainTex. Adding another _Color multiply to the final
  shader would not repair that assignment and could double-tint composed skin.
- xukmi/SkinPlus uses _Col0.._Col3 for additional regional tint. SkinX already
  declares and consumes these exact names before its overlay chain. _BaseColor
  is a separate, white-default X multiplier on the resulting albedo.
- MaterialEditor.SetShader assigns material.shader on the existing material,
  then applies only explicit XML defaults. SkinX has no XML defaults resetting
  MainTex, Col0..Col3 or BaseColor.

Added TestColorInheritance to the Bridge GPU fixture. It changes existing stock
and V+ materials to SkinX, preserving a simulated composed skin RenderTexture,
its ST and named colors, then tests live Col0/overlay writes and the independent
BaseColor multiplier. Result: 220 total checks passed, 113 GPU captures in
`CodexBridge/Reports/XSkin-20261004-114925/report.json`. These are Editor tests,
not substitutes for game acceptance. The user clarification closes this report;
no additional screenshot is required for it.

## Indirect Default Revision (0.3.1)

At the user's explicit request, all eight shaders and bundled carrier presets
now use IndirectDiffuseIntensity=0.25 instead of 1. Only this scalar default in
the frozen snapshots is revised; names/types/ranges and lighting code remain
unchanged. Ambient/Unity SH/custom SH/lightmap diffuse share the gain. Direct
lights, environment specular and Clearcoat have independent controls.

No ME XML default or runtime migration is added: explicitly stored values remain
as authored, while materials without an override take the new shader default.
The coat/eye and skin regression factories and GPU benchmark presets explicitly
retain intensity 1 so past appearance/performance baselines remain comparable.
A separate skin GPU fixture checks the reduced default and independent layers.

Verification through Unity 2019.4.9f1/D3D11:

- `CodexBridge/Reports/XSkin-20261004-123115/report.json`: 254 checks passed,
  123 captures. All eight fresh-material defaults are 0.25; explicit 1.7
  overrides survive copying and shader reassignment. Indirect-only pixels are
  one quarter of intensity 1; albedo, Base/Add direct light, visible coat and
  substrate environment reflection stay identical when only this gain changes.
- All 114 existing Skin PNGs match the prior intensity-1 regression report
  byte-for-byte; ten captures and one comparison sheet exercise the new default.
- `CodexBridge/Reports/XCoatEye-20261004-123120/report.json`: 266 checks passed,
  167 captures with the fixed regression lighting values.
- Static alpha/hair/coat/eye/skin contracts, both metadata regeneration suites
  and eight-carrier project isolation checks passed.

This verifies the authored defaults and controlled rendering, not a new live
Maker lighting acceptance or a new performance measurement.

Strict Windows64 bundle build and reload passed, including all eight packaged
carrier values and fresh shader defaults at 0.25. Latest artifact:
`CodexBridge/Builds/TomX-0.3.1/TomShadersX-0.3.1-20261004-203253.zipmod`.
SHA256: `8DF0A8606D6682FED61EC64E8A174AE326F45E44DFD517B6252AE5EA8D2EE6A6`.
Replace the previous Tom Shaders X package rather than installing duplicates.
No game installation, V+ LTS source or optional HairFeather/probe plugin changed.

## Remaining Game Acceptance

- Test real face/body materials, card reload, expression changes, blush/makeup,
  nipples, nail/lip/cheek gloss and clothing toggles with actual mod UVs/textures.
- Verify intended data import/filter/wrap settings, seams/negative transforms,
  normal detail at distance and compatibility with other material-writing mods.
- Check colored/shadowed lights, reflection probes/LPPV/custom SH in target scenes;
  the synthetic suite does not exercise every integration configuration.
- Measure representative fully skinned characters with hair/clothes, animations,
  game shadows/postprocessing and target devices before full-game certification.

No quality reductions are preselected. No automatic face/body role presets,
true SSS/transmission, Face SDF, POM or extra transparent skin variant is included.
