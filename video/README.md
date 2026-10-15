# Video: Darkest Basement, cum e făcut

Explainer de ~14 minute, în română, 1920×1080 la 30 fps, pentru Radu: fiecare parte a jocului, cum am gândit-o, ce matematică
e în spate și în ce ordine ai învăța ca să-l construiești singur. Scenariul complet, scenă cu scenă: [`SCRIPT.md`](SCRIPT.md).
Clipurile live din joc și cum se capturează: [`FOOTAGE.md`](FOOTAGE.md). Verificarea vocii: [`voice/QA.md`](voice/QA.md). Verificarea sunetului: [`AUDIO-QA.md`](AUDIO-QA.md).

Pipeline-ul e copiat și adaptat din `~/Projects/voost-motion-reels` (motorul de reel-uri JS al lui Radu): fiecare cadru e o funcție
pură de timp desenată în Canvas, randată în Chrome headless pe HP Forge, cu voce ElevenLabs și mixaj eșantion cu eșantion.

## Structura

- `engine/` — `player.html`, `core.js` (încarcă `film/film.js`, expune `window.__film`), `lib.js` (easing, zgomot, keyframes; din reels), fonturile.
- `film/film.js` — filmul: scenele, sloturile de gameplay, planul de sunet (muzica jocului în buclă, vocea, efectele jocului).
- `film/timeline.js` — cronologia calculată din voce: momentul fiecărei replici și al fiecărui cuvânt, sloturile, segmentele de randare.
- `film/scenes/s01.js … s12.js` — cele 12 scene. Fiecare are un `plan` (titlu, replici, pauze, sloturi) și `draw(ctx, t, S)`, legat de cuvinte (`S.w(n, 'cuvânt')`).
- `film/px.js` (pixel art pe grila 640×360 ×3, ca UI-ul jocului), `film/ui.js` (titluri, propoziții, panouri de cod), `film/kit.js` (personaje, cărți, zaruri, bilețele), `film/bg.js` (peretele subsolului, torțe, pergament).
- `film/model.js` — port exact în JS al `GameRng` și `MapGenerator`: scena 4 construiește harta reală a seed-ului 7 (identică cu cea din joc).
- `film/data/snippets.json` — codul arătat pe ecran, extras din repo după ancore (`tools/snippets.py`); `film/data/sim500.json` — rezultatul real al fiecărui seed 1–500 din `tools/sim`.
- `footage.json` — din ce clipuri live e făcut fiecare slot de gameplay (bucăți, tăieturi, efectele sonore puse pe momentele din clip); `tools/slots.py` îl asamblează.
- `assets.json` — manifestul asset-urilor (sprite-uri, texturi, capturi QA seed 7, muzica și efectele jocului); `tools/assets.py` le copiază în `out/assets/`.
- `voice/text/sNN.txt` — textul rostit; `voice/sNN/NN.mp3` + `voice/sNN.lines.json` — vocea pe replici, cu timpii cuvintelor; `voice/sNN.verify.json` + `voice/QA.md` — verificarea Whisper.
- `out/` → symlink spre `/Volumes/Dock & Hub 2TB/work/darkest-basement-video-out` (SSD, gitignored): asset-uri copiate, segmente MP4, mixajul, MP4-ul final, capturi.
  E symlink ca `tools/forge.sh` al jocului (care sincronizează tot repo-ul pe Forge) să nu copieze gigabytes de randări.

## Cum randezi (totul greu rulează pe HP Forge)

Forge găzduiește și producția Voost Control, deci fiecare pas ia lock-ul QA-ului Unity (`/tmp/darkest-basement-unity.lock`, ca randarea
și QA-ul să nu se suprapună) și rulează plafonat (MemoryMax 6G, CPUQuota 500%, RuntimeMaxSec), câte un job, în prim-plan. Dacă QA-ul
ține lock-ul, comanda iese cu 75 după 60 s: reîncearcă mai târziu.

```bash
cd video
python3 tools/assets.py                 # copiază asset-urile în out/assets (o dată, sau după ce se schimbă arta/capturile)
python3 tools/snippets.py               # re-extrage codul din repo (înainte de randarea finală: codul jocului se mai schimbă)
node tools/check.mjs --audio            # verificare rapidă pe Mac, fără browser: durata, scenele, sloturile, muzica
bash tools/forge.sh snap test 120 245.5 # capturi la anumite secunde → out/snap/test/
bash tools/forge.sh sheet v1 2          # 2 cadre din fiecare scenă + planșă → out/snap/v1/sheet.jpg
bash tools/forge.sh render s01 s02 s03  # segmentele scenelor date (fiecare apel < 9 min; împarte scenele pe mai multe apeluri)
bash tools/capture.sh slots             # footage.json → out/footage/<slot>.mp4 pe Forge (clipurile live; vezi FOOTAGE.md)
bash tools/forge.sh mix                 # planul de sunet + out/mix.wav pe Forge, afișează balanța vocii
bash tools/forge.sh finish darkest-basement-explainer-v2.mp4  # plan + mix (cu stem-uri) + lipire + mux → out/…mp4 și out/stems/, aduse pe Mac
node tools/timings.mjs && python3 tools/contact.py out/darkest-basement-explainer-v2.mp4 out/contact-v2.png
python3 tools/audio_qa.py out/darkest-basement-explainer-v2.mp4   # → AUDIO-QA.md (loudness, voce/muzică, efecte peste voce, treceri)
```

Previzualizare cu sunet: servește `video/` (`python3 -m http.server 8791`) și deschide `engine/player.html` (spațiu = play, ←/→ = cadru,
`&t=245` = sare). Pe Mac doar pentru o privire rapidă; Chrome cu 14 minute de film e greu.

## Vocea

Bogdan Gamaleț (`NKQBjfoAhInP7Bjx6P7l`), `eleven_multilingual_v2`, stabilitate 0,5, stil 0,2: o înregistrare pe scenă (ton constant),
tăiată în replici după alinierea pe caractere, cu `previous_text`/`next_text` din scenele vecine. Aleasă după două mostre cu termeni tehnici
(Gamaleț vs Ana Maria): Whisper a transcris-o pe a lui Gamaleț fără nicio greșeală, inclusiv „seed-ul”, pe care la Ana Maria l-a auzit „C-doul”.

```bash
python3 tools/vo.py s04 --voice NKQBjfoAhInP7Bjx6P7l              # regenerează toată scena (după ce schimbi voice/text/s04.txt)
python3 tools/vo.py s04 --voice NKQBjfoAhInP7Bjx6P7l --line 7 --keep-old   # doar replica 7
python3 tools/verify.py s04 --lines 7                               # Whisper (Groq), politica zero diferențe → voice/QA.md
```

Filmul se re-sincronizează singur după o voce nouă: toate animațiile sunt legate de momentele replicilor și ale cuvintelor.

## Scenele și sloturile

Cronologia (după `node tools/check.mjs`): s01 0:00 · s02 0:36 · s03 1:57 · s04 2:44 · s05 4:07 · s06 5:40 · s07 7:05 · s08 8:01 · s09 9:09 · s10 11:14 · s11 12:33 · s12 13:46 · final 14:26. Cele 12 sloturi de gameplay sunt în `FOOTAGE.md`.

## Starea (15.10.2026, v2)

- `out/darkest-basement-explainer-v2.mp4`: 14:26,6, 1920×1080, 30 fps, H.264 CRF 18 + AAC 256k, −14,5 LUFS integrat, true peak −1,2 dBTP, ~465 MB.
  Planșa: `out/contact-v2.png`. v1 rămâne în `out/` pentru comparație.
- Toate cele 12 sloturi de gameplay sunt clipuri live din build-ul de QA (seed 7), capturate la 30 fps fix; două bucăți rămân capturi statice
  (trapa din `s01-carti` și ecranul de victorie din `s05-seed`), pentru că ar cere un run întreg capturat. Detalii: `FOOTAGE.md`.
- Titlul din cold open (0:25) e logo-ul real al jocului care apare pixel cu pixel, cu sânge care picură; rândul de simboluri matematice e centrat.
- Vocea: 103/103 replici verificate cu Whisper (`voice/QA.md`), neschimbată față de v1 (0 credite ElevenLabs în v2).
- Sunetul: vocea stă la o mediană de 18 dB peste muzică; ce merită ascultat e în `AUDIO-QA.md`.
- Codul de pe ecran: `tools/snippets.py` rulat din nou pe codul înghețat. Singurele diferențe vin din `Core.GameTime` (ceasul pentru captura
  video, necomis): 3 rânduri din ScreenShake și HitStop (scena 9). Filmul arată forma comisă, `Time.unscaledTime` / `Time.unscaledDeltaTime`,
  identică cu build-ul v1.0 al lui Radu; dacă `GameTime` intră în repo, rulează `tools/snippets.py` și re-randează s09.

## Captura clipurilor live

Captura țintită din joc (`-capture-moments`, `-capture-jpg` etc.) și ceasul `GameTime` stau pe ramura **`capture-gametime`**. Pe `main` codul jocului rămâne identic cu build-ul v1.0 și cu fragmentele de cod din video. Ca să recapturezi clipuri: `git checkout capture-gametime`, apoi `video/tools/capture.sh`.
