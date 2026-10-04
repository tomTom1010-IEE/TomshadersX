# Tom Shaders X

- Target KKS, Unity 2019.4.9f1, Built-in Forward. This package is independent of V+ LTS.
- Keep public tom/* shader names and the frozen material contract stable.
- Do not include code or assets from the KKShadersPlus LTS sibling. Shared source
  must be copied locally; changes to one copy do not authorize changing the other.
- Never hand-edit Unity serialized assets or .meta files. Use Unity Editor APIs
  through CodexBridge. Preserve supplied GUIDs when moving assets.
- Keep material, prefab and .meta files in version control.
- Run Tests/Test-XAlpha.ps1 and Tests/Test-ProjectIsolation.ps1 after relevant edits.
- Use Bridge rendering for image checks, not computer-use automation.
- Do not push, create a remote repository or modify the KK port without a request.

## Public Site And License

- The user approved publication to tomTom1010-IEE/TomshadersX on 2026-10-04.
  Keep the remote's AGPL-3.0 LICENSE and LICENSES/MIT-Upstream.txt notices intact.
- docs/index.html is the English-default GitHub Pages showcase. Preserve the
  Chinese gallery/manual and both sets of title boards; share the original GPU
  captures, without rerendering or relabeling image differences as shader changes.
- Regenerate with Export-XShowcase.ps1 -ReportDirectory <GPU report> -OutputDirectory
  <repo>/docs. Do not copy local runtime logs or credentials into the public site.
- README embeds the English boards; Documents/Development.md holds the source
  package/build guide. Update both language manuals when public controls change.

## Closing Audit And User Documentation

- Documents/XSeriesClosingAudit.md records the 2026-10-04 code/Unity/ME audit;
  it is not a new in-game audit or performance certification.
- Keep Documents/TomShadersX-UserManual.zh-CN.md aligned with public controls.
  Run Test-XSeriesMetadata.ps1 after metadata edits, including category/help edits.
- renderxshowcase and Export-XShowcase.ps1 produce real sphere captures and an
  offline gallery/manual. Keep fixed exposure and same-angle controls; record
  camera/lighting differences. Do not label synthetic SH as captured Studio GI.

## Eye Follow-Up Decisions

- EyeWX/EyeX v1 feature scope is closed; see Documents/XSeriesEyeClosure.md for
  frozen contracts, evidence and outstanding release acceptance.
- On 2026-10-04 the user confirmed all scoped non-performance KKS eye game
  acceptance complete. Only performance qualification remains open in that
  scope; do not reopen the manual checklist without a relevant regression risk.
- The local Editor GPU baseline is measured in Documents/XSeriesGpuBaseline.md.
  Use the final seeded, V+ Add-enabled run for comparisons, not the pilots.
  Eye pairs are proxies, not full-character game/device performance certification.
- User decision on 2026-10-04: retain the current eye appearance and features.
  EyeX multi-light optimization is a follow-up, not a blocker for current closure
  or SkinX planning. Revisit only when representative game/device GPU budgets
  justify it; do not silently reduce iterations, dispersion or Add lighting.
- If a later request needs a near-side cavity wall to hide far-side iris content
  or other true height-field self-occlusion, remind the user to consider POM /
  first-visible-hit search. The current shallow solver does not guarantee that.
  Do not implement the upgrade without a separately agreed scope and cost check.
- Weak angle response alone is not a POM requirement: check actual gaze/view
  angle, depth profile/gain, IOR and bounds first. No extra corneal mesh; the
  eyebrow/eyeline vs sclera shader split remains deferred.

## Skin Implementation

- SkinX is the eighth public shader, shared by face and body, in experimental
  package 0.3.0. See Documents/XSeriesSkinContract.md and XSeriesSkinProgress.md.
- Keep the 167-property snapshot stable; run Test-XSkin.ps1 and
  Test-XSkinMetadata.ps1 plus existing family regressions after relevant edits.
- Skin GPU fixtures are synthetic. Do not inherit the eyes' game acceptance or
  claim that capsule/sphere timing certifies actual skinned KKS characters.
- Skin-only mask sampler sharing is explicit: respect the atlas import policy
  in the contract. Overlay and tiled liquid samplers remain independent.
- Preserve KKS body/face property names, UV0-UV3 and vertex-color overlay inputs.
  TEXCOORD3 is an overlay UV here, not X's optional UV4 outline-normal storage.
  Do not reinterpret legacy masks as the frozen X packed-material channels.

## Approved Default Revision

- User decision on 2026-10-04: Maker indirect lighting, not SkinX color
  inheritance, caused the apparent white skin. No color-property rename needed.
- Package 0.3.1 lowers IndirectDiffuseIntensity from 1 to 0.25 across all eight
  shaders and bundled carrier presets. This is the only approved frozen-default
  revision; property names, types, ranges and lighting algorithms stay unchanged.
- Preserve explicit saved values. Do not add ME XML defaults or runtime logic
  that overwrites existing material settings. Unset values use the new default.
