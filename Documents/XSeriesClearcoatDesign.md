# Shared Clearcoat Design

Date: 2026-10-03. Status: implemented experimental extension with Unity GPU fixtures.
Target: KKS, Unity 2019.4.9f1, Built-in Forward, D3D11. The design below records
the intended contract; [implementation evidence and deviations](XSeriesCoatEyeProgress.md)
separate completed Editor checks from pending in-game GPU/material acceptance.

## Two Step Delivery

1. Extend the X baseline with one optional Clearcoat module, integrate all five
   existing public shaders, and verify disabled compatibility and enabled cost.
2. Build [EyeWX and EyeX](XSeriesEyeDesign.md) on that baseline. Reuse Clearcoat
   for the outer reflection; keep iris refraction and dispersion eye-local.

The current [HairX v1 closure](XSeriesHairClosure.md) remains the reference, not a
claim that this extension has passed. Finish the shared-layer acceptance before
freezing the new eye contracts. Clearcoat does not depend on the feather provider.

## Scope and Defaults

All existing entries participate: `tom/MainOpaqueX`, `tom/MainAlphaX`,
`tom/MainAlphaX2Pass`, `tom/MainAlphaXBackFront` and `tom/HairX`. EyeWX and EyeX
consume the same module; a future SkinX should also reuse its lighting code.

Every shader has the same Clearcoat property meanings and a zero-weight default.
There is no mesh-name, material-name or renderer-role detection. EyeWX does not
need separate automatic sclera, eyeline or eyebrow presets. Demonstration values
may be documented, but neither shader installation nor card load applies them.

The first version is a neutral dielectric, isotropic GGX reflection layer with
its own roughness and normal. It is not volume transparency, a wetness simulation,
thin-film iridescence, a second hair-fiber lobe or a replacement for anisotropy.
No new geometry, color pass, GrabPass, screen depth or camera plugin is required.
Hair cards can use it artistically; it is not a cylinder-fiber wet-hair model.

## Experimental Public Contract

Names and defaults below are implemented and registered in manifest 0.2.0.
The ten declarations are tracked in `Tests/Clearcoat-v1.properties.txt`;
Editor compatibility checks have passed, while game/GPU acceptance remains open.
Append ten properties to each existing shader; do not reorder or repurpose the
98-property common baseline or any existing Alpha/Hair property.

| Property | Type and default | Meaning |
| --- | --- | --- |
| `_ClearCoat` | Range 0..1, 0 | Layer weight, not alpha or a metallic value. Exact zero bypasses coat lighting and coat texture fetches. |
| `_ClearCoatRoughness` | Range 0.04..1, 0.1 | Perceptual roughness of the outer layer, independently filtered. |
| `_ClearCoatIOR` | Range 1..2.5, 1.5 | Coat reflection/energy IOR; neutral scalar F0. Since 0.2.1 EyeX rays use independent EyeRefractionIOR. |
| `_ClearCoatMap` | 2D white | Linear data: R multiplies weight, G multiplies roughness. B/A reserved and ignored. Independent ST. |
| `_ClearCoatNormalSource` | Enum 0..3, 0 | 0 interpolated mesh normal, 1 base normal map only, 2 base plus detail, 3 independent coat normal map. |
| `_ClearCoatNormalMap` | 2D bump | Unity packed normal, used only by source 3; independent ST. |
| `_ClearCoatNormalScale` | Range 0..2, 1 | Strength of source 3 only. Does not rescale substrate normal maps. |
| `_ClearCoatEnvironmentStrength` | Range 0..2, 1 | Coat probe response, independent of substrate ReflectionMode/EnvironmentIntensity. Zero skips coat probe sampling. |
| `_ClearCoatEnergyBlend` | Range 0..1, 1 | Additional coat attenuation of diffuse-like body terms: 0 preserves Toon body, 1 uses the layer weighting below. Substrate specular remains attenuated. |
| `_ClearCoatDebugView` | Enum 0..5, 0 | 0 off, 1 coat direct, 2 coat environment, 3 resolved coat normal, 4 effective weight, 5 diffuse retention. |

Unspecified maps are neutral; no new texture is required to enable the layer.
Never borrow a channel from MaterialMap, LayerMask, AlphaMask or normal alpha.
Keep substrate SpecularStrength/IOR/Roughness/ReflectionMode and masks unchanged.
For example, substrate SpecularStrength=0 must not disable a separately enabled
coat, and a substrate ReflectionMask cannot silently erase it.

No new coat tint, metallic slider, band-count control or full second Toon control
panel in v1. The substrate retains its stylized specular. Coat highlights start
continuous and anti-aliased; a separate stylized coat transfer is later research
only if rendered use cases justify it. Display these controls under Clearcoat,
with normal, energy and debug controls in advanced groups where supported.

## Shared Code Ownership

The current core already separates raw lights, material data, surface terms and
BRDF evaluation in `TomToonLighting.cginc`. Keep that separation.

| Location | Planned responsibility |
| --- | --- |
| `TomClearcoat.cginc` (new) | Coat input record, normal selection, direct BRDF, retention factors and coat environment evaluation. No coverage, stencil, eye UV or pass entry points. |
| `TomToonBRDF.cginc` | Existing F0/Fresnel/GGX utilities; parameterized reuse, preserving the zero-coat path. |
| `TomToonLighting.cginc` | Evaluate substrate, optionally coat, compose by term ownership before fog/alpha. Share raw light, distance, cookie and shadow sampling. |
| `TomToonInput.cginc` | New uniforms; no change to old texture channels or varying layout solely for coat. |
| Existing shader declarations, manifest, tooltips | Same append-only properties, category and defaults across all five entries. |
| Future eye adapters | Supply surface normals and substrate inputs; call the same layer functions. No second eye-only coat implementation. |

Use a small `TomCoatData` alongside, not a replacement for, `TomMaterialData`.
It contains resolved weight, perceptual roughness, normal, normal variance and
scalar F0. Expose a normal-resolution function separately from reflective weight:
EyeX may need the interface normal with coat reflection disabled.

Do not clone TomMaterialData into a fake full material just to access probes.
Extract a low-level probe-sampling utility parameterized by normal and roughness;
leave substrate color, mask and artistic shaping in its original wrapper.
Test the extraction independently before adding the coat caller.

## Layer Evaluation

These equations are our lightweight X composition proposal, not a full multilayer
BSDF or a claim of strict conservation for Toon lighting. Let `Nb` be the substrate
normal, `Nc` the independently resolved coat normal, and `Ng` the oriented mesh
normal. Compute all three in world space; preserve mirrored tangent handedness
and back-face orientation. Source 0 means an interpolated mesh normal, not a
flat triangle normal and not an inference of the mesh's semantic role.

```text
c  = saturate(ClearCoat * CoatMap.r)
rc = clamp(ClearCoatRoughness * CoatMap.g, 0.04, 1)
F0 = ((IOR - 1) / (IOR + 1))^2
F(mu) = F0 + (1 - F0) * (1 - saturate(mu))^5

Tv = 1 - c * F(dot(Nc, V))
Tl = 1 - c * F(dot(Nc, L))
A_direct = Tv * Tl
A_body_direct = lerp(1, A_direct, ClearCoatEnergyBlend)

direct = A_body_direct * substrateDiffuse
       + A_direct * substrateSpecular
       + c * coatDirectGGX
```

`coatDirectGGX` already includes its Fresnel, NdotL and current-light visibility;
do not apply those a second time. Use the existing production GGX convention,
including its Unity gamma-path scaling and roughness floor. Keep the neutral coat
independent of substrate metallic tint and SpecularColor.

At IOR=1, explicitly treat interface reflectance and coat attenuation as zero;
plain Schlick with F0=0 otherwise leaves a spurious grazing reflection. For an
invalid or outward-facing coat normal, fall back to the oriented smooth mesh
normal. Use geometric and coat-normal horizon guards, not the substrate's
perturbed-normal horizon to incorrectly suppress a valid coat highlight.

Compute physical shadow data once per light. Coat direct light uses its physical
shadow attenuation, distance and cookie, plus Main/AdditionalLightIntensity.
Substrate artistic shadow remapping stays unchanged. No second shadow map fetch
and no shadow-free coat added after lighting. Respect existing main-light
ownership for baked/subtractive variants; do not add another baked direct term.

For indirect substrate responses, use a documented diffuse-hemisphere approximation:

```text
Favg = F0 + (1 - F0) / 21
A_indirect = Tv * (1 - c * Favg)
A_body_indirect = lerp(1, A_indirect, ClearCoatEnergyBlend)
```

This is a cosine-weighted average of Schlick's term, not exact rough-layer transport.
Apply A_indirect to substrate probe specular and A_body_indirect to indirect/vertex
diffuse. Vertex-light directions are unavailable after their existing aggregate;
do not claim per-light coat glints for vertex or baked lights. A first version
keeps the substrate IOR/F0 unchanged rather than secretly retuning old materials
for a new surrounding medium. White-furnace tests bound errors; they do not turn
the artist-controlled shader into a full physical BSDF.

Coat environment uses its own normal, roughness, neutral F0 and Fresnel, the same
Unity probe HDR decode/blending/box projection facilities, and the existing
specular-occlusion policy. It does not reuse substrate EnvironmentColor,
EnvironmentToonBlend, BaseColorTint or reflection masks. No main-light shadow
factor is multiplied into the whole environment. Missing probes follow Unity's
existing fallback; this is not live screen-space reflection.

## Composition Order and Passes

| Term / pass | Ownership |
| --- | --- |
| Direct diffuse, including diffuse MatCap Multiply | Substrate; apply A_body_direct consistently in Base and Add. |
| Indirect and aggregate vertex diffuse | Substrate; A_body_indirect in Base. |
| Substrate direct and probe specular | Under the coat; respective retention factors always apply. |
| Emission | Below coat; apply exit retention Tv. Clearcoat weight zero leaves legacy emission unchanged. |
| Additive MatCap and Rim | Deliberate art layers after physical layering, Base only; not coat energy. |
| ForwardBase | Main direct coat plus one coat environment contribution, then existing final fog/output processing. |
| ForwardAdd | Only this pixel light's substrate and direct coat, with unchanged alpha weighting and black fog. No probes/emission/art-layer repetition. |
| HairX complementary stencil groups | Same layer evaluator and existing HairFront alpha per group. Do not add another camera-opacity factor. |
| Alpha back/front color groups | Apply the same coat independently per already drawn face; preserve the existing layer and depth order. |
| Depth-only, ShadowCaster, Outline, StencilMask | No coat texture or BRDF work. Preserve existing coverage/state/width behavior. |

Coat changes RGB only. Straight and premultiplied alpha receive the complete RGB
once through their existing output helper. There is no alpha bypass for bright
highlights. Outline stays a stylized line, not another glossy surface; HairFront
coverage remains identical even though only color passes acquire coat lighting.

ClearCoatDebugView takes priority over the existing DebugView. Direct-coat debug
includes matching Base/Add contributions; environment, normal, weight and body
retention diagnostics draw only in Base. Existing DebugView indices keep their
substrate meanings rather than silently becoming new coat views. EyeDebugView
may override both in the future eye adapter. Debug output preserves coverage.

Use an orthographic-correct view vector for coat and the future eye adapter.
Do not silently alter the existing substrate's disabled rendering as part of a
camera-math cleanup. Validate perspective and orthographic paths separately.

## Disabled Cost and Resource Policy

Use a material-uniform weight branch first. Do not depend on MaterialEditor setting
a shader keyword when it changes a float. Zero must skip coat-map, coat-normal,
probe and BRDF evaluation, with derivatives kept out of divergent per-pixel paths.
An optional compile-time fast variant requires a separately verified keyword
lifecycle through card reloads and builds; it is not the default architecture.

Zero does not promise zero machine-level overhead: record registers, sampler
bindings, generated variants and GPU timing. Plan shared compatible sampler states
for the two new textures; audit wrap/filter requirements before reuse. Eye overlays
also need resource budget. A runtime branch cannot solve a compile-time sampler
limit. Preserve existing pass counts and add no mandatory render textures.

Coat probe reflection normally adds another environment lookup when both substrate
and coat are enabled. Reuse a sample only when direction, roughness and probe
selection match, never solely because they use the same cubemap.

## Compatibility and Versioning

- Preserve existing names, GUIDs, queues, material defaults and packed channels.
- Keep the old 98 common and 110 HairX declarations as immutable legacy snapshots.
  Add a versioned Clearcoat extension snapshot. Tests assert legacy declarations
  as an unchanged ordered subset plus exactly the new group, rather than merely
  increasing the accepted property count or deleting the old snapshots.
- Record the current Alpha variant declarations before extending them too.
  Test all supported alpha/depth choices, not only opaque and HairX.
- Existing cards obtain ClearCoat=0 when no new value is saved. Check actual
  MaterialEditor shader-swap, reset and serialized-value behavior in KKS.
- Build carriers, manifests and tooltips together using Editor APIs/Bridge for
  native assets. Do not automatically assign a coat texture or edit user cards.
- Keep V+ LTS and KK unchanged. The new bundle is a coordinated X update, not a
  second installed package registering duplicate tom/* shader names.

## Milestones and Acceptance

| Stage | Deliverable | Exit gate |
| --- | --- | --- |
| C0 | Snapshot legacy contracts/images; isolate probe helper; settle sampler budget | Existing static and rendered regressions still pass before the feature is enabled. |
| C1 | Shared coat on MainOpaqueX; parameter/mask/normal/energy/debug implementation | Finite response, independent normals, off equivalence and direct/probe ownership tests. |
| C2 | All Alpha variants and HairX; manifest/tooltips and versioned contract | No coverage, stencil, feather, alpha/depth or pass-count regression. |
| C3 | Built bundle, real-card lifecycle and GPU evidence | Measured disabled/enabled overhead and approved appearance; shared API ready for eyes. |

Required validation is additive to the existing X tests:

| Area | Required evidence |
| --- | --- |
| Disabled baseline | Same Unity/API/settings captures before/after; exact output-pixel match target. Explain and resolve any difference, do not silently widen tolerance. |
| Parameters | Weight 0/partial/1, IOR 1/extremes, roughness floor/max, masks with different channels, null/neutral maps. |
| Layer ownership | Substrate-only, coat-only and both; zero substrate specular/reflection does not disable coat; emission/art terms follow the documented order. |
| Lighting | Zero/1/4/8 pixel lights, directional/point/spot, cookies, hard/soft shadows, vertex lights and mixed/lightmapped modes. |
| Environment | No assigned probe, sky fallback, HDR, blended and box-projected probes; verify a second Add light does not duplicate IBL. |
| Normals | All four sources, mirrored UV/negative scale, back faces, fine detail, minification and grazing views; independent derivative AA. |
| Alpha/Hair | Straight/premultiplied, depth options, back/front accumulation; HairFront Off/Hard/Feather, Pixels/World, Base/Add/Outline agreement. |
| Cameras | Near/far, FOV, resolution, orthographic, full/partial/clipped Maker viewports; no cross-camera state. |
| Energy | White-furnace diagnostic for the constrained GGX setup, saturated Toon bodies, HDR peak behavior; record approximation limits instead of hiding them with a final clamp. |
| Cost | GPU medians and tail values at 1080p/1440p, close-up and multiple characters, all texture features; compare coat off/on at equal lights and pass counts. |

Do not invent a millisecond success threshold before recording baseline variance
on the target GPU. Unchanged draw/RT counts, shader compilation within resource
limits, exact disabled images and bounded/finite math are hard gates. Enabled
cost is reported explicitly and accepted against the user's scene budget before
release. Command submission or CPU Camera.Render time is not GPU confirmation.

## Sources and Limits

[Khronos clearcoat](https://github.com/KhronosGroup/glTF/blob/main/extensions/2.0/Khronos/KHR_materials_clearcoat/README.md)
provides the general independent-layer parameterization.
[Filament](https://google.github.io/filament/main/filament.html#materialsystem/clearcoatmodel)
discusses integrating an outer specular response with the underlying material.
The property ABI, Toon retention policy and integration equations here are our
project-specific proposal, not a verbatim implementation of either renderer.

[Unity Forward rendering](https://docs.unity3d.com/2019.4/Documentation/Manual/RenderTech-ForwardRendering.html)
and [sampler states](https://docs.unity3d.com/2019.4/Documentation/Manual/SL-SamplerStates.html)
inform pass and resource constraints. No copied source, shader changes, native
assets, benchmark results or release version are created by this document.
