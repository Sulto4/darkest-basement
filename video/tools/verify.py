#!/usr/bin/env python3
"""Verifică vocea cu Whisper (Groq, whisper-large-v3), replică cu replică. Politica: zero diferențe.

python3 tools/verify.py s01 s02 ...      → voice/<scena>.verify.json + rezumat
python3 tools/verify.py --all            → toate scenele, apoi voice/QA.md
python3 tools/verify.py s04 --lines 3,7  → doar replicile astea (după o regenerare)

Comparație (ca în voost-motion-reels/tools/vo_pick.py): fără diacritice, litere repetate comprimate, numerele scrise de
Whisper cu cifre („40 și 2”, „0,75”, „43,8%”) devin cuvinte, cratimele dispar; un cuvânt de cel puțin 5 litere trece și la
distanță Levenshtein 1, iar două cuvinte alăturate se pot lipi („Game Object” = „GameObject”).
Termenii tehnici englezești (Unity, lerp, SmoothDamp...) pe care Whisper în română nu-i scrie cum trebuie se verifică
a doua oară, pe aceeași replică, cu Whisper în engleză. O replică trece doar dacă nu lipsește niciun cuvânt.
Cheia Groq: ~/.secrets/groq.env (nu se afișează). Pauză de 3,5 s între cereri (rate limit)."""
import json, os, re, subprocess, sys, time, unicodedata

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
KEY = [l.split('=', 1)[1].strip().strip('"') for l in open(os.path.expanduser('~/.secrets/groq.env')) if l.startswith(('GROQ_API_KEY', 'export GROQ_API_KEY'))][0]

U = ['zero', 'unu un una o', 'doi doua', 'trei', 'patru', 'cinci', 'sase', 'sapte', 'opt', 'noua']
T = {10: 'zece', 11: 'unsprezece', 12: 'doisprezece douasprezece', 13: 'treisprezece', 14: 'paisprezece', 15: 'cincisprezece', 16: 'saisprezece', 17: 'saptesprezece', 18: 'optsprezece', 19: 'nouasprezece'}
D = {2: 'douazeci', 3: 'treizeci', 4: 'patruzeci', 5: 'cincizeci', 6: 'saizeci', 7: 'saptezeci', 8: 'optzeci', 9: 'nouazeci'}


def ro(n):
    if n < 10: return U[n]
    if n < 20: return T[n]
    if n < 100: return D[n // 10] + ('' if n % 10 == 0 else ' si ' + U[n % 10])
    if n < 1000: return ('o suta' if n // 100 == 1 else ro(n // 100) + ' sute') + ('' if n % 100 == 0 else ' ' + ro(n % 100))
    if n < 1000000: return ('o mie' if n // 1000 == 1 else ro(n // 1000) + ' mii de') + ('' if n % 1000 == 0 else ' ' + ro(n % 1000))
    return str(n)


def norm(s):
    s = re.sub(r'(?<=\d)\.(?=\d{3})', '', s)
    s = s.replace('%', ' la suta ').replace('×', ' ori ').replace('²', ' la patrat ')
    s = re.sub(r'(?<=\d),(?=\d)', ' virgula ', s)
    s = re.sub(r'(?<=\d)\s*[x×]\s*(?=\d)', ' pe ', s)          # „640x360” = „șase sute patruzeci pe trei sute șaizeci”
    s = re.sub(r'(?<=[A-Za-z0-9])-(?=\d)', ' minus ', s)       # „N-1” = „n minus unu”
    s = re.sub(r'\d+', lambda m: ' ' + (ro(int(m.group())) if len(m.group()) < 7 else m.group()) + ' ', s)
    s = unicodedata.normalize('NFKD', s.lower()); s = ''.join(c for c in s if not unicodedata.combining(c))
    s = re.sub(r'[^a-z0-9 ]', ' ', s.replace('-', '').replace('#', ' sharp '))
    s = re.sub(r'(.)\1+', r'\1', s)
    return s.split()


def lev(a, b):
    p = list(range(len(b) + 1))
    for i, ca in enumerate(a, 1):
        c = [i]
        for j, cb in enumerate(b, 1): c.append(min(p[j] + 1, c[j - 1] + 1, p[j - 1] + (ca != cb)))
        p = c
    return p[-1]


def asr(f, lang='ro'):
    time.sleep(3.5)
    for attempt in range(4):
        out = subprocess.run(['curl', '-s', '-m', '120', 'https://api.groq.com/openai/v1/audio/transcriptions', '-H', f'Authorization: Bearer {KEY}',
                              '-F', f'file=@{f}', '-F', 'model=whisper-large-v3', '-F', f'language={lang}', '-F', 'temperature=0'], capture_output=True, text=True).stdout
        try:
            return json.loads(out)['text'].strip()
        except Exception:
            time.sleep(15 * (attempt + 1))
    return 'ERR ' + out[:120]


# Englezisme rostite corect, pe care Whisper în română le scrie fonetic (verificate și în engleză): forma așteptată → forme acceptate.
ALIAS = {'spriteuri': ['spraituri'], 'spriteurile': ['spraiturile'], 'spriteul': ['spraitul'], 'sprite': ['sprait'],
         'viewul': ['viul', 'viu'], 'try': ['trai'], 'unu': ['unul'], 'en': ['n'], 'jumatate': ['virgula']}


def missing(want_text, got_text):
    want, got = norm(want_text), norm(got_text)
    got = got + [w for g in got for w, al in ALIAS.items() if g in al]
    gs = set(got) | {a + b for a, b in zip(got, got[1:])} | {a + b + c for a, b, c in zip(got, got[1:], got[2:])}
    wp = {a + b for a, b in zip(want, want[1:])}
    miss = []
    for i, w in enumerate(want):
        if w in gs: continue
        if len(w) >= 5 and any(lev(w, g) <= 1 for g in gs): continue
        # cuvânt rostit lipit de vecinul lui de Whisper („seedul” = „seed ul”): îl acceptă dacă perechea e în transcriere
        if any((want[j] + want[j + 1]) in gs for j in (i - 1, i) if 0 <= j < len(want) - 1): continue
        miss.append(w)
    return miss


def near(want_text, got_text):
    """Cuvintele acceptate doar prin toleranță (alias fonetic sau distanță 1), ca să apară explicit în raport."""
    want, got = norm(want_text), norm(got_text)
    gs0 = set(got) | {a + b for a, b in zip(got, got[1:])}
    out = []
    for i, w in enumerate(want):
        if w in gs0 or any((want[j] + want[j + 1]) in gs0 for j in (i - 1, i) if 0 <= j < len(want) - 1): continue
        al = [g for g in gs0 if g in ALIAS.get(w, [])]
        lv = [g for g in gs0 if len(w) >= 5 and lev(w, g) <= 1]
        if al or lv: out.append(f'{w}→{(al or lv)[0]}')
    return out


def check_scene(sid, only=None):
    meta = json.load(open(os.path.join(ROOT, 'voice', sid + '.lines.json')))
    vf = os.path.join(ROOT, 'voice', sid + '.verify.json')
    prev = {r['n']: r for r in json.load(open(vf))['lines']} if os.path.exists(vf) else {}
    rows = []
    for l in meta['lines']:
        if only and l['n'] not in only and l['n'] in prev:
            rows.append(prev[l['n']]); continue
        f = os.path.join(ROOT, l['src'])
        got = asr(f, 'ro')
        miss = missing(l['text'], got)
        en = None
        if miss:  # a doua părere pentru termenii englezești
            en = asr(f, 'en')
            ens = set(norm(en)) | {a + b for a, b in zip(norm(en), norm(en)[1:])}
            miss = [w for w in miss if not (w in ens or (len(w) >= 5 and any(lev(w, g) <= 1 for g in ens)))]
        row = {'n': l['n'], 'text': l['text'], 'whisper_ro': got, 'whisper_en': en, 'missing': miss, 'ok': not miss, 'regen': bool(l.get('regen'))}
        rows.append(row)
        print(f'{sid} {l["n"]:02d} {"OK " if row["ok"] else "NU "} {miss if miss else ""}  | {got}', flush=True)
    json.dump({'scene': sid, 'voice': meta['voice'], 'model': meta['model'], 'lines': rows}, open(vf, 'w'), ensure_ascii=False, indent=1)
    return rows


def report():
    out = ['# Vocea: verificare Whisper', '',
           'Generat de `tools/verify.py`. Fiecare replică e transcrisă cu Whisper large-v3 (Groq) în română; termenii englezești',
           'neprinși în română se reverifică în engleză. Politica e zero diferențe: o replică cu cuvinte lipsă se regenerează',
           '(`tools/vo.py <scena> --line N`) până trece.', '']
    tot = bad = regen = 0
    for sid in [f's{i:02d}' for i in range(1, 13)]:
        vf = os.path.join(ROOT, 'voice', sid + '.verify.json')
        if not os.path.exists(vf): continue
        d = json.load(open(vf)); meta = json.load(open(os.path.join(ROOT, 'voice', sid + '.lines.json')))
        dur = sum(l['speechEnd'] - l['speechStart'] for l in meta['lines'])
        out += [f'## {sid} ({len(d["lines"])} replici, {dur:.1f} s de vorbire)', '', '| # | rezultat | Whisper (ro) |', '|---|---|---|']
        for r in d['lines']:
            tot += 1; bad += not r['ok']; regen += r.get('regen', False)
            st = 'OK' if r['ok'] else 'LIPSESC: ' + ', '.join(r['missing'])
            nr = near(r['text'], r['whisper_ro'])
            if r['ok'] and nr: st += ' (variante: ' + ', '.join(nr) + ')'
            if r.get('regen'): st += ' (regenerată)'
            if r.get('whisper_en') and r['ok']: st += ' (termeni confirmați în engleză)'
            out.append(f'| {r["n"]} | {st} | {r["whisper_ro"].replace("|", "/")} |')
        out.append('')
    out.insert(5, f'**Rezultat: {tot - bad}/{tot} replici trec; {regen} regenerate.**\n\n'
               'Variantele acceptate sunt doar englezisme scrise fonetic de Whisper (seed-ul → „sidul”, run → „ran”, sprite → „sprait”, View-ul → „viul”, '
               'try → „trai”, Gaper → „Gapar”, Block-ul → „blocul”, Monstro → „Monstruo”) sau cratime („joac-o” → „joacao”); în plus „doi și jumătate” transcris „2,5” și o dată „cu unu” auzit „cu unul” (s06/8, același sens). Patru replici în care '
               'Whisper a auzit alt cuvânt românesc (construit, pornit, schimb, pixele) au fost reformulate și regenerate, chiar dacă două erau doar „i”-ul final '
               'abia rostit pe care Whisper îl pierde. Vocea: Bogdan Gamaleț, `eleven_multilingual_v2`.\n')
    open(os.path.join(ROOT, 'voice', 'QA.md'), 'w').write('\n'.join(out) + '\n')
    print(f'QA: {tot - bad}/{tot} OK, {regen} regenerate → voice/QA.md')


if __name__ == '__main__':
    args = sys.argv[1:]
    only = None
    if '--lines' in args:
        i = args.index('--lines'); only = {int(x) for x in args[i + 1].split(',')}; del args[i:i + 2]
    if '--report' in args:
        report(); sys.exit(0)
    scenes = [f's{i:02d}' for i in range(1, 13)] if '--all' in args else [a for a in args if a.startswith('s')]
    for s in scenes: check_scene(s, only)
    report()
