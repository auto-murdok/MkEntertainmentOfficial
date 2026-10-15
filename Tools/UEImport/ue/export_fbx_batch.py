# =============================================================================
# UEI - batch mesh exporter (many folders, one commandlet boot, one manifest)
# -----------------------------------------------------------------------------
# Runs inside the UnrealEditor-Cmd.exe pythonscript commandlet via the
# bootstrap in Run-ExportBatch.ps1 (which sets the env vars below and execs
# this file, so __file__ is NOT available here).
#
# Configuration (environment variables):
#   UEI_ASSET_PATHS  Semicolon-separated /Game/... folder paths, each holding
#                    StaticMesh assets (non-recursive, same as the single-shot
#                    exporter). Required.
#   UEI_GENERIC_PY   Absolute path to ue/export_level_actors.py's sibling
#                    export_fbx_and_map.py (exec'd once per folder). Required.
#   UEI_OUT_DIR      Output directory (shared manifest accumulates across
#                    folders). Required.
#   UEI_FILTER       Optional name filter applied to every folder.
#
# Completion marker: UEI BATCH DONE folders=<ok>/<n> -> <out>
# Per-folder failures log UEI EXPORT FAILED but do not stop the batch.
# =============================================================================

import unreal
import os


def _fail(reason):
    unreal.log('UEI EXPORT FAILED: %s' % reason)


paths = [p for p in os.environ.get('UEI_ASSET_PATHS', '').split(';') if p.strip()]
generic = os.environ.get('UEI_GENERIC_PY', '')
out_dir = os.environ.get('UEI_OUT_DIR', '')
name_filter = os.environ.get('UEI_FILTER', '')

if not paths or not generic or not out_dir:
    _fail('UEI_ASSET_PATHS, UEI_GENERIC_PY and UEI_OUT_DIR must be set')
else:
    try:
        src = open(generic, encoding='utf-8').read()
    except Exception as ex:
        _fail('cannot read generic exporter %s : %s' % (generic, ex))
        src = None
    if src is not None:
        os.environ['UEI_OUT_DIR'] = out_dir
        os.environ['UEI_FILTER'] = name_filter
        done = 0
        for p in paths:
            os.environ['UEI_ASSET_PATH'] = p
            try:
                exec(compile(src, generic, 'exec'))
                done += 1
            except Exception as ex:
                _fail('folder %s : %s' % (p, ex))
        unreal.log('UEI BATCH DONE folders=%d/%d -> %s' % (done, len(paths), out_dir))
