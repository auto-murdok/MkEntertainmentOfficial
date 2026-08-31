# SM_Gun_Pistol — URP Import & Prefab Setup

**Date:** 2026-08-31  
**Scope:** `Assets/_Game/Prefabs/Weapons/SM_Gun_Pistol/` in `Official` (`6000.3.23f1`, URP `17.3.0`)  
**Tooling:** `unity-cli` live Editor (`unity status` → `ready` on `7800`), `eval_file` — no hand-edited YAML (per `AGENTS.md`)

## Source Assets

```
SM_Gun_Pistol.fbx          guid:47c9a88bc8e96934092ff012f594c759  mesh FBX, UE cm
T_Pistol_BC.tga             guid:7c3a6c1a5f90df143ba12402f9ffbbef  BaseColor (Albedo)
T_Pistol_N.tga              guid:9254ac9498677554ca450626cd01f4ca  Normal
T_Pistol_ORM.tga            guid:8375fca0e793e644ca587e8fdb63b49e  ORM (R=AO, G=Roughness, B=Metallic)
M_Pistol.mat                guid:b9fbc4e2581e4af9846f9ed28e2f1981  URP Lit (created)
SM_Gun_Pistol.prefab        guid:975c9a7862007e046ba78ca2c7040665  weapon prefab (created)
```

All `.meta` files must travel with the asset — GUIDs are the reference (see cross-project copy gotcha: `SKM_Man_City` pink when `M_Universal_A.mat` missing).

## 1) FBX Import — `SM_Gun_Pistol.fbx.meta`

Required for UE → Unity (cm → m):

```yaml
materials:
  materialImportMode: 0   # None — was 2 (import via description) → no auto-mats
meshes:
  globalScale: 0.01       # was 1 → 100× too large
  useFileScale: 1
```

`remapMaterialsIfMaterialImportModeIsNone: 0`. Verified via `Get-Content SM_Gun_Pistol.fbx.meta | Select-String globalScale,materialImportMode`.

## 2) Texture Import — `T_Pistol_*.tga.meta`

| Texture | `textureType` | `sRGBTexture` | `textureShape` | Unity slot |
|---------|---------------|---------------|----------------|------------|
| `T_Pistol_BC` | `0` Default | `1` | `1` | `_BaseMap` / `_MainTex` (sRGB) |
| `T_Pistol_N` | `1` **Normal map** | `0` | `1` | `_BumpMap` + `_NORMALMAP` |
| `T_Pistol_ORM` | `0` Default | `0` **linear** | `1` | `_MaskMap` / `_Occlusion` / `_MetallicGloss` |

Fix: `T_Pistol_N` was `0/Default, sRGB 1` → now `1/0`; `T_Pistol_ORM` was `sRGB 1` → `0`. `BC` stays `sRGB 1`. All `maxTextureSize:2048`.

**ORM packing note (UE):** `R=AO, G=Roughness, B=Metallic`. URP Lit `MaskMap` expects `R=Metallic, G=AO, B=DetailMask, A=Smoothness (=1-Roughness)`. Current `M_Pistol` assigns raw ORM to `_MaskMap`/`_OcclusionMap`/`_MetallicGlossMap` for immediate PBR (BC+N correct). For pixel-perfect, repack: `Mask.R=ORM.B`, `Mask.G=ORM.R`, `Mask.A=1-ORM.G` (or split to `Occlusion(R)` + `MetallicGloss(R=Metallic, A=Smoothness)`).

TGA is fine; PNG conversion optional (re-import, keep GUIDs, smaller Git).

## 3) Material — `M_Pistol.mat`

URP Lit (`shader guid:933532a4fcc9baf4fa0491de14d08ed7`, same as `GridBlue_01_Mat.mat`, `M_Universal_A.mat`), **not** Standard/HDRP (project is URP):

```yaml
m_Shader: {fileID:4800000, guid:933532a4fcc9baf4fa0491de14d08ed7, type:3}
m_ValidKeywords: [_NORMALMAP, _MASKMAP]
_BaseMap/MainTex: {guid:7c3a6c1a...}   # BC
_BumpMap:        {guid:9254ac94...}   # N, _BumpScale 1
_MaskMap:        {guid:8375fca0...}   # ORM (see packing note)
_MetallicGlossMap / _OcclusionMap: {guid:8375fca0...}  # same ORM duplicated for slots
_Floats: _Metallic 0, _Smoothness 0.5, _OcclusionStrength 1, _WorkflowMode 1 (Metallic)
```

Created via file write + `AssetDatabase.Refresh()` (live Editor). Verify: `Get-Content M_Pistol.mat | Select-String _BaseMap,_BumpMap,_MaskMap,933532a4`.

## 4) Prefab — `SM_Gun_Pistol.prefab`

Created **live** via `unity command eval_file` (no YAML hand-edit):

```csharp
// CreatePistol.cs (summarized, fully-qualified, no usings per AGENTS.md §4)
var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
var root = new GameObject("SM_Gun_Pistol");
var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx); // child "Model"
model.transform.SetParent(root.transform, false);
foreach (var r in model.GetComponentsInChildren<Renderer>(true))
  r.sharedMaterials = new[] { mat }; // material overrides: m_Materials[0] → M_Pistol b9fbc4...
var shootPoint = new GameObject("ShootPoint"); // (0.15,0.02,0) muzzle approx
shootPoint.transform.SetParent(root.transform, false);
root.AddComponent<Animator>().runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(FakeGun.controller f0f536df...);
var handgun = root.AddComponent<Handgun>(); // 2670f2ff...
new SerializedObject(handgun).FindProperty("_bulletPrefab").objectReferenceValue = projectile; // 143653b7...
FindProperty("_shootPoint").objectReferenceValue = shootPoint.transform;
PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
```

Patched to add `Weapon` (`c85ce611...` → `Item` base `id`):

```csharp
var weapon = instance.AddComponent<Weapon>();
new SerializedObject(weapon).FindProperty("_id").stringValue = "SM_Gun_Pistol";
FindProperty("_fireRate").floatValue = 0.2f; FindProperty("_recoilForce").floatValue = 5f;
FindProperty("_clipSize").intValue = 12; FindProperty("_reserveAmmo").intValue = 36;
PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
```

**Result prefab structure** (`SM_Gun_Pistol.prefab:1,4,95,114`):

- `SM_Gun_Pistol (733922669...)` — `Transform:37763139... @ (0,0,0) scale 1`, `Animator:27377639...` (`m_Controller:9100000 f0f536df...`), `Handgun:79840754...` (`_bulletPrefab:53083595... 143653b7...`, `_shootPoint:31546451...`), `Weapon:c85ce6...` (`_id:SM_Gun_Pistol`, `_fireRate:0.2`, `_clipSize:12` etc), children `[Model, ShootPoint]`
- `Model (PrefabInstance:26029836...)` — source `47c9a88...`, overrides `m_Materials.Array.data[0] → 2100000:b9fbc4e...` (M_Pistol) ×2 renderers, `m_Name:Model`
- `ShootPoint (49877351...)` — `Transform:31546451... @ (0.15,0.02,0)`

Drop-in for `FakeGun.prefab` — same `Handgun`+`Weapon`+`Projectile` contract (`CharacterLocomotion` via `ItemCatalog`). Scale is correct via `globalScale 0.01`; no extra root scale needed.

## Verification

```powershell
Get-Content SM_Gun_Pistol.fbx.meta | Select-String globalScale,materialImportMode
Get-Content T_Pistol_N.tga.meta | Select-String textureType,sRGBTexture
Get-Content T_Pistol_ORM.tga.meta | Select-String sRGBTexture
Get-Content M_Pistol.mat | Select-String _BaseMap,_BumpMap,_MaskMap,933532a4
Get-Content SM_Gun_Pistol.prefab | Select-String "b9fbc4e|143653b7|_shootPoint|Handgun"
```

Also synced to `MkEntertainmentOfficial/Assets/_Project/Weapons/SM_Gun_Pistol/` (same GUIDs, same fixes) for parity.

## References

- `Assets/_Game/Prefabs/Weapons/FakeGun/FakeGun.prefab:42,95,108` — reference structure (Handgun 2670f2ff, Weapon c85ce6, Animator f0f536df, Projectile 143653b7)
- `Assets/_Game/Art/Environment/Materials/GridBlue_01_Mat.mat:11,29` — URP Lit template
- `AUDIT_FINAL.md`, `docs/shooting_engine_notes.md` — ORM→Mask swizzle (`R:Metal G:AO A:1-Rough`) used in mansion audit
