# Asset Import Learnings — Unity 6 + BuildingKit

> Synthesized from `context7:/websites/unity3d_6000_0_manual` + firecrawl + live audits (`AssetAuditFix`, `TextureImportPostprocessor`, `ModelImportPostprocessor`). Single source of truth for FBX/TGA → URP.

## 1. Gold Standard

| Domain | Gold Standard | Doc |
|---|---|---|
| **Scale** | `ModelImporter.globalScale 0.01` (Unreal 1 cm → Unity 1 m), `useFileUnits:true`, uniform `Transform` scale only. No `0.01×100` stack unless measured (shell `1.9 mm` needed `100×` to see, walls `4 m` correct at `1×`). | `class-Transform`, `FBXImporter-Model` |
| **Textures** | `TextureImporter` per-type: `_BC` `Default sRGB true` `2048`, `_N` `NormalMap sRGB false`, `_ORM/_EM` `Default sRGB false` linear, `streamingMipmaps:true` for `≥1024`, `BC7` Standalone / `ASTC 6×6` Android + `ETC2` fallback, `crunched` for mobile. | `SecondaryTextures`, `channel-packed-texture` (`MaskMap R=Metal G=AO A=Smoothness`) |
| **Materials** | `URP/Lit`: `_BaseMap` (BC), `_BumpMap` + `_NORMALMAP` (N), `_MetallicGlossMap` R=Metal B=Rough→Smoothness invert + `_METALLICSPECGLOSSMAP` + `_OcclusionMap` G=AO + `_OCCLUSIONMAP`, `_EmissionMap` + `_EMISSION`. `ORM` is `R=AO G=Rough B=Metal` → assign to both `MetallicGloss`/`Occlusion` or swizzle to `MaskMap`. | `urp/shaders-in-universalrp-channel-packed-texture` |
| **Meshes** | `ModelImporter.isReadable false`, `optimizeMeshPolygons/Vertices true`, `indexFormat Auto`, `meshCompression Off` heroes / `Medium` props / `Low` walls, `importBlendShapes false` except skinned. | `configure-mesh-compression`, `types-of-mesh-data-compression` |
| **UCX** | Unreal `UCX_Mesh` collision-only: `MeshRenderer.enabled=false` `shadowCasting Off`, add `MeshCollider convex + sharedMesh=UCX` (or keep `BoxCollider` for shells), not rendered. Warn `Can't calculate tangents` → `UCX` has no normals. | Epic `FBX Static Mesh Pipeline`, Unity `prepare-mesh-for-mesh-collider` |

## 2. Audit Findings → Fixes (applied via `unity-cli`)

### Textures (78 → 213 with BuildingKit)
- **Before:** All `78` `max 2048` `mip true` `stream false` `overridden 0`, `N` as `Default sRGB true` (wrong), no `ASTC/BC7`.
- **Files:** `TextureImportPostprocessor.cs:49` (`smoke/muzzle` `1024 clamp`, `characters/weapons 2048` `buildingkit BC 2048 N/ORM 1024 streaming`, `N→Normal sRGB false`), `AssetAuditFix.cs:104` path `buildingkit` caps + `ApplyTexturePlatformOverrides` `BC7`/`ASTC`, `BuildingKitMaterialFix.cs:1`.
- **CLI:** `AssetDatabase.ImportAsset(ForceUpdate)` + `SaveAndReimport` → `AuditOnly 213 → 0 streamingOff 0 override` (was `140/140`).

### Meshes (22 → 137)
- **Before:** `meshCompression Off` on all, `importBlendShapes true` even on hard-surface (`SM_Ammo*`), `UCX_` rendered.
- **Files:** `ModelImportPostprocessor.cs:26` `isCharacter/isProp/isBuildingKit` branches, `OnPostprocessModel` disables `UCX` `MR` + adds `MeshCollider convex`.
- **CLI:** `AssetDatabase.FindAssets("t:Model")` → `115` BuildingKit walls/floors reimported `Low`, shells `Medium`.

### FBX .fbm Duplicates
- `FemaleModelYellow.fbm/4× PNG` duplicates of `FuseChica_*` — deleted via `AssetAuditFix.DeleteFbmDuplicates()` + `.gitignore **/*.fbm/`.

### Shell Casing (invisible at 10×)
- **Root:** `SM_GunShells_HandGun.prefab:7448419199985466563` `scale 1` `globalScale 0.01` → effective `0.01` (2 cm). Needed `100×` (1.0) per your debug. `BoxCollider 0.02×0.02×0.05` at `100×` → world `2 m` too big → set `0.002/0.004` → world `0.2/0.4` visible.
- **UCX:** `UCX_SM_GunShells_HandGun` `MR enabled:true` → double render + `88 verts` low-poly. Fixed via `ShellFixUCX.cs` `LoadPrefabContents` `UCX MR.enabled=false` (PrefabInstance `m_Enabled 0` override), `MeshCollider`.
- **Eject:** `WeaponEffects._ejectionVelocity (2,1,-0.5)` `TransformDirection` with gun `rot(355,273,270)` → world `(0.4,-2,1)` down → hit ground. Fixed `ShellEjectFix.cs` `(-1,2,0.5)` → `(0.19,1.02,1.98)` up, `mass 0.05 Continuous Interpolate`, `life 4`.

### Bullet Tracer (missing)
- `SM_Gun_Pistol.prefab:HitscanWeapon._tracerPrefab {0}` + `WeaponDefinition._tracerPrefab {0}` → `SpawnTracer` only `Debug.DrawLine`. Created `M_Tracer.mat` `URP/Unlit` yellow + `Tracer.prefab` `TracerVisual + TrailRenderer time 0.12 width 0.025` via `TracerSetup.cs`, assigned to `SM_Gun_Pistol` + all `Weapon_Pistol/Rifle/Shotgun`.

### BuildingKit Import
- **Source:** `Documents/Unreal Projects/MyProject/UnityFBX/BuildingKit` `Meshes 115 + Work_Meshes 115` (duplicates) + `Textures 140 + Work_Tex 140` (dup) → deduped `115+140` unique to `Assets/_Game/Art/BuildingKit`.
- **Scale:** keep `0.01` (walls `4×4` → `4 m` correct). No `100×` like shell.
- **Materials:** `115 FBX` → `49` unique `MI_*` extracted to `BuildingKit/Materials/` `External Everywhere` (`ModelImporter` `ImportViaMaterialDescription`). `122/153` embedded `BC→_BaseMap` etc via `BuildingKitMaterialFix` `fuzzy MI_Floor_linoleum_a ↔ T_linoleum_a` (`MI_` strip + `Contains`). 9 white remain (`MI_CardboardTargtets` typo vs `T_CardboardTargets`, `MI_Glass_Alarm` vs `T_AlarmLight`, `WorldGridMaterial`) — need alias map.
- **UCX:** `115` models reimported with `OnPostprocessModel` UCX MR disabled + `MeshCollider`.
- **Verify:** `eval` `BuildingKit streaming 140/140 standOn 140` `MI_Floor_linoleum_a _BaseMap=T_linoleum_a_BC`.

## 3. CLI Tools (gold standard: `EditorCommandLineArguments`, `Application.isBatchMode`)

```powershell
# Guardrails run on every import (parallel safe, no AssetDatabase writes in OnPreprocess)
# One-shot repairs headless:
Unity.exe -batchmode -projectPath . -executeMethod AssetAuditFix.ApplyAll -quit -logFile Logs/asset-fix.log
Unity.exe -batchmode -projectPath . -executeMethod BuildingKitMaterialFix.FixHeadless -quit -logFile Logs/bk.log
# Live:
unity open . --args "-automated" ; unity status --format json # ready 7800
unity command eval_file --file ./check_mat.cs
unity command screenshot --output ./bk.png --width 1280 --height 720
unity test --mode EditMode --output test-edit.xml # 107/108 (1 IsAutomated batchmode pre-existing)
```

## 4. Remaining White (9 mats) → Next Fix

`WHITE: M_Props_Em, MI_CardboardTargtets (typo), MI_Glass_Alarm, MI_LaneDividers, MI_MetalLattice, MI_MetalShelves, MI_ShootingRange_mechanism, MI_VP_01, WorldGridMaterial` — `texByKey` has `cardboardtargets` not `cardboardtargtets`, `alarmlight` not `glass_alarm`. Fix: add alias map `targtets→targets`, `glass_alarm→alarmlight`, `vp_01→concrete_01` (or plain color), `WorldGrid` keep `WorldGridMaterial.mat` grid.

## 5. File Map

`TextureImportPostprocessor.cs:49` `AssetAuditFix.cs:104/214` `ModelImportPostprocessor.cs:26` `ShellScaleFix.cs` `ShellFixUCX.cs` `ShellEjectFix.cs` `TracerSetup.cs` `BuildingKitMaterialFix.cs` `BuildingKit/Meshes|Textures|Materials/` `QualitySettings.asset streamingMipmapsActive` `GraphicsSettings m_BrgStripping 2`.

