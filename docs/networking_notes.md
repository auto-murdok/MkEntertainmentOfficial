# Networking Notes — Netcode for GameObjects (milestone 1: player position sync)

Setup and verified behaviors for the networked arena (`NetworkedCombatArena`),
using NGO **2.13.2** + Unity Transport **2.6.0**. Gold-standard pattern
(Unity NGO client-server quickstart / Boss Room): server-authoritative
replication via root `NetworkObject` + `NetworkTransform`; host = server +
first client; start sessions from code via `NetworkManager.Singleton`.

## What is in place

| Piece | Location | Notes |
|---|---|---|
| `NetworkedCombatArena.unity` | `Assets/_Game/Scenes/Arenas/` | Copy of `ExpandedCombatArena`; the networked playground. Both are in EditorBuildSettings (the old `ZombieCombatArena` and the one-shot `ExpandedArenaGenerator` were removed). |
| `NetworkManager` GO | networked scene | `NetworkManager` + `UnityTransport` (127.0.0.1:7777 default). **`NetworkConfig.NetworkTransport` must reference the transport** — script-added components are NOT auto-wired, and `StartHost` without it errors + NREs. |
| `NetworkPrefabs_Arena.asset` | `Assets/_Game/Data/Network/` | `NetworkPrefabsList` registering `FemaleCharacter.prefab` so clients resolve manually-spawned instances. Assigned into `NetworkConfig.Prefabs.NetworkPrefabsLists` (NGO 2.13 renamed the single-list field). |
| `NetworkObject` + `NetworkTransform` | `FemaleCharacter.prefab` root | Server-authoritative transform replication of the root-motion-driven character. Dormant while no `NetworkManager` runs — the single-player arena is unaffected. |
| `NetworkArenaBootstrap` | `Scripts/Composition/`, on the NetworkManager GO | Auto-`StartHost()` in `Start`, then (coroutine) spawns the locally composed player rig as the host's player object. |
| `Game.Composition.asmdef` | — | References `Unity.Netcode.Runtime`. |

## Hard-won lessons (verified in Play Mode via unity-cli)

1. **The transport reference is not auto-wired.** Adding `UnityTransport` via
   script leaves `NetworkConfig.NetworkTransport` null →
   `[Netcode] [Initialize] No transport has been selected!` + NRE in
   `StartHost`. Assign it (serialized property `NetworkConfig.NetworkTransport`).
2. **Do not spawn a player object in the same frame `StartHost` returns.** The
   host's own client id (0) is already in `ConnectedClientsIds` immediately,
   but the connection is not approved yet — the spawn succeeds while the
   player-object registration silently drops (`IsPlayerObject=false`,
   `SpawnManager.GetPlayerNetworkObject` returns null). Wait for
   `IsConnectedClient` first, then `SpawnAsPlayerObject(LocalClientId)`.
3. **NGO 2.13 prefab config surface:** `NetworkPrefabsList` uses
   `PrefabList`/`Add()` (no `List` field), and lives at serialized path
   `NetworkConfig.Prefabs.NetworkPrefabsLists` (an array of list assets).
4. **Scene files are binary-serialized** in this project — never grep/patch
   `.unity` files; probe and edit through `eval_file` +
   `SerializedObject`/`PrefabUtility` only.

## Verified (host, Play Mode)

- `IsListening=True`, `IsHost/IsServer/IsClient=True`, 1 connected client.
- Player `NetworkObject`: `IsSpawned=True`, `IsOwner=True`,
  `IsPlayerObject=True`; `SpawnManager.GetPlayerNetworkObject(local)` returns
  the character; `NetworkTransform` present (server-authoritative,
  world-space).
- Console clean; full test suite green after the change (86 EditMode + 118
  PlayMode).

## Deliberately NOT networked yet (milestone order)

- Zombies, bite/hand-attack interactions, bullets, ammo pickups — local-only
  on the host for now.
- Client flow: `NetworkedCombatArena` auto-hosts; a client build / second
  editor instance (Multiplayer Play Mode) is the next milestone — the client
  path will need `PlayerSpawner` gating (clients must not locally instantiate
  the player rig; the server-spawned instance should be used instead).
- MainMenu still loads the single-player `ExpandedCombatArena`.
