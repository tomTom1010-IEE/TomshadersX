# TomX Material Contracts

Use exact property IDs from the current manifest, with a leading underscore in shader/API access.
This is a project-specific supplement to [SharedRendering](SharedRendering.md), not a substitute for
the [full manual](../TomShadersX-UserManual.en.md). Equal labels in V+ or BSDF are not interchangeable.

## Common Texture Channels

| Input | Meaning |
| --- | --- |
| MainTex | RGB substrate color; A participates in family-specific coverage |
| AlphaMask | R coverage in ordinary surface paths; Skin preserves the game clothing-mask contract |
| NormalMap / NormalMapDetail | Unity packed tangent normals, independent ST/scales |
| MaterialMap | R metallic, G roughness, B AO, A direct-specular mask when packed mode is enabled |
| LayerMask | R secondary Toon, G rim, B MatCap, A environment |
| MetallicMap / RoughnessMap | Separate R times scalar; roughness bias follows |
| OcclusionMap | R indirect occlusion, white unoccluded |
| SpecularMask | R direct-highlight mask, superseded by packed MaterialMap A in packed mode |
| ReflectionMask | R common reflection coverage, combined with LayerMask B/A |
| ClearCoatMap | R coat amount, G coat roughness multiplier; B/A unused |
| StrandDirectionMap | Linear RG flow axis, not a normal texture |

Some masks share samplers to fit D3D11 limits. Shared wrap/filter does not imply shared ST. Skin UV0
atlases need compatible sampler settings; never reinterpret Skin's game DetailMask as MaterialMap.
Skin preserves game overlay UV streams and vertex color, and also has a separate SkinControlMap.
Read their dedicated sections before replacing any body/face map.

## Normal Routing

Base/detail and independent ClearCoatNormalMap use Unity packed decoding, not BSDF's selectable
RGB/AG enum. A blue PNG imported by ME can be repacked for registered base/detail slots; an
unregistered independent-coat slot may take a different path. Verify that route and provide the
packed representation expected by the shader. Do not invent `NormalEncoding` controls for TomX.

Normal controls have separate owners:

- Global scales determine source normal strengths.
- ToonNormalInfluence / ToonDetailNormalInfluence affect direct Toon normals. Skin uses
  SkinDiffuseNormalDetail for its detail path.
- SpecularNormalInfluence / SpecularDetailNormalInfluence affect dry direct highlights; Hair uses
  its existing HairSpecular pair instead. AA follows the selected highlight frame.
- MatCapNormalSource selects mesh, main, or combined. Main/detail influence sliders do not override
  the selector: mesh ignores them and main ignores detail.
- Clearcoat and liquid retain independent normal sources. Changing one layer's influence does not
  disable all material normals, probe normals or geometric light-facing guards.

## Appearance Controls That Are Easy To Misread

`MatCapBlendMode` is continuous: 0 Multiply, 1 Add, intermediate values blend complete compositions.
MatCapColor RGB tints the one sampled map; A weights the whole appearance effect, not surface alpha.
Black RGB can darken Multiply and is not an off switch. Use A=0 or intensity=0 to disable it.
Multiply modulates per-light diffuse; Add stays Base-only. LiquidX does not expose the common MatCap.

ToonMinLighting defaults to 0.15 and IndirectDiffuseIntensity to 0.25. These do not overwrite saved
values. ToonShadeColor A is artistic fill strength, not V+'s reverse ShadowColor alpha or mesh alpha.
The earlier ToonDarkFillColor name is retired; old game cards are not automatically migrated.
Dark-side fill and minimum response are not BSSRDF/SSS or emission.

SpecularToonBlend mixes GGX and shaped Toon highlights. Roughness, SpecularSize and SpecularStrength
control different aspects. Hair dual lobes redistribute a shared direct budget; Surface Height
rotates the N/T reflection frame around a mesh U/V-derived axis, not world/head position.
Flow maps, angular shifts and surface-height bias are different inputs. Head Band is inactive.

EyeWX is role-independent. EyeX adds shallow optical UVs, regional depth and optional dispersion,
not automatic geometry detection or a guaranteed first-visible-hit POM solver. Clearcoat IOR is
independent of iris refraction IOR; turning off coat does not turn off eye parallax.

## Liquid And Height Are Separate Contracts

LiquidNormalEncoding values are 0 RawAG, 1 ExportedAGPNG, 2 UnityNormal. ExportedAGPNG repairs G
specifically and assumes the documented data sampling path. These values are **not** the BSDF normal
enum. Pigment opacity does not fade clear liquid reflections. Integrated wetness uses the shared
coat budget; LiquidX is an approximate premultiplied overlay over an unknown substrate, not volume water.

Tess GeometryMask RG gates smoothing/height separately. DisplacementMap is linear R around explicit
DisplacementMidlevel, with world-distance DisplacementStrength. Screen-edge tessellation density
depends on endpoint projection and has a separate pixel target. Do not copy BSDF HeightScale or
V+ object-space height values without authoring a conversion. Tess factor 1 is not zero geometry cost.

## Practical Diagnostic Sequence

1. Preserve original game textures and inspect alpha/mask/UV availability before switching shaders.
2. Disable Debug View, added coats/liquid and excessive postprocessing while isolating base inputs.
3. Verify normal transport with a neutral test; check material-map mode and texture channel meanings.
4. Establish Toon lighting, then direct highlights, environment/MatCap, then coat/rim/outline.
5. Test multiple lights and ensure Base-only layers do not repeat. Change one normal owner at a time.
6. For skin, inspect game-managed masks/overlays and Maker indirect light before changing color IDs.
7. Test alpha ordering, shadows and depth separately; reload a saved scene/card using the same inputs.

For maintenance, preserve property IDs, ranges and explicit overrides. Manifest category/help changes
are not lighting changes. Tooltip text is an embedded asset: a source XML edit alone does not update
an already built bundle. Rebuild only when the actual task requests a new compiled artifact.
