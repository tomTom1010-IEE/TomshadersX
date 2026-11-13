# TomX Rendering Pipeline

TomX is toon-first, not a renamed V+ shader or an exact OpenPBR implementation. It shares a rebuilt
native-light path across surface families while preserving separate hair, eye and game-skin inputs.
Current identities are in [ShaderCatalog](ShaderCatalog.json); parameter details are in the
[English manual](../TomShadersX-UserManual.en.md).

## Source Map

All paths below are relative to this shader package.

| File or directory | Responsibility |
| --- | --- |
| `Shaders/Tom/*.shader` | Public properties, render states, Pass order and family defines |
| `TomToonInput.cginc` | Common material declarations, vertex/varying streams and shared samplers |
| `TomToonLighting.cginc` | Material/light data, normals, native-light visibility, Base/Add composition |
| `TomToonBRDF.cginc`, `TomToonStyle.cginc` | GGX / Fresnel and artistic response functions |
| `TomClearcoat.cginc`, `TomLiquid*.cginc` | Separate coat and shared integrated/overlay liquid |
| `TomAlpha*.cginc` | Coverage, depth, ordering, outline and casting strategies |
| `TomHair*.cginc` | Hair flow/lobes, coverage and front-hair behavior |
| `TomEye*.cginc` | Eye-area coverage/stencil, iris inputs and shallow optics |
| `TomSkin*.cginc` | Game skin layers/UVs, coverage, skin lighting and face SDF |
| `TomTessellation.cginc`, `TomTessDomain.cginc` | Independent geometry stages wrapping the existing lighting |
| `Runtime` | Accepted Core-dependent Unity component mirrors plus historical conditional adapter templates; canonical runtime/build ownership is TomRenderInfrastructure |
| `manifest.xml`, `Tooltips` | ME registration, categories and explanatory text |

Include paths in the table, other than `Runtime`, are under `Shaders/Tom` when no directory is given.
`Shaders/KKPDeclarations.cginc` is an independent local compatibility copy. Do not include V+ source
by a sibling-relative path or assume maintaining one copy updates the other.

## Contribution Ownership

- ForwardBase owns main-light direct response, built-in/custom SH, Probe/environment, additive
  MatCap, rim, emission and family/layer composition. Four-light fallback has its own native inputs.
- ForwardAdd evaluates the selected additional pixel light, including its native distance/cookie
  and shadow visibility. Do not repeat SH, Probe, emission, rim or additive MatCap per lamp.
- Multiplicative MatCap still modulates each lamp's diffuse. A changed diffuse amplitude is not a
  change to the view-normal MatCap lookup direction. Pure additive MatCap avoids this Add sampling.
- Geometric horizon guards protect physically facing lobes separately from normal-map styling.
  Toon diffuse uses its own remapped response. `ToonMinLighting=0.15` is Base/main-light-only, not
  an emission term or final RGB floor, and cannot create illumination without that light.
- CastShadowColor is a bounded diffuse receiver tint, evaluated from each light's remapped
  visibility after ShadowStrength. It does not read NdotL or secondary-ramp masks. Base tints
  main diffuse, Toon fill/minimum compensation and main-shadowed indirect diffuse once; Add
  tints only that lamp's diffuse. Baked main ownership still gates Base contributions. Vertex
  lights, specular, additive MatCap, rim, emission and separate coat/liquid lobes are excluded.
  Coated/wet substrate retains its tint; liquid pigment uses LiquidShadowColor instead.
  There is no new sample, Pass or keyword; shadow reception/depth limitations are unchanged.
- Skin's experimental FaceShadowProtection is a diffuse-only receiver floor from NormalMask B,
  default zero. Base/Add use the protected diffuse visibility; existing main diffuse tint/indirect
  routing follows it. Specular, coat, liquid, raw-shadow diagnostics and caster geometry retain
  their original shadow signal. It cannot identify which renderer caused an occlusion.
  Retention was accepted on 2026-11-06 without changing the default or implementation.
  Do not disable face/body casters automatically or turn SDF into a shadow-map repair.
  Explicitly disabling the face renderer's caster while retaining receiveShadows removes
  its self-casting and still receives bangs, but also removes its shadows onto other objects.
  Preserving those outgoing shadows while excluding only self-reception requires separate
  source/receiver handling, outside this closure. Hard-shadow acne remains a limitation.
- Outline, caster, optional depth and stencil paths have separate visibility contracts. Preserve
  coverage and actual deformation across every active path; do not infer them from the color Pass.

ShadowCaster and ForwardBase scheduling follows LightMode, not their textual order in a shader.
The Built-in Forward loop renders shadow maps before its opaque receiver shading; moving the
caster declaration earlier does not isolate self-shadow sources. See [Pass tags](https://docs.unity3d.com/2019.4/Documentation/Manual/SL-PassTags.html#lightmode-tag)
and [render-loop order](https://docs.unity3d.com/es/2019.4/Manual/GraphicsCommandBuffers.html).

TomX's GGX helper follows its Unity-compatible color-response implementation, including a
`UNITY_COLORSPACE_GAMMA` branch. It is not TomBSDF's explicit KKSLinear-v1 adapter. Avoid transplanting
that adapter or legacy V+ artistic gamma conversions into TomX as a scene-tuning shortcut.

## Toon Ramp Contract

RampInputMode/RampColorMode default to 0 on the eleven substrates, not LiquidX.
FullAngle uses `(dot(toonNormal, lightDirection)+1)/2` without squaring or Skin Wrap,
and bypasses only the Toon geometry horizon. Continuous/specular/coat horizons and
actual shadow visibility are retained. RGB samples the existing ramps once per path;
procedural response stays gray, alpha is unused, RGB is bounded and no extra R or
manual gamma multiplication is applied. Secondary mask/ST/weight remain unchanged.
The maximum component drives dark weight and minimum amplitude; the minimum retains
the response hue, smoothly becoming neutral close to black. The 0.15 minimum remains
Base-only, before received-shadow tint and coating. SDF replaces the normal split using
RGB endpoints, not a re-evaluation of intermediate bands. Integrated liquid pigment
retains the legacy scalar response; dry substrate diffuse uses the vector response.
SH, vertex-light fallback and independent lobes do not inherit RGB ramp coloration.

## Geometry And Transparency

Ordinary entries have no Hull/Domain work. The three Tess entries add fixed or endpoint-projected
screen-edge subdivision, bounded by factor 1..16, optional smoothing and world-normal displacement.
The shared domain wrapper feeds original Base/Add/outline/caster functions; Alpha Tess also covers
its optional depth prepass. Factor 1 still invokes geometry stages.

Displacement uses an explicit midpoint and signed world-distance strength; it is not BSDF's inward
white-envelope Height-v1. Keep the exact 0.5 displacement carrier data. UV/normal seams need authored
protection. CPU bounds, colliders, skinning and mesh writers are separate concerns.

Alpha, Alpha2Pass and AlphaBackFront have distinct ordering/depth behavior. No variant is OIT.
Transparent shadow casting and receiving are separate. Eye/Hair stencil coordinates binary
visibility; it does not store continuous opacity. EyeX shallow refraction/parallax is not a full
height-field first-hit solver, volumetric glass, or a new corneal mesh.

## Optional Services

Infra1 distribution installs these helpers from
[TomRenderInfrastructure KKS 0.1.0](https://github.com/tomTom1010-IEE/TomRenderInfrastructure/releases/tag/v0.1.0)
before the shader pack. Runtime DLLs and Probe/Feather resources are no longer
embedded in the TomX shader archive; resource source ownership remains below.

| Service | Extra dependency | Missing-service boundary |
| --- | --- | --- |
| Ordinary Toon/GGX, coat, eye optics | None beyond normal game/ME environment | Shader paths remain available |
| Soft front-hair feather | TomHairFeather DLL and matching helper bundle | Hard-edge behavior, not automatic soft feather |
| Captured custom Probe/SH items | Separate Tom Lighting Probes package and its matching DLL pair | Built-in scene environment/SH remain the fallback |
| Animated head frame for Skin face SDF | Optional TomSkinSDF provider | Renderer-axis SDF basis, not guaranteed animated-head alignment |
| Expanded CPU bounds for Tess | Optional TomTessellation provider or manual bounds | Geometry may be culled by unchanged CPU bounds |

Head Band is archived: retain hidden compatibility IDs, but do not enable/rebuild the old provider
or claim it is an active placement feature. Surface-height controls are the current texture-free
hair reflection-frame bias. Optional services do not establish whole-character performance.

The separate TomRenderInfrastructure project is now the sole development/build owner of the three
TomX provider DLLs. Schema-2 staging requires the preserved ABI/serialization contract and all
four accepted behavior steps. The migrated providers require one TomRender.Core.dll; it owns
camera invocations, discovery candidates, exclusive resource leases, union bounds and field-scoped
MPBs, not shader BRDFs or a new GPU pass. Consume accepted cohort bytes, never historical DLLs.
The three authoring components remain Assembly-CSharp and retain their original metadata;
do not replace them with game plugin DLLs. The three local conditional plugin adapters are
historical templates only. TomX still owns the Feather helper shader/Bundle and shader-side
consumers. The source build guide records Core/Probe dependency pins and the separate complete
release/resource-review gate. Local runtime integration is not live Studio/package certification.

## Acceptance Boundaries

Read source version, installed bundle and provider versions separately. The public showcase uses
controlled GPU sphere renders; synthetic SH is not captured Studio GI. Existing family/packaged
reports and user acceptance have narrower dates/scopes than this source review. This documentation
change performs no game render or new build and does not certify every mesh, importer or GPU budget.
