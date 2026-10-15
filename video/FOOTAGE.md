# Clipuri live de capturat (pentru coordonator)

Prima versiune folosește capturi statice din rularea QA cu **seed 7** (`Logs/qa/full-7`), cu Ken Burns. Fiecare slot de gameplay
e un segment separat al filmului: când există `video/out/footage/<slot>.mp4`, randarea îl folosește în locul capturilor.
Sunetul (vocea, muzica, efectele) e mixat separat și nu depinde de clip: clipul trebuie să fie **mut** și să aibă exact durata slotului.

## Cum le capturezi

1. Build-ul de QA, cu cadre la 30 fps (pe Forge, prin unealta jocului, care ia lock-ul `/tmp/darkest-basement-unity.lock`):
   `tools/forge.sh qa tour 7 -capture-fps 30` pentru explorare și prima luptă; `tools/forge.sh qa combat 7 -capture-fps 30` pentru momentele de luptă;
   pentru boși, `qa full 7 -capture-fps 30` (lung; vezi atenția de mai jos). Cadrele ies în `Logs/qa/<script>/frames/frame_00000.png`.
2. Găsești momentul în `Logs/qa/<script>/qa-log.txt`: rândurile `captură N <tag>` au timpul de joc în față (`[12.4s]`); cu `-capture-fps 30`,
   cadrul e aproximativ `timp × 30`. Taie puțin înainte de moment.
3. `bash video/tools/footage.sh <slot> <dir-cadre-pe-Forge> <primul-cadru>` → `video/out/footage/<slot>.mp4` (exact cât slotul).
4. `bash video/tools/forge.sh render <slot>` (doar segmentul acela) și `bash video/tools/forge.sh final darkest-basement-explainer-v2.mp4`.

**Atenție la disc:** un cadru PNG 1920×1080 are ~2 MB; un `qa full` capturat întreg ar trece de 100 GB, iar Forge are ~40 GB liberi.
Pentru boși, pornește captura doar pe porțiunea necesară sau șterge cadrele imediat după ce tai clipurile. Nu aduce folderul `frames/` pe Mac.

## Sloturile (timpii din montajul v1; durata exactă e în `video/out/segments.json`)

Ordinea priorității: primele patru contează cel mai mult, pentru că o captură statică nu poate arăta mișcarea despre care vorbește vocea.

| prioritate | slot | în film | durată | ce trebuie să se vadă | captură / seed | peste ce voce |
|---|---|---|---|---|---|---|
| 1 | `s10-arc` | 11:52.0 | 4,0 s | Intrarea într-o cameră de luptă: camera iese din spatele echipei pe arc, cu dutch angle la mijloc, până în vederea laterală; monștrii apar. Clipul ideal începe cu ~0,5 s de hol. | `tour`, seed 7, prima `room_combat` → `side_view` | după „…un sinus care o ridică la mijloc” (fără voce) |
| 2 | `s09-atac` | 10:16.9 | 3,6 s | Un erou atacă din apropiere: anticipare (se trage înapoi), salt spre țintă, lovitura cu număr, întoarcerea. Ideal Azazel cu Brimstone Swipe sau Cain cu Lucky Punch. | `combat`, seed 7 | fără voce (după scena despre squash & respirație) |
| 3 | `s09-impact` | 11:10.1 | 4,2 s | Un crit (număr galben cu „!”, flash alb, hit-stop, shake), apoi o lovitură de boss (Monstro). | `combat` / `full`, seed 7 (`combat_crit`, `combat_boss_impact`) | fără voce (după hit-stop) |
| 4 | `s07-lovitura` | 7:45.5 | 4,2 s | O lovitură obișnuită urmată de o moarte: număr care sare, clipire albă, sânge pe jos, shake mic. | `combat`, seed 7 (`combat_hit` → `combat_kill`) | fără voce |
| 5 | `s02-joc` | 1:28.3 | 4,2 s | Isaac joacă o carte (Tear) pe un Gaper: cartea zboară, lacrima, lovitura, moartea. | `combat`, seed 7 | fără voce |
| 6 | `s06-runda` | 7:00.4 | 4,2 s | Începutul unei runde: bara de portrete de sus se reordonează, apar intențiile deasupra inamicilor, apoi tura unui erou cu mâna de cărți. | `combat`, seed 7 (`combat_round`, `combat_hero_turn`) | fără voce |
| 7 | `s01-intro` | 0:00.0 | 6,9 s | Meniul (2 s), echipa pe hol din spate (2,5 s), vederea laterală cu monștrii (2,4 s). Un montaj e în regulă. | `tour`, seed 7 | „Asta e Darkest Basement. Un joc pe ture…” |
| 8 | `s01-bucla` | 0:06.9 | 4,4 s | Harta (alegerea camerei) → holul → ușa care se deschide → mâna de cărți. Tăieturi pe cuvinte: „hartă” 0:08.1, „hol” 0:09.0, „ușa” 0:09.8, „cărți” 0:10.8. | `tour`, seed 7 | „Alegi o cameră pe hartă, mergi pe hol…” |
| 9 | `s01-carti` | 0:11.3 | 5,9 s | O carte jucată, raza Brimstone, ecranul „NEW CARD”, trapa care se deschide. | `tour` / `full`, seed 7 | „Fiecare erou are pachetul lui…” |
| 10 | `s01-bosi` | 0:17.2 | 4,0 s | Monstro, Gurdy, piciorul lui Mom, câte ~1 s. | `full`, seed 7 (doar porțiunile cu boși) | „Trei etaje, trei boși…” |
| 11 | `s04-harta` | 4:03.3 | 4,0 s | Panoul hărții pe etajul 1 al seed-ului 7 (aceeași formă ca harta construită în scena 4). Un zoom lent e suficient; static e acceptabil. | `tour`, seed 7, `map` | fără voce |
| 12 | `s05-seed` | 4:24.8 | 6,6 s | Meniul cu „7” tastat în câmpul SEED (~3 s), apoi ecranul de final al run-ului cu SEED 7 (~3,6 s). | `full`, seed 7 (meniu + `victory`) | „Asta te salvează la teste…” |

Timpii din coloana „în film” se mută dacă vocea sau planul unei scene se schimbă; `node video/tools/check.mjs` îi recalculează.
