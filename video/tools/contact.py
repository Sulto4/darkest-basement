#!/usr/bin/env python3
"""Planșa de control din MP4-ul FINAL (nu din capturi): 16 cadre, câte unul-două din fiecare scenă, cu timpul și scena scrise.
python3 tools/contact.py out/darkest-basement-explainer-v1.mp4 out/contact-v1.png   (citește out/timings.json)"""
import json, os, subprocess, sys, tempfile
from PIL import Image, ImageDraw, ImageFont

V = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
mp4, out = sys.argv[1], sys.argv[2]
T = json.load(open(os.path.join(V, 'out/timings.json')))
# momente alese: mijlocul replicilor care arată ideea principală a fiecărei scene (2 pentru scenele lungi)
pick = {'s01': [2, 6], 's02': [7], 's03': [3], 's04': [3, 8], 's05': [4, 9], 's06': [6], 's07': [3], 's08': [6], 's09': [5, 13], 's10': [7], 's11': [4], 's12': [3]}
shots = []
for s in T['scenes']:
    for n in pick.get(s['id'], []):
        l = next(x for x in s['lines'] if x['n'] == n)
        shots.append((s['id'], s['title'], (l['t0'] + l['t1']) / 2))
shots = shots[:16]
font = ImageFont.truetype(os.path.join(V, 'engine/fonts/Silkscreen-Regular.ttf'), 20)
W, H, cols = 480, 270, 4
sheet = Image.new('RGB', (cols * W + (cols + 1) * 8, ((len(shots) + cols - 1) // cols) * (H + 40) + 8), '#140c0a')
d = ImageDraw.Draw(sheet)
with tempfile.TemporaryDirectory() as tmp:
    for i, (sid, title, t) in enumerate(shots):
        f = os.path.join(tmp, f'{i}.png')
        subprocess.run(['ffmpeg', '-v', 'error', '-ss', f'{t:.2f}', '-i', mp4, '-frames:v', '1', '-y', f], check=True)
        im = Image.open(f).convert('RGB').resize((W, H), Image.LANCZOS)
        x, y = 8 + (i % cols) * (W + 8), 8 + (i // cols) * (H + 40)
        sheet.paste(im, (x, y + 32))
        d.text((x, y + 2), f'{int(t // 60)}:{t % 60:04.1f}  {sid}', font=font, fill='#e0b04a')
sheet.save(out)
print(out, len(shots), 'cadre')
