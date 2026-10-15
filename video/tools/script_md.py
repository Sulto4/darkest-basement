#!/usr/bin/env python3
"""Scrie video/SCRIPT.md din textul real al vocii (voice/<scena>.lines.json), timpii calculați (out/timings.json, din
tools/timings.mjs) și descrierea imaginii de mai jos. Rulează după orice schimbare de voce sau de plan:
node tools/timings.mjs && python3 tools/script_md.py"""
import json, os

V = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
T = json.load(open(os.path.join(V, 'out/timings.json')))

# Ce se vede, replică cu replică (n → descriere). Cifrele și codul vin din repo; nimic inventat.
VIS = {
 's01': {
  '_': 'Deschidere pe gameplay (capturi din rularea QA cu seed 7, Ken Burns; sloturi pentru clipuri live). Muzica: music_combat.',
  1: 'Meniul jocului (logo, câmpul SEED = 7).', 2: 'Holul văzut din spate, apoi vederea laterală cu monștrii (tăieturi pe „cobori” și „monștri”).',
  3: 'Harta → holul → ușa → mâna de cărți, câte o tăietură pe fiecare cuvânt-cheie.', 4: 'Cartea jucată, raza Brimstone, recompensa „NEW CARD”, trapa.',
  5: 'Monstro, Gurdy, piciorul lui Mom, lovitura lui Mom.', 6: 'Peretele subsolului, cei patru eroi și un Gaper intră în cadru; logo-ul se desface în 10 fâșii, din care ies simboluri: G=(V,E), BFS, seed, n!, sort, f∘g, ⌊x⌋, p̂, f(t), x².',
  7: 'Fâșiile se reasamblează; „cum e făcut, piesă cu piesă”. Propoziția: „fiecare piesă = o idee pe care o știi deja”.'},
 's02': {
  '_': 'Două panouri: MODEL (creierul, stare ca date C#) și VIEW (teatru de păpuși cu cortină). Muzica: music_menu.',
  1: 'Panourile intră din lateral.', 2: 'Creierul din pixeli; apar rândurile `isaac.Hp = 26`, `gaper.Hp = 5`, `isaac.Hand = [Tear, Tear, Spoon Bender]`.',
  3: 'Trei iconițe (sprite, cameră, sunet) tăiate cu roșu pe cuvintele lor.', 4: 'Cortina se trage; pe scenă Isaac și un Gaper respiră. Apar pe rând: desenează, animă, cântă, ascultă.',
  5: 'Două benzi între lumi („comenzi”, „evenimente”); bilețelul `PlayCard(isaac, tear, gaper)` zboară din View în Model.',
  6: '`gaper.Hp` devine 0; ies trei bilețele: `CardPlayed`, `Damaged 5`, `UnitDied`.', 7: 'View-ul consumă bilețelele pe rând: Isaac aruncă lacrima, Gaper clipește alb cu „5”, apoi moare (pată de sânge).',
  'slot:s02-joc': 'Gameplay: cartea jucată, lovitura, moartea.', 8: 'View-ul pleacă; terminalul arată testele reale (`PASS …`, `Tests: 35 passed, 0 failed.`).',
  9: 'Terminalul continuă cu simularea: `Runs=500`, `Wins=219 (43.8%)`.', 10: '`DarkestBasement.Model.asmdef` (evidențiat `"noEngineReferences": true`); o încercare `using UnityEngine;` în CombatEngine.cs → `error CS0246`.'},
 's03': {
  '_': 'Graful stărilor pe pergament (butoanele jocului), cu jetonul lui Isaac; în dreapta captura stării curente. Codul din GameFlow.cs.',
  1: 'Muchiile se trag, nodurile apar: MENIU, ETAJ NOU, HARTĂ, HOL, CAMERĂ, LUPTĂ, RECOMPENSĂ, GAME OVER, VICTORIE.', 2: 'Nodurile pulsează; `δ(stare, intrare) = stare nouă`.',
  3: 'Jetonul sare din stare în stare pe fiecare cuvânt; în dreapta: meniul, harta, holul, ușa, lupta, recompensa.', 4: '`IEnumerator Machine(...)`: evidențiat `while (_next != FlowState.None)`, apoi `yield return Safe(Routine(state), …)`.',
  5: 'Bucla de bază: hartă → hol → cameră → luptă → recompensă → hartă, cu capturile corespunzătoare.', 6: '`IEnumerator Routine(...)`: `switch (state)` cu `case`-urile.',
  7: 'Comentariul din antetul GameFlow.cs („Modelul decide regulile … GameFlow doar le pune în scenă”); două etichete: Modelul / GameFlow.'},
 's04': {
  '_': 'Harta REALĂ a seed-ului 7, generată pas cu pas de un port exact al MapGenerator.cs (film/model.js); pe final, aceeași hartă în joc.',
  1: 'Grila 5×4; o previzualizare a hărții finale cu etichetele „nod = cameră” și „muchie = hol”.', 2: 'Apare camera de start (2, 2); contoarele „camere / muchii”.',
  3: 'Frontiera (contur auriu, cu multiplicitatea ×2/×3 a celulelor vecine cu mai multe camere); se alege o celulă și se trage holul. Apoi pașii 2–9 accelerați. Harta se micșorează de la ×3 la ×2 (tot întreg), iar în dreapta apare bucla `while (map.Rooms.Count < count)` cu `rng.Pick(frontier)`.',
  4: 'Contoarele ajung la 10 camere / 9 muchii, „= n − 1”; arborele se colorează.', 5: 'Drumul unic 2→0→1→4 în roșu; apoi muchia în plus 2–4 și bucla 0–1–4–2 aurie; „= n, deci o buclă”.',
  6: 'Stegulețe pe cele 4 fundături; codul cu `if (leaves < 2) continue;`.', 7: 'Un „?” mare peste hartă.',
  8: 'BFS: cifrele 0, 1, 2, 3 apar pe camere în ordinea reală a cozii; coada BFS e desenată în dreapta, cu codul `Distances()`.', 9: 'Camerele la distanța 3 (ambele fundături); craniul boss-ului cade pe (4, 3). Codul selecției boss-ului.',
  10: 'Coroana comorii, săbiile luptelor, patul, curio-ul (caca).', 'slot:s04-harta': 'Harta din joc pentru seed 7 (aceeași formă).'},
 's05': {
  '_': 'Seed, generatorul xorshift pe starea reală a GameRng(7), restul împărțirii, fluxurile Fork, Fisher–Yates pe pachetul de start al lui Isaac.',
  1: 'Trei hărți reale pentru seed 3, 42 și 2024 (diferite).', 2: 'Două hărți „seed 7” care cresc identic, cameră cu cameră; semnul „=”.',
  'slot:s05-seed': 'Gameplay: câmpul SEED = 7 din meniu, apoi ecranul de final al aceluiași run (SEED 7).',
  4: '„GameRng: xorshift pe 32 de biți”; cei 32 de biți ai stării reale (0xA3C59AC4) se deplasează și se combină prin XOR: `x ^= x << 13`, `>> 17`, `<< 5`; codul `Next()`.',
  5: 'Același lanț de numere (valorile reale ale lui Next()) de două ori, identic.', 6: 'Un generator de jucărie cu 8 valori, „rest la 3”: coșurile primesc 3, 3, 2; valorile 0 și 1 sunt respinse și coșurile devin egale; rândurile `limit` și `do … while` din `Range()`.',
  7: 'seed 7 se ramifică în `Fork("map")`, `Fork("combat")`, `Fork("loot")`, fiecare cu primele valori reale `Range(0, 100)`; rândul din `Run.StartRun`.',
  8: 'O aruncare în plus în fluxul de luptă: valorile lui se schimbă (roșu); hartă și recompense: „✓ la fel”.',
  9: 'Cele cinci cărți de start ale lui Isaac (Tear, Tear, Yum Heart, The Sad Onion, Spoon Bender); i merge de la ultima spre prima, zarul dă j = Range(0, i+1) din fluxul de luptă al seed-ului 7, cărțile se schimbă în arc; codul `Shuffle`.',
  10: '„5! = 120”, „fiecare ordine: probabilitate 1/120”.'},
 's06': {
  '_': 'Scena de luptă laterală (eroii stânga, inamicii dreapta). Exemplul de inițiativă e ales cu egalități, ca să se vadă comparatorul.',
  1: 'Viteza fiecăruia, zarurile 0–3, sumele: Lilith 4, Isaac 7, Cain 7, Azazel 6, Gaper 6, Fly 10, Pooter 7.', 2: 'Portretele urcă într-o bară și se sortează descrescător: Fly, Cain, Isaac, Pooter, Azazel, Gaper, Lilith.',
  3: 'Egalitatea la 7: eroii întâi, apoi id-ul („cain” < „isaac”); comparatorul real din `TurnOrder.Roll`.',
  4: 'Cain (Strength 2) și un Gaper (Mark 2, Block 4); banda de funcții pornește: baza 5, +Strength → 7.',
  5: '×0,75 (sărit, „nu e” slăbit), ×1,5 marcat → 10,5, ×1,5 crit → 15,75.', 6: 'Rotunjire → 15, −Block → 11; Cain lovește, „11!” galben, HP 27 → 16.',
  7: '`D = ⌊ crit ∘ marcat ∘ slăbit ∘ (+Str) (bază) ⌋ − Block`; contraexemplul `⌊1,5·⌊1,5·5⌋⌋ = 10` dar `⌊1,5·1,5·5⌋ = 11`; codul `DamageCalculator.Calculate`.',
  8: 'Vectorul de 8 statusuri cu iconițele jocului; la „scad” Bleed 2→1, Poison 3→2, Weak 1→0, Mark 2→1, Block 5→0; `new int[8]`.',
  9: 'Intențiile deasupra inamicilor; linia punctată de la Gaper la Azazel.', 'slot:s06-runda': 'Gameplay: bara de ture și intențiile.'},
 's07': {
  '_': 'Azazel joacă Blood Beam; evenimentele sunt cele 7 pe care le produce CombatEngine.PlayCard, în ordinea reală.',
  1: 'Cartea intră în Model; ies instant 7 bilețele: EnergyChanged −2, CardPlayed, Damaged gaper 5, Damaged fly 4, UnitDied fly, UnitMoved pooter 3→2, StatusApplied Bleed 2; „≈ 0,02 ms”.',
  2: 'Toate animațiile deodată (haos, „?!”, shake), apoi bilețelele se aliniază într-o coadă.', 3: 'Capul cozii coboară pe scenă și își joacă animația; coada avansează.',
  4: '`IEnumerator Play(...)`: bucla `for` și `yield return Safe(PlayOne(events[i]))`.', 5: '`switch` din `PlayOne` cu `case Damaged ev`; pe Gaper: număr, clipire albă, sânge, shake (etichete pe cuvinte).',
  'slot:s07-lovitura': 'Gameplay: lovitura, moartea, raza.', 6: '`Safe(...)` cu `try`/`catch`; un bilețel roșu „crapă”, iar coada merge mai departe.'},
 's08': {
  '_': 'Grila de 500 de celule = seed-urile 1..500 jucate de AutoPlayer. Culorile sunt rezultatele reale (film/data/sim500.json, rulat cu tools/sim): victorie, mort în Caves, mort în Depths.',
  1: 'Grila goală.', 2: 'Cele trei reguli ale politicii, cu rândurile de scor din `AutoPlayer.Score`.', 3: 'Grila se umple seed cu seed; „câteva secunde, fără Unity”.',
  4: 'p̂ după n run-uri (curba reală) converge spre 0,438, în banda de încredere 95%.', 5: 'Prima versiune: toată grila roșie, „0 / 500”, „2,33 runde pe luptă”.',
  6: 'Glisoarele „HP inamici +50%” și „damage −”; grila revine la rezultatele reale; „219 / 500 = 43,8%”.', 7: 'Histograma mediei de runde pe luptă normală (500 de run-uri), ținta 3–4; boss 6,78 (Monstro 6,22 · Gurdy 6,59 · Mom 7,72).',
  8: 'Trei loturi cu intervale de încredere: 43,8% (1–500), 51,0% (1001–1500), 49,8% (3001–3500), toate în ținta 35–55%; „eroarea standard ≈ 2,2%”.'},
 's09': {
  '_': 'Grafice pe [0,1], o pistă de la a la b, atacul lui Azazel cu timpii din UnitView, SmoothDamp, trauma.',
  1: 'Azazel respiră.', 2: 'Formula `lerp(a, b, t) = a + (b − a) · t`, cu t care crește de la 0 la 1.', 3: 'f(t) = t: Isaac merge uniform; urmele egal depărtate arată viteza constantă.',
  4: '`f : [0, 1] → [0, 1], f(0) = 0, f(1) = 1`.', 5: 'Curba OutCubic se desenează peste diagonală; urmele se îndesesc la final; rândul din Easing.cs.', 6: 'OutBack trece de 1 (linia punctată); o carte „pop”-ează.',
  7: 'Atacul încetinit ×5: anticipare (OutQuad 0,16 s), salt (OutCubic 0,1 s), întoarcere (InOutQuad 0,22 s).', 8: 'Squash 1,12/0,88 și stretch 0,85/1,14; patru eroi respiră, fiecare cu sinusul lui (faze diferite).',
  'slot:s09-atac': 'Gameplay: anticipare, salt, lovitură.', 9: 'Răspunsul la o treaptă: subamortizat (oscilează), supraamortizat (lent), critic (SmoothDamp).', 10: '„smoothTime = 0,22 s” și rândul cu `Vector3.SmoothDamp` din CameraRig.',
  11: 'Graficul traumei: trei lovituri (+0,25, +0,55, +0,3), scădere liniară cu 1,5/s.', 12: 'trauma² (auriu) sub trauma: loviturile mici aproape dispar; codul `float shake = Trauma * Trauma;`.',
  13: 'Random (sare) lângă zgomot continuu (tremură).', 14: 'Cain lovește Gaper: 0,1 s înghețat (banda de cadre), apoi shake; codul `HitStop.Freeze`.', 'slot:s09-impact': 'Gameplay: crit, impactul lui Monstro.'},
 's10': {
  '_': 'Diagramă văzută de sus (holul, camera, vectorul f, quad-urile eroilor), arcul camerei, pixelii măriți.',
  1: 'Holul din joc (captură) și diagrama de sus.', 2: 'Camera se rotește; quad-urile rămân perpendiculare pe f; codul `f.y = 0f` + `LookRotation`.', 3: 'Comparația: „privește spre cameră” (pe margini se rotesc diferit) vs paralel cu planul camerei.',
  4: 'Arcul de sus (pivot, rază, „din spate” → „lateral”) și formulele θ(k), r(k), h(k) = … + 0,9·sin(πk), k = InOutCubic(t); codul din `Orbit`.', 'slot:s10-arc': 'Gameplay: camera trece din spate în lateral.',
  5: 'Isaac 48×48 mărit ×12 cu grila de pixeli; „32 px = 1 unitate”; „640 × 360”.', 6: 'Fața lui Isaac la ×3: fiecare pixel = 3×3.', 7: 'Aceeași față la ×2,5: coloane de 2 și 3; un ecran 1366×768 cu bandă neagră; codul `UpdateScale`.',
  8: 'Vederea laterală din joc cu o riglă de 10 unități; „64 px pe unitate = 2 × 2 pe pixel de artă”.'},
 's11': {
  '_': 'Drumul de învățare ca o hartă de etaj pe pergament; Isaac merge din cameră în cameră; în dreapta, o mică demonstrație a fiecărui pas.',
  1: 'Harta cu 7 camere și trapa „jocul tău”.', 2: 'Un log de luptă în consolă.', 3: 'GameObject cu componente; un Isaac care respiră.', 4: 'Bilețele de evenimente care pleacă spre animație.',
  5: 'Trei cărți; cea din mijloc se ridică la hover.', 6: 'Curba OutCubic.', 7: 'Un mini-graf cu distanțe BFS.', 8: 'O grilă de run-uri.', 9: 'Isaac ajunge la trapă; „fiecare pas se testează singur ✓”.'},
 's12': {
  '_': 'Trei cărți-exercițiu, cu fața în jos, care se întorc pe rând; sub fiecare, rândul de cod de la care pornești. Muzica: music_boss.',
  1: 'Cele trei cărți intră cu fața în jos.', 2: 'Cartea 1 („O carte nouă”) + rândul `Add("tear", …)` din CardDatabase.', 3: 'Cartea 2 („Altă curbă”) + `}, Ease.InOutCubic, () =>` din CameraRig.Orbit → `Ease.OutBack` sau `Ease.Linear`.',
  4: 'Cartea 3 („Monstro ×2”) + rândul lui Monstro din EnemyDatabase, 260 → 520, `tools/sim/run.sh`, „Wins = ?”.', 5: 'Cărțile cad; logo-ul jocului, trapa, echipa coboară; „Spor la coborât.”'},
}

fmt = lambda t: f'{int(t // 60)}:{t % 60:04.1f}'
out = ['# Darkest Basement: cum e făcut — scenariul', '',
       f'Durata: **{fmt(T["duration"])}** (1920×1080, 30 fps). Vocea: Bogdan Gamaleț, ElevenLabs `eleven_multilingual_v2`. ',
       'Textul de mai jos e exact cel rostit (numerele sunt scrise în litere pentru voce; pe ecran apar cu cifre). Timpii sunt calculați din voce (`film/timeline.js`).',
       'Codul arătat pe ecran e extras din repo după ancore (`tools/snippets.py` → `film/data/snippets.json`), nu rescris de mână.', '']
for s in T['scenes']:
    v = VIS.get(s['id'], {})
    out += [f'## {s["id"]} · {s["title"]}  ({fmt(s["t0"])}–{fmt(s["t1"])}, {s["t1"] - s["t0"]:.0f} s)', '']
    if '_' in v: out += [f'*{v["_"]}*', '']
    out += ['| # | timp | narațiune | pe ecran |', '|---|---|---|---|']
    slots = {sl['id']: sl for sl in s['slots']}
    cover = {sid for sid, sl in slots.items() if any(sl['t0'] <= l['t0'] + 0.01 and sl['t1'] >= l['t1'] - 0.01 for l in s['lines'])}
    done = set(cover)
    for l in s['lines']:
        for sid, sl in slots.items():
            if sid not in done and sl['t1'] <= l['t0'] + 0.01:
                out.append(f'| ▶ | {fmt(sl["t0"])} | *(gameplay, {sl["t1"] - sl["t0"]:.1f} s, slot `{sid}`)* | {v.get("slot:" + sid, "")} |'); done.add(sid)
        vis = v.get(l['n'], '')
        for sid, sl in slots.items():
            if sid in cover and sl['t0'] <= l['t0'] + 0.01 and sl['t1'] >= l['t1'] - 0.01:
                vis = f'**slot `{sid}`** (gameplay sub voce): ' + vis
        out.append(f'| {l["n"]} | {fmt(l["t0"])} | {l["text"]} | {vis} |')
    for sid, sl in slots.items():
        if sid not in done:
            out.append(f'| ▶ | {fmt(sl["t0"])} | *(gameplay, {sl["t1"] - sl["t0"]:.1f} s, slot `{sid}`)* | {v.get("slot:" + sid, "")} |')
    out.append('')
open(os.path.join(V, 'SCRIPT.md'), 'w').write('\n'.join(out))
print('SCRIPT.md', sum(len(s['lines']) for s in T['scenes']), 'replici')
