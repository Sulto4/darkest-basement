#!/usr/bin/env python3
"""Asamblează clipurile sloturilor de gameplay din bucăți de clipuri brute (out/footage/raw/<id>.mp4, tăiate din cadrele QA de
tools/capture.sh) și, unde nu există un moment live, din capturi statice cu Ken Burns. Specificația: video/footage.json.
Fiecare slot iese cu EXACT numărul de cadre din out/segments.json → out/footage/<slot>.mp4 (mut, 1920×1080, 30 fps).
Rulează pe Forge, unde sunt clipurile brute, prin `bash tools/capture.sh slots [slot …]` (lock + plafon), care cheamă:
  python3 tools/slots.py [slot …]"""
import json, os, subprocess, sys, tempfile

V = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
spec = json.load(open(os.path.join(V, 'footage.json')))
segs = {s['id']: s for s in json.load(open(os.path.join(V, 'out/segments.json')))['segments']}
assets = json.load(open(os.path.join(V, 'out/assets/index.json')))
X264 = ['-c:v', 'libx264', '-preset', 'medium', '-crf', '16', '-pix_fmt', 'yuv420p', '-r', '30', '-an']
want = sys.argv[1:] or [k for k in spec if not k.startswith('_')]


def raw(name, tmp):
    """Clipul brut; „a+b” = clipuri cu cadre consecutive (ferestre lipite), unite fără recodare."""
    parts = [os.path.join(V, 'out/footage/raw', c + '.mp4') for c in name.split('+')]
    if len(parts) == 1: return parts[0]
    lst = os.path.join(tmp, name.replace('+', '_') + '.txt'); open(lst, 'w').write(''.join(f"file '{p}'\n" for p in parts))
    out = os.path.join(tmp, name.replace('+', '_') + '.mp4')
    if not os.path.exists(out): subprocess.run(['ffmpeg', '-y', '-v', 'error', '-f', 'concat', '-safe', '0', '-i', lst, '-c', 'copy', out], check=True)
    return out


def zoom(p, n):
    """Ken Burns opțional (z = [zoom la început, zoom la sfârșit], f = punctul spre care merge, în fracții din cadru).
    Mărim întâi la 4K, ca zoom-ul lent să nu sară din pixel în pixel."""
    if 'z' not in p: return ''
    z0, z1 = p['z']; fx, fy = p.get('f', [0.5, 0.5])
    z = f"{z0}+({z1}-{z0})*on/{max(1, n - 1)}"
    return f",scale=3840:2160:flags=lanczos,zoompan=z='{z}':x='(iw-iw/zoom)*{fx}':y='(ih-ih/zoom)*{fy}':d=1:s=1920x1080:fps=30"


def piece(p, n, dst, tmp):
    """O bucată de n cadre: dintr-un clip brut (`clip`, de la secunda `from`, opțional încetinit cu `speed` < 1), dintr-un cadru
    al unui clip ținut pe ecran (`frame` + `at`) sau dintr-o captură statică din assets (`still`). Toate primesc opțional z/f.
    Timpii sunt în secunde de la primul cadru al clipului brut."""
    if 'frame' in p:  # un cadru din clip, ținut pe ecran (panoul hărții stă în joc doar o secundă)
        png = os.path.join(tmp, f"hold{abs(hash((p['frame'], p['at'])))}.png")
        subprocess.run(['ffmpeg', '-y', '-v', 'error', '-i', raw(p['frame'], tmp), '-vf', f"select=eq(n\\,{round(p['at'] * 30)})", '-frames:v', '1', png], check=True)
        p = {**p, 'still_path': png}
    if 'clip' in p:
        f0, sp = round(p.get('from', 0) * 30), p.get('speed', 1)
        # încetinirea (doar pentru meniu: animații de repaus) dublează cadre, nu inventează cadre noi
        vf = f"select=gte(n\\,{f0}),setpts=(PTS-STARTPTS)/{sp},fps=30{zoom(p, n)},tpad=stop_mode=clone:stop={n}"
        subprocess.run(['ffmpeg', '-y', '-v', 'error', '-i', raw(p['clip'], tmp), '-vf', vf, '-frames:v', str(n), *X264, dst], check=True)
    else:
        src = p.get('still_path') or os.path.join(V, assets['shots'][p['still']])
        q = p if 'z' in p else {**p, 'z': [1.0, 1.08]}
        subprocess.run(['ffmpeg', '-y', '-v', 'error', '-loop', '1', '-framerate', '30', '-i', src, '-vf', 'fps=30' + zoom(q, n),
                        '-frames:v', str(n), *X264, dst], check=True)


for slot in want:
    pieces = spec[slot]['pieces']
    total = segs[slot]['f1'] - segs[slot]['f0']
    # duratele bucăților: cele fixe în secunde, ultima ia restul
    fixed = [round(p['dur'] * 30) if 'dur' in p else None for p in pieces]
    rest = total - sum(f for f in fixed if f)
    ns = [f if f is not None else rest for f in fixed]
    if sum(ns) != total or min(ns) <= 0: raise SystemExit(f'{slot}: bucățile dau {ns}, slotul are {total} cadre')
    with tempfile.TemporaryDirectory() as tmp:
        files = []
        for i, (p, n) in enumerate(zip(pieces, ns)):
            f = os.path.join(tmp, f'{i}.mp4'); piece(p, n, f, tmp); files.append(f)
        lst = os.path.join(tmp, 'list.txt'); open(lst, 'w').write(''.join(f"file '{f}'\n" for f in files))
        out = os.path.join(V, 'out/footage', slot + '.mp4')
        subprocess.run(['ffmpeg', '-y', '-v', 'error', '-f', 'concat', '-safe', '0', '-i', lst, '-c', 'copy', out], check=True)
    got = int(subprocess.run(['ffprobe', '-v', 'error', '-count_packets', '-select_streams', 'v:0', '-show_entries', 'stream=nb_read_packets', '-of', 'csv=p=0', out], capture_output=True, text=True).stdout)
    print(f'{slot}: {got}/{total} cadre din {len(pieces)} bucăți: ' + ' | '.join(p.get('clip') or ('cadru ' + p['frame'] if 'frame' in p else 'captură ' + p['still']) for p in pieces))
    if got != total: raise SystemExit(f'{slot}: număr greșit de cadre')
