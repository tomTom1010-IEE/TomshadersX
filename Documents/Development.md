# Source and Build Guide

This is the source package for Koikatsu Sunshine and Unity 2019.4.9f1,
Built-in Forward. It is not a complete Unity project.

## Source Layout

- Shaders: twelve public tom/* shaders, shared includes and the optional hair
  feather helper shader.
- Material and Prefab: shader carrier materials/prefabs and neutral skin textures.
- Tooltips: MaterialEditor help text.
- Runtime: accepted Unity authoring component mirrors and retained historical
  adapter templates. Canonical runtime development/build ownership belongs to
  the separate TomRenderInfrastructure project.
- manifest.xml: Sideloader shader registrations and material controls.
- docs: the English/Chinese showcase and user manuals.

After manual or installation edits, use the shared infrastructure
`packaging/refresh_consumer_docs.py --tomx <this-root> --bsdf <BSDF-checkout>` to
refresh the manual HTML and base-first notices without rerendering images.

Read the [English manual](TomShadersX-UserManual.en.md) or
[Chinese manual](TomShadersX-UserManual.zh-CN.md) for shader selection, maps,
material controls and optional lighting providers.

## Unity Installation

Place this folder at Assets/Mods/TomShadersX in a compatible KKS Modding Tools
Unity project. Preserve the supplied .meta files so shader, material and prefab
references keep their GUIDs. Unity and game assemblies are external dependencies
and are not distributed here. The shader sources do not require the Vanilla Plus
source tree. The integrated authoring component mirrors require the single matching
TomRender.Core.dll at Assets/TomRenderInfrastructure/Runtime/TomRender.Core.dll.
Prepare/import it through the Infrastructure authoring workflow and Codex Bridge,
not by adding game plugin DLLs to this package. Core is a helper dependency, not a
mandatory replacement for the game's renderer or ordinary shader rendering.

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

For the supported Infra1 installation, install
[TomRenderInfrastructure KKS 0.1.0](https://github.com/tomTom1010-IEE/TomRenderInfrastructure/releases/tag/v0.1.0)
first. Its base ZIP supplies all runtime DLLs, the Feather helper and Probe 0.2.2
resources. TomX's new shader release contains no DLLs, Probe zipmod or helper
Bundle. This distribution split does not change shader algorithms or require a
new shader Bundle. The old Complete archive remains a historical release.

Ordinary shading, Clearcoat and EyeX optics do not need a plugin.
HairFront feathering uses the separate Runtime sources and the
HiddenHairFeather shader bundle; hard stencil mode remains available without it.
Building the runtime provider requires BepInEx and the matching Unity assemblies.
Studio reflection/SH probe providers are separate products, not bundled here.

### Canonical Runtime Ownership

Since the 2026-10-27 source consolidation, develop and compile TomHairFeather,
TomSkinSDF and TomTessellation only in the separate **TomRenderInfrastructure**
project, under `src/TomX`. Its README, `release.manifest.json`,
`migration/source-manifest.json` and `docs/Build-and-Staging.md` are authoritative
for builds, identities and audited DLL staging. The consumer now requires schema 2,
the preserved nine-assembly ABI/serialization audit, and hash-bound acceptance of
all four runtime-integration steps. Historical full-IL equality is not used as a
substitute for the new behavior/lifecycle tests. Do not maintain a second implementation.

| DLL / plugin version | Preserved BepInEx GUID | Destination |
| --- | --- | --- |
| TomHairFeather.dll / 0.3.0 | tom.shaders.x.hairfeather | BepInEx/plugins/TomHairFeather/ |
| TomSkinSDF.dll / 0.1.1 | tom.shaders.x.facesdf | BepInEx/plugins/TomShadersX/ |
| TomTessellation.dll / 0.1.1 | tom.shaders.x.tessbounds | BepInEx/plugins/TomShadersX/ |

All three migrated providers require the one matching **TomRender.Core.dll** in
`BepInEx/plugins/TomRenderInfrastructure/`. Core retains assembly version 1.0.0.0
and has no BepInEx entry. The three provider assembly identities retain version
0.0.0.0; plugin versions are separate.
Keep the existing namespaces, component full names, serialized fields and paths.
Replace a provider at its existing location, never install a duplicate GUID.
The current central stage contains eight DLLs. TomX consumes six: Core, the three
providers and the central Probe 0.2.2 DLL pair. BSDF and DOF DLLs are not pulled into
TomX. Do not mix the migrated cohort with a historical Probe DLL pair. New public
Infra1 packages use the separate base distribution; the old Complete archive and
its pre-Core providers remain unchanged as historical artifacts.

Three `.cs` files are indispensable **Unity authoring mirrors**:
TomHairFeatherCamera, TomSkinSDFHead and TomTessellationBounds. They were updated
through Bridge as part of the accepted integration, preserving their `.meta` GUIDs,
file IDs and Assembly-CSharp identity. Keep them aligned with the accepted central
authoring candidate. Do not delete them or replace them with plugin DLLs.
The three conditional `*Plugin.cs` files remain frozen historical templates, not
current runtime implementations or DLL inputs. Do not use them to build a second
provider. Future mirror changes require coordinated authoring/assembly review and
Bridge import. Inert Head Band compatibility files remain archived locally.

### Consume Audited Dependencies

Build/audit/stage from TomRenderInfrastructure using its existing commands and an
explicit `current` stage. Its `with-prototypes` stage is not a TomX dependency.
Maintainer workspaces retain local, unpublished integration scripts:

```powershell
./Tests/Import-XRenderInfrastructure.ps1 -InfrastructureRoot '<central-project>' `
  -InfrastructureStage '<central-project>/artifacts/staging/<reviewed-current-stage>' `
  -OutputDirectory '<Unity-project>/CodexBridge/Dependencies/<new-run>/Plugins'
# Pass that explicit directory to the ordinary twelve-shader package gate:
./Tests/Package-XRelease.ps1 -PluginDirectory '<Unity-project>/CodexBridge/Dependencies/<new-run>/Plugins' `
  -ResourceReview '<reviewed-integrated-release.json>'
```

The importer checks central source/build/audit/stage fingerprints, frozen ABI
provenance, all four accepted step receipts, exact accepted DLL bytes/identities,
three authoring source/GUID/file-ID records and a unique matching authoring Core.
It does not compile, import into Unity, build Bundles or deploy. No old local DLL
folder or `Plugins/build-receipt.json` is an accepted fallback. The historical
Build-XReleasePlugins name is import-only; the three per-plugin build scripts
reject execution. Their original compiler scripts remain central migration
controls, not active TomX entry points. The central active audit distinguishes
current runtime integration from immutable phase-1 sources and retired build tools.

The historical embedded-Complete packaging path additionally requires `-ResourceReview <review.json>` after
real packaged-game, resource/version-pairing and licensing review. The consumer-owned
review has schema 1, passed/licensingReviewed/packagedGameRegressionPassed flags,
the dependency sourceFingerprint/runtimeAcceptanceSHA256, and exact
mainBundleSHA256/featherBundleSHA256/previousCompletePackageSHA256 inputs. No such
release approval is supplied by local integration acceptance. Old Shader build
receipts become stale when authoring source dependencies change and are not bypassed.
Do not use that legacy Complete path for new Infra1 releases. The central
`packaging/package_release.py` produces the audited split archives from the frozen
runtime/resource cohort and unchanged shader zipmods. It rejects plugin payloads
in shader packs and records corresponding source commits. The old Probe 0.2.1
resource archive must not be reused with the new cohort.
Integrated archive checks require exactly one Core at its canonical destination;
the historical 22-entry 0.4.0 layout remains a separately identified verification mode.

### Shader-Owned Resources And Fallbacks

TomX still owns `Shaders/Tom/HiddenHairFeather.shader` and its matching
`hairx-feather.unity3d` (field contract 2). Keep the helper beside its DLL; do not
substitute the main shader bundle or drop the helper during dependency cleanup.
This ownership change does not rebuild or replace either Bundle.

SkinX Face SDF works with renderer axes without a plugin. The existing head
provider finds KKS cf_J_Head/cm_J_Head, humanoid Head or common Head/mixamorig:Head
bones. Custom Unity rigs can assign TomSkinSDFHead.head. Tracking uses a preserving
per-renderer property block; no additional helper Bundle or material reset.
Independent Tess shaders render without the bounds provider, but expanded geometry
can be culled by unchanged CPU bounds. The provider restores owned changes and
needs separate compatibility checks with other mesh writers. In Unity the retained
TomTessellationBounds authoring type remains available. No helper Bundle is needed.

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
