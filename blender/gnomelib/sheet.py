"""Combine preview PNGs into one labelled contact sheet (numpy + Blender image IO)."""
import math
import os

import bpy
import numpy as np


def _load(path):
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(img)
    return px


def contact_sheet(folder, out_path, cell=240, cols=8, names=None):
    files = sorted(f for f in os.listdir(folder) if f.endswith('.png'))
    if names:
        files = [f for f in files if os.path.splitext(f)[0] in names]
    if not files:
        return
    rows = math.ceil(len(files) / cols)
    sheet = np.ones((rows * cell, cols * cell, 4), dtype=np.float32)
    sheet[..., :3] = 0.18
    for i, f in enumerate(files):
        px = _load(os.path.join(folder, f))
        h, w = px.shape[:2]
        # nearest-neighbour downscale to the cell
        ys = (np.arange(cell) * h / cell).astype(int)
        xs = (np.arange(cell) * w / cell).astype(int)
        small = px[ys][:, xs]
        r, c = divmod(i, cols)
        # blender pixel rows start at the bottom
        y0 = (rows - 1 - r) * cell
        sheet[y0:y0 + cell, c * cell:(c + 1) * cell] = small
    img = bpy.data.images.new('sheet', cols * cell, rows * cell, alpha=True)
    img.pixels = sheet.ravel()
    img.filepath_raw = out_path
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)
    print('sheet:', out_path, len(files), 'models')
