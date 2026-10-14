# tools/art — generatoarele de pixel art

Sprite-urile din joc **nu se desenează de mână în editor**: le scriu scripturile de aici
(Python 3 + Pillow). Scripturile sunt sursa de adevăr; PNG-urile din `Assets/` se regenerează
oricând, identic la fiecare rulare. Regulile de stil sunt în `docs/ART.md`.

## Regenerare (din rădăcina repo-ului)

| Set | Comandă | Scrie |
|---|---|---|
| Eroi + Incubus | `python3 tools/art/heroes.py` | `Assets/_Game/Resources/Sprites/Heroes/<id>/*.png`, `Sprites/Familiars/incubus/*.png`, `docs/art-review/heroes.png`, `docs/art-review/heroes-in-context.png` |

Fiecare set nou adaugă aici un rând și produce planșa lui ×6 în `docs/art-review/`.
Nu scrie fișiere `.meta` (le face Unity) și nu folosește nimic aleator: aceeași comandă dă aceiași octeți.

## Fișiere

- `palette.py` — paleta din ART.md (constante hex). Culorile noi sunt doar nuanțe ale ei (`pk.tint`, `pk.mix`).
- `pixkit.py` — trusa comună (mai jos).
- `heroes.py` — eroii văzuți din spate și din profil, portretele și familiarul. E și un exemplu de cum se folosește trusa.

## pixkit pe scurt

```python
import pixkit as pk
c = pk.Canvas(48, 48)                       # pânză cu paletă indexată (0 = transparent)
m = pk.ellipse(11, 11, 36, 32)              # formele sunt MĂȘTI: mulțimi de pixeli (x, y)
pk.shade_sphere(c, m, [hi, base, shadow, deep])   # umbrire de sferă, lumina din stânga-sus
pk.stamp(c, 24, 19, [".KK.", "KWWK"], {"K": "#1d1a2e", "W": "#d6ecff"})  # pixeli puși de mână
pk.outline(c)                               # contur automat de 1 px în culoarea OUTLINE
pk.save_png(c, "out.png")
```

- **Măști**: `ellipse`, `rect`, `line`, `polyline`, `polygon`, `blob` (metaball); operații de mulțimi Python (`|`, `&`, `-`) și `shift`, `inner_edge`, `outer_edge`, `edge_band(mask, dx, dy)` (banda care „iese” din mască la deplasare: umbra/lumina de margine).
- **Pânză**: `fill`, `recolor`, `erase`, `paste`, `mask(color)`, `colors()`, plus scurtăturile `c.ellipse/rect/line/polygon/blob`.
- **Contur**: `outline(c)` (vecini pe laturi = contur subțire, rotund), `clean_orphans(c)`.
  Trucul din `heroes.py`: fiecare parte a corpului e un strat separat cu conturul ei, apoi straturile
  se suprapun. Așa apare linia închisă dintre cap și corp, ca în Isaac.
- **Umbrire**: `shade_sphere` (normala elipsei · lumina, cuantizată în 4 tonuri), `shade_flat` (bază + semilună de umbră), `dither_on` / `dither_fill` (Bayer 4×4).
- **Transformări de cadru** (întorc pânză nouă): `shifted`, `mirrored`, `flipped_v`, `squash_rows` / `stretch_rows` (picioarele rămân pe loc), `squash_cols` / `stretch_cols` (centrul rămâne pe loc), `palette_swap`, `white_flash`, `silhouette`, `crop_paste`.
- **Culori**: `tint(c, k)` (k<1 întunecă, k>1 deschide), `mix(a, b, t)`, `with_alpha(c, a)`.
- **Review**: `contact_sheet([(titlu, [(etichetă, Canvas), ...]), ...], path, scale=6)` — planșă cu etichete, pe fundal închis.

## Convenții pentru personaje (48×48)

- Picioarele pe rândul 46, conturul lor pe rândul 47 (ultimul); centrul pe x = 24 (pivot jos-centru în Unity).
- Cine plutește (Azazel) are umbra pe sol semi-transparentă pe rândurile 45–47.
- Capul ocupă ~60% din înălțime; lumina vine mereu din stânga-sus.
- Profilul privește spre dreapta (inamicii stau în dreapta). Lovitura se face aplecând corpul,
  picioarele rămân pe loc.
