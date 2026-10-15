"""Corectează timpii cuvintelor din voice/<id>.lines.json după energia reală a fișierelor:
alinierea ElevenLabs atribuie pauza dinaintea unei fraze primului ei cuvânt (start prea devreme cu 0,2–0,5 s).
Pentru fiecare cuvânt, startul devine primul cadru audibil din [start, end); speechStart se recalculează.
python3 tools/vo_onsets.py <id> [<id> ...] [--dry]"""
import json, subprocess, sys, numpy as np
SR = 16000
def env(path):
    raw = subprocess.run(['ffmpeg', '-v', 'error', '-i', path, '-f', 'f32le', '-ac', '1', '-ar', str(SR), '-'], capture_output=True, check=True).stdout
    x = np.frombuffer(raw, np.float32); hop = SR // 100
    e = np.sqrt(np.convolve(x * x, np.ones(hop) / hop, 'same'))[::hop]  # RMS la 10 ms
    return 20 * np.log10(e + 1e-9)
dry = '--dry' in sys.argv
for rid in [a for a in sys.argv[1:] if not a.startswith('--')]:
    f = f'voice/{rid}.lines.json'; d = json.load(open(f)); moved = []
    for l in d['lines']:
        e = env(l['src']); thr = e.max() - 32
        for w in l['words']:
            i0, i1 = int(w['start'] * 100), max(int(w['start'] * 100) + 1, int(w['end'] * 100) - 3)
            seg = e[i0:i1]
            if len(seg) and seg[0] < thr:  # începe în liniște
                k = int(np.argmax(seg >= thr)) if (seg >= thr).any() else None
                if k:
                    ns = round((i0 + k) / 100 - 0.02, 3)
                    if ns - w['start'] > 0.06: moved.append((l['n'], w['w'], round(ns - w['start'], 2))); w['start'] = ns
        if l['words']: l['speechStart'] = l['words'][0]['start']
    print(rid, f'{len(moved)} cuvinte mutate:', ', '.join(f'{n}:{w}+{dt}s' for n, w, dt in moved))
    if not dry: json.dump(d, open(f, 'w'), ensure_ascii=False, indent=1)
