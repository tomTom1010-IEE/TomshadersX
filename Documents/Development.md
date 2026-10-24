# Source and Build Guide

This is the source package for Koikatsu Sunshine and Unity 2019.4.9f1,
Built-in Forward. It is not a complete Unity project.

## Source Layout

- Shaders: twelve public tom/* shaders, shared includes and the optional hair
  feather helper shader.
- Material and Prefab: shader carrier materials/prefabs and neutral skin textures.
- Tooltips: MaterialEditor help text.
- Runtime: sources for the optional HairFront feather, SkinX head-direction and Tessellation Bounds providers.
- manifest.xml: Sideloader shader registrations and material controls.
- docs: the English/Chinese showcase and user manuals.

Read the [English manual](TomShadersX-UserManual.en.md) or
[Chinese manual](TomShadersX-UserManual.zh-CN.md) for shader selection, maps,
material controls and optional lighting providers.

## Unity Installation

Place this folder at Assets/Mods/TomShadersX in a compatible KKS Modding Tools
Unity project. Preserve the supplied .meta files so shader, material and prefab
references keep their GUIDs. Unity and game assemblies are external dependencies
and are not distributed here. The shader sources do not require the Vanilla Plus
source tree.

## Shader Bundle

Build the existing AssetBundle assignment chara/tom/shaders/tomx.unity3d with the
KKS Modding Tools project's AssetBundle build/postprocess workflow. Include all
twelve carrier prefabs (including the independent Tess variants):

- a_TomMainOpaqueX
- a_TomMainAlphaX
- a_TomMainAlphaX2Pass
- a_TomMainAlphaXBackFront
- a_TomHairX
- a_TomEyeWX
- a_TomEyeX
- a_TomSkinX
- a_TomLiquidX
- a_TomMainOpaqueXTess
- a_TomSkinXTess
- a_TomMainAlphaXTess

The bundle also includes tom_x_tooltips and referenced materials/textures.
Keep HairHighlightNeutral.asset and DisplacementNeutral.asset bound to their
carrier materials; the latter is an exact linear 0.5 height, not built-in gray.
The two Skin*Neutral.png files are explicit ME import assets for genuinely empty
DetailMask/SkinControlMap slots. Keep them linear, uncompressed and without mips.
They match the existing carrier neutral .asset data; do not replace original game
maps or rely on ShaderImporter defaults surviving a runtime shader switch. The
shader's black/white fallbacks remain non-neutral, as documented in manual section 8.
Package manifest.xml at the root of a zipmod, with the processed bundle at
abdata/chara/tom/shaders/tomx.unity3d. The manifest GUID is tom.Shaders.X.
Install the resulting zipmod with Sideloader and MaterialEditor.

Do not install an older combined package that registers the same tom/* shader
names alongside this package. Preserve existing shader/property names and Unity
GUIDs when updating the package.

## Optional Runtime Provider

Ordinary shading, Clearcoat and EyeX optics do not need a plugin.
HairFront feathering uses the separate Runtime sources and the
HiddenHairFeather shader bundle; hard stencil mode remains available without it.
Building the runtime provider requires BepInEx and the matching Unity assemblies.
Studio reflection/SH probe providers are separate products, not bundled here.

For HairFeather, compile Runtime/TomHairFeatherCamera.cs and
Runtime/TomHairFeatherPlugin.cs with TOM_X_BEPINEX defined, referencing BepInEx.dll,
the matching UnityEngine assemblies and netstandard.dll. Build
Shaders/Tom/HiddenHairFeather.shader into a separate hairx-feather.unity3d bundle
for StandaloneWindows64. Keep that bundle beside TomHairFeather.dll; its field
contract is version 2. Do not substitute the main tomx.unity3d for the helper.

SkinX Face SDF works with renderer axes without a plugin. Animated skinned heads
use Runtime/TomSkinSDFHead.cs and Runtime/TomSkinSDFPlugin.cs. Compile both as
TomSkinSDF.dll with TOM_X_BEPINEX defined, referencing BepInEx.dll, the matching
UnityEngine assemblies and netstandard.dll from the KKS installation. Install
the DLL under BepInEx/plugins. No additional shader bundle is needed. The adapter
finds KKS cf_J_Head/cm_J_Head, humanoid Head, or common Head/mixamorig:Head bones;
custom Unity rigs can assign TomSkinSDFHead.head explicitly. Tracking uses a
per-renderer property block and does not clone or reset material values.
Version 0.1.1 additionally recognizes SkinXTess; its plugin GUID is unchanged.

Independent Tess shaders do not require a plugin to render. To expand CPU culling
bounds for smoothing/height, compile Runtime/TomTessellationBounds.cs and
Runtime/TomTessellationPlugin.cs as TomTessellation.dll with TOM_X_BEPINEX and
the same game/BepInEx references. Install under BepInEx/plugins/TomShadersX.
No helper AssetBundle is needed. In Unity, attach TomTessellationBounds directly.
Provider 0.1.1 recognizes MainAlphaXTess as well as the existing two Tess entries;
replace the previous DLL rather than installing duplicate plugin GUIDs.
The provider reads shared material settings, expands skinned localBounds and uses
a private bounds-only mesh clone for static renderers. It restores owned changes
on disable; integrations with other mesh writers need explicit validation.

Tess variants target D3D11/SM5 in Base, Add, Outline and ShadowCaster, plus
MainAlphaXTess's optional depth prepass. Their
geometry stages are separate from ordinary shaders; factor 1 still costs Hull/
Domain work. Preserve the entire ordinary property block and fragment/lighting
implementations when maintaining each Tess entry, adding geometry controls only.
TomTessellation.cginc owns tessellation/deformation; TomTessDomain.cginc wraps the
existing vertex functions. Do not fork the lighting includes for Tess variants.
Detailed height/mask units, default values and seam limitations are in manual
section 12. This addition has not been certified for whole-character game cost.

## Defaults and Compatibility

HairX has three neutral texture-free surface-height controls.
Primary/SecondaryHeight default zero; HeightAxisAngle defaults mesh V
(90 degrees), independent of strand flow. Rotate the complete GGX/Toon N/T reflection frame about the
independent surface-height axis (-atan(height) slope bias), preserving true
geometric light/view guards, Fresnel, masks and share budget. Frame derivatives
drive AA, and both endpoint visibility terms use the same selected frame.
No texture, UV2, head transform, extra plugin, or extra lobe. Neutral paths retain
the exact original frame. This is an artistic reflection-coordinate bias,
not a numeric SpeclarHeight alias or guaranteed world/head-space translation.
Do not replace existing angular Shifts or change the surface-height defaults.

The head-coordinate Head Band experiment was archived on 2026-10-20.
Its nine original shader properties retain IDs/types/ranges/defaults as hidden,
unread compatibility slots; they are hidden in both ME and the Unity Inspector.
Saved mode 1 no longer selects a different BRDF or blocks Surface Height.
Head-space coordinate/profile calculations and provider uniforms are removed
from the active lighting path. Debug retains meanings 0..6; retired values
7/8/9 render the final view, including ordinary Add, outline and fog.
The component GUID is preserved with an inert compatibility class. Do not build
or package TomHairHeadBand.dll; the former build/package entry points reject
new runs. Older installations should remove that optional DLL to stop its old
scanner. HairFeather, Skin SDF and other companions remain independent.
The original local source, fixtures and setup notes are preserved outside Assets
in CodexBridge/Archive/HairHeadBand-20261020, excluded from release publication.

The development MatCap extension retains MatCapBlendMode's stored float ID and
default 1, replacing its Inspector enum with Range(0,1). Saved legal endpoints
retain the previous Multiply/Add compositions; saved fractional enum values now
blend continuously. MatCapColor is additive RGBA data, default white/A=1. Preserve
explicit saved values; initialize missing carrier values through Unity APIs.
All eleven MatCap-capable ordinary/Tess entries share the same implementation;
LiquidX is excluded. Reuse one sample per active Pass, modulate per-light diffuse
with the Multiply portion and keep the Add portion Base-only. Alpha weights the
whole effect after legacy intensity shaping, not surface coverage. MatCap Debug
View interpolates the original multiplier/add debug endpoints. No runtime plugin
or extra texture/bundle dependency is introduced.

Package 0.3.1 defaults indirect diffuse gain to 0.25 across the shaders and
carrier materials. Existing saved material overrides are preserved. Clearcoat
and eye optics are opt-in. EyeWX uses one role-independent default.

## Publication Scope

The public repository contains release sources/assets, usage/build documentation,
licenses and the static showcase. Local agent instructions, experiments,
benchmarks, test scripts/scenes, reports and caches are excluded by .gitignore.
New top-level paths are excluded by default and should be reviewed before being
added to the publication allowlist. Do not force-add development output.

## License

See [AGPL-3.0](../LICENSE), the preserved
[MIT upstream notice](../LICENSES/MIT-Upstream.txt), and
[third-party notices](../THIRD_PARTY_NOTICES.md).
