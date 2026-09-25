#!/usr/bin/env python3
"""Build every model: Blender scene -> GMDL (runtime) + FBX + .blend + preview PNG.

Usage:
  python3 blender/build.py                # all models
  python3 blender/build.py gnome oldMan   # only these
  python3 blender/build.py --no-render    # skip previews (fast)
  python3 blender/build.py --sheet        # also rebuild docs/models/sheet.png
"""
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'models'))

import bpy  # noqa: E402

from gnomelib import core  # noqa: E402
from gnomelib.export_gmdl import export_fbx, export_gmdl, export_palette  # noqa: E402
from gnomelib.render import render_preview  # noqa: E402

REPO = os.path.abspath(os.path.join(HERE, '..'))
OUT_GMDL = os.path.join(REPO, 'Assets', 'Resources', 'Models')
OUT_FBX = os.path.join(REPO, 'art', 'fbx')
OUT_BLEND = os.path.join(REPO, 'art', 'blend')
OUT_PREVIEW = os.path.join(REPO, 'docs', 'models')


def all_models():
    import characters
    registry = {}
    registry.update(characters.MODELS)
    for mod in ('furniture', 'items', 'environment'):
        try:
            m = __import__(mod)
            registry.update(m.MODELS)
        except ModuleNotFoundError:
            pass
    return registry


def build_one(name, fn, render=True):
    core.reset_scene()
    t = time.time()
    root = fn()
    obj = root.realize()
    size = export_gmdl(obj, os.path.join(OUT_GMDL, name + '.bytes'))
    export_fbx(obj, os.path.join(OUT_FBX, name + '.fbx'))
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT_BLEND, name + '.blend'), compress=True)
    if render:
        render_preview(obj, os.path.join(OUT_PREVIEW, name + '.png'))
    tris = sum(len(o.data.polygons) for o in bpy.data.objects if o.type == 'MESH' and not o.name.startswith('preview'))
    print(f'  {name:18s} {size / 1024:7.1f} KB  faces={tris:5d}  {time.time() - t:5.1f}s', flush=True)


def main():
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    render = '--no-render' not in sys.argv
    for d in (OUT_GMDL, OUT_FBX, OUT_BLEND, OUT_PREVIEW):
        os.makedirs(d, exist_ok=True)
    reg = all_models()
    names = args or list(reg.keys())
    print(f'Building {len(names)} models')
    for n in names:
        build_one(n, reg[n], render)
    core.PALETTE.save()
    export_palette(os.path.join(OUT_GMDL, 'palette.bytes'))
    if '--sheet' in sys.argv:
        from gnomelib.sheet import contact_sheet
        contact_sheet(OUT_PREVIEW, os.path.join(REPO, 'docs', 'models_sheet.png'))
    print('palette colours:', len(core.PALETTE.colors))


if __name__ == '__main__':
    main()
