# Tom Shaders X 0.4.0

## 2026-11-13 Shader Update

- Publishes the manually built twelve-shader zipmod with Full-Angle/RGB ramps,
  CastShadowColor, calibrated Skin defaults and optional face shadow protection below.
- Distribution tag: v0.4.0-20261113; embedded Sideloader version remains 0.4.0.
  Replace the previous tom.Shaders.X zipmod; do not install duplicate copies.
- This is a shader update, not a new complete runtime distribution. Keep the Infra1
  companion plugins/resources. No new plugin DLL is included or installed.
- Uses prior bounded Unity/Studio acceptance; the manual rebuild has archive/metadata
  checks, not a newly repeated GPU or game acceptance. Hard-shadow acne is not cured.

## Unreleased - Skin Shadow Protection Closure - 2026-11-06

- Retain SkinFaceShadowProtection on SkinX/SkinXTess as an opt-in NormalMask B
  diffuse receiver floor. Default remains 0; 0.5 is a starting point only for
  faces with suitable masks, not a new global face/body preset. Debug 14 shows B.
- Preserve shared lighting and accepted defaults, including ToonMinLighting 0.15.
  Do not automatically disable face/body shadow casting or replace user textures.
- Protection weakens self, bangs and external-object shadows in the same receiver
  region. It does not repair hard-shadow sampling artifacts or distinguish casters.
- Face SDF remains a separate optional control for authored Toon boundaries, not
  an acne fix. With receiveShadows on, explicitly disabling a face renderer's caster
  retains bangs reception but removes all its outgoing shadows, not just self-casting.
  Keeping its shadows onto other objects while excluding only self-reception needs
  separate source/receiver handling, outside this shader scope. This clarification
  does not change defaults or the existing test bundle.
- The earlier mouth-corner Toon contour and later shadow-map stripes are distinct
  cases. Toon threshold calibration relocates the former boundary; it does not
  correct the latter's depth comparison or cast-shadow geometry.
- Existing source/packaged GPU checks and bounded Studio comparisons support this
  closure, not universal asset or performance certification. The delivered delta
  retains its experimental labels; no new bundle, installation or GitHub release
  is performed by this documentation-only closure.

## Unreleased - Full-Angle And RGB Toon Ramps - 2026-11-05

- Added RampInputMode and RampColorMode to all eleven substrate shaders, including
  Tess. Both default to Legacy/Red Channel (0); existing values are not migrated.
- Full Angle maps signed Toon N dot L across back/side/front. Only the Toon diffuse
  hemisphere cutoff is removed; normal cast shadows and other light lobes remain.
- RGB reuses the two ramp textures and secondary mask. Alpha is ignored; procedural
  ramps stay gray. ToonMinLighting remains 0.15 and preserves the RGB response hue.
- SDF retains endpoint ownership. Liquid pigment keeps its legacy scalar inheritance;
  LiquidX is unchanged. No new sampler, Pass, keyword or plugin is required.
- Updated ME help and bilingual manuals, with optional gray/color ramp examples.
  This is an opt-in test update, not a GitHub release or a replacement infrastructure pack.

## Unreleased - Cast-Shadow Diffuse Tint - 2026-11-05

- Added CastShadowColor RGBA to all eleven substrate shaders and ME, including Tess.
  White/A=0 is neutral. RGB is a bounded multiplicative tint; A weights the effect.
- Mask follows remapped received-shadow visibility and ShadowStrength. Base tints main
  diffuse, Toon fill/minimum and indirect diffuse once; Add tints only its own diffuse.
  No secondary shadow color, new texture, Pass, keyword or plugin is introduced.
- Specular, reflection, rim, emission, coat and liquid pigment/reflection controls remain
  independent. LiquidX is unchanged. ShadowColor retains its legacy indirect tint and
  Skin compatibility link; no stored values are overwritten.
- ToonMinLighting remains 0.15, before tint and layer attenuation. Actual casting and
  existing engine/transparent shadow-reception limitations are unchanged. This is a
  source update, not a new installed or published bundle.

## Unreleased - Skin Toon Defaults - 2026-11-04

- SkinX and SkinXTess shader/carrier defaults now use ToonThreshold 0.1 instead of
  0.5. ToonSoftness stays 0.1 and ToonMinLighting stays 0.15.
- This is a global face/body default, not a mesh-dependent preset. Explicit saved
  material/card/scene values are preserved; no migration plugin is added.
- Other shader defaults, shared lighting and normal cast shadows are unchanged.
  The calibration improves ordinary face contours but does not guarantee smooth
  mouth/nose boundaries at every head/light angle. No new published bundle yet.

## Infra1 Distribution Update - 2026-10-28

Install [TomRenderInfrastructure 0.1.0](https://github.com/tomTom1010-IEE/TomRenderInfrastructure/releases/tag/v0.1.0)
first, then this release's shader ZIP/zipmod. Infra1 removes embedded helper DLLs,
Probe resources and the Feather helper from the shader archive; the base supplies
the tested cohort once for both TomX and BSDF. Remove old duplicate providers and
Probe 0.2.1 resources. Shader version 0.4.0 and its accepted Bundle are unchanged.
The optional native-DOF prototype is a separate base add-on, Native by default.
Controlled KKS regression covers native DOF/glass, Probe/SH and synthetic Feather
with save/reload; it does not certify whole-character performance. The historical
release and acceptance below remain intact; current tag: `v0.4.0-infra.1`.

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
