#!/usr/bin/env python3
"""ElevenLabs pentru reel-uri: voce cu timpi pe cuvinte, efecte sonore, muzică. Doar stdlib.
Cheia: ELEVENLABS_API_KEY din mediu sau din ~/.secrets/elevenlabs.env (nu se afișează niciodată).

  el.py account                                   → plan, caractere rămase
  el.py voices [--lang ro] [--q text] [--shared]  → voci (ale contului sau din biblioteca publică)
  el.py tts --voice ID --text-file f.txt --out audio/vo/x.mp3 [--model eleven_v3] [--speed 1.05] [--stability 0.4] [--style 0.3] [--lang ro]
        → x.mp3 + x.words.json ([{w, start, end}])
  el.py sfx --text "..." --dur 1.2 --out audio/sfx/id.mp3 [--influence 0.5]
  el.py music --prompt "..." --ms 30000 --out audio/music/x.mp3 [--plan plan.json]
  el.py music-plan --prompt "..." --ms 30000 --out plan.json
"""
import argparse, base64, json, os, re, sys, urllib.request, urllib.error, urllib.parse

API = 'https://api.elevenlabs.io'


def key():
    k = os.environ.get('ELEVENLABS_API_KEY')
    if not k:
        p = os.path.expanduser('~/.secrets/elevenlabs.env')
        if os.path.exists(p):
            for line in open(p):
                m = re.match(r'\s*(?:export\s+)?ELEVENLABS_API_KEY\s*=\s*["\']?([^"\'\s]+)', line)
                if m:
                    k = m.group(1)
    if not k:
        sys.exit('lipsește ELEVENLABS_API_KEY (~/.secrets/elevenlabs.env)')
    return k


def call(method, path, body=None, query=None, raw=False, timeout=300):
    url = API + path + ('?' + urllib.parse.urlencode(query) if query else '')
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method, headers={'xi-api-key': key(), 'content-type': 'application/json', 'accept': '*/*'})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            b = r.read()
            return b if raw else json.loads(b)
    except urllib.error.HTTPError as e:
        sys.exit(f'HTTP {e.code} {path}: {e.read().decode(errors="replace")[:800]}')


def words_from_alignment(al):
    chars, st, en = al['characters'], al['character_start_times_seconds'], al['character_end_times_seconds']
    words, cur = [], None
    for c, s, e in zip(chars, st, en):
        if c.isspace():
            if cur: words.append(cur); cur = None
            continue
        if cur is None: cur = {'w': '', 'start': s, 'end': e}
        cur['w'] += c; cur['end'] = e
    if cur: words.append(cur)
    for w in words:
        w['start'] = round(w['start'], 3); w['end'] = round(w['end'], 3)
        w['clean'] = re.sub(r'[^\wăâîșțĂÂÎȘȚ-]', '', w['w'])
    return [w for w in words if w['clean'] or w['w'].strip('[]')]


def cmd_account(a):
    s = call('GET', '/v1/user/subscription')
    print(json.dumps({k: s.get(k) for k in ('tier', 'character_count', 'character_limit', 'next_character_count_reset_unix', 'status', 'can_use_instant_voice_cloning')}, indent=1))


def cmd_voices(a):
    if a.shared:
        q = {'page_size': 60}
        if a.lang: q['language'] = a.lang
        if a.q: q['search'] = a.q
        if a.gender: q['gender'] = a.gender
        r = call('GET', '/v1/shared-voices', query=q)
        for v in r.get('voices', []):
            print(f"{v['voice_id']}  {v.get('name')!s:28} {v.get('gender')!s:7} {v.get('age')!s:12} {v.get('accent')!s:14} {v.get('language')!s:4} uses={v.get('cloned_by_count')} | {(v.get('description') or '')[:90]} | {v.get('preview_url')}")
    else:
        r = call('GET', '/v2/voices', query={'page_size': 100, **({'search': a.q} if a.q else {})})
        for v in r.get('voices', []):
            lab = v.get('labels') or {}
            print(f"{v['voice_id']}  {v.get('name')!s:30} {v.get('category')!s:12} {lab.get('gender','')!s:7} {lab.get('accent','')!s:12} {v.get('preview_url')}")


def cmd_tts(a):
    text = open(a.text_file).read().strip()
    vs = {'stability': a.stability, 'similarity_boost': a.similarity, 'style': a.style, 'use_speaker_boost': True, 'speed': a.speed}
    body = {'text': text, 'model_id': a.model, 'voice_settings': vs}
    if a.lang: body['language_code'] = a.lang
    if a.seed is not None: body['seed'] = a.seed
    r = call('POST', f'/v1/text-to-speech/{a.voice}/with-timestamps', body, query={'output_format': os.environ.get('EL_FORMAT', 'mp3_44100_128')})
    os.makedirs(os.path.dirname(a.out) or '.', exist_ok=True)
    open(a.out, 'wb').write(base64.b64decode(r['audio_base64']))
    al = r.get('alignment') or r.get('normalized_alignment')
    words = words_from_alignment(al) if al else []
    base = os.path.splitext(a.out)[0]
    json.dump(words, open(base + '.words.json', 'w'), ensure_ascii=False, indent=0)
    meta = {'voice': a.voice, 'model': a.model, 'settings': vs, 'text': text}
    json.dump(meta, open(base + '.meta.json', 'w'), ensure_ascii=False, indent=1)
    dur = words[-1]['end'] if words else 0
    print(f'{a.out}: {len(words)} cuvinte, ~{dur:.2f} s')


def cmd_sfx(a):
    body = {'text': a.text, 'prompt_influence': a.influence, 'model_id': a.model}
    if a.dur: body['duration_seconds'] = a.dur
    b = call('POST', '/v1/sound-generation', body, query={'output_format': os.environ.get('EL_FORMAT', 'mp3_44100_128')}, raw=True)
    os.makedirs(os.path.dirname(a.out) or '.', exist_ok=True)
    open(a.out, 'wb').write(b)
    print(a.out, len(b), 'B')


def cmd_music_plan(a):
    r = call('POST', '/v1/music/plan', {'prompt': a.prompt, 'music_length_ms': a.ms, 'model_id': 'music_v1'})
    json.dump(r, open(a.out, 'w'), ensure_ascii=False, indent=1)
    print(a.out)


def cmd_music(a):
    body = {'model_id': 'music_v1'}
    if a.plan:
        body['composition_plan'] = json.load(open(a.plan))
    else:
        body.update({'prompt': a.prompt, 'music_length_ms': a.ms, 'force_instrumental': True})
    b = call('POST', '/v1/music', body, query={'output_format': os.environ.get('EL_FORMAT', 'mp3_44100_128')}, raw=True, timeout=600)
    os.makedirs(os.path.dirname(a.out) or '.', exist_ok=True)
    open(a.out, 'wb').write(b)
    print(a.out, len(b), 'B')


def main():
    p = argparse.ArgumentParser()
    sp = p.add_subparsers(dest='cmd', required=True)
    sp.add_parser('account').set_defaults(f=cmd_account)
    v = sp.add_parser('voices'); v.add_argument('--lang'); v.add_argument('--q'); v.add_argument('--gender'); v.add_argument('--shared', action='store_true'); v.set_defaults(f=cmd_voices)
    t = sp.add_parser('tts'); t.add_argument('--voice', required=True); t.add_argument('--text-file', required=True); t.add_argument('--out', required=True)
    t.add_argument('--model', default='eleven_multilingual_v2'); t.add_argument('--speed', type=float, default=1.0); t.add_argument('--stability', type=float, default=0.45)
    t.add_argument('--similarity', type=float, default=0.8); t.add_argument('--style', type=float, default=0.35); t.add_argument('--lang'); t.add_argument('--seed', type=int); t.set_defaults(f=cmd_tts)
    s = sp.add_parser('sfx'); s.add_argument('--text', required=True); s.add_argument('--dur', type=float); s.add_argument('--out', required=True)
    s.add_argument('--influence', type=float, default=0.55); s.add_argument('--model', default='eleven_text_to_sound_v2'); s.set_defaults(f=cmd_sfx)
    m = sp.add_parser('music'); m.add_argument('--prompt'); m.add_argument('--ms', type=int, default=30000); m.add_argument('--plan'); m.add_argument('--out', required=True); m.set_defaults(f=cmd_music)
    mp = sp.add_parser('music-plan'); mp.add_argument('--prompt', required=True); mp.add_argument('--ms', type=int, default=30000); mp.add_argument('--out', required=True); mp.set_defaults(f=cmd_music_plan)
    a = p.parse_args()
    a.f(a)


if __name__ == '__main__':
    main()
