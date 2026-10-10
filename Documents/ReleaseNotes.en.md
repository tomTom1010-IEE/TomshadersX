# Tom Shaders X: 2026-10-11 Release

[简体中文](ReleaseNotes.zh-CN.md)

Release tag: `v0.3.1-20261011`. This dated revision retains shader package version
0.3.1 and the accepted runtime binaries. It replaces, rather than accompanies,
the earlier 0.3.1 shader zipmod.

## Changes

- HairX supports the KKS `_ColorMask` and `_Color`, `_Color2`, `_Color3` contract,
  while retaining independent BaseColor. An unset mask does not apply `_Color`
  to already colored hair textures.
- SkinX and the opaque/alpha families share a modern liquid layer with explicit
  normal encoding, coverage cutoff/softness, independent GGX wet reflections,
  roughness and IOR, and controlled normal strength/antialiasing. Eye/Hair do not
  gain liquid controls. The ninth shader, LiquidX, provides a thin-surface overlay.
- Liquid pigment alpha controls pigment opacity independently of wet reflections.
  Substrate attenuation and pigment shadow tint are separate controls, reducing
  unwanted dark edges without disabling scene shadows. LiquidEnergyBlend defaults
  to 0.25, LiquidAttenuationNormal to 0, and LiquidShadowColor alpha to 0.
- Toon normal influence and antialiasing controls help soften irregular diffuse
  transitions without changing reflection normals.
- ToonMinLighting remaps the shadowed main-light Toon diffuse response. Its new
  default is **0.15** in eight shaders and their bundled carriers; set 0 to disable.
  It lifts self/cast-shadowed diffuse once in the Base pass, not once per extra
  light. It is not emission or an absolute pixel-brightness floor.
- ToonDarkFillColor is renamed **ToonShadeColor**. Its optional colored-fill
  behavior is unchanged; alpha defaults to 0. Indirect diffuse still defaults to 0.25.
- English/Chinese manuals and MaterialEditor help are updated.

## Compatibility

Explicitly saved ToonMinLighting values are preserved. Older materials without
this property inherit 0.15 and may look brighter. Cards that saved the interim
ToonDarkFillColor property need its RGBA reapplied to ToonShadeColor; no automatic
game-card migration is included. ShadeColor is not a tint for the minimum-light control.

The liquid path replaces the earlier implementation; existing liquid materials
may require retuning. Exported liquid normal PNGs and raw game textures require
the appropriate explicit normal encoding described in the manual.

## Complete Package

- Tom Shaders X 0.3.1 revised shader zipmod, including all nine shaders and its bundle.
- Tom Lighting Probes 0.2.1 zipmod, including Studio RP/SH items and their bundle.
- TomHairFeather.dll and its paired hairx-feather.unity3d helper bundle.
- KKS_TomProbe.dll and TomProbe.Runtime.dll.
- Bilingual manuals/release notes, licenses and SHA256SUMS.txt.

The companion plugins are unchanged. The probe package retains previous SH
lighting while a new capture is prepared.

## Installation

1. Close KKS/CharaStudio and back up your installation/configuration.
2. Remove older Tom Shaders X and Tom Lighting Probes zipmods. Remove duplicate
   copies of the three DLLs and the paired hair helper bundle, including copies
   in custom plugin folders. Do not keep two revisions with the same mod GUID.
3. Merge the archive's mods and BepInEx folders into the KKS game root.
4. Leave zipmods compressed. Keep hairx-feather.unity3d beside TomHairFeather.dll.

Requires compatible BepInEx 5, Sideloader and MaterialEditor, installed separately.
Third-party loaders and game/Unity assemblies are not included. KKS only, not KK.
The user completed in-game acceptance of these changes; packaged Unity checks
also passed. This is not new whole-character performance certification.

[Download](https://github.com/tomTom1010-IEE/TomshadersX/releases/tag/v0.3.1-20261011)
| [User manual](TomShadersX-UserManual.en.md)
