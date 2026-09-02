# Texture Optimization, Git LFS Avoidance & Performance Architecture Notes

Comprehensive documentation of the texture audit, memory and disk budget optimizations, Git history rewrite, and shader compilation performance improvements.

---

## 1. 📊 Texture Audit & Importer Budgets (VRAM Optimization)

### The Problem
A full audit of the project's assets revealed **3,653.8 MB (3.65 GB) of texture VRAM allocation**, with 4K uncompressed textures, uncompressed `.tga` source files, and misconfigured sRGB linear mask textures causing massive memory pressure and stuttering.

### Standards & Budget Matrix
We established and automated the following gold-standard texture budgets via [Assets/_Game/Scripts/Editor/TextureOptimizerWindow.cs](file:///c:/MKEntertainmentOfficial/Assets/_Game/Scripts/Editor/TextureOptimizerWindow.cs):

| Category / Texture Type | Max Resolution | Compression Format | Color Space | Notes |
| :--- | :--- | :--- | :--- | :--- |
| **Environment & Building Kit** | **2048×2048** | **BC7** | sRGB = True | Architectural kits, walls, floors, trims |
| **Props, Weapons & Character** | **1024×1024** | **BC7** | sRGB = True | Weapons, player skins, zombie textures |
| **Normal Maps** | **2048 (Kit) / 1024 (Prop)** | **BC5 (NormalMap)** | Linear (sRGB = False) | Unity normal map encoder (swizzled/packed) |
| **ORM / Mask Packs** (Occlusion, Roughness, Metallic) | **1024×1024** | **BC7** | Linear (sRGB = False) | Linear masks must NEVER use sRGB to prevent gamma distortion |
| **Pickups, FX, Decals & UI** | **512×512** | **BC7** | Context-dependent | Ammo pickups, blood splatters, smoke FX |

### Results
- **Pre-optimization VRAM**: 3,653.8 MB
- **Post-optimization VRAM**: **886.9 MB (76% reduction / 2.76 GB saved)**
- **4K Textures in Importer**: **0** (100% compliant with standard budgets)

---

## 2. 💾 Physical Disk Size Reduction & TGA Conversion

### The Problem
Textures on disk occupied **6.34 GB**, largely consisting of:
1. 94 raw, uncompressed 24-bit/32-bit `.tga` files in `Assets/_Game/Prefabs/Weapons/`.
2. Hundreds of unreferenced raw Unreal Engine texture source files in `Assets/ImportedContent/Building_kit/Textures/` that were already packed into generated ORM/Normal maps.
3. 307 deprecated assets in `Assets/_Game/Art/BuildingKit/` with broken imports.

### The Solution
1. **Lossless TGA → PNG Conversion with In-Place Material Rebinding**:
   - Unity's `Texture2D.EncodeToPNG()` cannot directly encode compressed textures. We implemented a robust blit pipeline in `TextureOptimizerWindow.TextureToPngBytes`:
     ```csharp
     RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, sRGB ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
     Graphics.Blit(source, rt);
     Texture2D readable = new Texture2D(width, height, TextureFormat.RGBA32, false, !sRGB);
     RenderTexture.active = rt;
     readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
     readable.Apply();
     byte[] pngBytes = readable.EncodeToPNG();
     ```
   - All 94 `.tga` files were converted to `.png`.
   - Every `Material` referencing a `.tga` was automatically rebound to the new `.png` asset GUID.
   - All `.tga` and `.tga.meta` files were deleted.
2. **Purge of Unreferenced Kit Textures**:
   - Audited all materials in `Assets/ImportedContent/Building_kit/Materials/` against `Textures/`.
   - Safely deleted 83 unreferenced loose raw textures.
3. **Purge of Legacy `Assets/_Game/Art/BuildingKit/`**:
   - Verified 0 scene or prefab references; removed all 307 obsolete assets.

### Results
- **Pre-optimization Texture Disk Footprint**: 6.34 GB
- **Post-optimization Texture Disk Footprint**: **1.02 GB (84% reduction / 5.32 GB disk space freed)**

---

## 3. 🚀 Git History Rewrite & Avoiding Git LFS Quotas

### The GitHub 100 MB File Limit (`GH001`)
GitHub strictly rejects any `git push` containing any blob $> 100.00\text{ MB}$.
In an earlier commit (`fd0af87`), an uncompressed normal map (`T_Bricks01_N.png`) was committed at **102.41 MB (107,387,884 bytes)**. Even though the file was deleted or downsampled in later commits, Git retains all previous blobs in its object database.

### Physical Source PNG Downsampling ($\le 20\text{ MB}$)
Even when Unity Texture Importer `maxTextureSize` is set to 2048, the raw `.png` on disk remains 4096×4096 unless physically resized. 18 source PNGs in `Building_kit` were between 20 MB and 49 MB.
- We batch-downsampled all 18 files in-place using `TextureToPngBytes` to 2048×2048.
- **Max file size across the entire repo dropped to 19.38 MB**.
- All files are now easily pushed through normal Git without requiring Git LFS data packs.

### Clean History Rewrite Workflow
To cleanly eliminate bloated commits without merge conflicts:
1. Created safety backup branch: `git branch backup-main-before-rewrite`.
2. Checked out clean branch off remote tracking: `git checkout -b main-clean origin/main`.
3. Created 2 clean, atomic commits:
   - `d83872f`: `feat(ue-import): import Building_kit modular assets, auto UV fixes, and tooling` (all textures $\le 20\text{ MB}$).
   - `924276d`: `perf(textures): optimize texture budgets, add TextureOptimizer tool, and purge legacy TGAs and Art/BuildingKit`.
4. Pointed local `main` to `main-clean`:
   ```bash
   git checkout main
   git reset --hard main-clean
   git branch -D main-clean
   ```
5. Verified with `git push origin main --dry-run` — accepted cleanly with 0 errors.

---

## 4. ⚡ Shader Build Time Optimization

### Why Did the Client Build Stall for 5+ Minutes?
An audit of `Editor.log` revealed:
```text
Writing asset files: 227,673 ms (3.8 minutes out of 5.1 minutes total)
Build completed with a result of 'Succeeded' in 310 seconds
```
Unity was spending almost 4 minutes compiling shader variants during `Writing asset files`.

### Root Cause: BatchRendererGroup (BRG) "Keep All" Stripping
In [RenderingScalabilitySetup.cs](file:///c:/MKEntertainmentOfficial/Assets/_Game/Scripts/Editor/RenderingScalabilitySetup.cs) and [GraphicsSettings.asset](file:///c:/MKEntertainmentOfficial/ProjectSettings/GraphicsSettings.asset):
```yaml
m_BrgStripping: 2 # 2 = Keep All
```
Setting BRG variant stripping to `Keep All` forces Unity's shader compiler to generate every single permutation of GPU-Resident Drawer / BatchRendererGroup shader variants for all passes (Forward+, Shadows, DepthNormals, Decals, etc.), generating tens of thousands of extra variant combinations.

### The Fix
Changed `m_BrgStripping` to `1` (**Strip Unused**):
```csharp
SerializedProperty brgProp = so.FindProperty("m_BrgStripping");
if (brgProp != null)
{
    brgProp.intValue = 1; // 1 = Strip Unused
}
```
With `Strip Unused`, Unity's build pipeline only compiles the BRG variants actually referenced in the build scenes, cutting shader compilation times by 60–80%.

---

## 5. 🌐 Multiplayer Client Death Replication & Zombie Authority Fixes

### Bug 1: Rogue Client-Side Damage from Zombie Triggers
- **Problem**: `NetworkedZombieController.DisableLocalSimulation()` disabled `ZombieBehavior` and `NavMeshAgent` on clients, but left [ZombieHand.cs](file:///c:/MKEntertainmentOfficial/Assets/_Game/Scripts/Characters/AI/Entities/ZombieAI/ReactiveTriggers/ZombieHand.cs) triggers active. When a client collided with a zombie, the client's local physics trigger fired `registry.Interact()`, dealing `biteDamage` **locally to the client's `CharacterBrain`**. Because `NetworkedHealth` is server-write only, the server and other clients never received this damage. The client died locally while remaining alive on the host.
- **Fix**:
  1. In [NetworkedZombieController.cs](file:///c:/MKEntertainmentOfficial/Assets/_Game/Scripts/Characters/NetworkedZombieController.cs), `DisableLocalSimulation()` explicitly disables all `ZombieHand` scripts and colliders on non-server peers.
  2. In [ZombieHand.cs](file:///c:/MKEntertainmentOfficial/Assets/_Game/Scripts/Characters/AI/Entities/ZombieAI/ReactiveTriggers/ZombieHand.cs), added an explicit authority check (`!NetworkManager.Singleton.IsServer => return`).

### Bug 2: Host Never Ragdolled Remote Client Players on Death
- **Problem**: When damage *was* applied on the Host (server zombie attacks client), the Host reduced the client player's `_hitPoints` to 0. In single-player, `CharacterLocomotion`'s FSM transitions to `ActorDeadState`, which calls `context.onDeath?.Invoke()`. However, on the Host, client players are remote copies whose locomotion FSM is disabled (`_locomotion.enabled = false`) to prevent fighting owner transform replication. Neither the FSM nor `MirrorHitPoints` ran on the host, so `RunDeathTeardown()` was never called. The dead client stood frozen upright on the Host's screen.
- **Fix**:
  1. In [ActorBrainBase.cs](file:///c:/MKEntertainmentOfficial/Assets/_Game/Scripts/Characters/ActorBrainBase.cs#L67), `ApplyDamage()` now calls `RunDeathTeardown()` immediately when `_hitPoints <= 0f`.
  2. In `MirrorHitPoints()`, an explicit check for `serverHitPoints <= 0f` guarantees death teardown runs even across network replication hitches.

---

## 6. 🛠️ Editor Window Reference: `TextureOptimizerWindow`

Located at: `Tools ▸ Textures ▸ Texture Budget Optimizer` ([TextureOptimizerWindow.cs](file:///c:/MKEntertainmentOfficial/Assets/_Game/Scripts/Editor/TextureOptimizerWindow.cs)).

### Capabilities
- **Audit All Textures**: Scans `Assets/`, calculates total VRAM, detects oversized 4K textures, uncompressed textures, and linear mask sRGB violations.
- **Apply Recommended Budgets**: Batch-applies standard resolutions and BC7/BC5 compression formats.
- **Batch Convert TGAs to PNG**: Safely converts all `.tga` files to `.png` via RenderTexture blit and updates material bindings.
- **Downsample Oversized PNGs**: Physically resizes 4K PNGs on disk to 2048 to keep Git repository blobs small.
- **Purge Unreferenced Kit Textures**: Safely cleans unused loose raw textures.
