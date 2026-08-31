# Minimap — Room-by-Room Dynamic Map

Gold-standard (2025-26 Unity 6/URP): **three layers separate** —
room graph metadata → presentation → visibility mask. Reveal via explicit
`RevealRoom`/`RevealRoomAndNeighbors` from trigger/poll, not pixels.
Replicate compact `roomId` bitsets per peer, never the texture.

## Sources (≤1 year)

| # | Source | Date | Technique |
|---|---|---|---|
| 1 | Unity Manual — Render a camera view to a Render Texture | 2026-08-26 | `RenderTexture` + `Camera.targetTexture` → `RawImage`, orthographic top-down |
| 2 | Unity Manual — Multiple cameras in URP | 2026-08-26 | URP stacking / RT output, per-camera culling/post |
| 3 | Edgar — (PRO) Minimap (v2.0.10) | ©2026 | 512² RT + simplified tilemap layer + child icons |
| 4 | Edgar — (PRO) Fog of War (v2.0.10) | ©2026 | URP `RendererFeature` + fog texture (1px/tile) + `RevealRoom` / `RevealRoomAndNeighbors` |
| 5 | Unity Manual — AI Navigation (pack 2.0.14) | 2026-08-26 | Runtime NavMesh as connectivity validator |

Context7 URP `SingleCameraRequest` / `targetTexture` / `UniversalAdditionalCameraData`
validated the RT path; the **procedural UI path** was chosen for this project
(hand-built hospital, not generated) to avoid a live 3D capture cost
(Unity warns each active camera runs full loop).

## What ships in this repo

**Scene hook:** `NetworkedCombatArena.unity` (and `ExpandedCombatArena`) —
21 `Floor_*` rooms (`Floor_Atrium` … `Floor_WestCorridor`) scanned at runtime.
No pre-placed minimap camera/RT/prefab; map is 100% code.

| Piece | Location | Notes |
|---|---|---|
| `MapRoom` | `Assets/_Game/Scripts/Composition/Map/MapRoom.cs:4` | Pure data: `id`/`displayName`/`worldBounds`/`neighbors` |
| `MinimapDiscovery` | `.../Map/MinimapDiscovery.cs:10` | Scans `Floor_*` via `Renderer`→`Collider`→10×10 fallback, computes `MapExtents`, naive neighbor graph (expanded bounds intersect, 3 m). Poll-based reveal (`RevealPollInterval 0.15 s`, XZ `Bounds.Contains` +1.5 m). `Reveal`/`RevealWithNeighbors`/`RevealAll`/`ResetDiscovery`, `OnRoomRevealed` event. Local per-peer (owned `CharacterBrain` only via `NetworkManager.IsOwner` split). |
| `MinimapView` | `.../Map/MinimapView.cs:14` | Procedural `ScreenSpaceOverlay` `Canvas` (sorting 120, `CanvasScaler` 1920×1080) like `PlayerHud`. Top-left `260²` (expanded `560²` centered on `M`). `WorldToMapRect/Pos` projects XZ extents → map area, 8 px min so corridors stay visible. Circular dots (`GetCircleSprite` 64×64 procedural like `PlayerHud` vignette), local green + pooled remote red, yaw-rotated. Counter `X/21 ROOMS`, title `MAP [M]`. `Shift+R` → `RevealAll` (debug). Empty-scene guard hides panel. |
| Composition root | `PlayerSpawner.cs:43` | `Awake` adds `MinimapDiscovery`+`MinimapView` **before** networked early-out so both SP and NGO arenas get one map. |
| Asmdef | `Game.Composition.asmdef:5` | Added `Unity.TextMeshPro`/`UnityEngine.UI` |

## Verified live (unity-cli `eval_file`, no YAML edits)

```
MinimapDiscovery: rooms=21 discovered=2 (Floor_Atrium+Floor_PlayerStart)
  [0] Floor_AmbulanceBay  (-22,-12) 12×8 …  [20] Floor_WestCorridor (-14.5,-4) 3×60
Neighbour fan-out: Atrium 8, WestCorridor 10
Reveal("Floor_SecurityOffice") → now 3, RevealWithNeighbors(Atrium) → now 12
MinimapView: canvas=True, dots tracked, ToggleExpanded() centers 560²
```

Screenshots: compact top-left + expanded centered; `Shift+R` reveals all 21.

## Networking

Map is **local per-peer** (each `NetworkVariable` would need a `NetworkObject`; the
map lives on the `PlayerSpawner` GO which is not networked). For shared-map,
add a `NetworkBehaviour` on the player prefab with a `NetworkVariable<int>`
bitmask (21 rooms fits 32 bits) and server-authoritative `RevealServerRpc`.

## Next milestones (deliberately not networked yet)

- URP `RendererFeature` world fog (Edgar-style tile mask) if world fog needed.
- Server-shared bitmask replication.
- Explicit door/corridor graph instead of naive bounds intersect.

