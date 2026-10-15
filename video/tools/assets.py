#!/usr/bin/env python3
"""Copiază asset-urile din manifest (video/assets.json) în video/out/assets/ și scrie video/out/assets/index.json.
Capturile 1920×1080 devin JPEG (calitate 92) ca să încarce repede în paginile de randare.
python3 tools/assets.py   (rulează din orice director; idempotent: sare peste fișierele deja copiate)"""
import json, os, shutil, subprocess, sys

V = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))      # video/
R = os.path.dirname(V)                                               # rădăcina repo-ului jocului
OUT = os.path.join(V, 'out', 'assets')
m = json.load(open(os.path.join(V, 'assets.json')))
idx = {'sprites': {}, 'shots': {}, 'music': {}, 'sfx': {}}


def copy(src, dst):
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    if not os.path.exists(dst) or os.path.getmtime(dst) < os.path.getmtime(src):
        shutil.copy2(src, dst)


base = os.path.join(R, m['sprites']['from'])
for d in m['sprites']['dirs']:
    for root, _, files in os.walk(os.path.join(base, d)):
        for f in sorted(files):
            if not f.endswith('.png'): continue
            src = os.path.join(root, f)
            rel = os.path.relpath(src, base)
            key = os.path.splitext(rel)[0].replace(os.sep, '/')
            copy(src, os.path.join(OUT, 'sprites', rel))
            idx['sprites'][key] = 'out/assets/sprites/' + rel.replace(os.sep, '/')
copy(os.path.join(R, m['logo']), os.path.join(OUT, 'logo.png'))
tb = os.path.join(R, m['textures']['from'])
for d in m['textures']['dirs']:
    for f in sorted(os.listdir(os.path.join(tb, d))):
        if not f.endswith('.png'): continue
        copy(os.path.join(tb, d, f), os.path.join(OUT, 'env', d, f))
        idx['sprites'][f'env/{d}/' + f[:-4]] = f'out/assets/env/{d}/{f}'
missing = []
for k, p in m['shots'].items():
    src = os.path.join(R, p); dst = os.path.join(OUT, 'shots', k + '.jpg')
    if not os.path.exists(src): missing.append(p); continue
    if not os.path.exists(dst) or os.path.getmtime(dst) < os.path.getmtime(src):
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        subprocess.run(['ffmpeg', '-y', '-v', 'error', '-i', src, '-q:v', '2', dst], check=True)
    idx['shots'][k] = f'out/assets/shots/{k}.jpg'
for k, p in m['music'].items():
    copy(os.path.join(R, p), os.path.join(OUT, 'music', k + '.ogg')); idx['music'][k] = f'out/assets/music/{k}.ogg'
for s in m['sfx']['ids']:
    copy(os.path.join(R, m['sfx']['from'], s + '.ogg'), os.path.join(OUT, 'sfx', s + '.ogg')); idx['sfx'][s] = f'out/assets/sfx/{s}.ogg'
idx['logo'] = 'out/assets/logo.png'
json.dump(idx, open(os.path.join(OUT, 'index.json'), 'w'), indent=0)
print(f"assets: {len(idx['sprites'])} sprite-uri, {len(idx['shots'])} capturi, {len(idx['music'])} piese, {len(idx['sfx'])} efecte → {OUT}")
if missing: print('LIPSESC:', missing); sys.exit(1)
