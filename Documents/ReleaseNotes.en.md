# Tom Shaders X 0.4.0

[简体中文](ReleaseNotes.zh-CN.md)

KKS / Unity 2019.4 Built-in Forward / D3D11. Tag: `v0.4.0-20261024`.
Replace the previous main shader zipmod; do not install both revisions.

## Changes Since 0.3.1

- Twelve shaders, including independent MainOpaqueXTess, SkinXTess and MainAlphaXTess.
  Tess inherits ordinary material IDs and shared lighting; color, additional lights,
  outline, cast shadows and the Alpha depth prepass share deformation. Requires
  D3D11/SM5; factor 1 still has Hull/Domain cost.
- Optional Skin face SDF with independent threshold/region maps and explicit
  encoding. DetailMask is not reinterpreted as SDF. The optional head provider
  tracks animated heads without resetting shared materials.
- HairX dual highlight lobes, angular shifts, explicit flow conventions, local
  control maps, selected highlight normals and texture-free Surface Height bias.
  Height biases a reflection frame; it is not a world-space translation.
- Separate main/detail normal influence for dry Specular and MatCap. HairX uses
  its own controls once; its non-default horizon follows the selected normal while
  retaining the geometric guard. Other lighting layers remain independent.
- Continuous MatCapBlendMode from Multiply (0) to Add (1), preserving both endpoints.
  MatCapColor defaults white/A=1; alpha weights MatCap only. Add remains Base-only.
- Independent SkinShadeColor for DetailMask G / LineMask B painted diffuse shade,
  still weighted by SkinShadeStrength. Source 0 retains the legacy ShadowColor
  link; source 1 selects the independent color. Carrier templates select source 1.
  SkinLineColor and optional ToonShadeColor stay separate; SkinShadeColor A is unused.
- Reorganized Hair ME groups and bilingual help describe units, shared/primary/
  secondary scope, map channels and intentional shader-family differences.
- Head Band remains archived: nine hidden inert compatibility slots, no active
  head-coordinate mode and no HeadBand DLL in the release.

ToonMinLighting remains **0.15**; indirect diffuse gain remains **0.25**.
No default scene-shadow settings change. This release does not claim to eliminate
Built-in shadow-map grazing-angle artifacts.

## Compatibility

Back up cards, scenes and plugin configuration before updating. Saved values and
assigned maps remain authoritative. New Hair effects default neutral/off. Local
highlight-map mode requires its documented channels and neutral map, not HairGloss.
SDF needs compatible authored data and calibrated orientation, not just a toggle.
Tess can expose UV/hard-normal seams and change bounds; use the optional bounds
helper where needed. Compatibility with other mesh/bounds writers needs testing.

Skin maps use explicit binding, not automatic repair. If DetailMask or
SkinControlMap is missing, import the supplied `Material/SkinDetailNeutral.png`
(1,0,0,1) or `Material/SkinControlNeutral.png` (1,1,0,1) in ME. Empty runtime slots
still use ShaderLab black/white and are not neutral. Keep existing game/authored
maps; no extra fallback plugin or texture-size/content heuristic is added.

Manually remove an installed old `TomHairHeadBand.dll`, including copies in custom
plugin directories. Replace duplicate HairFeather/SkinSDF/Tessellation providers;
do not install multiple DLLs with the same plugin GUID.

## Complete Package And Installation

- Main 0.4.0 zipmod: twelve carriers, shaders, neutral dependencies and ME tooltips.
- Accepted Tom Lighting Probes 0.2.1 zipmod, `KKS_TomProbe.dll` and
  `TomProbe.Runtime.dll`, unchanged from the previous complete companion package.
- `TomHairFeather.dll` and paired `hairx-feather.unity3d` in the same folder.
- Optional `TomSkinSDF.dll` and `TomTessellation.dll`, with no extra helper bundle.
- Bilingual installation notes, manuals/release notes, AGPL/MIT notices and SHA-256
  checksums. No tests, reports, caches or local game texture/model fixtures.

Close KKS/CharaStudio, remove superseded zipmods and duplicate provider files, then
merge `mods` and `BepInEx` into the game root. Leave zipmods compressed. BepInEx 5,
Sideloader and MaterialEditor are external prerequisites; game/Unity assemblies
and third-party loaders are not redistributed. KKS only, not KK.

## Validation Scope

Release gates cover all twelve families, focused Unity regressions, strict bundle
build/reload and explicit packaged rendering. Editor results do not certify
whole-character game performance or every imported asset. The original showcase
keeps its fixed-exposure GPU captures; it is not a new rendering of every addition.

[Download](https://github.com/tomTom1010-IEE/TomshadersX/releases/tag/v0.4.0-20261024)
| [User manual](TomShadersX-UserManual.en.md)
