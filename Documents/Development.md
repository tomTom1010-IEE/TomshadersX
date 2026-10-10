# Source and Build Guide

This is the source package for Koikatsu Sunshine and Unity 2019.4.9f1,
Built-in Forward. It is not a complete Unity project.

## Source Layout

- Shaders: nine public tom/* shaders, shared includes and the optional hair
  feather helper shader.
- Material and Prefab: shader carrier materials/prefabs and neutral skin textures.
- Tooltips: MaterialEditor help text.
- Runtime: source for the optional HairFront feather provider.
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
nine carrier prefabs:

- a_TomMainOpaqueX
- a_TomMainAlphaX
- a_TomMainAlphaX2Pass
- a_TomMainAlphaXBackFront
- a_TomHairX
- a_TomEyeWX
- a_TomEyeX
- a_TomSkinX
- a_TomLiquidX

The bundle also includes tom_x_tooltips and referenced materials/textures.
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

## Defaults and Compatibility

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
