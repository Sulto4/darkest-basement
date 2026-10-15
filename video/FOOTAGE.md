# Clipurile live din joc

Din v2, fiecare slot de gameplay e un clip live din build-ul de QA, **seed 7**, capturat cadru cu cadru la 30 fps fix (`Time.captureFramerate`),
deci mișcarea e lină chiar dacă Forge desenează jocul în software cu ~4 cadre/s. Sunetul (vocea, muzica, efectele) e mixat separat: clipurile
sunt mute, iar efectele sonore ale slotului sunt puse pe momentele din clip (`sfx` în `footage.json`).

## Cum se capturează

1. **Captura țintită** (`Assets/_Game/Scripts/QA/QaDirector.cs`): cu `-capture-fps 30 -capture-moments tag1,tag2`, QA-ul scrie doar ferestrele din
   jurul momentelor cerute (`-capture-pre` secunde dinainte, dintr-un tampon în memorie, și `-capture-post` după), nu tot run-ul.
   `-capture-max n` = câte ferestre pe tag, `-capture-jpg` = JPEG 92 în loc de PNG (~300 KB pe cadru), `-capture-quit` = iese după ultima fereastră.
   În `qa-log.txt` rămâne „fereastră TAG: cadrele A–B”.
2. **Ceasul nescalat** (`Assets/_Game/Scripts/Core/GameTime.cs`): `Time.unscaledTime` merge în timp real și sub captură, deci shake-ul, hit-stop-ul,
   numerele de damage și UI-ul ar fi alergat de ~7 ori prea repede în clip. `Core.GameTime` e identic cu `Time.unscaledTime` /
   `unscaledDeltaTime` când nu se capturează și urmează pașii de captură când se capturează. Turul QA `tour 12345` dă aceeași secvență de
   157 de momente ca înainte, cu 0 erori și 0 excepții.
3. **Rularea** (pe Forge, ca unitate systemd detașată, sub lock-ul `/tmp/darkest-basement-unity.lock`, plafonată):
   ```bash
   bash tools/capture.sh push && bash tools/capture.sh job buildlinux 2400 tools/unity.sh build-linux
   bash tools/capture.sh job combat 3600 tools/unity.sh qa combat 7 -capture-fps 30 -capture-jpg -capture-max 2 -capture-pre 1.5 -capture-post 4.5 \
     -capture-moments menu,map,corridor,door,room_combat,side_view,combat_round,combat_hero_turn,combat_card_played,combat_hit,combat_kill,combat_crit,combat_beam,reward_reveal
   bash tools/capture.sh job monstro2 3600 tools/unity.sh qa sandbox 7 -combat-sandbox monstro -capture-fps 30 -capture-jpg \
     -capture-moments combat_boss_attack,combat_boss_impact -capture-pre 1 -capture-post 2.5 -capture-quit      # la fel gurdy; mom cu -capture-max 3
   bash tools/capture.sh job showcase 3600 tools/unity.sh qa sandbox 7 -combat-sandbox showcase -capture-fps 30 -capture-jpg \
     -capture-moments combat_crit,combat_kill -capture-max 3 -capture-pre 1 -capture-post 2.5 -capture-quit
   bash tools/capture.sh wait 560                       # așteptare blocantă, limitată; apoi:
   bash tools/capture.sh clips <script> <prefix>        # fiecare fereastră → out/footage/raw/<prefix>-NN.mp4 (pe Forge)
   bash tools/capture.sh clean <script>                 # șterge imediat cadrele de pe Forge; frames/ nu ajunge niciodată pe Mac
   ```
   Durate măsurate: `combat` ~11 min, fiecare boss din sandbox 3–7 min.
4. **Planșe de control**: `bash tools/review.sh out/footage/raw/<clip>.mp4` (pe Forge) → `out/footage/review2/<clip>.jpg`, cu secunda din clip pe
   fiecare cadru; `FROM=5 TO=13 STEP=6` pentru un interval fin.
5. **Sloturile**: `footage.json` spune din ce bucăți e făcut fiecare slot; `bash tools/capture.sh slots` → `out/footage/<slot>.mp4` cu exact
   numărul de cadre al slotului. Randarea (`tools/forge.sh render`) folosește clipul în locul capturilor statice.

## Clipurile brute (pe Forge, `out/footage/raw/`)

| clip | captură | ce e în el |
|---|---|---|
| `combat-01` … `combat-08` | `qa combat 7` | meniul cu SEED 7, harta, holul, ușile, intrarea în prima luptă (arcul camerei), runda 1 întreagă (Cain, Azazel cu Brimstone, Lilith), uciderile, runda 2 cu Isaac, ecranul NEW CARD |
| `monstro2-01/02` | sandbox `monstro` | Monstro scuipă sânge; Monstro sare și aterizează peste echipă |
| `gurdy2-01` | sandbox `gurdy` | Gurdy: Mouth Beam |
| `mom2-01/02/03` | sandbox `mom`, max 3 | mâna lui Mom care mătură echipa; ușa cu ochiul și scheletele, piciorul care coboară |
| `showcase-01…04` | sandbox `showcase` | lovituri cu moarte în sânge; două lovituri critice (Dr. Fetus, „2!” și „9!”, flash alb) |

Capturile vechi (`monstro-*`, `gurdy-01`, `mom-old-*`) sunt dinainte de `GameTime` (shake și flash prea repezi) și nu sunt folosite.

## Sloturile (v2)

| slot | în film | durată | clip live | ce rămâne static |
|---|---|---|---|---|
| `s01-intro` | 0:00.0 | 6,9 s | meniul (încetinit ×0,5), holul din spate, vederea laterală la începutul rundei 2 | — |
| `s01-bucla` | 0:06.9 | 4,4 s | harta se deschide → holul → ușa → mâna lui Azazel, tăiate pe „hartă / hol / ușa / cărți” | — |
| `s01-carti` | 0:11.3 | 5,9 s | Lilith trage și joacă o carte, Brimstone, NEW CARD | trapa („THE WAY DOWN IS OPEN”): ar cere un etaj întreg capturat |
| `s01-bosi` | 0:17.2 | 4,0 s | Monstro, Gurdy (Mouth Beam), piciorul lui Mom, mâna lui Mom | — |
| `s02-joc` | 1:28.3 | 4,2 s | Isaac joacă Tear: lacrima, lovitura, Gaper-ul moare, BLOCKED, runda 2 | — |
| `s04-harta` | 4:03.3 | 4,0 s | panoul hărții, etajul 1 (un cadru din clip ținut cu zoom lent: în joc panoul stă deschis ~1 s) | cadrul ținut |
| `s05-seed` | 4:24.8 | 6,6 s | meniul cu SEED 7, zoom pe câmp (încetinit ×0,4) | ecranul de victorie cu SEED 7: ar cere un run întreg capturat |
| `s06-runda` | 7:00.4 | 4,2 s | ROUND 1, bara de ordine, intențiile, primul atac, mâna lui Cain | — |
| `s07-lovitura` | 7:45.5 | 4,2 s | două lovituri ale lui Lilith, fiecare cu clipire albă, număr și moarte în sânge | — |
| `s09-atac` | 10:16.9 | 3,6 s | Cain: carte, salt, lovitura cu număr, întoarcerea; a doua oară omoară | — |
| `s09-impact` | 11:10.1 | 4,2 s | două lovituri critice cu flash, apoi Monstro aterizează peste echipă | — |
| `s10-arc` | 11:52.0 | 4,0 s | camera iese din spatele echipei pe arc până în vederea laterală | — |

Dacă se schimbă vocea sau planul unei scene, timpii se mută: `node tools/check.mjs` îi recalculează, iar `bash tools/capture.sh slots`
reface clipurile la noile durate (ultima bucată din fiecare slot ia restul).
