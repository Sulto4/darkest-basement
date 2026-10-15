# Darkest Basement: cum e făcut — scenariul

Durata: **14:26.6** (1920×1080, 30 fps). Vocea: Bogdan Gamaleț, ElevenLabs `eleven_multilingual_v2`. 
Textul de mai jos e exact cel rostit (numerele sunt scrise în litere pentru voce; pe ecran apar cu cifre). Timpii sunt calculați din voce (`film/timeline.js`).
Codul arătat pe ecran e extras din repo după ancore (`tools/snippets.py` → `film/data/snippets.json`), nu rescris de mână.

## s01 · Jocul în 30 de secunde  (0:00.0–0:36.9, 37 s)

*Deschidere pe gameplay (capturi din rularea QA cu seed 7, Ken Burns; sloturi pentru clipuri live). Muzica: music_combat.*

| # | timp | narațiune | pe ecran |
|---|---|---|---|
| 1 | 0:00.8 | Asta e Darkest Basement. | **slot `s01-intro`** (gameplay sub voce): Meniul jocului (logo, câmpul SEED = 7). |
| 2 | 0:02.6 | Un joc pe ture în care cobori cu patru eroi într-un subsol plin de monștri. | **slot `s01-intro`** (gameplay sub voce): Holul văzut din spate, apoi vederea laterală cu monștrii (tăieturi pe „cobori” și „monștri”). |
| 3 | 0:07.2 | Alegi o cameră pe hartă, mergi pe hol, deschizi ușa și te bați cu cărți. | **slot `s01-bucla`** (gameplay sub voce): Harta → holul → ușa → mâna de cărți, câte o tăietură pe fiecare cuvânt-cheie. |
| 4 | 0:11.6 | Fiecare erou are pachetul lui. Câștigi o luptă, primești o carte nouă și cobori mai jos. | **slot `s01-carti`** (gameplay sub voce): Cartea jucată, raza Brimstone, recompensa „NEW CARD”, trapa. |
| 5 | 0:17.5 | Trei etaje, trei boși, iar la final te așteaptă Mom. | **slot `s01-bosi`** (gameplay sub voce): Monstro, Gurdy, piciorul lui Mom, lovitura lui Mom. |
| 6 | 0:21.7 | În video-ul ăsta îl desfacem bucată cu bucată. Cum e gândit, ce matematică stă în spate și cum l-ai putea construi tu de la zero. | Peretele subsolului, cei patru eroi și un Gaper intră în cadru; logo-ul se desface în 10 fâșii, din care ies simboluri: G=(V,E), BFS, seed, n!, sort, f∘g, ⌊x⌋, p̂, f(t), x². |
| 7 | 0:29.6 | O să vezi că aproape fiecare piesă e o idee pe care o știi deja de la facultate. Doar că pusă la treabă. | Fâșiile se reasamblează; „cum e făcut, piesă cu piesă”. Propoziția: „fiecare piesă = o idee pe care o știi deja”. |

## s02 · Model și View  (0:36.9–1:57.7, 81 s)

*Două panouri: MODEL (creierul, stare ca date C#) și VIEW (teatru de păpuși cu cortină). Muzica: music_menu.*

| # | timp | narațiune | pe ecran |
|---|---|---|---|
| 1 | 0:39.0 | Prima decizie e și cea mai importantă. Jocul are două lumi separate. | Panourile intră din lateral. |
| 2 | 0:43.7 | Prima lume e Modelul. El e creierul. Știe regulile, ține minte câtă viață are fiecare și ce cărți are în mână. | Creierul din pixeli; apar rândurile `isaac.Hp = 26`, `gaper.Hp = 5`, `isaac.Hand = [Tear, Tear, Spoon Bender]`. |
| 3 | 0:50.6 | Modelul nu știe că există Unity. N-are sprite-uri, n-are cameră, n-are sunet. E C sharp curat, doar numere și liste. | Trei iconițe (sprite, cameră, sunet) tăiate cu roșu pe cuvintele lor. |
| 4 | 0:59.1 | A doua lume e View-ul. Ăsta e teatrul de păpuși. Desenează, animă, cântă și ascultă ce face jucătorul. | Cortina se trage; pe scenă Isaac și un Gaper respiră. Apar pe rând: desenează, animă, cântă, ascultă. |
| 5 | 1:06.3 | Între ele circulă două feluri de mesaje. View-ul trimite comenzi. De exemplu, joacă cartea asta pe ținta asta. | Două benzi între lumi („comenzi”, „evenimente”); bilețelul `PlayCard(isaac, tear, gaper)` zboară din View în Model. |
| 6 | 1:14.0 | Modelul calculează tot pe loc și răspunde cu o listă de evenimente. Cartea a fost jucată. Gaper a luat cinci damage. Gaper a murit. | `gaper.Hp` devine 0; ies trei bilețele: `CardPlayed`, `Damaged 5`, `UnitDied`. |
| 7 | 1:23.0 | View-ul ia lista și o povestește pe ecran, eveniment cu eveniment. | View-ul consumă bilețelele pe rând: Isaac aruncă lacrima, Gaper clipește alb cu „5”, apoi moare (pată de sânge). |
| ▶ | 1:28.3 | *(gameplay, 4.2 s, slot `s02-joc`)* | Gameplay: cartea jucată, lovitura, moartea. |
| 8 | 1:33.0 | De ce ne complicăm așa? Pentru că un Model fără grafică îl poți testa în câteva milisecunde. | View-ul pleacă; terminalul arată testele reale (`PASS …`, `Tests: 35 passed, 0 failed.`). |
| 9 | 1:38.4 | Poți să joci sute de run-uri complete în consolă, fără să deschizi Unity. Revenim la asta mai târziu. | Terminalul continuă cu simularea: `Runs=500`, `Wins=219 (43.8%)`. |
| 10 | 1:45.3 | Și regula nu e doar o promisiune. Modelul stă într-un assembly separat, care n-are voie să vadă motorul. Dacă încerci să folosești ceva din Unity acolo, codul nu compilează. | `DarkestBasement.Model.asmdef` (evidențiat `"noEngineReferences": true`); o încercare `using UnityEngine;` în CombatEngine.cs → `error CS0246`. |

## s03 · Jocul ca mașină de stări  (1:57.7–2:44.4, 47 s)

*Graful stărilor pe pergament (butoanele jocului), cu jetonul lui Isaac; în dreapta captura stării curente. Codul din GameFlow.cs.*

| # | timp | narațiune | pe ecran |
|---|---|---|---|
| 1 | 1:59.8 | Bun. Cum curge jocul de la meniu până la final? | Muchiile se trag, nodurile apar: MENIU, ETAJ NOU, HARTĂ, HOL, CAMERĂ, LUPTĂ, RECOMPENSĂ, GAME OVER, VICTORIE. |
| 2 | 2:03.4 | Gândește-te la el ca la un automat finit, ca la cursul de limbaje formale. | Nodurile pulsează; `δ(stare, intrare) = stare nouă`. |
| 3 | 2:08.4 | La orice moment ești într-o singură stare. Meniu, hartă, hol, cameră, luptă, recompensă. | Jetonul sare din stare în stare pe fiecare cuvânt; în dreapta: meniul, harta, holul, ușa, lupta, recompensa. |
| 4 | 2:15.1 | Fiecare stare e o corutină. O joci de la cap la coadă, iar la final ea spune care e starea următoare. | `IEnumerator Machine(...)`: evidențiat `while (_next != FlowState.None)`, apoi `yield return Safe(Routine(state), …)`. |
| 5 | 2:22.4 | Din hartă pleci pe hol. De pe hol intri în cameră. După luptă iei recompensa și te întorci pe hartă. | Bucla de bază: hartă → hol → cameră → luptă → recompensă → hartă, cu capturile corespunzătoare. |
| 6 | 2:29.5 | Toată bucla asta e un while cu un switch. Atât conduce un run întreg. | `IEnumerator Routine(...)`: `switch (state)` cu `case`-urile. |
| 7 | 2:34.8 | Și GameFlow nu decide nimic. Modelul spune ce e în cameră și cine moare. GameFlow doar pune în scenă și așteaptă alegerea ta. | Comentariul din antetul GameFlow.cs („Modelul decide regulile … GameFlow doar le pune în scenă”); două etichete: Modelul / GameFlow. |

## s04 · Harta e un graf  (2:44.4–4:07.9, 84 s)

*Harta REALĂ a seed-ului 7, generată pas cu pas de un port exact al MapGenerator.cs (film/model.js); pe final, aceeași hartă în joc.*

| # | timp | narațiune | pe ecran |
|---|---|---|---|
| 1 | 2:46.5 | Harta unui etaj e un graf. Camerele sunt noduri pe o grilă de cinci pe patru, iar holurile sunt muchiile. | Grila 5×4; o previzualizare a hărții finale cu etichetele „nod = cameră” și „muchie = hol”. |
| 2 | 2:54.4 | Cum o generăm? Pornim dintr-o cameră aleasă la întâmplare. | Apare camera de start (2, 2); contoarele „camere / muchii”. |
| 3 | 2:59.0 | La fiecare pas ne uităm la locurile libere de lângă camerele deja puse. Alegem unul la întâmplare și îl legăm de vecinul lui. | Frontiera (contur auriu, cu multiplicitatea ×2/×3 a celulelor vecine cu mai multe camere); se alege o celulă și se trage holul. Apoi pașii 2–9 accelerați. Harta se micșorează de la ×3 la ×2 (tot întreg), iar în dreapta apare bucla `while (map.Rooms.Count < count)` cu `rng.Pick(frontier)`. |
| 4 | 3:07.5 | Fiecare cameră nouă vine cu exact o muchie. Deci n camere au n minus unu muchii și totul e conectat. Adică un arbore de acoperire. | Contoarele ajung la 10 camere / 9 muchii, „= n − 1”; arborele se colorează. |
| 5 | 3:16.5 | Doar că într-un arbore, între două camere există un singur drum. N-ai ce alege. Așa că mai adăugăm una sau două muchii. Apar bucle, deci apar alegeri. | Drumul unic 2→0→1→4 în roșu; apoi muchia în plus 2–4 și bucla 0–1–4–2 aurie; „= n, deci o buclă”. |
| 6 | 3:27.0 | Cu o condiție. O muchie nouă intră doar dacă rămân măcar două fundături, pentru că ne trebuie locuri pentru boss și pentru comoară. | Stegulețe pe cele 4 fundături; codul cu `if (leaves < 2) continue;`. |
| 7 | 3:35.1 | Acum, unde punem boss-ul? Cât mai departe de start. | Un „?” mare peste hartă. |
| 8 | 3:38.7 | Aici intră BFS-ul. Startul are distanța zero, vecinii lui unu, vecinii vecinilor doi, și tot așa, cu o coadă. | BFS: cifrele 0, 1, 2, 3 apar pe camere în ordinea reală a cozii; coada BFS e desenată în dreapta, cu codul `Distances()`. |
| 9 | 3:47.2 | Camera cu distanța cea mai mare devine boss-ul. La egalitate alegem o fundătură, ca să nu dai peste boss în drum spre altceva. | Camerele la distanța 3 (ambele fundături); craniul boss-ului cade pe (4, 3). Codul selecției boss-ului. |
| 10 | 3:55.3 | Comoara merge în altă fundătură. Restul camerelor se împart în lupte, un pat pentru odihnă și un curio. | Coroana comorii, săbiile luptelor, patul, curio-ul (caca). |
| ▶ | 4:03.3 | *(gameplay, 4.0 s, slot `s04-harta`)* | Harta din joc pentru seed 7 (aceeași formă). |

## s05 · Aleatoriu, dar controlat  (4:07.9–5:40.7, 93 s)

*Seed, generatorul xorshift pe starea reală a GameRng(7), restul împărțirii, fluxurile Fork, Fisher–Yates pe pachetul de start al lui Isaac.*

| # | timp | narațiune | pe ecran |
|---|---|---|---|
| 1 | 4:10.0 | Acum, norocul. Un roguelike trebuie să fie altfel la fiecare run, dar noi vrem să-l putem și repeta. | Trei hărți reale pentru seed 3, 42 și 2024 (diferite). |
| 2 | 4:16.9 | Soluția e un seed. Un singur număr de la care pornește tot. Același seed înseamnă același run, pas cu pas. | Două hărți „seed 7” care cresc identic, cameră cu cameră; semnul „=”. |
| 3 | 4:25.2 | Asta te salvează la teste. Dacă un bug apare pe seed-ul șapte, îl reproduci de câte ori vrei. | **slot `s05-seed`** (gameplay sub voce):  |
| 4 | 4:32.0 | În spate e un generator pseudo-aleator foarte mic. Ia un număr pe treizeci și doi de biți și îl amestecă cu trei deplasări de biți și sau exclusiv. | „GameRng: xorshift pe 32 de biți”; cei 32 de biți ai stării reale (0xA3C59AC4) se deplasează și se combină prin XOR: `x ^= x << 13`, `>> 17`, `<< 5`; codul `Next()`. |
| 5 | 4:42.2 | Nu e aleator de-adevăratelea. E o funcție deterministă care doar arată aleator. | Același lanț de numere (valorile reale ale lui Next()) de două ori, identic. |
| 6 | 4:48.2 | Un detaliu fin. Ca să alegi un număr între zero și n, nu iei pur și simplu restul împărțirii, pentru că unele resturi ar ieși mai des. Codul aruncă câteva valori și mai încearcă o dată. | Un generator de jucărie cu 8 valori, „rest la 3”: coșurile primesc 3, 3, 2; valorile 0 și 1 sunt respinse și coșurile devin egale; rândurile `limit` și `do … while` din `Range()`. |
| 7 | 5:00.7 | Apoi, fluxuri separate. Din seed-ul principal facem trei generatoare. Unul pentru hartă, unul pentru luptă și unul pentru recompense. | seed 7 se ramifică în `Fork("map")`, `Fork("combat")`, `Fork("loot")`, fiecare cu primele valori reale `Range(0, 100)`; rândul din `Run.StartRun`. |
| 8 | 5:10.1 | Așa, dacă se schimbă ceva în luptă, harta rămâne la fel. Altfel o singură aruncare de zar în plus ar muta tot restul run-ului. | O aruncare în plus în fluxul de luptă: valorile lui se schimbă (roșu); hartă și recompense: „✓ la fel”. |
| 9 | 5:19.2 | Pentru pachetul de cărți folosim Fisher-Yates. Mergi de la ultima carte spre prima. Pentru fiecare poziție i alegi un j între zero și i, și le schimbi între ele. | Cele cinci cărți de start ale lui Isaac (Tear, Tear, Yum Heart, The Sad Onion, Spoon Bender); i merge de la ultima spre prima, zarul dă j = Range(0, i+1) din fluxul de luptă al seed-ului 7, cărțile se schimbă în arc; codul `Shuffle`. |
| 10 | 5:32.1 | Toate cele o sută douăzeci de permutări ale celor cinci cărți ies cu aceeași probabilitate. Și sunt trei rânduri de cod. | „5! = 120”, „fiecare ordine: probabilitate 1/120”. |

## s06 · Lupta pe numere  (5:40.7–7:05.1, 84 s)

*Scena de luptă laterală (eroii stânga, inamicii dreapta). Exemplul de inițiativă e ales cu egalități, ca să se vadă comparatorul.*

| # | timp | narațiune | pe ecran |
|---|---|---|---|
| 1 | 5:42.8 | Lupta. Fiecare rundă începe cu inițiativa. Fiecare personaj aruncă un zar de la zero la trei și îl adună la viteza lui. | Viteza fiecăruia, zarurile 0–3, sumele: Lilith 4, Isaac 7, Cain 7, Azazel 6, Gaper 6, Fly 10, Pooter 7. |
| 2 | 5:52.0 | Apoi sortăm descrescător. Asta e toată ordinea turelor. O sortare. | Portretele urcă într-o bară și se sortează descrescător: Fly, Cain, Isaac, Pooter, Azazel, Gaper, Lilith. |
| 3 | 5:58.1 | La egalitate merg întâi eroii, iar dacă tot e egal, decide id-ul. Comparatorul trebuie să fie o ordine totală, pentru că sortarea din C sharp nu e stabilă. Altfel aceeași rundă ar putea ieși diferit. | Egalitatea la 7: eroii întâi, apoi id-ul („cain” < „isaac”); comparatorul real din `TurnOrder.Roll`. |
| 4 | 6:12.6 | Damage-ul e o compunere de funcții. Pornești de la baza cărții și aduni Strength-ul. | Cain (Strength 2) și un Gaper (Mark 2, Block 4); banda de funcții pornește: baza 5, +Strength → 7. |
| 5 | 6:18.5 | Dacă atacatorul e slăbit, înmulțești cu zero virgulă șaptezeci și cinci. Dacă ținta e marcată, cu unu virgulă cinci. Crit-ul, încă o dată unu virgulă cinci. | ×0,75 (sărit, „nu e” slăbit), ×1,5 marcat → 10,5, ×1,5 crit → 15,75. |
| 6 | 6:28.9 | Rotunjești în jos, și abia la final scazi Block-ul. | Rotunjire → 15, −Block → 11; Cain lovește, „11!” galben, HP 27 → 16. |
| 7 | 6:33.2 | Fiecare pas e o funcție mică. Le poți testa separat, dar nu le poți muta oricum, pentru că rotunjirea nu comută cu înmulțirea. | `D = ⌊ crit ∘ marcat ∘ slăbit ∘ (+Str) (bază) ⌋ − Block`; contraexemplul `⌊1,5·⌊1,5·5⌋⌋ = 10` dar `⌊1,5·1,5·5⌋ = 11`; codul `DamageCalculator.Calculate`. |
| 8 | 6:42.3 | Statusurile sunt doar opt numere într-un vector. Block, Bleed, Poison, Stun și celelalte. Cele mai multe scad cu unu pe tură. | Vectorul de 8 statusuri cu iconițele jocului; la „scad” Bleed 2→1, Poison 3→2, Weak 1→0, Mark 2→1, Block 5→0; `new int[8]`. |
| 9 | 6:51.3 | Iar inamicii își arată intenția de la începutul rundei. Știi dinainte pe cine vrea Gaper să muște. Asta transformă lupta într-un puzzle. | Intențiile deasupra inamicilor; linia punctată de la Gaper la Azazel. |
| ▶ | 7:00.4 | *(gameplay, 4.2 s, slot `s06-runda`)* | Gameplay: bara de ture și intențiile. |

## s07 · Coada de animații  (7:05.1–8:01.8, 57 s)

*Azazel joacă Blood Beam; evenimentele sunt cele 7 pe care le produce CombatEngine.PlayCard, în ordinea reală.*

| # | timp | narațiune | pe ecran |
|---|---|---|---|
| 1 | 7:07.2 | Când joci o carte, Modelul rezolvă totul pe loc. Damage, morți, mutări, statusuri. Într-o fracțiune de milisecundă. | Cartea intră în Model; ies instant 7 bilețele: EnergyChanged −2, CardPlayed, Damaged gaper 5, Damaged fly 4, UnitDied fly, UnitMoved pooter 3→2, StatusApplied Bleed 2; „≈ 0,02 ms”. |
| 2 | 7:15.6 | Dar dacă le-ai arăta pe toate deodată, n-ai înțelege nimic. Așa că View-ul le pune la coadă. | Toate animațiile deodată (haos, „?!”, shake), apoi bilețelele se aliniază într-o coadă. |
| 3 | 7:21.9 | Ia primul eveniment, îl animă, așteaptă să se termine, apoi trece la următorul. | Capul cozii coboară pe scenă și își joacă animația; coada avansează. |
| 4 | 7:29.1 | În Unity asta se face cu corutine. O funcție care face yield return se oprește acolo și continuă când e gata animația. | `IEnumerator Play(...)`: bucla `for` și `yield return Safe(PlayOne(events[i]))`. |
| 5 | 7:36.9 | Un switch alege animația pentru fiecare tip de eveniment. Damaged înseamnă un număr care sare, o clipire albă, puțin sânge și un shake. | `switch` din `PlayOne` cu `case Damaged ev`; pe Gaper: număr, clipire albă, sânge, shake (etichete pe cuvinte). |
| ▶ | 7:45.5 | *(gameplay, 4.2 s, slot `s07-lovitura`)* | Gameplay: lovitura, moartea, raza. |
| 6 | 7:50.2 | Și încă ceva. Dacă o animație aruncă o excepție, lupta merge mai departe. Fiecare pas e învelit într-un try. Un bug vizual n-are voie să-ți blocheze jocul. | `Safe(...)` cu `try`/`catch`; un bilețel roșu „crapă”, iar coada merge mai departe. |

## s08 · Echilibrare prin simulare  (8:01.8–9:09.4, 68 s)

*Grila de 500 de celule = seed-urile 1..500 jucate de AutoPlayer. Culorile sunt rezultatele reale (film/data/sim500.json, rulat cu tools/sim): victorie, mort în Caves, mort în Depths.*

| # | timp | narațiune | pe ecran |
|---|---|---|---|
| 1 | 8:03.9 | Cum știi dacă jocul e prea greu? Nu-l joci tu de o mie de ori. Îl pui pe calculator să-l joace. | Grila goală. |
| 2 | 8:10.0 | AutoPlayer e un jucător simplu și lacom. Dă damage unde contează, se vindecă atunci când e jos și pune Block când vede că vine o lovitură. | Cele trei reguli ale politicii, cu rândurile de scor din `AutoPlayer.Score`. |
| 3 | 8:18.6 | Pentru că Modelul nu depinde de Unity, simulatorul joacă cinci sute de run-uri complete în câteva secunde. | Grila se umple seed cu seed; „câteva secunde, fără Unity”. |
| 4 | 8:25.7 | Asta e metoda Monte Carlo. Nu calculezi probabilitatea exact. O estimezi din foarte multe încercări. | p̂ după n run-uri (curba reală) converge spre 0,438, în banda de încredere 95%. |
| 5 | 8:33.7 | Prima versiune a dat zero victorii din cinci sute. Luptele se terminau în două runde și ceva. Prea brutal. | Prima versiune: toată grila roșie, „0 / 500”, „2,33 runde pe luptă”. |
| 6 | 8:41.0 | Am crescut viața inamicilor cu vreo cincizeci la sută și am scăzut damage-ul. Acum botul câștigă două sute nouăsprezece din cinci sute. Adică patruzeci și trei virgulă opt la sută. | Glisoarele „HP inamici +50%” și „damage −”; grila revine la rezultatele reale; „219 / 500 = 43,8%”. |
| 7 | 8:53.0 | O luptă normală ține în medie trei runde și jumătate, iar una cu boss aproape șapte. Exact cât ne-am propus. | Histograma mediei de runde pe luptă normală (500 de run-uri), ținta 3–4; boss 6,78 (Monstro 6,22 · Gurdy 6,59 · Mom 7,72). |
| 8 | 9:00.1 | Cu cinci sute de run-uri, eroarea e de câteva procente. De asta am verificat și pe alte seed-uri. A ieșit cam cincizeci la sută, tot în țintă. | Trei loturi cu intervale de încredere: 43,8% (1–500), 51,0% (1001–1500), 49,8% (3001–3500), toate în ținta 35–55%; „eroarea standard ≈ 2,2%”. |

## s09 · Mișcarea care se simte bine  (9:09.4–11:14.8, 125 s)

*Grafice pe [0,1], o pistă de la a la b, atacul lui Azazel cu timpii din UnitView, SmoothDamp, trauma.*

| # | timp | narațiune | pe ecran |
|---|---|---|---|
| 1 | 9:11.5 | Acum partea care face un joc să se simtă bine. Și e mai multă matematică decât ai crede. | Azazel respiră. |
| 2 | 9:17.2 | Totul pornește de la lerp. Interpolare liniară. a plus, b minus a, ori t. | Formula `lerp(a, b, t) = a + (b − a) · t`, cu t care crește de la 0 la 1. |
| 3 | 9:23.4 | Pentru t de la zero la unu mergi pe segment de la a la b. Dacă t crește uniform, mișcarea arată robotic. Pornește brusc și se oprește brusc. | f(t) = t: Isaac merge uniform; urmele egal depărtate arată viteza constantă. |
| 4 | 9:33.2 | Trucul e să treci t printr-o funcție de easing. O funcție de la zero, unu în zero, unu, cu f de zero egal cu zero și f de unu egal cu unu. | `f : [0, 1] → [0, 1], f(0) = 0, f(1) = 1`. |
| 5 | 9:43.5 | Ease out cubic pleacă repede și frânează moale. E unu minus, unu minus t, la puterea a treia. | Curba OutCubic se desenează peste diagonală; urmele se îndesesc la final; rândul din Easing.cs. |
| 6 | 9:51.0 | Ease out back trece puțin de unu și revine. De aici vine senzația de pop. | OutBack trece de 1 (linia punctată); o carte „pop”-ează. |
| 7 | 9:57.2 | Când un erou atacă, întâi se trage puțin înapoi, ca un arc care se încarcă. Apoi sare spre țintă pe ease out cubic, într-o zecime de secundă, și se întoarce acasă lin. | Atacul încetinit ×5: anticipare (OutQuad 0,16 s), salt (OutCubic 0,1 s), întoarcere (InOutQuad 0,22 s). |
| 8 | 10:08.8 | Pe deasupra se turtește și se lungește puțin. Iar când stă, respiră. Scara lui pe verticală urmează un sinus. | Squash 1,12/0,88 și stretch 0,85/1,14; patru eroi respiră, fiecare cu sinusul lui (faze diferite). |
| ▶ | 10:16.9 | *(gameplay, 3.6 s, slot `s09-atac`)* | Gameplay: anticipare, salt, lovitură. |
| 9 | 10:21.1 | Camera folosește SmoothDamp. Gândește-te la un arc cu amortizare critică. Ajunge la țintă cât mai repede, fără să treacă de ea și fără să oscileze. | Răspunsul la o treaptă: subamortizat (oscilează), supraamortizat (lent), critic (SmoothDamp). |
| 10 | 10:30.9 | Pe hol, camera urmărește echipa cu un timp de netezire de zero virgulă douăzeci și doi de secunde. Ținta sare, camera nu. | „smoothTime = 0,22 s” și rândul cu `Vector3.SmoothDamp` din CameraRig. |
| 11 | 10:40.1 | Și shake-ul. Fiecare lovitură adaugă trauma, un număr între zero și unu care scade în timp. | Graficul traumei: trei lovituri (+0,25, +0,55, +0,3), scădere liniară cu 1,5/s. |
| 12 | 10:46.4 | Camera nu tremură cu trauma, ci cu trauma la pătrat. Loviturile mici abia se simt, cele mari se simt mult. | trauma² (auriu) sub trauma: loviturile mici aproape dispar; codul `float shake = Trauma * Trauma;`. |
| 13 | 10:53.5 | Direcția vine din zgomot Perlin, nu din random. Zgomotul e continuu, așa că imaginea tremură, nu sare dintr-un loc în altul. | Random (sare) lângă zgomot continuu (tremură). |
| 14 | 11:02.1 | Pune peste asta un hit-stop, timpul înghețat câteva zeci de milisecunde, și creierul citește lovitura ca având greutate. | Cain lovește Gaper: 0,1 s înghețat (banda de cadre), apoi shake; codul `HitStop.Freeze`. |
| ▶ | 11:10.1 | *(gameplay, 4.2 s, slot `s09-impact`)* | Gameplay: crit, impactul lui Monstro. |

## s10 · 3D care arată a pixel art  (11:14.8–12:33.8, 79 s)

*Diagramă văzută de sus (holul, camera, vectorul f, quad-urile eroilor), arcul camerei, pixelii măriți.*

| # | timp | narațiune | pe ecran |
|---|---|---|---|
| 1 | 11:16.9 | Holul e 3D adevărat, dar personajele sunt plate. Sunt billboard-uri. Un dreptunghi cu sprite-ul pe el. | Holul din joc (captură) și diagrama de sus. |
| 2 | 11:24.0 | La fiecare frame îl rotim după cameră. Iei direcția în care se uită camera, pui componenta verticală pe zero, ca să rămână drept, și îl aliniezi cu ea. | Camera se rotește; quad-urile rămân perpendiculare pe f; codul `f.y = 0f` + `LookRotation`. |
| 3 | 11:34.5 | Atenție, nu se uită spre cameră. E paralel cu planul ei. Așa sprite-urile de pe margini nu se deformează. | Comparația: „privește spre cameră” (pe margini se rotesc diferit) vs paralel cu planul camerei. |
| 4 | 11:41.5 | Când intri în luptă, camera iese din spatele echipei pe un arc, ca o macara. Unghiul și raza se interpolează separat, iar înălțimea mai primește un sinus care o ridică la mijloc. | Arcul de sus (pivot, rază, „din spate” → „lateral”) și formulele θ(k), r(k), h(k) = … + 0,9·sin(πk), k = InOutCubic(t); codul din `Orbit`. |
| ▶ | 11:52.0 | *(gameplay, 4.0 s, slot `s10-arc`)* | Gameplay: camera trece din spate în lateral. |
| 5 | 11:56.6 | Acum pixelii. Treizeci și doi de pixeli de artă fac o unitate în lume. Interfața e desenată la șase sute patruzeci pe trei sute șaizeci. | Isaac 48×48 mărit ×12 cu grila de pixeli; „32 px = 1 unitate”; „640 × 360”. |
| 6 | 12:05.6 | Apoi o mărim cu un factor întreg. Pe Full HD, ori trei. Fiecare pixel devine un pătrat perfect de trei pe trei. | Fața lui Isaac la ×3: fiecare pixel = 3×3. |
| 7 | 12:13.5 | Cu un factor ca doi și jumătate, unele coloane ar ieși mai late decât altele, și se vede imediat. De asta doar numere întregi, iar ce rămâne pe margini e bandă neagră. | Aceeași față la ×2,5: coloane de 2 și 3; un ecran 1366×768 cu bandă neagră; codul `UpdateScale`. |
| 8 | 12:24.7 | Chiar și camera de luptă e calculată așa. Vede exact zece unități în lățime, deci fiecare pixel de artă iese doi pe doi. | Vederea laterală din joc cu o riglă de 10 unități; „64 px pe unitate = 2 × 2 pe pixel de artă”. |

## s11 · Cum l-ai construi tu  (12:33.8–13:46.5, 73 s)

*Drumul de învățare ca o hartă de etaj pe pergament; Isaac merge din cameră în cameră; în dreapta, o mică demonstrație a fiecărui pas.*

| # | timp | narațiune | pe ecran |
|---|---|---|---|
| 1 | 12:35.9 | Bun. Dacă ar fi să pornești de la zero, în ce ordine ai învăța? | Harta cu 7 camere și trapa „jocul tău”. |
| 2 | 12:41.0 | Unu. C sharp simplu, fără Unity. Clase, liste, dicționare. Scrie o luptă în consolă, cu doi eroi și un inamic. Exact ce face simulatorul. | Un log de luptă în consolă. |
| 3 | 12:51.3 | Doi. Unity de bază. Ce e un GameObject, ce e o componentă, ce face Update. Pune un sprite care respiră cu un sinus. | GameObject cu componente; un Isaac care respiră. |
| 4 | 12:59.6 | Trei. Leagă cele două lumi cu o coadă de evenimente. Lupta ta din consolă produce evenimente, iar o corutină le animă una câte una. | Bilețele de evenimente care pleacă spre animație. |
| 5 | 13:08.6 | Patru. Interfața. Un Canvas, câteva butoane, o carte care se ridică atunci când treci cu mouse-ul peste ea. | Trei cărți; cea din mijloc se ridică la hover. |
| 6 | 13:16.6 | Cinci. Camera și mișcarea. Lerp, easing, SmoothDamp. Fă holul văzut din spate și tranziția în lateral. | Curba OutCubic. |
| 7 | 13:24.9 | Șase. Harta. BFS și arborele de acoperire, desenate pe o grilă. | Un mini-graf cu distanțe BFS. |
| 8 | 13:31.3 | Șapte. Echilibrarea. Lași un bot să joace, strângi statistici și ajustezi cifrele. | O grilă de run-uri. |
| 9 | 13:38.3 | Regula care leagă totul e simplă. Fiecare pas se poate testa singur. Așa rămâne un proiect mare sub control. | Isaac ajunge la trapă; „fiecare pas se testează singur ✓”. |

## s12 · Temă pentru acasă  (13:46.5–14:26.6, 40 s)

*Trei cărți-exercițiu, cu fața în jos, care se întorc pe rând; sub fiecare, rândul de cod de la care pornești. Muzica: music_boss.*

| # | timp | narațiune | pe ecran |
|---|---|---|---|
| 1 | 13:48.5 | Ca să-l faci al tău, trei exerciții. | Cele trei cărți intră cu fața în jos. |
| 2 | 13:51.3 | Unu. Adaugă o carte nouă în CardDatabase și joac-o. Textul ei se scrie singur din efecte, fără nicio linie nouă de interfață. | Cartea 1 („O carte nouă”) + rândul `Add("tear", …)` din CardDatabase. |
| 3 | 14:01.1 | Doi. Schimbă curba de easing de la tranziția camerei. Pune ease out back sau chiar liniar, și simte diferența. | Cartea 2 („Altă curbă”) + `}, Ease.InOutCubic, () =>` din CameraRig.Orbit → `Ease.OutBack` sau `Ease.Linear`. |
| 4 | 14:09.6 | Trei. Dublează viața lui Monstro, rulează simulatorul și citește noua rată de victorie. | Cartea 3 („Monstro ×2”) + rândul lui Monstro din EnemyDatabase, 260 → 520, `tools/sim/run.sh`, „Wins = ?”. |
| 5 | 14:17.0 | Dacă știi să explici de ce s-a schimbat cifra, ai înțeles jocul. Spor la coborât. | Cărțile cad; logo-ul jocului, trapa, echipa coboară; „Spor la coborât.” |
