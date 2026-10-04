# Independent X Project (2026-10-02)

## Boundary

TomShadersX is a standalone mod/source repository inside the existing KKS Unity
host project. It is not a copy of the entire Unity project. Vanilla Plus LTS stays
in Assets/Mods/KKShadersPlus-release1.7.1 with its original repository and history.
The new local Git repository uses main; no remote, commit or push was created.

| Package | Manifest GUID | Bundle |
| --- | --- | --- |
| Vanilla Plus LTS | xukmi.Shaders.VanillaPlus | chara/xukmi/shaders/vanillaplus.unity3d |
| Tom Shaders X | tom.Shaders.X | chara/tom/shaders/tomx.unity3d |

The X manifest starts at version 0.1.0. Both packages remain Koikatsu Sunshine.

## Migrated Without Renaming Public Assets

- Shaders/Tom and all three tom/Main*X shader declarations.
- Three bound materials and three shader-carrier prefabs.
- X-specific tooltips, Editor validation helpers, tests and evidence.
- X stage roadmaps, progress records, and material/alpha contracts.
- Acceptance scenes, materials and diagnostic textures now in Tests/ReferenceAssets.
- The reference unitypackage was re-exported by Unity using the new paths.

Unity AssetDatabase.MoveAsset preserved GUIDs. Existing material-to-shader,
prefab-to-material and acceptance-scene references remain valid. Serialized Unity
assets and .meta files were not hand-edited. The new package's prefabs and tooltip
catalog received the new bundle assignment through Unity importers.

Shaders/KKPDeclarations.cginc is the only shared source dependency discovered.
Unity copied it into this project with a distinct GUID; the contents initially
match exactly, and the old file was not changed. Future changes can evolve
independently. LICENSE was also copied with original MIT attribution.

V+ retains all 49 non-X manifest shader entries, including its existing BSDF,
Blend, overlay and in-progress Skin work. Only three X entries and the X tooltip
catalog were removed from its manifest. Its remaining shader properties, package
metadata, game, bundle bindings and code were not changed by separation.

## Verification

- Test-ProjectIsolation.ps1: 23 local include references resolve inside this repo.
- All three prefab/material/shader chains resolve inside this repo through
  AssetDatabase.GetDependencies, with no dependency on the LTS folder.
- No remaining LTS native assets reference the migrated X asset GUIDs.
- Shader property counts remain 98 / 104 / 104; manifest and tooltip tests pass.
- Representative compilation: 13 variants warmed for each of the three shaders.
- Opaque: 21 regression renders pass; PNG hashes including the contact sheet
  match before migration (22 files).
- Alpha: 64 pixel checks pass; all 80 regression PNGs match before migration.
- File-hash auditing confirms retained LTS files are unchanged except the
  intentional manifest removal and README explanation.

Bridge results:
CodexBridge/Outbox/xsplit-final-1-verify-001.result.json through
xsplit-final-6-archive-001.result.json in the host project.
Render runs: XStageTwo-20261002-152629 and XAlpha-20261002-152630.
These are regression fixtures, not a new claim of exhaustive visual acceptance,
DOF/SSAO compatibility or production performance.

## Recovery and Release

The host project keeps the pre-split file hashes, original manifest, Unity-exported
backup and move journal in CodexBridge/Backups/XSplit-20261002.
Early migration attempts hit Unity directory-import timing failures and rolled
back. The final migration pre-registered destination parents, then moved 26 asset
roots in a guarded batch and validated them successfully.
Editor/TomXProjectSplit.cs is an explicit one-time migration/verification tool,
not a runtime dependency. Do not rerun Migrate on an already-separated tree.

The old repository shows migrated tracked files as deletions; their contents are
now in the independent new repository. The old Git history remains intact.
Neither repository has been committed or pushed as part of this separation.

No game installation or already-built zipmod was modified. Rebuild and package
both source folders before release. Do not co-install a previously combined V+
zipmod still registering tom/* with the standalone X zipmod. Shader names were
kept for material compatibility, but package identity and bundle paths changed;
existing saved game content still requires a packaged migration test.
