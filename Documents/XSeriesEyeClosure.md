# Eye Series Feature Closure

Date: 2026-10-04. Scope: KKS, Unity 2019.4.9f1, Built-in Forward, D3D11.
Main package remains 0.2.1; optional HairFeather adapter remains 0.3.0.

## Decision

Close the current EyeWX/EyeX v1 feature scope. The user confirmed that the painted
depth map works and that angle-dependent parallax becomes clear after the
diagnostic parameter comparison. This resolves the reported weak-angle-effect
question; it does not establish which individual setting caused it.

Acceptance update, 2026-10-04: the user confirmed that all scoped non-performance
KKS in-game acceptance is complete. Record this as user-reported game evidence,
separate from the automated Editor results below. The local Editor GPU baseline
is now complete; full-game/device performance qualification remains pending.
This does not certify every head mod, device or unsupported rendering target.

The closure review found no new rendering blocker within the tested scope. It
fixed a metadata maintenance defect and added compatibility guards. No shader
formula, coverage, queue, default material, native carrier or built game package
changed. Feature and scoped game acceptance are closed, but this is not a
performance-qualified production release. Keep the experimental designation
until the remaining performance gate passes.

## Frozen Scope

| Entry | Public properties | Behavior retained |
| --- | --- | --- |
| `tom/EyeWX` | 103 | Cutout eye-area surface, shared X lighting/clearcoat, neutral EyeW color alias, dedicated Ref 2 stencil writer; default queue 2472. |
| `tom/EyeX` | 131 | Iris/pupil, game rotation/highlight/expression inputs, shared coat, independent refraction IOR, optional RGB dispersion, procedural or painted shallow recess; default queue 2474. |

The declaration snapshots are `Tests/EyeWX-v1.properties.txt` and
`Tests/EyeX-v1.properties.txt`. `Test-XCoatEye.ps1` checks names, attributes,
labels, types, ranges, defaults and ordering against them. Future additions
require an explicit compatibility review; never regenerate a snapshot merely
to make a regression pass.

- Both eyes retain `StencilMask`, `FORWARD`, `FORWARDADD`; no Outline or
  ShadowCaster is inherited. All use the existing cutoffs, culling and LEqual
  depth test, with ZWrite Off. Color passes do not rewrite stencil.
- Refraction changes interior RGB/normal sampling, not alpha/stencil coverage.
  Game highlights remain a Base-only surface layer; Base/Add share optical UVs.
- Clearcoat IOR controls reflection/layer energy, not parallax. Eye Refraction
  IOR controls the interior rays and RGB spread. The interface normal remains
  shared and unwarped, so the layers are not independent physical lens systems.
- Existing Flat mode remains default. Cone, bowl, GrayscaleHeight and
  DepthRMaskA share Iris Depth as the global gain; map UV follows MainTex.
- No automatic sclera/brow/eyeline preset detection. Splitting the eyebrow/
  eyeline shader remains deferred, not a missing mandatory eye feature.
- No extra corneal mesh, GrabPass, camera depth, POM or new runtime plugin.
  Feather still needs its optional adapter and complete supported writers.

## Metadata Repair

`Sync-CoatEyeMetadata.ps1` still had the 0.2.0 version assignment, missing hints
for the five surface-extension properties, old coupled-IOR wording and obsolete
debug/shape enum limits. It could downgrade the version on an otherwise no-op
run or fail while recreating missing surface properties.

The synchronizer now preserves the release version, supplies the current
independent-IOR/map hints, migrates their obsolete wording and repairs owned enum ranges. Existing unrelated
metadata remains untouched. The new `Test-XEyeMetadata.ps1` reproduces the old
version failure in an isolated temporary copy, then checks current/future
version preservation, missing-property regeneration, shape/debug range repair,
EyeWX separation and byte-idempotence. It never rewrites the live catalog.

The live manifest/tooltips already contained the correct 0.2.1 values and pass
the no-op test unchanged. No new zipmod or game installation is needed for this
tooling-only fix.

## Closure Evidence

Paths below are relative to the Unity project. Report names use UTC timestamps;
this closure date uses the user's local date.

- Fresh Bridge command: `CodexBridge/Outbox/20261004-eye-closure-render.result.json`.
- Fresh report: `CodexBridge/Reports/XCoatEye-20261003-190251/report.json`.
  266 checks passed, 167 GPU captures, zero non-finite pixels; 180 PNGs including
  six contact sheets and seven raw data templates.
- All 180 PNGs are byte-identical to the preceding 0.2.1 report
  `XCoatEye-20261003-142132`. The candidate's
  `coat-baseline-comparison.json` records the hashes. This comparison is against
  0.2.1, not the older pre-clearcoat baseline.
- The fixture covers independent IORs, zero-effect paths, original and actual
  stencil, supported writer union, map alignment, camera/viewport variations,
  finite grazing output and Base/Add optical agreement. Representative shader
  compilation is part of the run, not an exhaustive variant proof.
- Separate compiler check:
  `CodexBridge/Outbox/20261004-eye-closure-compile.result.json`. All seven shaders
  are supported; 87 requested variants warmed, with no reported warnings/errors.
  EyeWX/EyeX each contribute 11 Forward variants; the five older entries have 13.
- Static EyeSurface, CoatEye, EyeMetadata, Alpha, Hair and ProjectIsolation
  suites passed. EyeSurface includes 675 CPU reference intersections; Hair
  includes 2020 feather samples; 72 shader includes stay inside this package.
- GPU environment: Unity 2019.4.9f1, D3D11, RTX 4070 Ti SUPER, Gamma. This is
  the synthetic single-mesh Editor fixture, not a game GPU benchmark.
- The user subsequently confirmed completion of all scoped non-performance
  KKS game acceptance on 2026-10-04, including the previously open integration
  checks. The subsequent local Editor GPU baseline is linked below; full-game
  performance remains unqualified. Prior evidence and the tiny 0.2.0
  diagnostic-image difference remain recorded in
  [the progress ledger](XSeriesCoatEyeProgress.md).

## Conditional POM Reminder

User decision: if a future request needs a **near-side cavity wall to occlude
far-side iris content**, reveal/hide the cavity floor as the view moves, or
correctly resolve multiple height-field crossings, remind the user to evaluate
**POM / first-visible-hit search** before proposing an implementation.

The present six-step bounded refinement assumes one local shallow intersection.
More iterations or a larger depth/strength does not turn it into a first-hit
solver. A single-valued depth texture can still have multiple oblique-ray
intersections. [GPU Gems' relief-mapping discussion](https://developer.nvidia.com/gpugems/gpugems3/part-iii-rendering/chapter-18-relaxed-cone-stepping-relief-mapping)
explains why a suitable search is needed to avoid skipping the visible surface.

Weak angle response alone does not trigger this upgrade: first inspect gaze
tracking, actual view angle, depth profile/gain, iris-radius calibration,
refraction IOR, offset limits and fallback diagnostics. A wider recessed floor
with a smooth transition can strengthen relative motion within the present
model. Depth-derived cavity normals or lighting would be separate enhancements,
not a substitute for visibility testing.

Any POM proposal must remain opt-in, preserve the existing default/material
contract and original stencil/alpha, and address first-hit correctness, grazing
stability, atlas filtering, temporal behavior, Base/Add agreement and measured
GPU cost including dispersion. Self-shadowing needs a separate light-ray test;
overhangs/multilayer geometry and silhouettes are not automatically solved by a
single height-field POM. The no-extra-corneal-mesh constraint remains in force.
This reminder is conditional project guidance, not a scheduled notification or
authorization to add POM now.

## Game Acceptance

User-confirmed complete on 2026-10-04 for the tested KKS scope. The previously
open non-performance acceptance categories (material/card lifecycle, eye
animation and texture bindings, and integration with hair writers/viewports)
are no longer blockers. This confirmation does not add support for previously
unsupported writers or certify every possible character/setup. Do not repeat
the full manual checklist unless a later change introduces a relevant regression
risk.

## Remaining Performance Gate

- The local Editor GPU fixture baseline is complete; see
  [Clearcoat and Eye GPU baseline](XSeriesGpuBaseline.md): 217 cases and 13,020
  timestamp samples, including local V+ LTS/lilToon and Add isolation controls.
- Full-character target-game/device GPU cost and compiled variant/register/sampler
  budgets remain unqualified. The synthetic eye-pair baseline does not close
  these broader release-performance checks.

Linear color space, VR/stereo and KK remain uncertified and outside this KKS
closure. They are not additional gates for the accepted scope, and no automatic
expansion into these targets is authorized.

The remaining performance check is not a reason to add more eye features now.
User decision on 2026-10-04: preserve current effects and defer EyeX multi-light
optimization. It is not a blocker for current feature closure or SkinX planning.
This scheduling decision does not certify unmeasured whole-game/device costs.
See [surface authoring and diagnosis](XSeriesEyeSurface.md) for normal usage.
