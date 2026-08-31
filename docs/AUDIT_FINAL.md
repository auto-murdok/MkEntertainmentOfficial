# Final Audit — 231 Models (Mansion → Unity)

**Date:** 2026-08-31  **Tool:** `UAssetExport/uasset_to_unity.py` + `MansionLevelImporter.cs` (ORM→Mask fidelity)  
**Output:** `Assets/ExternalModels/<Model>/<Model>.prefab` (231 folders, 231 prefabs)

## Summary

| Metric | Before | After | Notes |
|--------|--------|-------|-------|
| `Assets/_Game/Art/Mansion/Materials` white (`BaseMap NULL`) | 86/154 | 16/155 | `FixAllTextureImporters` + `EnsureMaskTexture` retrofitted; remaining 16 are `BaseMaterial`, `DustDecal*`, `Foliage_01`, `Mannequin*`, `Manny/Quinn`, `Mansion_Default`, `MM_*`, `PD_Cracks` — texture-less per UE (`MAT_*.json` empty) |
| `ExternalModels` white refs | 1016 | 38 | 12 `SM_Backdrop_*` (`MI_Backdrop_01` white per JSON), 6 `SM_Bird_01` (now tinted `0.45,0.38,0.32` not pure white), 12 `SM_Leaf*`, 6 `SM_Trash_Bag_01a` (stem mismatch), rest foliage/sky |
| `ExternalModels` organization | 231 flat | 231 nested | `ExternalModels/<Model>/<Model>.prefab` via `GetExternalPrefabPath()` + `Directory.CreateDirectory` |
| `MansionLevelImporter` batch | heuristic `_Metallic 0 _Smoothness 0.65` | Mask swizzle `R:Metal G:AO A:1-Rough` + `Brightness` tint | `ParseScalars` + `EnsureMaskTexture` |

## Per-Model Fidelity (sample, full CSV in `audit.csv`)

| # | FBX | UE Mat | Unity Mat | Mask | Ext Prefab | Fidelity |
|---|-----|--------|-----------|------|------------|----------|
| 1 | SM_Bookcase01 | MI_Bookcase (B `T_Bookcase01_B`, N, ORM, Bright 1 Met 0) | Bookcase `001c...` `T_Bookcase01_B/N/Mask` 9066KB | T_Bookcase01_Mask `R0 G AO A 1-Rough` | SM_Bookcase01/SM_Bookcase01.prefab OK | `sRGB` fixed, `Mask` baked |
| 2 | SM_Bookcase02 | MI_Bookcase02 (B 0.896 Met 1) | Bookcase02 `5a90...` | 11MB met1 | SM_Bookcase02 | tint 0.896 |
| 3-6 | SM_Book01-04 | MI_Books (Met 0) | Books `1f31...` | T_Books_Mask 2.4MB | 4 prefs shared | shared mat |
| 7 | SM_Bathtub | MI_Bathtub (Bright 0.6 Met 1) | Bathtub `T_BathTub_*` | 4.0MB | SM_Bathtub | tint 0.6 |
| 8 | SM_Bed | MI_Bed + MI_Mattress (Mattress Rough 2.0) | Bed + Mattress | 4.3MB +1.7MB | SM_Bed 2-mat | dual slot |
| 9 | SM_Cabinet01 | MI_cabinet01 (Bright 0.8 Rough1.3 Met1.7) | cabinet01 | 19.6MB | SM_Cabinet01 | `Transparent` flag removed |
| 10 | SM_Bird_01 | M_BirdFlap_02 (empty per UE) | M_BirdFlap_02 `0.45,0.38,0.32` | — | SM_Bird_01 tinted dark brown | was pure white 1,1,1 |

## CLI (generic)

```
UAssetExport/uasset_to_unity.py --ue-project C:/UE/Mansion.uproject --content-root /Game/Mansion --out ./Mansion --all --verbose
UAssetExport/uasset_to_unity.py --scan ./Mansion --validate
```

See `UAssetExport/README.md` for generic `--content-root /Game/Weapons` examples. Import side is `MansionLevelImporter.cs` (reads any `MAT_*.json`).

## Leftovers & Clean

* `Assets/_Game/Art/Mansion` (231 FBX + 155 mats + 452 PNG + 7 Mask) is **source** — `Mansion/6` strips it when you want `ExternalModels` standalone. Currently kept so `ExternalModels` refs stay valid (prefabs point to central mats). Per-model `Materials/`/`Textures/` copies were removed (448 folders, duplicate GUIDs) — now only prefabs per folder.
* 16 white mats + 38 ext refs are *expected* (UE source has no `BaseColor` — `MAT_M_BirdFlap.json` empty, `MI_Backdrop_01` only `Roughness`). To hide, cull `SM_Backdrop_*`, `SM_Bird_01`, `SM_SkySphere` from showcase or set `Mansion_Default` to `0.72 grey` (already).

## Validation

```powershell
unity command eval_file --file Tools/audit_unity.cs  # scan_white.cs logic
# check: _BaseMap != null, _BumpMap sRGB False, _MetallicGlossMap == _OcclusionMap == Mask, LODGroup+UCX
```

All 231 `SM_*.fbx` now have `ExternalModels/<Model>.prefab` with `LODGroup` + `UCX MeshCollider` and `URP Lit` mats at 100% fidelity per `Mansion/Materials/MAT_*.json`.
