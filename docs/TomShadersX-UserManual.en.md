# Tom Shaders X User Manual

For Tom Shaders X **0.3.1**, Koikatsu Sunshine, Unity 2019.4 Built-in Forward.
Optional companions: Hair Feather 0.3.0 and Tom Lighting Probes 0.2.1.
Property names below follow MaterialEditor (ME), without the leading underscore.

The closing audit covers code, ME metadata and Unity rendering. It does not include a new in-game acceptance run or whole-character performance certification. Showcase spheres are reproducible examples, not presets automatically applied to characters.

## 1. Installation and Shader Selection

Place the main zipmod in the game's `mods` directory. Sideloader and MaterialEditor register the shaders. The separated V+ LTS package can coexist with X; do not also install an older combined package that registers the same `tom/*` names.

| Shader | Purpose and distinction |
| --- | --- |
| `tom/MainOpaqueX` | General opaque or cutout surfaces; not alpha-blended glass |
| `tom/MainAlphaX` | Conventional transparency; optional DepthPrepass is off by default |
| `tom/MainAlphaXBackFront` | Back/front color layers for transparent shells; not a solution to arbitrary intersecting-mesh sorting |
| `tom/MainAlphaX2Pass` | Legacy depth-prepass compatibility entry; not BackFront, and not the first choice for new materials |
| `tom/HairX` | Cutout hair, anisotropic highlights, stencil-based front-hair transparency and optional feathering |
| `tom/EyeWX` | Eye-area surfaces such as sclera and eyelines; cutout plus stencil writer, with no automatic mesh-role detection |
| `tom/EyeX` | Iris and pupil shading, local refraction, shallow parallax, depth maps and optional RGB dispersion |
| `tom/SkinX` | One face/body shader preserving game skin layers, overlays, clothing masks and liquid conventions |

Ordinary surface shading, Clearcoat and EyeX optics need no additional plugin. HairX Feather mode requires `TomHairFeather.dll` and `hairx-feather.unity3d` in the same plugin directory. Unsupported configurations fall back to a hard edge; feathering is not available automatically without its provider.

For Studio probe items, install the separate Tom Lighting Probes zipmod and place `KKS_TomProbe.dll` and `TomProbe.Runtime.dll` in `BepInEx/plugins/TomProbe`. Do not keep duplicate older DLLs. Probe-control shaders belong on the probe items, not character materials.

## 2. A Consistent Editing Workflow

1. Select the appropriate shader and preserve game-managed textures, colors and UV parameters. Keep a card or scene backup before switching.
2. Turn off every Debug View. Check base texture, transparency and clipping before styling.
3. Start with Toon Shading and Lighting to establish the light/dark balance.
4. Adjust Material and Direct Specular next, distinguishing continuous highlights from Toon highlights.
5. Choose Environment Reflection or MatCap, then add Clearcoat, Rim and Outline as needed.
6. Tune eye, hair and skin features on top of the common baseline; there is no need to replace the stencil or transparency design.

Shared controls use consistent categories, types and ranges across the eight shaders. Game-compatible aliases deliberately retain their original spelling, including `SpeclarHeight`. If your ME version presents an enumeration as a numeric slider, enter the documented **integer**, not an intermediate value. The shaders use ME's existing texture, number and color controls; this manual does not assume custom dropdown support in every version.

`IndirectDiffuseIntensity` defaults to **0.25** for new materials. It scales indirect diffuse only, not direct lights, reflections or Clearcoat. Explicitly saved older values are not automatically changed. If skin looks washed out in Maker, check indirect illumination and exposure before renaming game color properties.

### Useful Starting Points

These are diagnostic starting points, not universal character presets. Keep lighting fixed and change one group at a time.

| Goal | Starting settings |
| --- | --- |
| Continuous diffuse | `UseRamp=0` |
| Clear two-tone shading | `UseRamp=1`, `RampMode=0`, `ToonThreshold=0.5`, `ToonSoftness=0.05` |
| Softer Toon transition | Increase `ToonSoftness` from the settings above, rather than only increasing indirect light |
| Continuous GGX highlight | `SpecularToonBlend=0`; adjust `Roughness` |
| Toon highlight | `SpecularToonBlend=1`; adjust `SpecularSize/Threshold/Softness`; enter integer `SpecularBands` from 1 to 4 |
| Wet-looking coat | `ClearCoat=0.5`, `ClearCoatRoughness=0.12`, `ClearCoatIOR=1.5` |
| Shallow recessed iris | `EyeOpticsMode=1`, `IrisDepth=0.08`, `EyeOpticsStrength=1`; inspect from an oblique angle |

## 3. Lighting and Stylization

**Toon Shading:** `UseRamp` blends continuous diffuse with Toon shading. `RampMode=0` uses procedural thresholds; 1 uses the R channel of RampTex. SecondaryRamp is blended through LayerMask R and SecondaryToneStrength. RampTex is a light-response curve, not a directly applied multicolor gradient.

**Shadows:** ShadowStrength controls diffuse shadow participation. ShadowRemapStrength / Threshold / Softness reshape shadow visibility. UseRampForShadows controls how shadows enter the Toon curve. External occlusion is distinct from the unlit side of a curved surface. Direct Clearcoat highlights remain shadowed; they are not unshadowed emission.

**Direct specular:** SpecularStrength is intensity, Roughness controls continuous-highlight roughness, and SpecularSize controls Toon-highlight size. These are not interchangeable. SpecularIOR / FresnelStrength govern the substrate's dielectric response; increasing Metallic reduces substrate diffuse. SpecularAA helps reduce normal/highlight-edge aliasing but does not replace all temporal antialiasing.

**Multiple lights:** MainLightIntensity / AdditionalLightIntensity scale the main and per-pixel additional lights. Direct diffuse, specular and Clearcoat respond to additional lights. SH, environment reflection, additive MatCap, Rim and emission are not added again for each light. Multiplicative MatCap still modulates each light's diffuse.

**Indirect diffuse:** LightProbeBlend transitions from AmbientColor to Unity's SH / Light Probe path. CustomSHVolumeBlend controls a ready custom SH Volume. IndirectToonBlend can stylize indirect shading but cannot create missing directional information. If no volume is ready, the built-in indirect path remains available.

**Environment reflection:** ReflectionMode: 0 off, 1 MatCap, 2 environment, 3 both. Environment mode needs an available probe or sky reflection. EnvironmentIntensity, Roughness and ToonBlend control its intensity, blur and stylization. With the default UseMaterialRoughnessForEnvironment=1, material roughness takes precedence over EnvironmentRoughness. Box Projection is useful for bounded interiors; it is not ray tracing.

**MatCap:** A view-space 2D appearance texture, not a live scene reflection. MatCapBlendMode: 0 multiply, 1 add. MatCapNormalSource: 0 geometric normal, 1 main normal, 2 main plus detail.

**Rim / Outline / Emission:** Rim is a view-dependent edge term; Outline is an expanded-shell contour. They are independent. Screen-space OutlineWidth is measured in pixels. Emission does not automatically illuminate nearby objects. SkinX uses UV4 for game overlays, so it cannot also store smoothed outline normals there.

## 4. Texture Channel Reference

Import color textures as color and disable sRGB for data textures. Import normal textures using Unity's normal-map workflow. Do not interchange ordinary RGB normals, RG flow directions and grayscale depth. For runtime ME imports, check the behavior of your installed version; this audit did not verify every in-game importer's color-space handling.

| Texture | Channels |
| --- | --- |
| MainTex | RGB color; A coverage, with behavior depending on the shader |
| MaterialMap | R metallic, G roughness, B AO, A direct-specular mask; replaces separate maps when UsePackedMaterialMap=1 |
| LayerMask | R secondary Toon, G Rim, B MatCap, A environment reflection |
| MetallicMap / RoughnessMap | R multiplies the matching scalar; RoughnessBias is added afterward |
| OcclusionMap | R AO; white means unoccluded; affects indirect terms, not a direct main-light switch |
| SpecularMask | R direct specular; not environment, MatCap or Rim; ignored in packed mode |
| ReflectionMask | R common reflection coverage, multiplied by LayerMask B/A |
| ClearCoatMap | R coat weight, G coat roughness multiplier; B/A unused; white is neutral |
| StrandDirectionMap | Linear RG decoded to a tangent-space axis in [-1,1]; not a Unity normal map |
| EyeSurfaceMap | Grayscale height by default, or R depth / A region; see the eye section |

Some textures share samplers to fit the Unity/D3D11 sampler budget. Shared filtering/wrap state does not imply shared ST transforms. In particular, SkinX UV0 masks need compatible atlas import settings. EyeSurfaceMap uses the final iris UV and has no independent ST.

## 5. Clearcoat

Clearcoat is a colorless dielectric specular/environment layer over the substrate. It is not parallax. Parallax changes the sampled position of internal detail; Clearcoat represents the glossy outer surface. Both can be enabled together.

- `ClearCoat=0` disables the layer without changing alpha or stencil.
- `ClearCoatRoughness` is independent of substrate Roughness. Lower values concentrate the highlight.
- `ClearCoatIOR` affects coat Fresnel and layer energy only. A value of 1 removes coat reflection but **does not change EyeX parallax displacement**.
- `ClearCoatEnvironmentStrength` is independent of substrate ReflectionMode. Disabling substrate reflection does not disable coat reflection.
- `ClearCoatEnergyBlend=0` preserves Toon diffuse brightness; 1 uses a layered attenuation approximation. Substrate specular is still attenuated by the coat.
- `ClearCoatNormalSource`: 0 geometric, 1 main, 2 main plus detail, 3 independent coat normal. EyeX evaluates its interface normal on the unshifted surface.

A bright coat can visually obscure iris contrast without disabling parallax. Temporarily turn off the coat or inspect displacement with EyeDebugView to distinguish the two.

## 6. HairX

StrandAngle: U direction is 0 degrees; V is 90 degrees. StrandDirectionBlend blends the manual direction with a flow map. HairAnisotropy controls elongated highlights. The current model has one specular lobe, not dual-layer hair scattering or anisotropic environment reflection.

HairFrontMode: 0 off, 1 hard stencil, 2 inward feather. HairFrontOpacity=1 is opaque. Outside stencil, hair remains ordinary cutout. Changing camera-side transparency does not punch holes in the cast shadow.

Feather Width Mode: 0 pixels, 1 projected world units. Pixels preserve legacy behavior; world units project with distance, FOV and resolution. Width, Midpoint and Power are independent. The provider caps large projected radii at a 256-pixel budget and reports clamping in its log.

Eye writers must render before hair. Typical queues are EyeWX 2472, EyeX 2474 and HairX 2475. Avoid arbitrary queue changes to expose one layer. The feather proxy recognizes supported writers; it is not an exact stencil copy for arbitrary shaders. Unsupported sclera can cause iris/sclera joins to become false boundaries. Use a supported EyeWX/EyeWPlus writer and check its queue. Mode 1 is a diagnostic comparison, not proof that feathering is universally correct.

The current provider targets monoscopic D3D11 Forward. Unsupported configurations, including stereo cameras, fall back to hard edges.

## 7. EyeWX and EyeX

EyeWX uses the game's `_Color` alias, with 0.5 neutral, followed by BaseColor. EyeX retains native iris rotation, expression overlays and highlight layers; do not indiscriminately clear those game aliases. Painted surface highlights can coexist with Clearcoat, but excessive stacking may look like duplicated reflections.

AlphaMask / Alpha / Cutoff control unshifted source coverage. StencilCutoff additionally gates stencil writing. EyeWX draws surviving coverage as cutout; EyeX continues to alpha-blend surviving coverage. **Optical region, transparency coverage and stencil coverage are separate.**

### Procedural Parallax

Enable EyeOpticsMode=1. IrisCenterX/Y and IrisRadiusX/Y define an ellipse in final MainTex UV. IrisDepthShape: 0 legacy flat floor, 1 shallow cone, 2 shallow bowl. IrisDepth is the shared global recess gain for both procedural and mapped modes, scaled relative to local iris radius, not meters or pixels. EyeOpticsStrength=0 restores original sampling.

EyeRefractionIOR is independent of ClearCoatIOR. Increasing refraction IOR reduces lateral parallax by design in this ray model. IOR=1 removes refractive bending, not the parallax caused by a recessed surface. To reproduce an older material's pre-separation result, manually copy its former ClearCoatIOR value to EyeRefractionIOR once. Saved materials are not forcibly migrated.

### Painted Depth and Region

UseEyeSurfaceMask=1 replaces the procedural ellipse boundary and EdgeFade with the texture. Align it with the main iris texture and its rotation/ST.

| EyeSurfaceMapMode | Encoding | No-effect region |
| --- | --- | --- |
| 0 grayscale height | White = surface / zero depth; black = maximum depth; A ignored | Paint white |
| 1 R depth + A region | R white = deepest, black = zero depth; A white = inside region | A=0, with a smooth boundary transition |

Both modes multiply IrisDepth. In mapped mode, radii still calibrate depth/displacement scale but no longer clip the painted region. A affects depth and entry transition, not surface transparency.

Suitable shapes include shallow flat floors, shallow cones, shallow bowls and smooth single-valued height fields. Do not convert painted highlights or iris color brightness directly into geometry. Avoid vertical walls, high-frequency noise and folded multilayer surfaces.

EyeOpticsMaxOffset limits extreme displacement. EyeUVMin/Max define safe atlas bounds. Shifted samples outside that region, or invalid geometric conditions, fall back to original UV. Increasing gain without limit is not a way to obtain a deep cavity.

### Dispersion and Viewing Angle

EyeDispersion=0 uses a single path. Positive values use a three-ray RGB geometric approximation. This is not wave optics and does not turn the whole coat into a rainbow. The difference is generally clearest at high-contrast edges and oblique angles.

Change the camera's actual direction relative to the eye surface when testing. Reframing alone, or an eye that continuously faces the camera, may show little change. Start with a clear texture and a shallow cone/bowl, then adjust Depth, IOR, maximum offset and safe bounds gradually.

This is not POM and does not guarantee that a near-side cavity wall hides far-side iris content. If that genuine self-occlusion becomes necessary, consider **POM / first-visible-hit search**. Weak angle response alone is not a reason to upgrade. No additional corneal mesh is needed.

## 8. SkinX and Standard Character Textures

The game already composites main skin color into MainTex. BaseColor is an additional tint, not a replacement for that composition. ColMask provides optional extra regional tinting; it is not the game texture compositor's ColorMask.

| Input | Purpose |
| --- | --- |
| ColMask + Col0..3 | Blend Col0 toward Col1 by R, then Col2 by G, then Col3 by B |
| DetailMask | R painted-highlight/specular gating, G painted shadow, B Rim/Outline suppression, A skin versus nail/lip gloss |
| LineMask | R internal lines, G game line-width exponent, B detail shadow; A unused |
| NormalMask | G can reduce diffuse normal detail; not a normal map and does not inherit the legacy B shadow fallback |
| overtex1 | UV1 (Unity UV2), controlled by vertex R; body nipple / face lip layers, with legacy tex1mask RG encoding |
| overtex2 | UV2 (Unity UV3), controlled by vertex B; the corresponding body layer or dynamic face blush |
| overtex3 | UV3 (Unity UV4); face eyeshadow, with body meaning dependent on the asset |
| AlphaMask | Clothing R/G masks activated by alpha_a/b; fixed threshold 0.5 |
| SkinControlMap | R softening, G warm transition, B painted wet region; not a replacement for original DetailMask |

DetailNormalMapScale also participates in game internal lines and painted shadows. SkinDiffuseNormalDetail reduces diffuse detail only, preserving specular/coat detail. SkinStrength / Wrap / Warmth provide front-side softening and a warm light-dark transition. They are not screen-space or volumetric SSS and do not transmit through external occluders.

SpecularPower / SpecularPowerNail preserve the game's skin and nail/lip gloss gains. SkinGameGloss controls their participation. They are not Roughness and do not automatically tune Clearcoat. SkinPatternStrength / notusetexspecular control the moving painted-highlight mask.

### Liquid and Wetness

`liquidmask` uses RGB combinations to encode five game regions. `liquidftop/fbot/btop/bbot/face` are the corresponding amounts from 0 to 2. `Texture2` R/G hold first- and second-stage liquid coverage. `Texture3` is the liquid tangent normal, not depth or color. These are final Skin material slots; do not confuse them with identically named game-compositor inputs.

For compatibility, ME presents LiquidTiling as a color control, but it is a raw vector: R/G are UV offsets; B/A are UV scales, default `(0,0,1,1)`. Do not apply color gamma correction to it.

SkinLiquidColorStrength controls liquid base color. SkinLiquidMaterial blends substrate roughness. SkinCoatCoverage: 0 ordinary coat map, 1 game liquid, 2 painted wetness, 3 union of both. This only limits Clearcoat coverage; ClearCoat must also be enabled. SkinWetness multiplies SkinControlMap B.

## 9. Reflection Probes and SH Volumes

In Studio, add an item from Tom Lighting > Probes and edit its control material through ME. RP 256/512/1024 and SH grid variants are starting configurations, not locked item types. There is no need to delete and reimport an item to change parameters. The plugin reads those parameters; the control shader does not capture lighting itself.

| Common control | Meaning |
| --- | --- |
| ProbeEnabled / Intensity | Enable and contribution strength; intensity changes do not recapture |
| ProbeRefresh | Either 0-to-1 or 1-to-0 requests one refresh; holding 1 does not refresh every frame |
| ProbeRefreshScope / Group | Scope 0 self, 1 same group, 2 all; Group is an integer |
| ProbeUpdateMode | 0 load, 1 position change, 2 interval, 3 manual |
| ProbeCaptureOnLoad | Capture on initial activation or re-enabling, independent of update mode |
| ProbeInterval | Request interval in seconds; RP minimum 0.5, SH minimum 5; not a completion-time guarantee |
| ProbeSize / Offset X/Y/Z | World-axis-aligned bounds/offset; use these instead of item scale/rotation |
| ProbeNearClip / FarClip | Each cube face's clipping planes; NearClip is not a spherical exclusion radius |
| ProbeCapturePreset | 0 environment layers, 1 including characters, 2 custom 32-bit layer mask |
| ProbeQuality | RP: 0=256, 1=512, 2=1024 pixels per cubemap face |
| SHGridX/Y/Z | 2-8 per axis; total samples equal their product |
| SHCaptureQuality | 0/1/2/3 mean 8/16/32/64 pixels per face |
| SHSamplesPerFrame | Request 1-4 points per frame, also subject to the plugin's shared budget |

RP supplies specular environment reflection; SH supplies low-frequency directional diffuse, not sharp highlights or real-time shadows. Custom SH selects one volume per renderer using bounds center, priority and edge weight. It is not per-pixel blending of multiple volumes.

Capture axes remain world-aligned when the item rotates. A parent rotation that changes the item's world position is still a real position change. Near clip only clips nearby geometry. Prefer capture layers to avoid capturing the character itself; characters on unusual layers are not guaranteed to be excluded by the environment preset.

The shared queue performs one capture task at a time. A group refresh schedules SH before RP. During SH refresh, the previous atlas and matching bounds remain active until the replacement is complete and rebound. The first capture, without historical data, uses the built-in indirect fallback. RP resolution/HDR changes recreate its helper and may temporarily fall back to other probes or the sky.

Only parameters are saved with the scene, not captured lighting; reloads require recapture. Each SH sample renders six faces and performs synchronous readback. Spreading work across frames is not free asynchronous GI; complex scenes can still stall. RP requires Unity's Realtime Reflection Probes quality option. The plugin does not silently change that global setting.

## 10. Troubleshooting and Limits

| Symptom | Check first |
| --- | --- |
| Washed-out color | Indirect diffuse/environment strength, Clearcoat, exposure, duplicate highlights; new indirect default is 0.25 |
| Roughness seems ineffective | Toon versus continuous specular, material-driven environment roughness, zero texture G |
| Missing coat reflection | ClearCoat, IOR=1, coat coverage map, SkinCoatCoverage, available environment |
| Weak parallax | Relative view angle, Optics mode, Depth/Strength, IOR, depth-map color space, bounds fallback |
| Internal seams in eye-area feathering | Complete supported sclera/iris/eyeline writers and queues before hair |
| Incorrect intersecting transparency | Queue, mesh intersections and depth writing; BackFront is not OIT |
| No SH response | Plugin/item, completed capture, bounds, CustomSHVolumeBlend, Debug View |
| Mask colors instead of shading | Reset Eye/Skin/Clearcoat/Lighting Debug modes |

Deferred without reducing current effects: EyeX targeted multi-light optimization (not a closing blocker); POM only when true self-occlusion is required; separately designed HairX dual lobes and other additions; the EyeWX sclera versus brow/eyeline split. Probe capture budgets and whole-/multi-character game GPU cost require separate measurements, not inference from spheres.

## 11. Showcase Reference

The companion gallery provides standard-sphere comparisons, eye-angle views, individual PNGs, generated texture inputs and expandable tables recording all material parameters. Comparisons use consistent camera/lighting and a fixed display transform except where a documented angle or lighting change is the subject of the comparison. The SH showcase isolates diffuse response with known coefficients; an independent Unity runtime regression checks actual probe capture/refresh behavior.

For source installation and packaging, see Documents/Development.md in the source repository.
