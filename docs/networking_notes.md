# Networking Notes — Netcode for GameObjects (milestones 1–2: session, player spawn & position sync)

Setup and verified behaviors for the networked arena (`NetworkedCombatArena`),
using NGO **2.13.2** + Unity Transport **2.6.0**. Gold-standard pattern
(Unity NGO client-server quickstart / Boss Room): server-authoritative
replication via root `NetworkObject` + `NetworkTransform`; host = server +
first client; start sessions from code via `NetworkManager.Singleton`.

## What is in place

| Piece | Location | Notes |
|---|---|---|
| `NetworkedCombatArena.unity` | `Assets/_Game/Scenes/Arenas/` | Copy of `ExpandedCombatArena`; the networked playground. Both are in EditorBuildSettings (the old `ZombieCombatArena` and the one-shot `ExpandedArenaGenerator` were removed). |
| `NetworkManager` GO | networked scene | `NetworkManager` + `UnityTransport` (127.0.0.1:7777 default). **`NetworkConfig.NetworkTransport` must reference the transport** — script-added components are NOT auto-wired, and `StartHost` without it errors + NREs. `NetworkConfig.PlayerPrefab` = `FemaleCharacter.prefab` → NGO auto-spawns it on every peer. |
| `NetworkPrefabs_Arena.asset` | `Assets/_Game/Data/Network/` | `NetworkPrefabsList` registering `FemaleCharacter.prefab`. Assigned into `NetworkConfig.Prefabs.NetworkPrefabsLists` (NGO 2.13 renamed the single-list field). |
| `NetworkObject` + `NetworkTransform` | `FemaleCharacter.prefab` root | **Owner-authoritative** transform replication (`AuthorityMode = Owner`): each peer commits its own player's pose — required for client movement (see lesson 8). Dormant while no `NetworkManager` runs — the single-player arena is unaffected. |
| `NetworkAnimator` | `FemaleCharacter.prefab` root | **Owner-authoritative** animation sync (`AuthorityMode = Owner`, Animator assigned). Replicates all Animator params/states continuously; late joiners get the full current state on spawn. Triggers are the exception — see lesson 9. |
| `NetworkedPlayerComposition` | `Scripts/Composition/`, on the player prefab | `NetworkBehaviour`: the **owner** composes the local rig (input handler, core components/UI, aim target, cameras, HUDs) in `OnNetworkSpawn` via the shared `PlayerRigging` helper; remote players do nothing. |
| `NetworkArenaBootstrap` | `Scripts/Composition/`, on the NetworkManager GO | Auto-`StartHost()`; starts a **client** instead when launched with `-mlclient`/`-client` (`Environment.GetCommandLineArgs`). |
| `ClientLaunchRedirect` | MainMenu scene | Jumps straight to `NetworkedCombatArena` on `-mlclient` launches (player builds boot scene 0). |
| `PauseMenuController` | attached by `PlayerSpawner` in both arenas | Esc / gamepad-Start system menu (RESUME / QUIT TO MENU). Quit shuts the NGO session down (`NetworkManager.Shutdown()`), then reloads `MainMenu` — disconnect for clients, stop-hosting for the host, plain exit in the single-player arena. Not a pause: the simulation keeps running (networked reality). Input polled off `Keyboard.current`/`Gamepad.current` — UI plumbing, deliberately outside the InputHandler subject. |
| `Game.Composition.asmdef` / `Game.Characters.asmdef` | — | Reference `Unity.Netcode.Runtime`. |

## Hard-won lessons (verified in Play Mode via unity-cli)

1. **The transport reference is not auto-wired.** Adding `UnityTransport` via
   script leaves `NetworkConfig.NetworkTransport` null →
   `[Netcode] [Initialize] No transport has been selected!` + NRE in
   `StartHost`. Assign it (serialized property `NetworkConfig.NetworkTransport`).
2. **NGO auto player spawn beats manual spawn.** Assigning
   `NetworkConfig.PlayerPrefab` makes NGO spawn the prefab on host and clients
   (no same-frame spawn race). Spawning as player object manually in the same
   frame `StartHost` returns silently drops the player-object registration
   (`IsPlayerObject=false`) — kept here only as a fallback; the shipped flow
   is auto-spawn + owner composition in `OnNetworkSpawn`.
3. **Remote player objects have no local input subject.** `CharacterBrain.Start`
   asserted `_playerInput` and threw on the *other* peer's player object —
   remote networked players must early-out of local input wiring
   (`NetworkObject.IsSpawned && !IsOwner`).
8. **The client "can't move" with a server-authoritative `NetworkTransform`.**
   Symptom: the host moves fine, the client's player does nothing (local root
   motion is overwritten every network tick by the server's stale pose).
   Root cause: NGO's `NetworkTransform` defaults to `AuthorityMode = Server` —
   correct for server-simulated NPCs, wrong for player-controlled objects.
   Fix: on the player prefab set `AuthorityMode = Owner` (serialized property
   `AuthorityMode`, enum 0 = Server / 1 = Owner). Result: each peer commits
   its own player's pose (`CanCommitToTransform = IsOwner`) and every other
   peer applies the replicated pose. Keep server authority for anything the
   server simulates (future zombies), owner authority for anything a peer
   controls. Verified live: host view shows `serverAuth=False`,
   host player `canCommit=True`, remote (client) player `canCommit=False`.
9. **Animation sync gold standard: `NetworkAnimator` + root-motion rule.**
   - `NetworkAnimator` on the player prefab with `AuthorityMode = Owner`
     (matching the owner-auth transform): all Animator parameters and states
     replicate continuously, and late joiners receive the full current state
     on spawn (`OnSynchronize`). `SetBool`/`SetFloat`/`SetInteger` need **no**
     code changes — the authority-side values are picked up automatically.
   - **Triggers are the exception:** `Animator.SetTrigger` never replicates —
     trigger-capable code must call `NetworkAnimator.SetTrigger`, and that is
     an error on non-owners. `CharacterLocomotion.SetAnimatorTrigger(hash)`
     routes accordingly (networked owner → NetworkAnimator, otherwise the raw
     animator); all player trigger call sites go through it (currently only
     `TakeBite`). Zombies are host-local for now; when networked they need
     the same routing under **server** authority.
   - **Root motion must apply only on the owner.** Remote copies play the
     replicated animation state for the visuals, but their pose comes from
     the `NetworkTransform` — leaving `applyRootMotion = true` on a remote
     copy double-applies motion and drifts. `NetworkedPlayerComposition`
     sets `animator.applyRootMotion = IsOwner` on spawn.
   - **Remote copies must rebuild the rig graph.** The rig constraints spawn
     with the prefab's NULL scene refs, so remote rigged aim would track
     nothing. `NetworkedPlayerComposition.BuildRemoteRig()` (idempotent,
     called on every non-owner `OnNetworkSpawn` — host and clients alike)
     wires all `MultiAimConstraint`s to a local forward-mounted
     `RemoteAimTarget` (chest height, 8 m ahead — it rotates with the
     replicated transform, so remote aim tracks where the character faces)
     and re-runs `rigBuilder.Clear()+Build()`. Regression-tested in
     `NetworkedPlayerPrefabTests.BuildRemoteRig_WiresAllConstraintSourcesToALocalTarget`.
     Known approximation: the remote aim pose tracks the character's facing,
     not the owner's exact crosshair — if exact crosshair direction is ever
     needed, replicate the aim point with a `NetworkVariable<Vector3>` and
     point `RemoteAimTarget` at it.
   - **Remote copies run a reduced runtime (anti-pattern guard):** the
     gameplay FSM (`CharacterLocomotion`) is disabled on non-owners — its
     idle-state visual writers damp the replicated aim layer/rig weights back
     down, which is why remote aim died after ~1 s before this fix. Remote
     visuals are owned by NetworkAnimator (states/params) + the
     `NetworkVariable<bool> _isAiming` mirror (owner writes via
     `locomotion.isAiming`, remote applies the same damped layer/rig writers
     as `CharacterAimState`, including the replicated `isReloading` param).
     Verified live: aim holds indefinitely on the non-owner view (user-
     confirmed) — rig weight rises 0→1 and stays.
   - Anti-pattern audit of the runtime loop: pre-hashed animator params
     (`AnimatorUtils.IsReloadingHash`), components cached at spawn (no
     per-frame `GetComponent`), allocation-free per-frame damp writers
     (mirrors the FSM's own writers), NetworkVariable written only on change.
   - Verified live: both peers see the other player run with the correct
     animation; structure probe: `NA.AuthorityMode=Owner`, animator wired,
     remote copy `applyRootMotion=False`.
4. **NGO 2.13 prefab config surface:** `NetworkPrefabsList` uses
   `PrefabList`/`Add()` (no `List` field), and lives at serialized path
   `NetworkConfig.Prefabs.NetworkPrefabsLists` (an array of list assets).
5. **Scene files are binary-serialized** in this project — never grep/patch
   `.unity` files; probe and edit through `eval_file` +
   `SerializedObject`/`PrefabUtility` only.
6. **Player builds:** URP Compatibility Mode must stay off (deprecated in
   Unity 6.3 — build fails). Toggle lives in
   `UniversalRenderPipelineGlobalSettings.asset` → managed reference
   `RenderGraphSettings.m_EnableRenderCompatibilityMode` (serialized path
   `m_Settings.m_SettingsList.m_List`, not a plain field). Test assemblies
   must stay out of the build: the PlayMode asmdef carries a
   `UNITY_INCLUDE_TESTS` define constraint (compiles in the Editor, skipped
   by players). Marking it Editor-only instead corrupts the EditMode test
   run, and no constraint at all breaks the player compile
   (`UnityTest` unresolved — UTF player support is off by default).
7. **unity-cli build `--outputPath` is used verbatim as the exe path** — pass
   `Builds/NetworkClient.exe` (with extension), or the player lands
   extensionless and `CreateProcess` can never launch it (it silently appends
   `.exe` to non-.exe paths).

## Tooling / session learnings (milestone 2)

- **Rebuild the client after every script change before re-verifying.** The
  built player does not see editor recompiles; a session against a stale
  client "passes" while validating old code (the remote-subject assertion was
  only truly fixed in the client after a rebuild).
- **The client is a black box — its log is your eyes:**
  `%USERPROFILE%\AppData\LocalLow\<Company>\<Product>\Player.log`. Bootstrap,
  composition and netcode `Debug.Log`s all land there; assertion/exception
  lines there are the fastest signal that the client-side composition broke.
- **A build leaves a different scene open in the editor.** Building loads
  each build-settings scene ("Load scene (and close previous ones)") and the
  last one stays open — re-open `NetworkedCombatArena` before entering play
  mode after a build, or the host session starts in the wrong scene
  (`NetworkManager.Singleton` is null in `MainMenu`).
- **Entering play mode after a recompile is safe, but recompile during play
  mode exits play** (domain reload kills the session) — stop play first when
  iterating on scripts.
- **The Unity Console accumulates old failures.** Errors from crashed/aborted
  sessions persist with their timestamps — `clear_console` before every
  verification pass and always check the entry timestamp before chasing an
  "error".
- **Editor wedged = restart, don't debug.** Main-thread unity-cli commands
  timing out while the process idles at 0% CPU (typical after a failed build
  or a modal) → `Stop-Process -Id <pid> -Force`, `unity open . --args
  "-automated"`, wait for `unity status` → ready (rule codified in
  AGENTS.md).
- **NGO spawn ordering is safe for composition:** for NGO-spawned objects the
  sequence is `Awake`/`OnEnable` → `OnNetworkSpawn` → `Start`, so wiring the
  input subject in `OnNetworkSpawn` lands before `CharacterBrain.Start`
  resolves it.
- **Host connection timing:** the host's own client id is in
  `ConnectedClientsIds` immediately after `StartHost`, but approval completes
  a few frames later (`IsConnectedClient`). Anything spawn-related must wait
  for it.
- **`NetworkTransform` + root motion just work:** the character is
  root-motion-driven; the server-authoritative root-transform sync streams
  animation-driven movement with no extra code.
- **`ZombieSpawner` guard pattern:** `NetworkManager.Singleton` is `null` in
  single-player scenes and tests, so `if (nm != null && !nm.IsServer) return;`
  is safe everywhere without touching the single-player path.

## Command cheat-sheet (multiplayer build/test loop)

All long operations are async — poll `*_status` in ≤60 s increments
(AGENTS.md responsiveness rules). Everything below runs from the project root.

```bash
# ── Script recompile (after any code change; poll until completed/failed) ──
unity command recompile
unity command recompile_status

# ── Session roles ──
#   Menu buttons: HOST GAME / JOIN GAME (localhost 127.0.0.1:7777)
#   Command line: no args = host · -mlclient / -client = join
#   (menu choice wins over the command line; Auto = command line)

# ── Host in the editor ──
unity command open_scene --path "Assets/_Game/Scenes/Arenas/NetworkedCombatArena.unity"
unity command editor_play                    # auto-hosts (or menu → HOST GAME)
unity command editor_stop

# ── Build the standalone client (player builds are async) ──
unity command build --target StandaloneWindows64 --outputPath "Builds/NetworkClient.exe" --scenes "Assets/_Game/Scenes/Arenas/NetworkedCombatArena.unity" --confirm
unity command build_status                   # poll until status=completed

# ── Run the client (joins localhost; shows the menu → JOIN also works) ──
Start-Process -FilePath "Builds\NetworkClient.exe" -ArgumentList "-mlclient", "-screen-width", "960", "-screen-height", "540"
Stop-Process -Name "NetworkClient" -Force    # cleanup

# ── Client-side logs (the client is a black box otherwise) ──
Get-Content "$env:USERPROFILE\AppData\LocalLow\DefaultCompany\Official\Player.log" -Tail 40

# ── Test suite (keep green after every behaviour change) ──
unity command run_tests --mode editmode --async_tests
unity command run_tests --mode playmode --async_tests
unity command test_status                    # poll until status=completed

# ── Live probes during a session (eval_file, never inline eval) ──
# ConnectedClients count, spawned player NetworkObjects:
#   NetworkManager.Singleton.ConnectedClientsIds.Count
#   FindObjectsByType<NetworkObject>() → name/OwnerClientId/IsSpawned/transform.position
# Editor console errors:
unity command console --level error --tail 5
```

Typical full loop: `recompile` → build client (poll) → editor `editor_play`
(host) → launch `NetworkClient.exe -mlclient` → probe host (`ConnectedClients
= 2`, two player NetworkObjects) → check client `Player.log` → `editor_stop`
→ kill client → run both test suites.

## Two-instance verification (host + built client)

1. Editor: open `NetworkedCombatArena`, play → auto-host.
2. `Builds/NetworkClient.exe -mlclient` → boots into the networked scene and
   connects to 127.0.0.1:7777.
3. Verified: host sees 2 connected clients and two `FemaleCharacter(Clone)`
   NetworkObjects (owner 0 = host, owner 1 = client); client log shows
   `Session started as client` + `Local rig composed for client player object`;
   moving the host player streams its position to the client; game-view
   screenshot shows both characters.
4. Movement authority probe (after lesson 8's fix):
   `nt.IsServerAuthoritative()` is `False` on both players;
   `nt.CanCommitToTransform` is `True` for the local player, `False` for the
   remote one; movement works on **both** peers (verified manually by the
   user on the built client).

## Deliberately NOT networked yet (milestone order)

- Zombies, bite/hand-attack interactions, bullets, ammo pickups — host-only
  (`ZombieSpawner` early-outs on networked clients). When zombies get
  networked, their `NetworkTransform` must stay **server-authoritative**
  (default) — only player-controlled objects use owner authority.
- Client death/game-flow (GameStateManager channels are wired server-side
  only in networked scenes); remote players currently keep their default layer
  (the local-player layer assignment in `OnActorStart` runs on every peer and
  should eventually be owner-only).
- Gameplay actions (shoot/bite/pickup) are not networked yet — movement
  authority is resolved (owner-committed `NetworkTransform`), but combat and
  item logic still run host-locally.
