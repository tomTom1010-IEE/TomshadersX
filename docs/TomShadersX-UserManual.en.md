# Tom Shaders X User Manual

For Tom Shaders X **0.4.0**, Koikatsu Sunshine, Unity 2019.4 Built-in Forward.
Install TomRenderInfrastructure KKS 0.1.0 first: it supplies Core, Hair Feather 0.3.0, Tom Lighting Probes 0.2.2, SkinX Face SDF 0.1.1 and Tessellation Bounds 0.1.1. Infra1 shader releases no longer embed DLLs. Shader/Bundle bytes are unchanged; this is not a new whole-character performance certification.
Property names below follow MaterialEditor (ME), without the leading underscore.

The closing audit covers code, ME metadata and Unity rendering. It does not include a new in-game acceptance run or whole-character performance certification. Showcase spheres are reproducible examples, not presets automatically applied to characters.

## 1. Installation and Shader Selection

Install [TomRenderInfrastructure 0.1.0](https://github.com/tomTom1010-IEE/TomRenderInfrastructure/releases/tag/v0.1.0), then place the main TomX zipmod in the game's `mods` directory. Sideloader and MaterialEditor register the shaders. The shader pack no longer includes helper DLLs or Probe resources; use the base once for both X and BSDF. The separated V+ LTS package can coexist with X; do not install an older combined package with duplicate `tom/*` registrations.

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
| `tom/LiquidX` | Independent thin-liquid surface or transparent overlay using the shared liquid core |
| `tom/MainOpaqueXTess` | Independent D3D11 tessellation / displacement version of MainOpaqueX |
| `tom/MainAlphaXTess` | Independent D3D11 version of MainAlphaX; shared deformation in depth prepass, color, additional lights, outline and shadows |
| `tom/SkinXTess` | Independent D3D11 tessellation / displacement version of SkinX, preserving face/body layers and UVs |

Ordinary surface shading, Clearcoat and EyeX optics need no additional plugin. HairX Feather mode requires `TomHairFeather.dll` and `hairx-feather.unity3d` in the same plugin directory. Unsupported configurations fall back to a hard edge; feathering is not available automatically without its provider.

The base supplies the Tom Lighting Probes 0.2.2 zipmod and matching `KKS_TomProbe.dll` / `TomProbe.Runtime.dll` in `BepInEx/plugins/TomProbe`. Replace old 0.2.1 resources and DLLs together; do not keep duplicates. Probe-control shaders belong on probe items, not character materials.

Maintainer source ownership: build TomHairFeather, TomSkinSDF and TomTessellation only in TomRenderInfrastructure. Infra1 requires one matching TomRender.Core.dll in BepInEx/plugins/TomRenderInfrastructure and the accepted provider/Probe cohort; Core has no BepInEx entry. TomX keeps its three Assembly-CSharp authoring mirrors, original metadata and Feather helper source. The old 0.4.0 Complete archive remains historical; new Infra1 shader archives omit providers. Controlled KKS regression covers native DOF/glass, Probe/SH and synthetic Feather/save-load, not all characters or GPU budgets. See the source build guide and base installation instructions.

## 2. A Consistent Editing Workflow

1. Select the appropriate shader and preserve game-managed textures, colors and UV parameters. Keep a card or scene backup before switching.
2. Turn off every Debug View. Check base texture, transparency and clipping before styling.
3. Start with Toon Shading and Lighting to establish the light/dark balance.
4. Adjust Material and Direct Specular next, distinguishing continuous highlights from Toon highlights.
5. Choose Environment Reflection or MatCap, then add Clearcoat, Rim and Outline as needed.
6. Tune eye, hair and skin features on top of the common baseline; there is no need to replace the stencil or transparency design.

Applicable substrate shaders share the common lighting baseline and editing categories. Equal names do not guarantee identical types, ranges or defaults in every family: LiquidX styles pigment on a thin liquid overlay, while Skin and Eye keep deliberate game-compatible and optical differences. Each Tess entry inherits its ordinary parent's controls and lighting, then adds geometry controls. Game-compatible aliases deliberately retain their original spelling, including `SpeclarHeight`. If your ME version presents an enumeration as a numeric slider, enter the documented **integer**, not an intermediate value. MatCapBlendMode is explicitly continuous, not an enumeration. The shaders use ME's existing texture, number and color controls; this manual does not assume custom dropdown support in every version.

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

**Toon normals and soft edges:** MainOpaqueX, the three Alpha variants, HairX, EyeWX, EyeX and SkinX share this path. `ToonNormalInfluence` (0..1, default 1) controls the main normal in direct Toon diffuse only. `ToonDetailNormalInfluence` (0..1, default 1) independently controls detail normals; SkinX reuses `SkinDiffuseNormalDetail` instead, with its existing diffuse/SH behavior. Main influence 0 does not disable the detail input. Global NormalMapScale/DetailNormalMapScale still determine the source normal strength. Specular, environment and Clearcoat retain their full normal inputs. EyeX Toon normals follow the refracted iris sample without changing refraction itself.

Toon diffuse now uses one mesh-horizon transition driven by ToonSoftness, without multiplying the mapped-normal horizon again. NormalHorizonFade still protects continuous diffuse, specular, Clearcoat and liquid. `ToonAA` (0..2, default 1) adds a pixel-derivative minimum width to the procedural band and diffuse horizon. Texture ramps retain their own filtering; AA cannot repair compression blocks or erase large authored bumps. For cleaner skin boundaries, try ToonNormalInfluence=0.25, SkinDiffuseNormalDetail=0.1 and ToonSoftness=0.12, then adjust for the asset. Glossy detail is preserved without disabling normal maps globally.

**Toon minimum lighting:** `ToonMinLighting` (0..1, default 0.15; set 0 to disable) remaps the main-light Toon diffuse response after the mesh horizon and cast shadows: `u' = m + (1-m)*u`. It lifts self-shadowed and externally shadowed regions while retaining the fully lit endpoint. Start at 0.1..0.25 with ToonShadeColor A=0. Bundled carriers use 0.15. Explicitly saved values are preserved; older materials without a saved value inherit the new default. This is a normalized diffuse-response floor, not a final pixel RGB floor or a GI/emission control. Albedo, main light color/intensity, distance/cookie, diffuse material weight and later coating/MatCap attenuation still apply. No light means no lift. UseRamp=0 disables it; partial UseRamp blends its contribution. It runs once in Base, respects baked direct ownership and does not alter continuous diffuse, Add lighting, SH, specular or reflection lobes. ToonShadeLevel still controls the procedural ramp before the horizon and shadows.

**Toon shade color:** `ToonShadeColor` is the renamed `ToonDarkFillColor`; its optional colored-fill algorithm is unchanged. RGB multiplies albedo; A is fill strength, not surface opacity. Default RGB=(0.35,0.35,0.35), A=0. It is not a tint on the new minimum response. To brighten fully self-shadowed areas, use ToonMinLighting instead of increasing this color.

**Rename compatibility:** the shader and Material Editor property ID changed to ToonShadeColor. The Unity build step migrates saved values in project release materials without overwriting an explicitly saved new value. Existing game cards/material edits stored as ToonDarkFillColor are not automatically migrated by a shader; reapply their RGBA values to ToonShadeColor. No game files or runtime migration plugin are modified.

Fill follows the ForwardBase main light's color, MainLightIntensity, distance/cookie attenuation and physical cast shadows, even when ShadowStrength bypasses shadows for ordinary diffuse. No main light means no fill; UseRamp=0 also disables it. It runs once, never repeated in ForwardAdd, and respects baked direct-light ownership. Metallic and optional diffuse-energy suppression still reduce diffuse fill. This is artistic main-light-driven fill, not emission, physical transmission or SSS. ShadowColor and the indirect default of 0.25 remain unchanged.

Clearcoat and clear integrated liquid attenuate colored fill and minimum-response lift as broad substrate illumination, not as a back-facing specular ray. Their actual reflection lobes and horizon protection are unchanged. Pigmented liquid can replace the substrate contribution as before. The separate LiquidX overlay retains its accepted pigment path and does not expose these substrate controls. The minimum defaults to 0.15; set it to zero to disable the lift. Explicitly saved values are preserved.

**Shadows:** ShadowStrength controls diffuse shadow participation. ShadowRemapStrength / Threshold / Softness reshape shadow visibility. UseRampForShadows controls how shadows enter the Toon curve. External occlusion is distinct from the unlit side of a curved surface. Direct Clearcoat highlights remain shadowed; they are not unshadowed emission.

**SkinX grazing-light shadows:** SkinX now defaults to `UseRampForShadows=0`, including its bundled carrier. The surface still uses Toon lighting, but cast-shadow visibility multiplies the result without also crossing the Toon threshold. Set 1 to restore the previous routing. Explicitly saved scene/card values are not migrated; other shader families keep their defaults. `ToonMinLighting` remains 0.15. For cleaner grazing-angle self-shadows, select **Soft Shadows on the light** and keep ShadowRemapStrength=0 as a starting point. ToonSoftness and ShadowSoftness do not enable the engine's shadow filtering. The shader does not modify scene lights or force a shadow quality setting. Raw shadow-map striping can still exist, especially with Hard Shadows; decoupling the ramp is not a replacement shadow filter. Tune Light Bias and Normal Bias independently only as needed, checking nose/contact shadows and thin external casters. Excessive bias or ShadowOffset can detach or erase valid shadows; their defaults are unchanged.

**Direct specular:** SpecularStrength is intensity, Roughness controls continuous-highlight roughness, and SpecularSize controls Toon-highlight size. These are not interchangeable. SpecularIOR / FresnelStrength govern the substrate's dielectric response; increasing Metallic reduces substrate diffuse. SpecularAA helps reduce normal/highlight-edge aliasing but does not replace all temporal antialiasing.

**Independent highlight normals:** `SpecularNormalInfluence` and `SpecularDetailNormalInfluence` (0..1, both default 1) separately control the main/detail inputs for dry direct GGX and Toon highlights, in Base and ForwardAdd. Zero removes only that input; setting both to zero uses the mesh normal. HairX keeps its existing `HairSpecularNormalInfluence` / `HairSpecularDetailNormalInfluence` pair instead, applied once to both lobes, with no duplicate shared pair. AA follows the selected highlight normal; geometric light-facing protection and cast shadows remain active. These controls do not change diffuse/SH, probe reflections, MatCap, Clearcoat or liquid. Global normal scales still set source strength. EyeX uses the refracted iris samples, with the pre-refraction surface footprint retained for AA. Default 1/1 preserves old materials; no texture samples are added.

**Multiple lights:** MainLightIntensity / AdditionalLightIntensity scale the main and per-pixel additional lights. Direct diffuse, specular and Clearcoat respond to additional lights. SH, environment reflection, additive MatCap, Rim and emission are not added again for each light. Multiplicative MatCap still modulates each light's diffuse.

**Indirect diffuse:** LightProbeBlend transitions from AmbientColor to Unity's SH / Light Probe path. CustomSHVolumeBlend controls a ready custom SH Volume. IndirectToonBlend can stylize indirect shading but cannot create missing directional information. If no volume is ready, the built-in indirect path remains available.

**Environment reflection:** ReflectionMode: 0 off, 1 MatCap, 2 environment, 3 both. Environment mode needs an available probe or sky reflection. EnvironmentIntensity, Roughness and ToonBlend control its intensity, blur and stylization. With the default UseMaterialRoughnessForEnvironment=1, material roughness takes precedence over EnvironmentRoughness. Box Projection is useful for bounded interiors; it is not ray tracing.

**MatCap:** A view-space 2D appearance texture, not a live scene reflection. `MatCapBlendMode` is a continuous 0..1 mix: 0 retains the original Multiply, 1 retains the original Add, and intermediate values interpolate their complete compositions rather than stacking both at full strength. The property name and default 1 are unchanged; saved 0/1 endpoints preserve their appearance. Previously saved fractional enum values now produce a real mix instead of the old 0.5 hard switch. MatCapNormalSource: 0 geometric normal, 1 main normal, 2 main plus detail.

`MatCapColor` defaults to white RGBA (1,1,1,1). RGB multiplies the sampled MatCap color. A is an independent 0..1 whole-layer weight: 0 restores the no-MatCap result, 1 retains full weight. It fades the original intensity-shaped result, so Multiply keeps its existing saturated intensity while Add keeps its original unsaturated intensity. A never changes mesh opacity, clipping or shadow casting; the MatCap texture's alpha remains unused. Black RGB is not an off switch: it can darken Multiply. Use A=0 or MatCapIntensity=0 to disable the effect. Explicit saved colors are preserved.

Multiply still modulates diffuse consistently in Base and each ForwardAdd; additive MatCap is added in Base only. Specular, environment reflection, coat/liquid reflection lobes, Rim and emission are not directly tinted by MatCapColor. Existing coat and liquid composition remains in place. Each active Pass reuses one MatCap sample for both portions. Pure Add skips MatCap sampling in ForwardAdd; intermediate modes require the multiplication path there and are not a zero-cost substitute for pure Add. These controls apply to all eleven MatCap-capable ordinary/Tess entries, not the LiquidX helper.

`MatCapNormalInfluence` and `MatCapDetailNormalInfluence` (0..1, both default 1) independently control MatCap's main/detail inputs across all eight substrate shaders, including HairX. The source selector remains authoritative: Geometry ignores both sliders; MainNormal uses only the main slider; CombinedNormal uses both. Zero main influence does not remove detail in CombinedNormal mode. Both zero give the mesh normal. The controls affect additive MatCap and each light's diffuse-multiplicative MatCap, not other layers. EyeX MatCap follows refracted normal samples. Defaults retain the original selected source and existing material values.

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

### ME Categories

The four adjacent hair-highlight categories follow flow, primary/common controls, secondary controls, then optional local controls. This is a metadata-only reorganization: property IDs, ranges, defaults, saved values and lighting calculations are unchanged. ME can still sort rows by property name/type according to its settings.

| Category | Controls and scope |
| --- | --- |
| Hair Direction | StrandAngle, StrandDirectionMode, StrandDirectionMap and StrandDirectionBlend. Shared strand flow only. |
| Hair Highlight | Shared HairAnisotropy, HairSpecularNormalInfluence, HairSpecularDetailNormalInfluence and HairHeightAxisAngle; primary HairPrimaryHeight, HairHighlightShift, HairPrimaryLongitudinalWidth and HairPrimaryAzimuthalWidth. |
| Hair Secondary Highlight | HairSecondaryWeight and all secondary height, angular shift, physical/Toon shape and tint controls, including HairSecondaryHeight. |
| Hair Local Controls | UseHairHighlightMap, HairHighlightMap, HairPrimaryShiftMapScale and HairSecondaryShiftMapScale. Optional, not required for Surface Height. |
| Environment Reflection | HairEnvironmentAnisotropy now lives beside the existing environment controls; the separate Hair Environment category is removed. |
| Direct Specular | Gain, color, mask, shadow visibility, Bands and AA are shared by both lobes. SpecularToonBlend/Size/Threshold/Softness shape only the primary. |
| Material | Shared physical roughness, map, bias, IOR and Fresnel. The secondary adds its own RoughnessOffset; Toon Size is separate. |

There is no separate Hair Surface Height category: primary height and the common height axis are in Hair Highlight, while secondary height is in Hair Secondary Highlight. HairFront and Debug stay independent. Head Band remains archived and hidden.

SecondaryWeight=0 makes other secondary controls visually inactive. UseHairHighlightMap=0 ignores its map and local gains. A zero ShiftMapScale removes that channel's angular offset, not the map's coverage. Toon Size/Threshold/Softness need a positive Toon blend; SecondaryRoughnessOffset affects only a continuous secondary contribution. These dependencies are documented in tooltips, not implemented by changing saved values or dynamically hiding additional controls.

### Color, Flow and Highlights

ColorMask follows the KKS/xukmi RGB rule: blend white toward Color using R, then toward Color2 using G, then toward Color3 using B. These are sequential overrides, not additive weights. An unassigned or black mask preserves the texture color; red selects Color, white selects Color3. ColorMask has independent tiling/offset; filtering and wrap follow MainTex to respect the D3D11 sampler limit. BaseColor remains an additional whole-material multiplier. For native game coloring, leave BaseColor white; older manually tinted HairX materials may need BaseColor reset to white to avoid double tinting. Shader switching still depends on the game/ME retaining the original mask and Color/Color2/Color3 values.

StrandAngle: U direction is 0 degrees; V is 90 degrees. StrandDirectionBlend blends the manual direction with linear RG flow data (`RG*2-1`, not a Unity normal map). StrandDirectionMode=0 keeps the old unoriented-axis sign alignment. Mode 1 treats arrows as root-to-tip and interpolates angles, including opposite arrows. Author this direction consistently; UV layout cannot identify hair roots automatically. Zero flow falls back to StrandAngle. Missing mesh tangents only get a finite fallback, not a guaranteed correct strand direction.

HairHighlightShift and HairSecondaryShift rotate each direct specular frame along the strand, in degrees. Positive tilts the specular normal toward the positive strand arrow; screen-space band motion depends on geometry, light and view. This is not world height. Both continuous GGX and Toon highlights shift; diffuse, shadow projection and the coat normal keep their own frames. Primary/secondary LongitudinalWidth and AzimuthalWidth multiply the along/across-strand widths (1 is neutral). HairAnisotropy remains the common basic aspect control.

HairSecondaryWeight=0 preserves the single-lobe appearance. A positive share w forms `(1-w)*primary + w*secondary` under the existing global SpecularStrength, SpecularColor, mask, IOR and real-light visibility. Secondary Size/Threshold/Softness/ToonBlend shape its Toon endpoint; SecondaryRoughnessOffset modifies its physical endpoint after the existing roughness map. SpecularBands and SpecularAA are shared. SecondaryColor RGB is a relative tint (alpha unused); SecondaryBaseColorTint blends toward effective MainTex * BaseColor * KKS three-color tint. This is an artistic two-lobe reflection approximation, not complete fiber scattering. Both lobes are beneath the independent Clearcoat layer.

UseHairHighlightMap enables independent UV0 local controls: linear R/G are signed primary/secondary shifts centered at 0.5, scaled by Primary/SecondaryShiftMapScale in degrees; B/A are primary/secondary coverage. The supplied carrier material explicitly binds the neutral texture `(0.5,0.5,1,1)`. When enabling the feature on another material, assign a control map: an unbound runtime slot uses white (full coverage and positive shift), not automatic neutral data. The off default and zero local gains preserve old materials. Import custom data with sRGB off. It has independent ST but shares MainTex filtering/wrap state; author filtering and atlas edges accordingly. Masks only attenuate their lobes, with no renormalization or geometry clipping. SpecularMask remains the total direct-highlight mask. Global plus local shifts are bounded to +/-40 degrees.

HairSpecularNormalInfluence and HairSpecularDetailNormalInfluence independently flatten the existing main/detail inputs for direct highlights only. Defaults 1/1 reproduce the previous path. In Base and ForwardAdd, both lobes use the selected normal for highlight shape, derivative AA and normal-horizon attenuation: an excluded normal no longer suppresses the highlight indirectly. Mesh light-facing protection and real cast shadows remain active. Toon diffuse, probe normal, MatCap and Clearcoat retain their normals. HairEnvironmentAnisotropy=0 preserves old probe reflections; positive values blend a bent reflection direction using HairAnisotropy and the strand axis. Environment roughness stays independent, with no extra probe samples or ForwardAdd environment work. Unity's isotropic cubemap prefilter makes this an approximation, not a two-lobe anisotropic IBL convolution.

HairHighlightDebugView: 1 weighted primary direct lobe; 2 weighted secondary; 3 world-space direction; 4 primary/secondary effective shifts in RG (0.5 = zero, endpoints +/-40 degrees); 5 coverage in RG; 6 selected highlight normal. Lobe views include additional lights before coat transmission; data views suppress Add. All suppress fog/outline while preserving coverage. New defaults are neutral/off, with no automatic replacement of saved values. Example starting point: primary shift -10, secondary shift +12, share 0.35, secondary size 0.62, softness 0.3, Toon blend 0.7, roughness offset 0.18 and hair-color tint 0.35. These are optional example settings, not new defaults.

### Texture-Free Surface Height

HairPrimaryHeight and the shared HairHeightAxisAngle are in Hair Highlight; HairSecondaryHeight is in Hair Secondary Highlight. Primary/SecondaryHeight (both default 0, range -1..1) bias the existing procedural anisotropic reflection frame, not a Gloss image. No positioning-mode switch, HairGloss texture, UV2, head binding or extra plugin is required. These are dimensionless artistic slope biases, not distances or degrees: rotate the complete N/T reflection frame by `-atan(Height)` about `cross(selectedNormal, surfaceHeightAxis)`. GGX and Toon use the same biased frame, including their internal visibility terms; real geometric light/view guards, light data, Fresnel, shadows, masks, tint and the (1-w)/w budget remain. Selected-frame derivatives drive normal AA. No third lobe or unlit layer is added. The previous angular Shift parameters keep their degree-based, strand-relative meaning and are applied before the independent surface-height frame bias.

HairHeightAxisAngle defines a separate mesh-tangent-plane position axis: 90 degrees is mesh V, -90 reverses V, and 0 is U. It does not use StrandAngle or the strand flow map. Thus changing strand orientation no longer changes which mesh axis the height control uses. Both zero heights bypass it. The selected existing main/detail highlight normal projects the axis, without additional texture reads. Tangents follow normal mesh deformation. Mesh V is not guaranteed world/head/screen-up, and rotated/mirrored or inconsistent card UV layouts can still require axis calibration; this is not an absolute head-height guarantee. The xukmi comparison motivated the simple control workflow, not numeric compatibility with its SpeclarHeight/UV2/Gloss sampling algorithm. No legacy alias is introduced.

Start with PrimaryHeight=0.2, SecondaryHeight=0, HeightAxisAngle=90, Primary Shift=0 and SecondaryWeight=0. Inspect HairHighlightDebugView=1 (primary lobe), then try -0.2/0/+0.2. If movement is reversed, set HeightAxisAngle=-90. If the asset's U is the intended height direction, use 0 instead. Keep the desired strand flow unchanged; existing Size/Roughness/LongitudinalWidth/AzimuthalWidth still control shape. Return HairHighlightDebugView to 0 for the final render. For a secondary, try Weight=0.25 and SecondaryHeight=-0.15, then tune its existing size/softness. Real light and shadow changes can alter the visible brightness maximum.

The former Head Band experiment is archived. Its nine saved properties are hidden compatibility slots and no longer affect rendering, including saved PositionMode=1. Only Surface Height is active. Debug now offers 0..6; retired values 7/8/9 fall back to the final view rather than displaying head-frame data. TomHairHeadBand.dll is no longer built or included: remove that obsolete optional DLL from an older test installation to stop its background scanning. Keep the unrelated HairFeather, SDF and other companion plugins.

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

### Missing Skin Maps: Explicit Binding

Keep correctly assigned game or authored maps. If either slot is genuinely empty,
explicitly import the corresponding supplied data PNG in ME:

- `DetailMask`: `Material/SkinDetailNeutral.png`, RGBA **(1,0,0,1)**. It keeps the
  highlight gate open, adds no painted shade/Rim suppression and selects skin gloss.
- `SkinControlMap`: `Material/SkinControlNeutral.png`, RGBA **(1,1,0,1)**. Softening
  and warmth remain slider-controlled, with no authored wet region; A is unused.

These files are included in the complete ZIP and source package. An empty runtime
slot is **not** a neutral map: ShaderLab falls back to black DetailMask (R/A=0)
and white SkinControlMap (B=1). That can suppress gloss or make authored-wetness coat
coverage full. Editor importer defaults and carrier bindings do not guarantee ME
shader switching will fill empty slots. There is no automatic replacement plugin
or heuristic based on texture dimensions/content. This applies equally to SkinXTess.

DetailNormalMapScale also participates in game internal lines and painted shadows. SkinDiffuseNormalDetail reduces diffuse detail only, preserving specular/coat detail. SkinStrength / Wrap / Warmth provide front-side softening and a warm light-dark transition. They are not screen-space or volumetric SSS and do not transmit through external occluders.

SpecularPower / SpecularPowerNail preserve the game's skin and nail/lip gloss gains. SkinGameGloss controls their participation. They are not Roughness and do not automatically tune Clearcoat. SkinPatternStrength / notusetexspecular control the moving painted-highlight mask.

### Independent Painted Shade Color

SkinX and SkinXTess separate painted diffuse shade from cast-shadow tint. In ME, Skin Response contains SkinShadeStrength, SkinShadeColorSource and SkinShadeColor together. Select SkinShadeColorSource=1 (Independent) to tint the combined DetailMask G / LineMask B painted shade with SkinShadeColor RGB; SkinShadeStrength still controls its strength. Color alpha is unused: it is neither opacity nor a second strength. Both source channels retain their existing union, not two stacked shade layers. The tint affects skin diffuse lighting in Base/Add and indirect/fill paths, not specular, coat or liquid reflection lobes.

ShadowColor retains the shared baseline role: Base-pass indirect diffuse tint in main-light cast shadow. SkinLineColor independently controls internal lines; ToonShadeColor remains optional colored main-light fill, not a global shadow tint. ToonMinLighting stays 0.15.

Compatibility: the shader default SkinShadeColorSource=0 (FollowShadowColor) preserves old saved materials and runtime-created materials without the new controls. In this mode SkinShadeColor is ignored and painted shade still follows ShadowColor. The two release carriers use Independent with their previous ShadowColor RGB copied, preserving their starting appearance. For an existing card, first copy its ShadowColor into SkinShadeColor, then select source 1; no automatic game-card migration or parameter overwrite is performed.

### Optional Face SDF

SkinX and SkinXTess support an independent face SDF threshold map. Enable `UseFaceSDF` and `UseRamp`, assign `FaceSDFTex`, and use `FaceSDFMask` R to limit it to the face. `FaceSDFStrength=0` or a black region restores normal-driven Toon shading. The default switch is off; existing body/face materials retain their settings. DetailMask keeps its original role and is not an SDF map.

Import both maps as linear data (sRGB off), not normal maps. They use UV0, independent scale/offset, and shared bilinear/clamp sampling. Higher threshold-map values stay lit longer as the light moves behind the head. `FaceSDFChannel` selects R/G/B/A; `FaceSDFInvert` reverses polarity. Side modes: 0 Single uses the selected channel on both sides; 1 MirrorU mirrors U for head-left light about `FaceSDFMirrorU` (default 0.5, in texture space after ST); 2 RGPair uses R for head-right and G for head-left without mirroring. RGPair ignores the channel selector. `FaceSDFMask` is never mirrored.

`FaceSDFAngleMode=0` uses cosine encoding: (1-frontCosine)/2. Mode 1 uses absolute azimuth/180 degrees. Both have thresholds 0 at front, 0.5 at side and 1 at back. Select the convention used by the asset; a single format cannot automatically interpret every imported SDF. `FaceSDFOffset` shifts the light threshold (positive expands shade). `FaceSDFSoftness` controls the edge width, with `ToonAA` providing pixel filtering. Existing primary/secondary ramp endpoints supply the two brightness levels; intermediate texture-ramp bands are not replayed. The SDF region replaces the normal-driven Toon boundary and its diffuse horizon, while actual cast-shadow visibility is still applied. `ToonMinLighting` remains 0.15.

`FaceSDFDirectionSource=0` uses renderer axes; mode 1 uses the optional `TomSkinSDF.dll` head provider and falls back to renderer axes when tracking is unavailable. Install that DLL under BepInEx/plugins to follow animated KKS head bones or a humanoid Head bone. It needs no helper AssetBundle and does not alter material settings. `FaceSDFForward` / `FaceSDFUp` XYZ define the local head axes (default +Z/+Y; W unused); in ME these are vector values displayed as color fields, not tints. Imported -Z faces may require Forward=(0,0,-1,0). In tracked mode the axes are head-bone local, not renderer local. Unsupported custom rigs require an explicit `TomSkinSDFHead.head` binding in Unity. Lights directly above/below the head smoothly fall back to normal shading because their azimuth is undefined.

SDF changes direct Toon diffuse in Base/Add only. Specular, SH, Clearcoat, liquid normals, geometry clipping and shadow casting keep their original paths. It cannot identify self-shadow separately from hair/hand/clothing projection. `FaceSDFDebugView` shows effective region (1), selected data times region (2), or the main-light lit region before cast shadows (3). Reset it to 0 for the final material. Data views suppress additional-light/outline/fog contributions.

### Liquid and Wetness

SkinX, MainOpaqueX, all three Alpha variants and their available Tess derivatives share the same liquid layer. The separate `tom/LiquidX` shader uses this core for material-copy overlays and independent thin water surfaces. HairX, EyeX and EyeWX do not gain liquid controls. This retains the modern liquid system released in 0.3.1, with no switch back to the earlier experimental algorithm.

LiquidCoverageMode selects 0 KKS Regions, 1 Custom Mask R, or 2 Full Surface. KKS uses `liquidmask` RGB combinations and five 0..2 amounts: liquidftop/fbot/btop/bbot/face. Texture2 R appears during 0..1; G is added during 1..2 using a max union. LiquidCoverageMap supplies coverage in mode 1. These are final rendering slots, not the game's identically named compositor inputs. The shader does not supply missing game textures or automatically update amounts on arbitrary meshes.

LiquidCutoff (default 0.02) removes low pattern/custom-mask values and remaps the remainder. LiquidEdgeSoftness (0.04) adds a smooth, pixel-filtered transition. Cleanup happens before the KKS region amounts, so their animation remains continuous. This one coverage controls pigment, surface normals and all wet reflection contributions. It does not clip the mesh. SkinLiquidMaterial is the entire layer strength; zero restores the dry material.

Texture3 is a tangent normal, not a height image. LiquidNormalEncoding: 0 Raw AG reads numerical X=A, Y=G and preserves the game's runtime texture sampling flags; 1 Exported AG PNG inverse-decodes only the sRGB-encoded G channel while retaining numerical alpha, and requires data sampling (sRGB off); 2 Unity Normal uses a normal-map importer. Never combine mode 1 with automatic sRGB decode. Select the mode from the actual input pipeline, not the grayscale appearance. The bundled neutral AG texture is for mode 0; if no normal texture is used in another mode, set SkinLiquidNormalScale to zero.

LiquidTiling is a raw vector even when ME displays it as a color: R/G are offset, B/A are scale, default (0,0,1,1). It precedes each pattern, normal and custom-mask ST. These textures use repeat/trilinear/aniso8 sampling without discontinuous UV wrapping. The KKS region mask has independent ST and linear-clamp sampling, without liquid tiling. Treat pattern/region/custom coverage as linear data; do not apply color gamma to LiquidTiling.

SkinLiquidNormalScale scales decoded slopes, followed by normalization and a smooth LiquidMaxNormalAngle limit (default 60 degrees relative to the substrate main normal). LiquidNormalAA filters unresolved specular detail (default 1). First verify the encoding, then adjust slope strength; increasing roughness does not repair an incorrectly decoded normal. LiquidDiffuseNormal affects only pigment lighting and defaults to zero. It never moves the dry material's Toon shadow.

Liquid is a single dielectric wet interface over the substrate, with physical GGX and approximate transmission. LiquidIOR defaults to 1.33, approximately 0.02 normal-incidence reflectance; 1 removes interface reflection. SkinLiquidRoughness and LiquidSpecularStrength are independent of dry Toon highlights, dry masks and skin/nail gloss. LiquidEnvironmentStrength independently controls probe reflections, without requiring the dry ReflectionMode. Environment/indirect terms occur only in Base; per-light direct terms respect cookies and physical shadows in Base/Add. Artistic specular gain is not a physical energy-conservation control.

LiquidEnergyBlend (default 0.25) controls substrate attenuation, including the reduction of substrate specular by index matching. Zero preserves underlying lighting; one uses full approximate interface attenuation. It never changes the liquid's own GGX/probe reflection. LiquidAttenuationNormal (default 0) independently controls liquid detail in the transmission normal: zero uses the substrate main normal, preventing fine droplet slopes from carving dark grooves into the base; one opts into detailed attenuation. Reflections always retain the full liquid normal. These controls do not cancel real cast shadows or disable the wet layer. Low Energy Blend is an artistic, non-energy-conserving option; this is not a full refractive light-transport model. The existing common Clearcoat is still replaced inside wet coverage.

SkinLiquidColor RGB is pigment; its A times SkinLiquidColorStrength is pigment opacity. A=0 preserves the underlying color and Toon shading under a transparent wet film, apart from interface attenuation/reflection. Opaque pigment uses separate soft diffuse lighting; LiquidToonBlend optionally mixes in the substrate's Toon visibility. The liquid GGX lobe itself stays non-Toon.

LiquidShadowColor optionally tints the pigment's shaded areas: RGB is a multiplicative tint clamped to 0..1; A is tint strength (default 0). The tint affects direct pigment lighting and Base-pass indirect pigment lighting, fades with pigment opacity, and disappears at SkinLiquidColor A=0 or SkinLiquidColorStrength=0. It does not color transparent reflections, modify dry skin, add emissive fill, or replace real shadows. For unwanted clear-film darkening adjust LiquidEnergyBlend/LiquidAttenuationNormal, not this color. To fade the entire liquid effect, use SkinLiquidMaterial.

Wet coverage replaces the common Clearcoat interface instead of adding a second coat. Common coat remains outside that coverage. SkinCoatCoverage, SkinWetness and SkinLiquidCoatNormal still affect the common coat, but do not create an additional liquid interface. Integrated liquid does not change mesh alpha, cutout, outline, depth or shadow-caster coverage; fully transparent Alpha pixels remain invisible.

For LiquidX over another material, use a duplicate material slot/mesh with matching UVs, choose Custom Mask or KKS Regions, and copy the relevant pattern/normal/region textures. MainTex A and AlphaMask R optionally match the substrate's cutout; use a white cutout input when it should not clip. Default depth offset units are -1 to reduce coplanar fighting. For a separate thin water surface, select Full Surface or a custom mask and set both depth offsets to zero. LiquidX defaults to transparent pigment (A=0), uses premultiplied transparency, does not write depth or cast an opaque shadow, and can receive lighting/shadows and probe reflections.

The overlay cannot replace an unknown substrate's BRDF: its transmission uses framebuffer blending, unlike the integrated layer's substrate-aware attenuation. LiquidEnergyBlend and LiquidAttenuationNormal also control the overlay's background dimming, while pigment shadow tint affects only its own colored contribution. Do not overlay LiquidX on top of an already enabled integrated liquid layer. This is thin-surface shading, not water volume simulation: no GrabPass, scene refraction, thickness absorption, waves, fluid simulation or order-independent transparency. Transparent sorting and intersecting surfaces retain Unity's normal limitations.

LiquidDebugView: 1 effective coverage; 2 coverage-masked world normal; 3 liquid direct specular; 4 liquid environment reflection; 5 pigment opacity; 6 raw coverage before cleanup. Zero returns to the final material. A useful first check is that dry pixels stay black in the coverage view and unchanged when toggling SkinLiquidMaterial.

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

Deferred without reducing current effects: EyeX targeted multi-light optimization (not a closing blocker); POM only when true self-occlusion is required; further hair transmission/scattering; the EyeWX sclera versus brow/eyeline split. Probe capture budgets and whole-/multi-character game GPU cost require separate measurements, not inference from spheres.

## 11. Showcase Reference

The companion gallery provides standard-sphere comparisons, eye-angle views, individual PNGs, generated texture inputs and expandable tables recording all material parameters. Comparisons use consistent camera/lighting and a fixed display transform except where a documented angle or lighting change is the subject of the comparison. The SH showcase isolates diffuse response with known coefficients; an independent Unity runtime regression checks actual probe capture/refresh behavior.

For source installation and packaging, see Documents/Development.md in the source repository.

## 12. Independent Tessellation and Displacement

Choose `tom/MainOpaqueXTess`, `tom/SkinXTess` or `tom/MainAlphaXTess` explicitly. All inherited property names, clipping, light response, liquid, coat and shadow behavior remain those of their ordinary counterpart; SkinXTess also retains Face SDF, UV0-UV3 and vertex-color overlay gates. MaterialEditor still has to preserve saved values when switching shaders. These variants require **D3D11 / shader model 5** and have no silent lower-feature fallback. The ordinary shaders contain no Hull/Domain stages and remain the zero-tessellation-cost choice. Factor 1 still invokes these stages in a Tess shader.

| Control | Definition and default |
| --- | --- |
| TessellationMode | 0 fixed factor, 1 projected screen-edge length; default 1 |
| TessellationFactor | Subdivision cap 1..16; default 4, also clamped for script-set values |
| TessellationEdgeLength | Screen-edge target 4..64 pixels, default 16; smaller is denser, subject to the cap |
| TessellationSmooth | Phong surface smoothing 0..1, default 0; independent of subdivision and height |
| UseGeometryMask / GeometryMask | Off by default; linear RG: R smoothing, G displacement, B/A unused; white permits both, black protects the original surface |
| DisplacementMap | Linear R height, not a normal map; sampled on UV0 with independent tiling/offset |
| DisplacementStrength | Signed world-space scale -0.05..0.05, default 0 |
| DisplacementMidlevel | Zero-height reference 0..1, default 0.5 |
| DisplacementMip | Explicit height/mask mip 0..8, default 0; increase for distant/noisy detail |
| DisplacementNormalStrength | Height-gradient correction 0..1, default 1; 0 retains interpolated mesh normals |
| TessellationBoundsPadding | Optional helper's extra world-space padding 0..0.1, default 0; does not move geometry |

Height displacement is `(saturate(R) - Midlevel) * Strength * mask.G`, along the interpolated world normal. A white map at Strength=0.02 and Midlevel=0.5 moves outward by 0.01 Unity world units. The bundled carrier explicitly binds an exact linear 0.5 neutral texture. When constructing materials in code, assign a height map explicitly before enabling displacement; engine built-in gray defaults are not an exact-neutral contract. Both data maps need sRGB disabled; ME runtime imports must provide data sampling as well. GeometryMask has independent UV0 tiling/offset. Its R channel does not control density, and its G channel never fades the dry material's coverage.

Start with smoothing and height at zero; verify the original appearance, then increase density, smoothing and height separately. Color, ForwardAdd, Outline, ShadowCaster and MainAlphaXTess's optional DepthPrepass use the same deformation. There is no patch-frustum/backface culling that would discard off-camera shadow casters. Screen-edge density is view-dependent; fixed mode is useful when reproducible topology matters. More subdivision cannot recover missing authored shape. Height normals are a shallow gradient approximation, not exact curved-surface transport or self-intersection handling.

MainAlphaXTess preserves straight/premultiplied AlphaBlendMode, global Alpha, optional color cutoff/ZWrite, DepthPrepass (default off), DepthShadowCutoff and CastShadows. Depth and cast-shadow coverage still follow the inherited Alpha rules; enabling tessellation does not make a translucent surface cast physically transmissive shadows. The depth prepass writes the deformed surface, not the original mesh. Tessellation does not fix transparent sorting or intersecting meshes. No BackFront, legacy 2Pass, Hair, Eye or Liquid Tess variants are added by this extension. Use Tessellation Bounds 0.1.1 or newer for automatic MainAlphaXTess bounds support; do not install a second copy of the older DLL.

Pass features disabled through fragment clipping can still execute geometry stages, including DepthPrepass=0. Such switches are not a guarantee of zero tessellation work for that Pass; use the ordinary shader to remove tessellation stages entirely.

For bounds support, install optional `TomTessellation.dll` in `BepInEx/plugins/TomShadersX`. No extra helper AssetBundle is required. In Unity, add `TomShadersX.TomTessellationBounds` to the renderer. The shader renders without this component, but Unity otherwise culls using its old CPU bounds, potentially clipping displaced/smoothed geometry or its shadow. The helper expands SkinnedMeshRenderer.localBounds; for MeshRenderer it creates a private mesh clone with adjusted bounds, never changing the original shared mesh. It restores its own changes on disable, preserving external mesh replacements/bounds edits. Arbitrary simultaneous mesh-writer mods still need compatibility checks. The game provider discovers active Tess renderers about once per second; attached helpers update bounds each LateUpdate. This optional CPU scanning/cloning cost is separate from tessellation GPU cost.

No automatic welding is performed across UV seams, split normals, body/face joins or separate materials. Heights and normals must agree along shared edges. Use black geometry-mask borders around protected seams; sharp mismatches can still crack. The conservative smoothing bounds may be broad. GPU deformation changes neither colliders nor CPU vertices. Extra triangles, height/gradient samples and every additional-light geometry pass require scene/device profiling; this release does not certify whole-character performance. The optional SDF head provider must be 0.1.1 or newer to recognize SkinXTess.
