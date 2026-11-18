"""IR-09 v02 live bridge for an INTERACTIVE Blender session, so the user can watch the refinement as it happens.

Start:  blender.exe <work .blend> --python Tools/IronRequiem/Blender/live_bridge_v02.py
Every second it looks for Tools/IronRequiem/Blender/Working/protagonist_v02/bridge/request.py (written only by
the coordinator, inside this project), runs it in the GUI session, writes request.log (ok/traceback + printed output)
and renames the request to done-<n>.py as an audit trail. It never saves the file by itself and never touches files
outside Working/protagonist_v02 unless the request script does so explicitly. Remove the timer by closing Blender.
"""
import contextlib
import io
import time
import traceback
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[3]
BRIDGE = ROOT / 'Tools/IronRequiem/Blender/Working/protagonist_v02/bridge'
REQUEST = BRIDGE / 'request.py'
LOG = BRIDGE / 'request.log'
BRIDGE.mkdir(parents=True, exist_ok=True)


def _tick():
    if not REQUEST.exists():
        return 1.0
    code = REQUEST.read_text(encoding='utf-8')
    n = len(list(BRIDGE.glob('done-*.py'))) + 1
    REQUEST.rename(BRIDGE / f'done-{n:03d}.py')
    out = io.StringIO()
    status = 'ok'
    start = time.time()
    try:
        with contextlib.redirect_stdout(out):
            exec(compile(code, f'bridge-request-{n:03d}', 'exec'), {'__name__': '__bridge__', 'bpy': bpy, 'ROOT': ROOT})
    except Exception:
        status = 'error\n' + traceback.format_exc()
    LOG.write_text(f'request {n:03d} {status}\nseconds {time.time() - start:.2f}\n{out.getvalue()}', encoding='utf-8')
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            area.tag_redraw()
    return 0.5


if not bpy.app.timers.is_registered(_tick):
    bpy.app.timers.register(_tick, first_interval=1.0, persistent=True)
print('IRON REQUIEM live bridge ready:', REQUEST)
