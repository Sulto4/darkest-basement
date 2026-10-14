# Ghid de artă — stilul The Binding of Isaac: Rebirth

Ținta: cine vede un screenshot trebuie să zică „e Isaac”. Pixel art grotesc și drăguț în același timp: capete uriașe, corpuri mici, lacrimi, sânge, caca, contur gros închis.

## Reguli fixe

- **Pixel art adevărat**: fiecare pixel e pe grilă, fără anti-aliasing, fără blur, fără gradient neted. Importul în Unity se face cu Point filter, fără compresie și fără mipmaps (îl setează `PixelArtImportPostprocessor`).
- **PPU = 32**: 32 px = 1 unitate în lume, pentru toate sprite-urile din lume.
- **Contur** de 1 px, închis la culoare (`#1b0f0f`), pe tot exteriorul. În interior se folosesc linii mai închise din aceeași nuanță, nu negru.
- **Lumina vine din stânga-sus.** 3 tonuri pe material (bază, umbră, umbră adâncă) + 1 highlight mic.
- **Proporții Isaac**: capul are ~55–60% din înălțimea personajului, corpul e mic, cu brațe-butuc și picioare scurte. Ochii mari și umezi, lacrimi care curg.
- **Pivot**: jos-centru (la picioare) pentru personaje și inamici.
- **Paleta** de mai jos e baza. Culori în plus sunt permise doar ca nuanțe ale lor.

## Paleta de bază

| Rol | Culori |
|---|---|
| Contur | `#1b0f0f` |
| Piele (Isaac, Cain) | `#f2d3c4` `#dcae9c` `#b9857a` highlight `#fff0e8` |
| Lacrimi | `#7fb2e5` `#4a7fbf` highlight `#d6ecff` |
| Sânge | `#d33c3c` `#9e1b1b` `#5c0d0d` |
| Caca / maro | `#a8743d` `#8a5a2b` `#6b4423` `#4a2e17` |
| Basement (pereți/podea) | `#6e5241` `#5e4232` `#4a3326` `#3a271c` `#2a1b13` |
| Caves | `#7a7f86` `#5c6168` `#43474d` `#2f3236` · apă `#3e6f7a` · ciuperci `#c9a227` |
| Depths | `#4a3a40` `#3d2b33` `#2a1f24` `#1a1316` · carne `#7a2a33` |
| Azazel | negru `#231c22` `#151015`, ochi `#e03a3a`, coarne `#4b3b44` |
| Lilith | păr `#241c2b` `#3a2c45`, mov `#6b3f7d`, piele `#e9cbbf` `#cfa596`, Incubus `#2a2030` + ochi `#ff5a5a` |
| Cain | păr `#6b4423` `#4a2e17`, haine verde-oliv `#5b6b3a` `#43502a`, petic ochi `#2a1b13` |
| UI | pergament `#e8dcc4` `#c8b89a`, cerneală `#2a1b13`, aur `#e0b04a` `#a8782a` |
| Statusuri | block `#6fa8dc`, bleed `#d33c3c`, poison `#7cc04a`, stun `#f2d14a`, weak `#9a8fb0`, mark `#ff8a3d`, strength `#e05050`, taunt `#c0c0c0` |

## Personajele

| ID | Cum arată |
|---|---|
| `isaac` | chel, palid, ochi mari albastru-închis plini de lacrimi, gură mică tristă, fără haine (doar scutec/pânză), lacrimi albastre |
| `cain` | păr castaniu scurt, petic pe ochiul stâng (are un singur ochi), haine verzi, aer de tip mai „cool” |
| `lilith` | părul negru lung îi acoperă complet ochii (legată la ochi), piele palidă, rochiță mov închis. Lângă ea plutește **Incubus**: demon mic negru, cu coarne, aripioare și ochi roșii |
| `azazel` | demon negru, coarne mici, aripioare negre de liliac, ochi roșii strălucitori, colți, plutește puțin |

**Vederi pentru fiecare erou** (canvas 48×48):
- **din spate**: `back_idle_0..1`, `back_walk_0..3`. E vederea de pe hol, cu ceafa capului mare și umerii.
- **lateral, cu fața spre dreapta**: `side_idle_0..1` (respirație), `side_attack_0..1` (anticipare + lovitură), `side_hurt_0`, `side_dead_0` (cadavru / băltoacă).
- `portrait`: 24×24, doar fața, pentru bara de ture și rama cărților.

**Inamici** (privesc spre stânga; canvas 32×32 pentru Fly/Spider/Pooter, 48×48 pentru restul): `idle_0..1`, `attack_0..1`, `hurt_0`, `dead_0`, `portrait`.
**Boss-i**: Monstro 96×96, Gurdy 128×128. Mom e un picior uriaș cu pantof roșu și tivul rochiei, care intră de sus, plus o mână și un ochi care apare din ușă: 128×160.

## Mediul (texturi 3D)

- Texturi tileable de 32×32 (sau 64×64), pentru fiecare etaj: `wall`, `floor`, `ceiling`, `beam`. Plus ușile: `door_normal`, `door_boss`, `door_treasure`, `door_bedroom` (64×64, ca ușile din Isaac).
- Recuzită (sprite-uri): `torch_0..3` (animată), `poop_0..2`, `rock_0..2`, `tinted_rock`, `bones_0..1`, `web_0`, `blood_decal_0..4`, `chest_closed/open`, `bed`, `pedestal`, `trapdoor`, `mushroom_0..1` (caves), `chain_0` (depths), `spikes_0..1`.

## UI

- Rezoluție de referință **640×360**, afișată ×3 la 1920×1080 (pixel-perfect).
- **Cartea**: 64×90. Rama e colorată după erou (Azazel roșu-negru, Cain verde-maro, Isaac albastru-pal, Lilith mov). Sus e ilustrația de 56×36, plus cost în colț (bulină), nume și text, și o bijuterie de raritate (Common gri, Uncommon albastru, Rare auriu). Plus `card_back`.
- Iconițe de 12×12: statusuri, intenții (`intent_attack`, `intent_attack_aoe`, `intent_block`, `intent_debuff`, `intent_buff`, `intent_summon`, `intent_unknown`), energie (`energy_full`, `energy_empty`), hartă (`map_unknown`, `map_combat`, `map_treasure`, `map_bedroom`, `map_curio`, `map_boss`, `map_start`, `map_empty`, `map_current`).
- Panouri 9-slice: hârtie mototolită / pergament, ca meniurile din Isaac.
- Fonturi: pixel, cu licență OFL (de exemplu *Pixelify Sans*, *Silkscreen*). Pentru titluri, un font mai „scris de mână” dacă există unul OFL potrivit.

## Pipeline

- Sursa de adevăr: scripturile din `tools/art/` (Python + Pillow). Ele scriu PNG-urile în `Assets/_Game/Resources/...`.
- Pentru review, fiecare set produce și o **planșă** (contact sheet) mărită ×6, în `docs/art-review/`.
- Ilustrațiile cărților și recuzita complexă pot porni de la imagini generate (Vertex AI, vezi skill-ul `scroll-film-studio`). Rezultatul final trebuie redus la rezoluția țintă, cuantizat pe paletă și curățat pixel cu pixel. Nu intră în joc imagini „AI moi”.
- Nu se copiază sprite-uri din jocul original. Totul e desenat de noi, în stilul lui.
