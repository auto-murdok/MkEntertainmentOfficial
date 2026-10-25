# Two-player proof process

How a two-player shooting proof is produced for MkEntertainmentOfficial,
and the rules it runs under. Implemented by `tools/two-player-proof/run-proof.ps1`
(live copy: `C:\Users\ljtinitanao\run-proof.ps1`), which drives
`run-test.ps1` (the scenario runner) and OBS Studio.

## The away rule

A proof run takes over the screen: two game windows side by side plus an
OBS recording. **It only runs when the user is not at the computer.**

- `run-proof.ps1` first measures keyboard/mouse idle time
  (`GetLastInputInfo`). If the user has been active within the last
  **10 minutes** (the `-MinIdleMinutes` default), the run is *deferred*:
  it prints `PROOF DEFERRED`, exits with code 10, and launches nothing.
- No step of the process may bypass the guard: no placing windows, no
  launching builds, no OBS while the user is active.

## What a proof run does

1. Away guard (above).
2. Launches two standalone builds of the harness branch
   (`test/two-player-harness`) on one PC: the host plays the strafing
   target, the client plays the shooter. The shooter must be the remote
   client — in host mode the host's own shots never cross the network,
   so a host shooter would prove nothing.
3. Both players are driven by `TwoPlayerTestHarness` through the
   production input surface only (`setMovementInput`, `setLookInput`,
   `setIsAiming`, `HandleShoot`, `HandleReload`). At takeover the
   harness disables the local `CharacterBrain`, which otherwise polls
   its (unpressed) input actions every frame and overwrites the bot —
   that fight is why early footage showed the shooter firing from a
   relaxed stance instead of holding the aim. With the brain disabled
   the shooter holds the aim from acquisition to the kill.
4. OBS Studio Game Capture records both windows (profile/collection
   `TPTest`, 1920×540 canvas) into one side-by-side video. GDI capture
   (`ffmpeg gdigrab`) is not usable on this PC — it delivered ~1.5 real
   fps while the games presented at 60 fps, under every graphics API
   and window style tried; neither installed ffmpeg build ships
   `ddagrab`. Game Capture hooks each process directly and is the
   supported route.
5. The runner verifies the video is fresh and non-trivial, extracts the
   facts from the peer logs, and archives a dated proof folder.

## The proof folder

`C:\Users\ljtinitanao\tp-proofs\<yyyyMMdd-HHmmss>-<scenario>\`

- `side-by-side.mp4` — host view (left) and client view (right),
  evidence HUDs burned in by the harness.
- `host.log`, `client.log` — the two peers' player logs.
- `summary.txt` — date, scenario, verdict, shots fired, reloads,
  target-died flag, exception count, idle time before the run.

## Pass criteria (clean scenario)

- The client logs `TARGET DIED` (the server-authoritative kill landed).
- Zero exceptions across both logs.
- The host's HUD verdicts accept the honest shots (rejections, when the
  harness intentionally misbehaves, are a finding, not a pass).
- The shooter visibly holds the aim pose during the engagement, in both
  views (owner rig + replicated remote aim visuals).

## Invoking it

- Scheduled task **`RunProof`** on the gaming PC runs
  `run-proof.ps1` and logs to `C:\Users\ljtinitanao\run-proof.log`.
- Or ask Muse to run a proof; Muse runs the same guarded task and
  reports the summary — it does not run proofs while the user is at the
  computer, and says so when a run is deferred.
