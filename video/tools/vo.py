#!/usr/bin/env python3
"""Vocea explainer-ului, scenă cu scenă (adaptat din voost-motion-reels/tools/vo.py).

Intrare: voice/text/<scena>.txt, o replică pe rând (rândurile goale și cele cu # se ignoră). Textul e cel ROSTIT
(numerele scrise în litere, „C sharp” în loc de „C#”); ce apare pe ecran se scrie separat, în scene.
Ieșire: voice/<scena>/take.mp3 (+ take.alignment.json), voice/<scena>/NN.mp3 (o replică) și voice/<scena>.lines.json:
  {"voice", "model", "lines": [{"n", "text", "src", "dur", "speechStart", "speechEnd", "words": [{w, clean, start, end}]}]}
  (timpi în secunde, relativi la fișierul replicii)

  vo.py s02 --voice <id>                 → o singură înregistrare pe toată scena (ton constant), tăiată în replici
  vo.py s02 --voice <id> --line 4        → regenerează doar replica 4 (cerere separată, cu previous_text/next_text)
  vo.py s02 --voice <id> --line 4 --keep-old  → păstrează varianta veche în voice/_takes/
Cheia ElevenLabs: ~/.secrets/elevenlabs.env (nu se afișează). Costul: ~1 credit / caracter (multilingual_v2).
"""
import argparse, base64, json, os, re, shutil, subprocess, sys, time

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))  # video/
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import el  # noqa: E402

ORDER = [f's{i:02d}' for i in range(1, 13)]


def read_lines(sid):
    p = os.path.join(ROOT, 'voice/text', sid + '.txt')
    return [l.strip() for l in open(p, encoding='utf-8') if l.strip() and not l.lstrip().startswith('#')]


def clean(w):
    return re.sub(r'[^\wăâîșțĂÂÎȘȚ-]', '', w)


def mp3_dur(path):
    out = subprocess.run(['ffprobe', '-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', path], capture_output=True, text=True).stdout
    return round(float(out.strip()), 3)


def neighbours(sid):
    """Textul din jur (finalul scenei anterioare, începutul celei următoare), ca vocea să țină tonul între scene."""
    i = ORDER.index(sid) if sid in ORDER else -1
    prev = ' '.join(read_lines(ORDER[i - 1])[-2:]) if i > 0 and os.path.exists(os.path.join(ROOT, 'voice/text', ORDER[i - 1] + '.txt')) else None
    nxt = ' '.join(read_lines(ORDER[i + 1])[:1]) if 0 <= i < len(ORDER) - 1 and os.path.exists(os.path.join(ROOT, 'voice/text', ORDER[i + 1] + '.txt')) else None
    return prev, nxt


def settings(a):
    return {'stability': a.stability, 'similarity_boost': 0.8, 'style': a.style, 'use_speaker_boost': True, 'speed': a.speed}


def tts(a, text, prev=None, nxt=None):
    body = {'text': text, 'model_id': a.model, 'voice_settings': settings(a), 'language_code': 'ro'}
    if a.model != 'eleven_v3':
        if prev: body['previous_text'] = prev
        if nxt: body['next_text'] = nxt
    if a.seed is not None: body['seed'] = a.seed
    for attempt in range(3):
        try:
            return el.call('POST', f'/v1/text-to-speech/{a.voice}/with-timestamps', body, query={'output_format': 'mp3_44100_192'}, timeout=600)
        except SystemExit as e:  # el.call iese cu mesaj la HTTP != 2xx
            if attempt == 2 or '429' not in str(e): raise
            time.sleep(20)


def cut(src, c0, c1, dst):
    # aresample: ffmpeg 8 + libmp3lame dă „inadequate AVFrame plane padding” la tăiere directă
    subprocess.run(['ffmpeg', '-y', '-v', 'error', '-i', src, '-ss', f'{c0:.3f}', '-to', f'{c1:.3f}', '-af', 'aresample=44100',
                    '-c:a', 'libmp3lame', '-b:a', '192k', dst], check=True)


def scene_take(a, sid, lines, d):
    full = ' '.join(lines)
    prev, nxt = neighbours(sid)
    r = tts(a, full, prev, nxt)
    take = os.path.join(d, 'take.mp3')
    open(take, 'wb').write(base64.b64decode(r['audio_base64']))
    al = r.get('alignment') or r.get('normalized_alignment')
    json.dump(al, open(os.path.join(d, 'take.alignment.json'), 'w'))
    return resplit(sid, lines, d, al)


def resplit(sid, lines, d, al):
    """Taie take.mp3 în replici după alinierea pe caractere (fără cerere nouă: refolosește un take deja plătit)."""
    ch, st, en = al['characters'], al['character_start_times_seconds'], al['character_end_times_seconds']
    full = ' '.join(lines)
    if len(ch) != len(full): raise SystemExit(f'{sid}: alinierea are {len(ch)} caractere, textul {len(full)}')
    take = os.path.join(d, 'take.mp3'); total = mp3_dur(take)
    spans, pos = [], 0
    for t in lines:
        spans.append((pos, pos + len(t) - 1)); pos += len(t) + 1
    starts = [st[s0] for s0, _ in spans]; ends = [en[e0] for _, e0 in spans]
    cuts = [0.0] + [(ends[i] + starts[i + 1]) / 2 for i in range(len(lines) - 1)] + [total]
    out = []
    for i, text in enumerate(lines):
        s0, e0 = spans[i]; c0, c1 = cuts[i], cuts[i + 1]
        sub = {'characters': ch[s0:e0 + 1], 'character_start_times_seconds': [x - c0 for x in st[s0:e0 + 1]], 'character_end_times_seconds': [x - c0 for x in en[s0:e0 + 1]]}
        words = el.words_from_alignment(sub)
        f = os.path.join(d, f'{i + 1:02d}.mp3')
        cut(take, c0, c1, f)
        out.append((f, words))
    return out


def entry(n, text, f, words):
    dur = mp3_dur(f)
    return {'n': n, 'text': text, 'src': os.path.relpath(f, ROOT), 'dur': dur,
            'speechStart': words[0]['start'] if words else 0, 'speechEnd': min(dur, words[-1]['end']) if words else dur, 'words': words}


def main():
    p = argparse.ArgumentParser()
    p.add_argument('scene'); p.add_argument('--voice', required=True)
    p.add_argument('--model', default='eleven_multilingual_v2'); p.add_argument('--speed', type=float, default=1.0)
    p.add_argument('--stability', type=float, default=0.5); p.add_argument('--style', type=float, default=0.2)
    p.add_argument('--seed', type=int); p.add_argument('--line', type=int, help='regenerează doar replica asta (1-based)')
    p.add_argument('--keep-old', action='store_true'); p.add_argument('--resplit', action='store_true', help='re-taie take-ul existent, fără cerere')
    a = p.parse_args()
    sid = a.scene
    lines = read_lines(sid)
    d = os.path.join(ROOT, 'voice', sid); os.makedirs(d, exist_ok=True)
    jf = os.path.join(ROOT, 'voice', sid + '.lines.json')
    if a.line:
        meta = json.load(open(jf))
        i = a.line - 1
        if len(meta['lines']) != len(lines): raise SystemExit('textul scenei s-a schimbat; regenerează toată scena')
        f = os.path.join(d, f'{a.line:02d}.mp3')
        if a.keep_old and os.path.exists(f):
            keep = os.path.join(ROOT, 'voice/_takes', sid); os.makedirs(keep, exist_ok=True)
            shutil.copy(f, os.path.join(keep, f'{a.line:02d}-{int(time.time())}.mp3'))
        prev = ' '.join(lines[max(0, i - 2):i]) or neighbours(sid)[0]
        nxt = lines[i + 1] if i + 1 < len(lines) else neighbours(sid)[1]
        r = tts(a, lines[i], prev, nxt)
        open(f, 'wb').write(base64.b64decode(r['audio_base64']))
        words = el.words_from_alignment(r.get('alignment') or r.get('normalized_alignment'))
        meta['lines'][i] = entry(a.line, lines[i], f, words)
        meta['lines'][i]['regen'] = True
        print(f'{sid} {a.line:02d} {meta["lines"][i]["dur"]:5.2f}s  {lines[i]}')
    else:
        if a.resplit:
            res = resplit(sid, lines, d, json.load(open(os.path.join(d, 'take.alignment.json'))))
        else:
            res = scene_take(a, sid, lines, d)
        meta = {'voice': a.voice, 'model': a.model, 'speed': a.speed, 'stability': a.stability, 'style': a.style,
                'lines': [entry(i + 1, lines[i], f, w) for i, (f, w) in enumerate(res)]}
        for l in meta['lines']: print(f'{sid} {l["n"]:02d} {l["dur"]:5.2f}s  {l["text"]}')
    json.dump(meta, open(jf, 'w'), ensure_ascii=False, indent=1)
    # alinierea ElevenLabs pune pauza dinaintea unei fraze în primul ei cuvânt: corectăm după energia reală
    subprocess.run([sys.executable, os.path.join(ROOT, 'tools/vo_onsets.py'), sid], cwd=ROOT, check=True)
    print(jf, f'total {sum(l["dur"] for l in meta["lines"]):.1f}s, {len(" ".join(lines))} caractere')


if __name__ == '__main__':
    main()
