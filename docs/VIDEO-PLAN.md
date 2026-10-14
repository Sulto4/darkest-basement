# Video: cum e făcut Darkest Basement

Ținta: Radu (student la Matematică) înțelege fiecare parte a jocului și știe cum ar construi-o singur. Durează ~12–15 minute, cu voce în română și scene desenate animate (o idee pe ecran, o propoziție), intercalate cu gameplay real.

Principiu: fiecare sistem din joc e o idee matematică pe care o știi deja, doar că pusă la treabă.

## Structura

| # | Scenă | Ideea | Ce se vede | ~Durată |
|---|---|---|---|---|
| 1 | Jocul în 30 de secunde | ce construim | gameplay: hol, ușă, luptă, Brimstone, recompensă | 0:30 |
| 2 | Două lumi: Model și View | logica e separată de ce vezi pe ecran | un creier (Model) care trimite bilețele (evenimente) unui teatru de păpuși (View) | 1:30 |
| 3 | Jocul ca mașină de stări | la orice moment ești într-o singură stare | graf cu stări (Meniu → Hartă → Hol → Luptă → Recompensă) și o bilă care se mută | 1:00 |
| 4 | Harta e un graf | arbore de acoperire aleatoriu + muchii în plus = bucle | grila 5×4 se umple cameră cu cameră; BFS colorează distanțele; boss-ul = cel mai departe | 1:30 |
| 5 | Aleatoriu, dar controlat | un seed = același run; PRNG; fluxuri separate | două run-uri cu seed 42 identice pas cu pas; Fisher–Yates amestecă pachetul | 1:30 |
| 6 | Lupta pe numere | inițiativa = viteză + zar, sortare; formula de damage; statusuri | bara de ture se sortează; formula se construiește termen cu termen | 1:30 |
| 7 | Evenimente și coada de animații | Modelul calculează instant, View-ul povestește încet | o listă de evenimente intră într-o pâlnie și iese ca animații, una câte una | 1:00 |
| 8 | Echilibrare prin simulare | Monte Carlo: 500 de run-uri jucate de un AI | histograme: rată de victorie 43,8%, ~3,5 runde pe luptă | 1:00 |
| 9 | Mișcarea care se simte bine | lerp, easing (funcții pe [0,1]), SmoothDamp, shake = trauma² cu zgomot Perlin | grafice ale curbelor de easing lângă aceeași mișcare | 2:00 |
| 10 | 3D care arată ca pixel art | billboard (vectori), PPU 32, randare la 640×360 și scalare întreagă | camera și sprite-urile plate care se rotesc spre ea; zoom pe pixeli | 0:45 |
| 11 | Cum l-ai construi tu | ordinea de învățare și cum se leagă bucățile | o hartă de învățare, pas cu pas | 1:30 |
| 12 | Final | ce poți schimba singur ca să înveți | 3 exerciții concrete | 0:30 |

## Drumul de învățare (scena 11)

1. **C# simplu** (clase, liste, dicționare, interfețe): scrie Model-ul unei lupte în consolă, fără Unity. Exact cum e `tools/sim`.
2. **Unity de bază**: GameObject + Component, `Update`, `Transform`, `SpriteRenderer`. Pune un sprite care respiră (sinus).
3. **Coada de evenimente**: modelul produce evenimente, View-ul le animează cu corutine.
4. **UI**: Canvas, butoane, o carte care se ridică la hover.
5. **Camera și mișcarea**: lerp, easing, SmoothDamp. Holul din spate.
6. **Graful hărții**: BFS și arbore de acoperire, desenate pe o grilă.
7. **Echilibrare**: simulare cu AutoPlayer, statistici, ajustezi cifrele.

Fiecare pas se poate testa singur. Asta e regula care face un proiect mare să rămână controlabil.

## Exerciții pentru final

- Adaugă o carte nouă în `CardDatabase` și vezi-o în joc (Model → View fără cod nou de UI).
- Schimbă funcția de easing de la tranziția camerei și simte diferența.
- Rulează `tools/sim/run.sh` după ce dublezi HP-ul lui Monstro și citește noua rată de victorie.

## Producție

- Voce: ElevenLabs, voce română, verificată cu Whisper.
- Scene: SVG/Canvas animat în JS, randat cadru cu cadru (stilul explainer-elor Voost).
- Gameplay: build-ul Linux pornit cu `-qa full -capture-fps 30`, cadre randate pe Forge și encodate cu ffmpeg.
- Montaj: timeline în cod, iar randarea finală pe Forge, plafonată.
