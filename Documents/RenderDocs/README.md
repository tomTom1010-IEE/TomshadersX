# TomX Rendering Documentation

Context-free entry for users and agents. Reviewed 2026-10-27 against package **0.4.0**,
GUID `tom.Shaders.X`, KKS / Unity 2019.4 Built-in Forward. No earlier conversation or sibling
checkout is needed. RenderDocs means rendering documentation, not RenderDoc frame captures.

## Read First

Starting with a new machine or an unconnected toolchain? Follow [Environment setup](EnvironmentSetup.md)
and its portable [entry skill](skills/kk-scene-pipeline-setup/SKILL.md) before material authoring.
The skill covers Blender MCP, the matching Unity template/bridge, StudioMCP and a saved-scene round trip.

1. [Shared rendering knowledge](SharedRendering.md): terminology, input calibration and safe workflow.
2. [Pipeline](Pipeline.md): source ownership, Pass contributions, companions and boundaries.
3. [Material contracts](MaterialContracts.md): channel maps, controls and migration hazards.
4. [Shader catalog](ShaderCatalog.json): current manifest identities, carrier paths, literal Pass
   names, categories and source fingerprints. It is a static inventory, not GPU acceptance.
5. [English manual](../TomShadersX-UserManual.en.md) and [build guide](../Development.md).

## Choose An Entry

| Need | Shader |
| --- | --- |
| General opaque / cutout | `tom/MainOpaqueX` |
| Coverage transparency | `tom/MainAlphaX` |
| Ordered shell layers | `tom/MainAlphaXBackFront` |
| Older depth-prepass compatibility | `tom/MainAlphaX2Pass`, not the same as BackFront |
| Hair cards and front-hair stencil | `tom/HairX` |
| Sclera / eyelines / eye-area surface | `tom/EyeWX` |
| Iris/pupil and shallow optical effects | `tom/EyeX` |
| Game face/body skin layers | `tom/SkinX` |
| Independent thin-liquid overlay | `tom/LiquidX` |
| Optional real subdivision/displacement | `tom/MainOpaqueXTess`, `tom/MainAlphaXTess`, `tom/SkinXTess` |

The main bundle is `chara/tom/shaders/tomx.unity3d`. Carrier names and the embedded tooltip asset
are recorded in the catalog/build guide. Keep required native materials, textures, prefabs and
`.meta` GUIDs; do not replace them with hand-authored serialized files.

## First Agent Prompt

```text
Read Documents/RenderDocs/README.md, Pipeline.md and MaterialContracts.md, then inspect the exact
shader's manifest and tooltip entries. Identify the installed version, renderer/slot, map encoding,
active branches and lighting before suggesting settings. Do not infer settings from another shader
family or historical chat. State what can be checked statically and what needs an actual render.
Do not alter assets, build, deploy, operate applications or push without the current task's authority.
```

## Maintain The Reference

From this package root, with PowerShell 7:

```powershell
./Documents/RenderDocs/Audit-RenderDocs.ps1
# Only after reviewing actual source/metadata changes and updating the prose:
./Documents/RenderDocs/Audit-RenderDocs.ps1 -RefreshCatalog -ReviewDate YYYY-MM-DD
```

The default command is read-only. It detects source/manifest drift, duplicate identities and missing
carrier references; literal Pass names do not flatten UsePass or preprocess keywords. Review the
source for effective execution. Updating the catalog is not a compile/render/bundle acceptance.
The unshipped Eye stencil diagnostic is explicitly excluded from the source inventory, matching
the existing publication policy. It is not required to use these docs or to run the default audit.

These portable docs are publication-eligible. Local AGENTS, experiments and evidence remain excluded
by the repository's existing publication policy. No publication or new runtime acceptance occurred
while adding this reference. Existing manual/report acceptance retains its own dates and scope.
