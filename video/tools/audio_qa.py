#!/usr/bin/env python3
"""Verificarea obiectivă a sunetului (nimeni nu l-a ascultat încă): scrie video/AUDIO-QA.md.
Intrări: MP4-ul final, stem-urile din out/stems/ (vo.wav, music.wav, sfx.wav; scrise de mix.mjs --stems) și out/timings.json.
  python3 tools/audio_qa.py out/darkest-basement-explainer-v2.mp4
Măsoară: loudness integrat și true peak pe tot filmul (ffmpeg ebur128); loudness pe capitol; vocea față de muzică și de efecte
pe ferestre de 400 ms în care se vorbește; efectele care acoperă vocea; continuitatea muzicii la schimbările de capitol."""
import json, os, re, subprocess, sys
import numpy as np

V = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
mp4 = sys.argv[1]
T = json.load(open(os.path.join(V, 'out/timings.json')))
SR = 8000


def mono(path):  # stream decodat de ffmpeg la 8 kHz mono: puțină memorie pe Mac
    raw = subprocess.run(['ffmpeg', '-v', 'error', '-i', path, '-ac', '1', '-ar', str(SR), '-f', 'f32le', '-'], capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.float32)


def ebur(path, ss=None, to=None):
    args = ['ffmpeg', '-hide_banner', '-nostats']
    if ss is not None: args += ['-ss', f'{ss:.2f}', '-to', f'{to:.2f}']
    args += ['-i', path, '-vn', '-af', 'ebur128=peak=true', '-f', 'null', '-']
    err = subprocess.run(args, capture_output=True, text=True).stderr
    summ = err[err.rfind('Summary:'):]
    I = float(re.search(r'I:\s*(-?[\d.]+) LUFS', summ).group(1))
    lra = float(re.search(r'LRA:\s*(-?[\d.]+) LU', summ).group(1))
    tp = re.search(r'Peak:\s*(-?[\d.]+) dBFS', summ)
    return I, lra, float(tp.group(1)) if tp else None


def rms_db(x):
    return 20 * np.log10(np.sqrt(np.mean(x * x)) + 1e-9)


out = ['# Sunetul: verificare obiectivă (v2)', '',
       'Generat de `tools/audio_qa.py` din MP4-ul final și din stem-urile mixajului (`tools/mix.mjs --stems`). Măsurători, nu ascultare:',
       'ce e marcat **de verificat** merită ascultat de Radu.', '']
I, lra, tp = ebur(mp4)
out += [f'## Întregul film', '', f'- Loudness integrat: **{I:.1f} LUFS** (ținta −14), LRA {lra:.1f} LU, true peak **{tp:.1f} dBTP** (plafon −1).',
        '- Clipping: ' + ('niciunul (true peak sub 0 dBFS).' if tp is not None and tp < 0 else f'**de verificat** (true peak {tp} dBTP).'), '']

vo, mu, fx = (mono(os.path.join(V, 'out/stems', n + '.wav')) for n in ('vo', 'music', 'sfx'))
n = min(len(vo), len(mu), len(fx)); vo, mu, fx = vo[:n], mu[:n], fx[:n]
W = int(0.4 * SR)
rows = []
for i in range(0, n - W, W // 2):
    v = rms_db(vo[i:i + W])
    if v < -40: continue  # fără voce în fereastră
    rows.append((i / SR, v - rms_db(mu[i:i + W]), v - rms_db(fx[i:i + W]), v - rms_db(mu[i:i + W] + fx[i:i + W]), v))
R = np.array(rows)

out += ['## Pe capitole', '', '| capitol | loudness (LUFS) | voce − muzică, mediana (dB) | voce − (muzică+efecte), minim (dB) | ferestre sub 6 dB |', '|---|---|---|---|---|']
flags = []
for s in T['scenes']:
    li, _, _ = ebur(mp4, s['t0'], s['t1'])
    m = (R[:, 0] >= s['t0']) & (R[:, 0] < s['t1'])
    if m.sum() == 0: continue
    vm, vb = np.median(R[m, 1]), R[m, 3].min()
    low = int((R[m, 3] < 6).sum())
    out.append(f'| {s["id"]} {s["title"]} | {li:.1f} | {vm:.1f} | {vb:.1f} | {low} / {int(m.sum())} |')
    if abs(li + 14) > 3: flags.append(f'{s["id"]}: loudness {li:.1f} LUFS, la peste 3 LU de restul filmului')
out += ['', f'Pe tot filmul: vocea e cu **{np.median(R[:, 1]):.1f} dB** peste muzică (mediana) și cu {np.median(R[:, 3]):.1f} dB peste muzică+efecte.', '']

# efectele care acoperă vocea: fereastră cu voce în care efectele sunt la mai puțin de 3 dB sub voce. O fereastră de 400 ms
# poate prinde doar capătul unui cuvânt (vocea slabă, efectul în pauză): o despărțim de cele în care vocea e la nivel de vorbire.
VMED = float(np.median(R[:, 4]))
bury = R[R[:, 2] < 3]
words = bury[bury[:, 4] >= VMED - 10]
out += ['## Efecte peste voce', '']
if len(bury):
    out.append(f'{len(bury)} ferestre de 400 ms (din {len(R)} cu voce) în care un efect e la mai puțin de 3 dB sub voce; în {len(bury) - len(words)} dintre ele '
               f'vocea e cu peste 10 dB sub nivelul ei obișnuit (capăt de cuvânt sau respirație, efectul cade în pauză). '
               + (f'Rămân **{len(words)} de verificat**, cu vocea la nivel de vorbire:' if len(words) else 'Niciuna nu acoperă vorbire la nivel normal.'))
    out.append('')
    for t, _, vf, _, v in bury[:30]:
        sc = next((s for s in T['scenes'] if s['t0'] <= t < s['t1']), None)
        ln = next((l for l in sc['lines'] if l['t0'] - 0.2 <= t <= l['t1'] + 0.2), None) if sc else None
        tag = '**de verificat**' if v >= VMED - 10 else 'în pauză'
        out.append(f'- {int(t // 60)}:{t % 60:04.1f} ({sc["id"] if sc else "?"}), {tag}: efect la {-vf:+.1f} dB față de voce, vocea la {v - VMED:+.0f} dB față de nivelul ei'
                   + (f' — „{ln["text"][:60]}…”' if ln else ''))
    if len(words): flags += [f'efect peste vorbire la {int(t // 60)}:{t % 60:04.1f}' for t in words[:, 0]]
else:
    out.append('Niciun efect nu ajunge la mai puțin de 3 dB sub voce.')
out.append('')

# muzica la schimbările de capitol: nivelul în ferestre de 250 ms, 3 s înainte și 3 s după fiecare graniță
# Muzica e coborâtă sub voce și urcă singură în pauze; între capitole e o pauză de ~3 s fără voce, unde muzica urcă intenționat.
# Pe fiecare graniță: nivelul muzicii sub voce (înainte/după), nivelul ei în pauză (mediana) și cel mai mare vârf scurt din pauză
# față de mediana pauzei: un crossfade prost sau o lovitură din piesă ies peste ea cu mai mult de 6 dB.
B = int(0.25 * SR)
lvl = lambda x, j: rms_db(x[j:j + B])
out += ['## Muzica la schimbările de capitol', '',
        'În pauza dintre capitole (fără voce) muzica urcă intenționat și coboară când revine vocea. „Urcare” = cât e muzica în pauză '
        'față de muzica de sub voce; „vârf scurt” = cea mai puternică fereastră de 250 ms din pauză față de restul pauzei.', '',
        '| graniță | muzica sub voce, înainte (dB) | în pauză (dB) | vârf scurt (dB) | sub voce, după (dB) | verdict |', '|---|---|---|---|---|---|']
for s in T['scenes'][1:]:
    b = int(s['t0'] * SR)
    js = list(range(max(0, b - 5 * SR), min(n - B, b + 5 * SR), B))
    gap = [j for j in js if lvl(vo, j) < -50]
    pre = [lvl(mu, j) for j in js if j < b and lvl(vo, j) > -40]
    post = [lvl(mu, j) for j in js if j > b and lvl(vo, j) > -40]
    g = [lvl(mu, j) for j in gap]
    if not g: continue
    gm, gx = float(np.median(g)), max(g)
    before, after = (float(np.median(pre)) if pre else float('nan')), (float(np.median(post)) if post else float('nan'))
    verdict = 'ok'
    if min(g) < gm - 12: verdict = '**de verificat**: gol în muzică'
    if gx > gm + 6: verdict = f'**de verificat**: vârf scurt de {gx - gm:.0f} dB în pauză'
    out.append(f'| {int(s["t0"] // 60)}:{s["t0"] % 60:04.1f} → {s["id"]} | {before:.1f} | {gm:.1f} | {gx:.1f} | {after:.1f} | {verdict} |')
    if verdict != 'ok': flags.append(f'trecerea spre {s["id"]}: {verdict}')
out.append('')
out += ['## De ascultat', ''] + ([f'- {f}' for f in flags] or ['- Nimic peste praguri.']) + ['']
open(os.path.join(V, 'AUDIO-QA.md'), 'w').write('\n'.join(out))
print('\n'.join(out[:12])); print('...'); print('AUDIO-QA.md scris')
