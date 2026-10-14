# Fonturile jocului

Toate sunt fonturi pixel cu licența **SIL Open Font License 1.1 (OFL-1.1)**. Le-am descărcat din repo-ul oficial Google Fonts
(`https://github.com/google/fonts`, folderul `ofl/<nume>/`) pe 2026-10-14. Lângă fiecare font, în același folder, este
fișierul de licență original `OFL.txt`. Niciun font nu declară „Reserved Font Name”, deci redenumirea fișierelor e permisă.

| Folder în `Assets/_Game/Resources/Fonts/` | Fișier | Familie, versiune | Autor (copyright) | Licență | Mărimea la care pixelii cad pe grilă |
|---|---|---|---|---|---|
| `Micro5/` | `Micro5-Regular.ttf` | Micro 5, v1.001 | Copyright 2023 The Soft Type Project Authors (Sarah Cadigan-Fried), github.com/scfried/soft-type-micro | OFL-1.1 (`Micro5/OFL.txt`) | **11 px** (majuscule de 5 px) |
| `Silkscreen/` | `Silkscreen-Regular.ttf`, `Silkscreen-Bold.ttf` | Silkscreen, v1.001 | Copyright 2001 The Silkscreen Project Authors (Jason Kottke), github.com/googlefonts/silkscreen | OFL-1.1 (`Silkscreen/OFL.txt`) | **8 px** (doar majuscule, 5 px) |
| `PixelifySans/` | `PixelifySans-Variable.ttf` (în repo: `PixelifySans[wght].ttf`) | Pixelify Sans, v1.000, variabil 400–700 | Copyright 2021 The Pixelify Sans Project Authors (Stefie Justprince), github.com/eifetx/Pixelify-Sans | OFL-1.1 (`PixelifySans/OFL.txt`) | ~20 px (grila nu e întreagă: e pentru meniuri mari, nu pentru text de 1×) |
| `JacquardaBastarda9/` | `JacquardaBastarda9-Regular.ttf` | Jacquarda Bastarda 9, v1.001 | Copyright 2023 The Soft Type Project Authors (Sarah Cadigan-Fried), github.com/scfried/soft-type-jacquarda-bastarda | OFL-1.1 (`JacquardaBastarda9/OFL.txt`) | **13 px** (sau 26 px) |

## La ce folosim fiecare

- **Micro 5** — textul mic de pe cărți (nume + descriere) și din tooltip-uri. O carte are 56 px lățime de text:
  „Deal 12 damage to” încape pe un rând, iar 4 rânduri de 7 px încap în cutia de text. E fontul de corp principal.
- **Silkscreen** (Regular/Bold) — numere și etichete scurte în majuscule: costul de pe bulina cărții, HP `18/24`,
  numărul de lângă iconițe, „END TURN”, „ROUND 1”. Bold pentru cifrele care trebuie să sară în ochi.
- **Pixelify Sans** — titluri de meniu și texte mari (≥ 20 px la referința 640×360). Are litere mici frumoase, dar la mărimi
  mici pixelii nu cad pe grilă, deci nu-l folosim pentru textul de pe cărți.
- **Jacquarda Bastarda 9** — fontul „afișaj”: o gotică pixel, mai zgâriată, pentru titluri de ecran (Victory, numele etajului,
  ecranul de final). Logo-ul „DARKEST BASEMENT” nu e scris cu un font: e desenat de mână în `ui.py` (`title_logo.png`).

## Setări în Unity (TextMeshPro)

- Font Asset generat cu **Render Mode = RASTER (hinted)** sau **SDF** cu filtru Point pe atlas; mărimea de eșantionare = mărimea
  nativă din tabel (sau un multiplu întreg). Pe Canvas-ul de 640×360 scalat ×3, folosește exact mărimea nativă × scara, ca să
  rămână pixel-perfect.
- Pixelify Sans e un font variabil: TMP folosește instanța implicită (Regular 400). Dacă vrem Bold, generăm un Font Asset separat
  dintr-o instanță statică.

## Verificare

`tools/art/envkit.py` (`FONT_FILES`) conține mărimile native; `ui.py` le folosește la planșe și la `docs/art-review/card-ui-mock.png`,
randate cu `fontmode = "1"` (fără anti-aliasing).
