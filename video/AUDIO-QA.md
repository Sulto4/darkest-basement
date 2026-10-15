# Sunetul: verificare obiectivă (v2)

Generat de `tools/audio_qa.py` din MP4-ul final și din stem-urile mixajului (`tools/mix.mjs --stems`). Măsurători, nu ascultare:
ce e marcat **de verificat** merită ascultat de Radu.

## Întregul film

- Loudness integrat: **-14.5 LUFS** (ținta −14), LRA 3.8 LU, true peak **-1.2 dBTP** (plafon −1).
- Clipping: niciunul (true peak sub 0 dBFS).

## Pe capitole

| capitol | loudness (LUFS) | voce − muzică, mediana (dB) | voce − (muzică+efecte), minim (dB) | ferestre sub 6 dB |
|---|---|---|---|---|
| s01 Jocul în 30 de secunde | -14.8 | 20.6 | -14.9 | 14 / 155 |
| s02 Model și View | -14.4 | 16.8 | -16.2 | 25 / 324 |
| s03 Jocul ca mașină de stări | -14.3 | 17.1 | -16.7 | 18 / 198 |
| s04 Harta e un graf | -14.3 | 20.2 | -13.2 | 27 / 334 |
| s05 Aleatoriu, dar controlat | -14.1 | 19.7 | -12.9 | 31 / 388 |
| s06 Lupta pe numere | -14.3 | 20.8 | -7.7 | 19 / 339 |
| s07 Coada de animații | -15.0 | 21.3 | -12.4 | 14 / 208 |
| s08 Echilibrare prin simulare | -14.3 | 20.6 | -11.9 | 18 / 287 |
| s09 Mișcarea care se simte bine | -14.8 | 16.6 | -16.5 | 32 / 498 |
| s10 3D care arată a pixel art | -14.6 | 20.3 | -14.3 | 13 / 315 |
| s11 Cum l-ai construi tu | -14.6 | 16.5 | -8.7 | 35 / 295 |
| s12 Temă pentru acasă | -14.8 | 19.4 | -8.6 | 15 / 141 |

Pe tot filmul: vocea e cu **18.3 dB** peste muzică (mediana) și cu 17.9 dB peste muzică+efecte.

## Efecte peste voce

16 ferestre de 400 ms (din 3482 cu voce) în care un efect e la mai puțin de 3 dB sub voce; în 16 dintre ele vocea e cu peste 10 dB sub nivelul ei obișnuit (capăt de cuvânt sau respirație, efectul cade în pauză). Niciuna nu acoperă vorbire la nivel normal.

- 0:06.4 (s01), în pauză: efect la -0.4 dB față de voce, vocea la -15 dB față de nivelul ei — „Un joc pe ture în care cobori cu patru eroi într-un subsol p…”
- 0:06.6 (s01), în pauză: efect la +5.2 dB față de voce, vocea la -20 dB față de nivelul ei — „Un joc pe ture în care cobori cu patru eroi într-un subsol p…”
- 0:16.8 (s01), în pauză: efect la +6.9 dB față de voce, vocea la -24 dB față de nivelul ei — „Fiecare erou are pachetul lui. Câștigi o luptă, primești o c…”
- 0:17.2 (s01), în pauză: efect la +13.5 dB față de voce, vocea la -24 dB față de nivelul ei — „Fiecare erou are pachetul lui. Câștigi o luptă, primești o c…”
- 0:21.4 (s01), în pauză: efect la +5.7 dB față de voce, vocea la -22 dB față de nivelul ei
- 2:14.8 (s03), în pauză: efect la +0.4 dB față de voce, vocea la -23 dB față de nivelul ei
- 3:49.6 (s04), în pauză: efect la +7.3 dB față de voce, vocea la -18 dB față de nivelul ei — „Camera cu distanța cea mai mare devine boss-ul. La egalitate…”
- 7:26.0 (s07), în pauză: efect la +4.6 dB față de voce, vocea la -13 dB față de nivelul ei — „Ia primul eveniment, îl animă, așteaptă să se termine, apoi …”
- 10:01.4 (s09), în pauză: efect la +0.2 dB față de voce, vocea la -16 dB față de nivelul ei — „Când un erou atacă, întâi se trage puțin înapoi, ca un arc c…”
- 12:41.2 (s11), în pauză: efect la +3.7 dB față de voce, vocea la -20 dB față de nivelul ei — „Unu. C sharp simplu, fără Unity. Clase, liste, dicționare. S…”
- 12:59.8 (s11), în pauză: efect la +0.2 dB față de voce, vocea la -18 dB față de nivelul ei — „Trei. Leagă cele două lumi cu o coadă de evenimente. Lupta t…”
- 13:08.8 (s11), în pauză: efect la +1.4 dB față de voce, vocea la -18 dB față de nivelul ei — „Patru. Interfața. Un Canvas, câteva butoane, o carte care se…”
- 13:42.0 (s11), în pauză: efect la -2.1 dB față de voce, vocea la -14 dB față de nivelul ei — „Regula care leagă totul e simplă. Fiecare pas se poate testa…”
- 13:51.0 (s12), în pauză: efect la -1.3 dB față de voce, vocea la -12 dB față de nivelul ei
- 14:20.4 (s12), în pauză: efect la -0.5 dB față de voce, vocea la -13 dB față de nivelul ei — „Dacă știi să explici de ce s-a schimbat cifra, ai înțeles jo…”
- 14:20.8 (s12), în pauză: efect la +2.0 dB față de voce, vocea la -24 dB față de nivelul ei — „Dacă știi să explici de ce s-a schimbat cifra, ai înțeles jo…”

## Muzica la schimbările de capitol

În pauza dintre capitole (fără voce) muzica urcă intenționat și coboară când revine vocea. „Urcare” = cât e muzica în pauză față de muzica de sub voce; „vârf scurt” = cea mai puternică fereastră de 250 ms din pauză față de restul pauzei.

| graniță | muzica sub voce, înainte (dB) | în pauză (dB) | vârf scurt (dB) | sub voce, după (dB) | verdict |
|---|---|---|---|---|---|
| 0:36.9 → s02 | -37.5 | -25.4 | -23.0 | -33.6 | **de verificat**: gol în muzică |
| 1:57.7 → s03 | -32.6 | -23.3 | -20.6 | -33.5 | ok |
| 2:44.4 → s04 | -33.5 | -26.0 | -20.9 | -33.8 | ok |
| 4:07.9 → s05 | nan | -25.9 | -15.6 | -28.7 | **de verificat**: vârf scurt de 10 dB în pauză |
| 5:40.7 → s06 | -33.8 | -28.2 | -22.0 | -36.5 | **de verificat**: vârf scurt de 6 dB în pauză |
| 7:05.1 → s07 | nan | -26.6 | -23.9 | -37.5 | ok |
| 8:01.8 → s08 | -36.5 | -26.4 | -16.5 | -37.9 | **de verificat**: vârf scurt de 10 dB în pauză |
| 9:09.4 → s09 | -30.6 | -25.6 | -23.0 | -33.6 | **de verificat**: gol în muzică |
| 11:14.8 → s10 | nan | -22.7 | -20.8 | -35.1 | ok |
| 12:33.8 → s11 | -34.3 | -25.7 | -19.6 | -32.4 | **de verificat**: vârf scurt de 6 dB în pauză |
| 13:46.5 → s12 | -32.6 | -25.0 | -20.9 | -35.9 | ok |

## De ascultat

- trecerea spre s02: **de verificat**: gol în muzică
- trecerea spre s05: **de verificat**: vârf scurt de 10 dB în pauză
- trecerea spre s06: **de verificat**: vârf scurt de 6 dB în pauză
- trecerea spre s08: **de verificat**: vârf scurt de 10 dB în pauză
- trecerea spre s09: **de verificat**: gol în muzică
- trecerea spre s11: **de verificat**: vârf scurt de 6 dB în pauză
