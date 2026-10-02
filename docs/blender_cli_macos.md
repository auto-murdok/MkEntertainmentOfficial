# Headless Blender CLI on macOS — 3D model polish (FBX) environment

Recorded 2026-10-02 after the initial macOS setup. Follow-up sessions: read this
BEFORE looking for Blender or hand-editing FBX meshes. Companion workflow rules
live in the `unity-fbx-polish` skill (`.agents/skills/unity-fbx-polish/SKILL.md`).

## 1. What is installed on this machine (macOS 26.7, Apple Silicon, Homebrew 7.0.7+)

| Tool | Version | Install route |
|---|---|---|
| Blender | **5.2.2 LTS** (2026-09-15) | `brew install --cask blender` |
| Blender CLI | `/opt/homebrew/bin/blender` | Homebrew ≥7.0 `command_wrapper` artifact of the cask (links into `$(brew --prefix)/bin`); falls back to `/Applications/Blender.app/Contents/MacOS/Blender` |
| ImageMagick | 7.1.2-32 | `brew install imagemagick` |
| trimesh | 4.12.2 (Python 3.9 ceiling) | `python3 -m pip install --user -i https://pypi.org/simple trimesh` |

Do NOT re-download ad-hoc portable Blender builds; the cask is brew-updatable
(`brew upgrade --cask blender`) and its wrapper keeps `blender` on PATH.
`blender@lts` cask = same 5.2.2 and CONFLICTS with `blender` — never install both.

## 2. Probe-first validation (what was actually verified on 5.2.2)

Run: `blender --background --factory-startup --python <probe> -- <sample.fbx> <outdir>`
(sample used: `Assets/ImportedContent/Building_kit/Meshes/SM_ExternalWall_Baseflor_Corner01.fbx`)

- The legacy Python FBX add-on `io_scene_fbx` is **still bundled and factory-enabled**
  in 5.2.2: `bpy.ops.import_scene.fbx` / `bpy.ops.export_scene.fbx` register and
  behave as on 4.x. All five skill helpers ran unmodified.
- FBX ≥5.0's new C++ importer is `bpy.ops.wm.fbx_import` (menu default). Do NOT
  switch helpers to it without re-validating the cm object-space scale
  (`scale=(0.01,0.01,0.01)` on import) and material-slot/UV-layer naming
  (`UVmap_0`, `LightMapUV`) the helpers depend on.
- Export → re-import round-trip preserves objects, materials, UV layers.
- Full dry-run loop passed: `remove_degenerate.py` → `flip_faces.py` →
  `uv_rotate90.py` → `uv_scale.py` → `worldspace_preview.py` (render proof OK).

## 3. Known issues & fixes (so you don't rediscover them)

1. **`worldspace_preview.py` (and any headless render) needs an ABSOLUTE `out.png`
   path.** A relative path fails: `Render error ... cannot save: 'preview.png'`.
2. **`Material.use_nodes` / `World.use_nodes` emit DeprecationWarnings** — they
   still work on 5.2, removal is slated for Blender 6.0. Keep helpers 4.x-compatible
   (the Windows pipeline uses portable Blender 4.2 in `Tools/UEImport/vendor/blender`),
   so leave `use_nodes` in until the Windows vendor is bumped too.
3. **pip is preconfigured to an unreachable corporate mirror**
   (`artifactory.grubhub.com` → connect timeouts). Always pass
   `-i https://pypi.org/simple` for one-off Python packages.
4. **Git push over HTTPS failed** (`could not read Username for 'https://github.com'`)
   until `gh auth setup-git` was run once. gh is authed as `gralmurdok` (SSH+keyring);
   re-run `gh auth setup-git` after any credential reset.
5. The old Linux portable-Blender path (`~/workspace/blender-portable/...4.0.2-linux-x64`)
   is gone; on this machine the CLI is brew's. Denoising stays OFF in QA renders for
   stable proof shots (no longer because 4.0.2 lacked OIDN — 5.2 ships it; the rule stands
   for reproducibility).

## 4. Recipes

```bash
# version smoke test
blender --version

# probe a new/updated Blender before trusting the helpers
blender --background --factory-startup --python probe_blender.py -- \
  Assets/ImportedContent/Building_kit/Meshes/SM_ExternalWall_Baseflor_Corner01.fbx /tmp/mbpro

# one helper (note: absolute paths everywhere in headless mode)
blender --background --python .agents/skills/unity-fbx-polish/bin/worldspace_preview.py -- \
  "$PWD/preview.png" "$PWD/Assets/ImportedContent/Building_kit/Textures/T_Bricks03_B.png" \
  300 4.0 "$PWD/mesh.fbx" 0
```

Probe script kept at: `.agents/skills/unity-fbx-polish/bin/probe_blender.py` (§5).

## 5. Windows side (for context)

`Tools/UEImport/cue4parse/setup-prereqs.ps1` installs portable Blender **4.2 LTS**
into `Tools/UEImport/vendor/blender` (Windows-only zip path; 4.2 went EOL Jul 2026).
Bumping it to 5.2 LTS is a separate, deliberate change — until then helpers must
stay 4.x-compatible (see §3.2).

## 6. Research sources (2026-10-02)

- blender.org/releases — 5.2.2 LTS (2026-09-15), 4.5 LTS supported to Jul 2027
- developer.blender.org/docs/release_notes/5.0/pipeline_io — C++ FBX importer default,
  Python add-on marked legacy (still bundled)
- developer.blender.org/docs/release_notes/5.2/pipeline_io — no FBX removal in 5.2
- Homebrew 7.0.0 release notes (2026-09-13) + docs.brew.sh/Cask-Cookbook —
  `command_wrapper` artifact → `$HOMEBREW_PREFIX/bin/blender`
- formulae.brew.sh/cask/blender — cask at 5.2.2, arm64 dmg, conflicts `blender@lts`
- pypi.org/project/bpy — 5.2.2 wheel exists but pins CPython 3.13 (not used; app CLI chosen)
