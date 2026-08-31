# SM_Gun_Pistol — URP Import, GunFX & Weapon Setup

**Date:** 2026-08-31  
**Scope:** `Assets/_Game/Prefabs/Weapons/SM_Gun_Pistol/` (+ `GunFX_Pistol/`) in `Official` (`6000.3.23f1`, URP `17.3.0`)  
**Tooling:** `unity-cli` live Editor (`unity status` → `ready` on `7800`), `eval_file` — no hand-edited YAML (per `AGENTS.md`)

## Overview

The official pistol and its GunFX were ported from UE `MyProject` (UE 5.8) to a Unity-native URP workflow:

- **Meshes:** `SM_Gun_Pistol` (pistol) + `SM_GunShells_HandGun` (ejected casing)
- **Pistol PBR:** `T_Pistol_BC/N/ORM` → `M_Pistol` (URP Lit)
- **Shell PBR:** `T_Basic_Shells_*` (basecolor/normal/metallic/AO) → `M_Shell_Brass` (URP Lit brass)
- **FX:** `T_Muzzle_Pistol_*_3x3` (3×3 flipbook) + `T_Smoke*` → `MuzzleFlash`/`MuzzleSmoke` ParticleSystems + `M_Muzzle_Additive`/`M_Smoke_Alpha`
- **Code:** `WeaponEffects` + `ShellCasing` (pooled) hooked to `Handgun`'s `FirearmEvents.onShoot`
- **Wiring:** `ItemCatalog_Default` now equips `SM_Gun_Pistol` instead of `FakeGun`

All `.meta` GUIDs must travel with the asset (see cross-project copy gotcha: `SKM_Man_City` pink when `M_Universal_A.mat` missing).

## Source Assets

```
UnityFBX/SM_Gun_Pistol.fbx                     guid:47c9a88bc8e96934092ff012f594c759  mesh FBX, UE cm (151,968 bytes)
UnityFBX/GunFX_Pistol/Meshes/SM_GunShells_HandGun.fbx guid:d37c338e8d525924aae93da1047fb3fd  shell FBX (30,432 bytes)
T_Pistol_BC.tga                                guid:7c3a6c1a5f90df143ba12402f9ffbbef  BaseColor (Albedo, sRGB, 2048)
T_Pistol_N.tga                                 guid:9254ac9498677554ca450626cd01f4ca  Normal (linear, 2048)
T_Pistol_ORM.tga                               guid:8375fca0e793e644ca587e8fdb63b49e  ORM (R=AO G=Roughness B=Metallic, linear, 2048)
T_Basic_Shells_basecolor.tga                   guid:88190c8e69264c34196b60bdd913f64a  Shell BaseColor (sRGB, 2048)
T_Basic_Shells_normal.tga                      guid:602aae6009641ed42a5cf85000a6b079  Shell Normal (linear, 2048)
T_Basic_Shells_metallic.tga                    guid:e719ea83f3faa7e4aad836246d18dfed  Shell Metallic (grayscale, linear, 2048)
T_Basic_Shells_ambientocclusion.tga            guid:2128136c0cf0c3b41a0115c826265300  Shell AO (grayscale, linear, 2048)
T_Muzzle_Pistol_01_3x3.tga etc. (02/03)        3× 16 MB flipbooks, sRGB, alpha, 3×3
T_SmokeBurst_01/05, T_SmokePuff_01/05, T_Smoke_Thin.tga  smoke/trail, sRGB, alpha
M_Pistol.mat                                   guid:b9fbc4e2581e4af9846f9ed28e2f1981  URP Lit (pistol)
M_Shell_Brass.mat                              guid:8645edbeeb39c2a4c80ebcf54c52e916  URP Lit (shell brass)
SM_Gun_Pistol.prefab                           guid:975c9a7862007e046ba78ca2c7040665  weapon prefab
SM_GunShells_HandGun.prefab                    guid:d2326e9af5fe87e4283f2c39bbfdb0db  pooled shell prefab
MuzzleFlash.prefab                             guid:0c13414778fd1d747a63f3bfa4adc75d
MuzzleSmoke.prefab                             guid:648e91ce93c687b439b6e9d2131aa6b2
```

Source in UE: `Content/MTF_Environment/Assets/SM_Gun_Pistol` + `Content/GunFX/Meshes/Gun_Shells/SM_GunShells_HandGun` + `Content/GunFX/Textures/PBR/Gun_Shells/Basic/T_Basic_Shells_*`. Niagara systems (`NS_Muzzle_Pistol_*`, `NS_Ejection_HandGun`, `NS_BulletTrail`, `NS_Impact_*`) are UE-only graphs — only meshes/textures are portable; behavior is rebuilt via Shuriken.

Export via `UnrealEditor.exe MyProject.uproject -ExecutePythonScript=.../export_shell_textures.py` using `unreal.Exporter.run_asset_export_task` (TGA preserves alpha for flipbooks). Updated `export_gunfx_pistol.py` expected `T_Basic_Shells_BC/N/ORM` but the actual assets are `T_Basic_Shells_basecolor/normal/metallic/ambientocclusion` — a dedicated `export_shell_textures.py` now handles the correct 4 (no roughness; metallic is scalar, smoothness via scalar).

## 1) FBX Import

Both FBXs require UE cm → m:

```yaml
materials:
  materialImportMode: 0   # None — no auto-materials
meshes:
  globalScale: 0.01       # was 1 → 100× too large
  useFileScale: 1
animationType: None (shell) / Generic
```

Verified via `Get-Content *.fbx.meta | Select-String globalScale,materialImportMode`. Applied live with `ModelImporter.SaveAndReimport()`.

## 2) Texture Import

| Texture | `textureType` | `sRGBTexture` | Unity slot |
|---------|---------------|---------------|------------|
| `T_Pistol_BC` | `0` Default | `1` | `_BaseMap` / `_MainTex` (sRGB) |
| `T_Pistol_N` | `1` Normal map | `0` | `_BumpMap` + `_NORMALMAP` |
| `T_Pistol_ORM` | `0` Default | `0` linear | `_OcclusionMap` only (see note) |
| `T_Basic_Shells_basecolor` | `0` Default | `1` | `_BaseMap` (sRGB) |
| `T_Basic_Shells_normal` | `1` Normal map | `0` | `_BumpMap` + `_NORMALMAP` |
| `T_Basic_Shells_metallic` | `0` Default | `0` linear | `_MetallicGlossMap` (R=metallic) |
| `T_Basic_Shells_ambientocclusion` | `0` Default | `0` linear | `_OcclusionMap` (R=AO) |
| `T_Muzzle_Pistol_*_3x3` | `0` Default | `1` | `M_Muzzle_Additive` BaseMap, `AlphaSource: FromInput`, `Wrap: Clamp`, `Tiles 3×3` |
| `T_Smoke*` | `0` Default | `1` | `M_Smoke_Alpha`, `AlphaIsTransparency` |

All `maxTextureSize:2048`. Muzzle flipbooks retain `Wrap Clamp` + `Bilinear` for texture-sheet animation.

**Pistol ORM note (UE):** `R=AO, G=Roughness, B=Metallic`. URP Lit `MaskMap` expects `R=Metallic, G=AO, A=Smoothness (=1-Roughness)`. The texture is intentionally kept as non-readable (runtime memory) — therefore no repack is applied. `M_Pistol` uses raw `T_Pistol_ORM` only as `_OcclusionMap` (R channel) and scalar `_Metallic:0.85` / `_Smoothness:0.5`. For pixel-perfect you would need to make the source readable, `GetPixels32`, and write `Mask.R=ORM.B, G=ORM.R, A=1-ORM.G` to a new `_MaskMap` or split `Occlusion(R)` + `MetallicGloss(R=Metallic, A=Smoothness)`.

**Shell PBR note:** GunFX Basic shells are author split (no ORM). The set is `basecolor` + `normal` + `metallic` (grayscale) + `ambientocclusion` (grayscale); roughness is implicit — `M_Shell_Brass` supplies `_Smoothness:0.85` as a scalar with `Metallic:1` (brass).

## 3) Materials

All URP Lit (`shader guid:933532a4fcc9baf4fa0491de14d08ed7`), **not** Standard/HDRP.

**M_Pistol** (`b9fbc4...`):
```yaml
m_Shader: {fileID:4800000, guid:933532a4fcc9baf4fa0491de14d08ed7}
m_ValidKeywords: [_NORMALMAP, _OCCLUSIONMAP]  # _METALLICSPECGLOSSMAP + _MASKMAP intentionally off (ORM fallback)
_BaseMap/MainTex: {guid:7c3a6c1a...}  # BC
_BumpMap:        {guid:9254ac94...}  # N, _BumpScale 1
_OcclusionMap:   {guid:8375fca0...}  # ORM.R
_Floats: _Metallic 0.85, _Smoothness 0.5, _OcclusionStrength 1, _WorkflowMode 1
```

**M_Shell_Brass** (`8645ed...`):
```yaml
m_Shader: {fileID:4800000, guid:933532a4fcc9baf4fa0491de14d08ed7}
m_ValidKeywords: [_NORMALMAP, _METALLICSPECGLOSSMAP, _OCCLUSIONMAP]
_BaseMap/MainTex: {guid:88190c8e...}  # T_Basic_Shells_basecolor
_BumpMap:         {guid:602aae60...}  # T_Basic_Shells_normal
_MetallicGlossMap:{guid:e719ea83...}  # T_Basic_Shells_metallic (R)
_OcclusionMap:    {guid:2128136c...}  # T_Basic_Shells_ambientocclusion (R)
_Floats: _Metallic 1, _Smoothness 0.85, _BumpScale 1, _OcclusionStrength 1, _WorkflowMode 1 (Metallic)
```

**M_Muzzle_Additive** (FX folder): `Universal Render Pipeline/Particles/Unlit`, `_BaseMap: T_Muzzle_Pistol_01_3x3`, HDR tint `(1,0.55,0.12)`, `_Surface: Transparent`, `SrcAlpha/One` (additive), `ZWrite 0`.
**M_Smoke_Alpha**: same shader, `_BaseMap: T_SmokePuff_01`, gray `(0.55,0.55,0.55,0.7)`, `SrcAlpha/OneMinusSrcAlpha`.

All created via `eval_file` + `AssetDatabase.CreateAsset`.

## 4) Prefabs

Created **live** via `unity command eval_file` (no YAML hand-edit):

**SM_Gun_Pistol.prefab** (`975c9a...`, structure `1,4,95,114`):
- `SM_Gun_Pistol` — `Transform` @ `(0,0,0) scale 1`, `Animator` (`m_Controller:9100000 f0f536df...` = `FakeGun.controller`), `Handgun` (`2670f2ff`, `_bulletPrefab:143653b7` Projectile, `_shootPoint`), `Weapon` (`c85ce6`, `_id:SM_Gun_Pistol`, `_fireRate:0.2`, `_clipSize:12`, `_reserveAmmo:36`), `WeaponEffects` (see §5), children `[Model, ShootPoint, Muzzle/MuzzleSmoke, Eject]`
- `Model (PrefabInstance:26029836...)` — source `47c9a88...`, overrides `m_Materials.Array.data[0] → M_Pistol b9fbc4...`
- `ShootPoint` — `@ (0.15,0.02,0)` muzzle approx, `Muzzle` snapped to it after creation
- `Muzzle` (instanced `MuzzleFlash.prefab`) + `MuzzleSmoke` child

**SM_GunShells_HandGun.prefab** (`d2326e...`):
- Root `SM_GunShells_HandGun` — `Transform`, `Rigidbody (mass 0.01, angularDamping 0.05, gravity, continuous)`, `BoxCollider (0.02,0.02,0.05)`, `ShellCasing (9a0e3b...)`, child `SM_GunShells_HandGun` mesh instance (FBX `d37c33...` with material override `M_Shell_Brass`)

**MuzzleFlash.prefab** / **MuzzleSmoke.prefab**: Billboard `ParticleSystem` prefabs (`Duration 0.06/0.5`, `Burst 1/3`, `Size 0.3/0.15`, `Speed 0/0.5`, `ColorOverLifetime fade`, `Shape disabled`, `TextureSheetAnimation 3×3 whole-sheet` on flash). Muzzle flash lives as `Muzzle`, smoke as `Muzzle/MuzzleSmoke` inside the pistol.

## 5) Weapon Code — `WeaponEffects` + `ShellCasing`

New scripts in `Assets/_Game/Scripts/Items/Weapons/` (`Game.Items`):

**`WeaponEffects.cs` (`5ee8ac75...`)** — local presentation only; gameplay remains `Handgun`/`BulletProjectile`:
```csharp
[Header("Muzzle")] ParticleSystem _muzzleFlash, _muzzleSmoke; Light _muzzleLight;
[Header("Shell Ejection")] ShellCasing _shellPrefab; Transform _ejectPoint; float _shellLife=3;
Vector3 _ejectionVelocity=(2,1,-0.5), _ejectionTorque=(4,7,3);
ObjectPool<ShellCasing> _shellPool (default 4, max 16, collectionCheck);
PlayShootEffects() { _muzzleFlash.Play(true); _muzzleSmoke.Play(true); flash light 0.04s; _shellPool.Get().Launch(_ejectPoint.position/rotation, _ejectPoint.TransformDirection(vel), torque, life, _shellPool.Release) }
```

**`ShellCasing.cs` (`9a0e3be4...`)**:
```csharp
Rigidbody _rigidbody; Action<ShellCasing> _release; Coroutine _releaseRoutine;
Launch(pos,rot,vel,torque,life,release){ _release=release; SetPositionAndRotation; linearVelocity=vel; angularVelocity=torque; ReleaseAfter(life) }
OnDisable(){ linearVelocity=angularVelocity=0; stop coroutine }
```

**`Weapon.cs:51` patch** — composition-root rule: `Weapon.RegisterEvents` now composes the FX delegate:
```csharp
var effects = GetComponent<WeaponEffects>();
if (effects == null) { _firearm.RegisterEvents(events); return; }
_firearm.RegisterEvents(new FirearmEvents{ onShoot = events.onShoot + effects.PlayShootEffects, onReloadStarted = events.onReloadStarted, onReloadFinished = events.onReloadFinished });
```
So `HandgunShootingState:42`’s `fireArmEvents.onShoot` (fired only when `ExecuteActualShoot` actually launches a pooled `BulletProjectile` — dry fire never spends a round nor triggers recoil/FX) drives both `CharacterLocomotion.onWeaponShoot` (recoil) and `WeaponEffects.PlayShootEffects`. Networking: `BulletProjectile` + `NetworkedDamage` remain server-authoritative; FX is pure local pooled presentation.

## 6) ItemCatalog

`Assets/_Game/Data/Items/ItemCatalog_Default.asset:16` now references `SM_Gun_Pistol.prefab`:
```yaml
items:
- {fileID:5784423109619740749, guid:975c9a7862007e046ba78ca2c7040665, type:3}
```
Previously `FakeGun` (`76ea57a27dc38e247a970b11a9733b93`). `CharacterLocomotion.EquipWeapon` instantiates via the catalog; no FPS hand IK yet (same as `FakeGun`).

## 7) Verification

```powershell
Get-Content SM_Gun_Pistol.fbx.meta | Select-String globalScale,materialImportMode
Get-Content GunFX_Pistol/Meshes/SM_GunShells_HandGun.fbx.meta | Select-String globalScale,materialImportMode
Get-Content T_Pistol_N.tga.meta | Select-String textureType,sRGBTexture
Get-Content GunFX_Pistol/Textures/T_Basic_Shells_normal.tga.meta | Select-String textureType,sRGBTexture
Get-Content GunFX_Pistol/Textures/T_Basic_Shells_metallic.tga.meta | Select-String sRGBTexture
Get-Content M_Pistol.mat | Select-String _BaseMap,_BumpMap,_OcclusionMap,933532a4
Get-Content GunFX_Pistol/M_Shell_Brass.mat | Select-String _BaseMap,_BumpMap,_MetallicGlossMap,_OcclusionMap,933532a4
Get-Content SM_Gun_Pistol.prefab | Select-String "WeaponEffects|MuzzleSmoke|SM_GunShells|_muzzle|_shellPrefab"
```

Live checks (`eval_file`):
- `verify_pistol_setup.cs` — pistol, WeaponEffects, Muzzle, MuzzleSmoke, Eject, ShellPrefab/Casing, URP Lit, OcclusionMap, shell scale 0.01 all true.
- `verify_shell.cs` — `M_Shell_Brass` URP Lit with all 4 maps, metallic 1 / smoothness 0.85, sRGB settings correct (basecolor sRGB, others linear, normal is NormalMap).
- EditMode 108 + PlayMode 130 tests green after changes (`unity command run_tests --mode editmode/playmode --async_tests` + `test_status`).

## 8) End-to-End Flow

`Input → CharacterLocomotion.HandleShoot → Weapon.TriggerShoot(aimPos) → Handgun.Shoot → HandgunShootingState.Enter (CrossFade fakeGun_shoot, ExecuteActualShoot pools BulletProjectile, clip--, onShoot) → onShoot → WeaponEffects.PlayShootEffects (muzzle + shell) + CharacterLocomotion.onWeaponShoot (recoil).` Shell casings live 3 s via pooled `ShellCasing`, then return to `ObjectPool`.

## References

- `Assets/_Game/Prefabs/Weapons/FakeGun/FakeGun.prefab:42,95,108` — reference Handgun/Weapon/Animator/Projectile contract
- `Assets/_Game/Art/Environment/Materials/GridBlue_01_Mat.mat:11,29` — URP Lit template
- `Assets/_Game/Scripts/Items/Weapons/Handgun.cs:42,110,132` — pooling, shoot direction, MuzzleExitOffset
- `Assets/_Game/Scripts/Items/Weapons/States/HandgunShootingState.cs:42` — onShoot gated on real launch
- `Assets/_Game/Scripts/Items/Weapon.cs:29,51` — finite reserve + FX composition
- `docs/shooting_engine_notes.md`, `AUDIT_FINAL.md` — ORM→Mask swizzle reference
- Unity 6 Manual: `TextureImporter` (sRGB/normal), `ModelImporter.globalScale`, Particle System `Texture Sheet Animation`, `Universal Render Pipeline/Lit`
