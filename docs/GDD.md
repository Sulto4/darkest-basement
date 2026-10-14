# Darkest Basement — Game Design Document

Fan game privat (proiect de facultate, curs opțional, Facultatea de Matematică). Nu se publică.
Combină: **explorarea din Darkest Dungeon 1** (hartă de camere + holuri), **cărțile din Slay the Spire** (fiecare erou are pachetul lui de abilități), **personajele, inamicii și stilul vizual din The Binding of Isaac**.

- Motor: **Unity 6.3 LTS (6000.3.26f1)**, C#, URP. Platformă: **PC** (build Windows; build macOS doar pentru testare).
- Un run complet: **45–60 de minute**.
- Limba jocului (UI, cărți): engleză, ca în Isaac. Documentația: română.

## 1. Bucla de joc

```
Meniu → Run nou (seed) → Etaj 1 → Etaj 2 → Etaj 3 → Mom (boss final) → Victorie
                └─ pe fiecare etaj: Hartă → alegi camera vecină → mergi pe hol (3D, din spate)
                     → eveniment pe hol? → intri în cameră → luptă / comoară / pat / curio
                     → după luptă: carte random → înapoi pe hartă … → camera boss → trapă → etajul următor
```

Ce **nu** luăm din Isaac: mecanicile separate (inimioare, monede, bombe, chei, iteme pasive, devil deals).
Din Isaac vin doar personajele, inamicii, atacurile ca temă și art style-ul.
Ce **nu** luăm din Darkest Dungeon: stres, torță, oraș, provizii. Păstrăm doar layoutul etajului, holurile și lupta pe ranguri și viteză.

## 2. Etajele și harta (stil Darkest Dungeon)

| Etaj | Temă | Inamici | Boss |
|---|---|---|---|
| 1. Basement | cărămidă maro, caca, pânze de păianjen | Fly, Gaper, Pooter, Horf, Clotty, Mulligan | **Monstro** |
| 2. Caves | piatră umedă, apă, ciuperci, oase | Hopper, Maw, Globin, Charger, Spider | **Gurdy** |
| 3. Depths | piatră neagră, lanțuri, sânge închegat | Knight, Bony, Leaper, Host, Nerve Ending | **Mom** (final) |

**Harta unui etaj** = un graf. Camerele sunt noduri pe o grilă de 5×4, iar holurile sunt muchii între camere vecine.
- 9–11 camere pe etaj. Generare: creștere aleatorie din camera de start, cu o muchie spre o cameră deja plasată pentru fiecare cameră nouă. Rezultă un **arbore de acoperire aleatoriu**. Se mai adaugă 1–2 muchii, ca să apară bucle și alegeri.
- **Camera boss** = camera cu distanța BFS maximă față de start (de preferință fundătură). **Camera de comoară** = altă fundătură.
- Tipuri: Start, Combat (~50%), Treasure (1), Bedroom (1, odihnă), Curio (1–2), Boss (1), Empty.
- **Ceața**: layoutul se vede tot. Tipul camerei apare doar după ce ai vizitat o cameră vecină (scouting). Boss-ul (craniu) se vede mereu.
- Te poți întoarce prin camere deja curățate. Pe holurile curățate se merge rapid (×3).

**Holul** are 3 segmente. Fiecare segment are ~25% șanse de eveniment, ascuns până ajungi la el:
- **Ambush**: luptă cu 2–3 inamici slabi (camera trece în lateral chiar pe hol).
- **Curio mic** (grămadă de caca, cufăr mic): rezultat random (vindecare / carte / capcană).
- **Trap** (țepi): damage mic la un erou random. Eroul cu cea mai mare viteză are 50% șanse s-o evite.

## 3. Explorarea (3D, din spate)

- Holul e **3D**: podea, pereți, tavan cu grinzi, făcute procedural din texturi pixel art (filtru point). Recuzită: torțe cu flacără pâlpâitoare (lumini point), caca, oase, sânge pe jos, pietre, pânze.
- Eroii sunt **sprite-uri plate (billboard)**, văzuți **din spate**, în formație 2×2 eșalonată: rândul din față are rangurile 1 și 2, puțin în față, iar rândul din spate rangurile 3 și 4.
- Camera stă **în spatele și puțin deasupra** echipei și o urmărește lin (SmoothDamp), cu un head-bob discret.
- Control: ține **W / D / → / click stânga** ca să mergi înainte. Echipa se oprește singură la evenimente.
- La capătul holului e o ușă în stil Isaac. Arată tipul camerei: boss = dinți și cranii, comoară = aurie. Se deschide cu o animație și intri.
- **Intrarea într-o cameră de luptă**: în ~1,2 s camera se rotește din spate în **vedere laterală** (lerp poziție + slerp rotație, easing in-out). Eroii trec pe sprite-urile laterale și se așază în stânga. Inamicii apar în dreapta, cu praf și efecte.
- Camerele fără luptă le vezi tot din spate. **Treasure**: piedestal cu o carte care strălucește, ca piedestalele de iteme din Isaac. **Bedroom**: patul lui Isaac. **Curio**: obiect interactiv.

## 4. Lupta (stil Darkest Dungeon, vedere laterală)

- **Eroii sunt în stânga, inamicii în dreapta.** Fiecare parte are rangurile 1–4. Rangul 1 e cel mai aproape de centru.
- **Runda**: fiecare combatant aruncă inițiativa = `Speed + Random(0..3)`. La egalitate, eroii acționează primii. Ordinea se vede într-o bară de portrete sus.
- **Intenții** (claritate în stil Slay the Spire): la începutul rundei, fiecare inamic își alege acțiunea și o arată cu o iconiță deasupra capului (sabie + număr, scut, otravă etc.).
- **Tura unui erou**:
  - Fiecare erou are **pachetul lui**: draw pile → hand → discard. Când draw pile se golește, discard-ul se amestecă la loc (Fisher–Yates).
  - La început de tură trage **3 cărți** și are **2 energie**. Cărțile costă 0–2. Joacă cât îl ține energia.
  - La final de tură, mâna rămasă merge în discard.
  - Acțiuni care nu sunt cărți: **Move** (schimbă locul cu un aliat vecin, 1 energie) și **End Turn**.
- **Carte** = abilitate: nume, cost, raritate, erou, **rangurile din care se poate juca**, **ținta** (inamic / toți inamicii / inamici pe anumite ranguri / aliat / sine / toți aliații), lista de efecte, ilustrație și descriere generată din efecte.
- **Targetare**: la hover, cartea se ridică și țintele valide pulsează. Dacă nu se poate juca, e gri și spune de ce („Needs rank 3–4”). Joci prin drag pe țintă sau click carte → click țintă. Taste: `1–3` pentru carte, `Space` pentru End Turn.
- **Statusuri**: Block (absoarbe damage; dispare la începutul turei următoare a purtătorului), Bleed (damage la început de tură, scade cu 1), Poison (la fel, se cumulează), Stun (pierde tura următoare), Weak (−25% damage), Mark (+50% damage primit), Strength (+X damage pe lovitură, pe toată lupta), Taunt (inamicii trebuie să-l atace pe el).
- **Damage** = `(baza + Strength) × (Weak ? 0.75 : 1) × (Mark ? 1.5 : 1) × (crit ? 1.5 : 1)`, rotunjit în jos, minus Block. Nu există miss.
- **Moartea**: eroul la 0 HP moare definitiv în acel run. Rangurile din spate avansează. Când mor toți 4, run-ul s-a terminat.
- O luptă normală durează **3–4 runde (~2–3 min)**. Boss-ii durează 6–8 runde.

## 5. Recompense și deck building

- **După fiecare luptă câștigată**: o **carte random** din pool-ul eroilor în viață. Șanse: Common 65%, Uncommon 30%, Rare 5%. Boss-ul garantează Rare.
  - Cartea se întoarce cu o animație de pachet. Alegi **Take** (zboară în pachetul eroului ei, iar portretul eroului clipește) sau **Skip** (ca să nu-ți diluezi pachetul).
- **Treasure**: alegi 1 din 3 cărți, cel puțin una Uncommon+.
- **Bedroom** (odihnă), alegi una: vindecă toată echipa cu 35% / scoți o carte din pachetul unui erou / **upgradezi** o carte (`+`, cifre mai mari).
- **Între etaje** (trapa): +25% HP fiecărui erou în viață.

## 6. Eroii

| Erou | Rol | HP | Speed | Ranguri preferate | Fantezie |
|---|---|---|---|---|---|
| **Azazel** | tank / bruiser | 34 | 4 | 1–2 | demon cu aripi, Brimstone de aproape |
| **Cain** | skirmisher / crit | 28 | 6 | 1–3 | un ochi, noroc, bombe |
| **Isaac** | ranged / utility | 26 | 5 | 2–4 | lacrimi, iteme clasice |
| **Lilith** | summoner / support | 22 | 4 | 3–4 | nu atacă ea, atacă Incubus (familiarul) |

Pachet de start: 5 cărți pe erou (marcate S). Pool-ul: restul. Valorile sunt de pornire și se echilibrează prin simulare.

### Azazel
| Carte | Rar. | Cost | Din rang | Țintă | Efect |
|---|---|---|---|---|---|
| Brimstone Swipe ×2 | S | 1 | 1–2 | inamic r1–2 | 6 dmg |
| Demon Wings ×2 | S | 1 | oricare | sine | 7 Block |
| Blood Beam | S | 2 | 1–2 | toți inamicii r1–2 | 5 dmg + Bleed 2 |
| Goat Head | C | 1 | 1–2 | sine | 6 Block + Taunt 1 |
| Blood Bag | C | 1 | oricare | sine | vindecă 7 |
| Pentagram | U | 1 | oricare | sine | +2 Strength |
| Charged Brimstone | U | 2 | 1–2 | inamic r1–3 | 14 dmg |
| Ceremonial Robes | U | 1 | oricare | sine | 10 Block + 1 Strength |
| Abaddon | R | 0 | oricare | sine | +3 Strength, pierde 5 HP |
| Maw of the Void | R | 2 | 1–2 | toți inamicii | 8 dmg + Bleed 3 |

### Cain
| Carte | Rar. | Cost | Din rang | Țintă | Efect |
|---|---|---|---|---|---|
| Lucky Punch ×2 | S | 1 | 1–3 | inamic r1–2 | 5 dmg, 25% crit |
| Lucky Foot ×2 | S | 1 | oricare | sine | 5 Block + trage 1 carte |
| Bomb Toss | S | 2 | 1–3 | toți inamicii r2–4 | 4 dmg |
| The Poop | C | 1 | 1–3 | inamic oricare | 3 dmg + Weak 2 |
| Lucky Penny | C | 0 | oricare | sine | trage 2 cărți |
| Mom's Knife | U | 1 | 1–3 | inamic oricare | 7 dmg + Mark 2 |
| Cricket's Head | U | 2 | 1–2 | inamic r1–2 | 12 dmg, 30% crit |
| Dice Shard | U | 0 | oricare | sine | aruncă mâna, trage 3, +1 energie |
| Midas' Touch | R | 2 | 1–2 | inamic r1–2 | 10 dmg, 50% crit; crit = Stun 1 |
| Dr. Fetus | R | 2 | 1–3 | toți inamicii | 6 dmg, 25% crit |

### Isaac
| Carte | Rar. | Cost | Din rang | Țintă | Efect |
|---|---|---|---|---|---|
| Tear ×2 | S | 1 | 2–4 | inamic oricare | 5 dmg |
| Yum Heart | S | 1 | oricare | aliat | vindecă 5 |
| The Sad Onion | S | 1 | 2–4 | inamic oricare | 3 dmg × 2 lovituri |
| Spoon Bender | S | 1 | 2–4 | inamic oricare | 4 dmg + Mark 2 |
| Book of Belial | C | 1 | oricare | aliat | +2 Strength |
| 20/20 | U | 1 | 2–4 | inamic oricare | 4 dmg × 2 lovituri |
| Ipecac | U | 2 | 2–4 | inamic oricare | 8 dmg + Poison 4 |
| The D6 | U | 0 | oricare | sine | aruncă mâna, trage 3 |
| Holy Mantle | R | 2 | oricare | toți aliații | 6 Block fiecare |
| Polyphemus | R | 2 | 2–4 | inamic oricare | 18 dmg |

### Lilith
| Carte | Rar. | Cost | Din rang | Țintă | Efect |
|---|---|---|---|---|---|
| Incubus Shot ×2 | S | 1 | 3–4 | inamic oricare | 5 dmg (trage Incubus) |
| Cambion Shield | S | 1 | oricare | aliat | 6 Block |
| Box of Friends | S | 2 | 3–4 | inamic oricare | 3 dmg × 3 lovituri |
| Cursed Eye | S | 1 | 3–4 | toți inamicii | Weak 2 |
| Mom's Eyeshadow | C | 1 | 3–4 | inamic oricare | Stun 1 (boss: în loc de Stun, Weak 2) |
| Lil' Brimstone | U | 2 | 3–4 | toți inamicii r1–2 | 7 dmg |
| Succubus | U | 1 | oricare | toți aliații | +1 Strength |
| Multidimensional Baby | U | 1 | 3–4 | inamic oricare | 4 dmg × 2 + Mark 2 |
| Guardian Angel | R | 2 | oricare | aliat | vindecă 10 + 5 Block |
| Twisted Pair | R | 1 | 3–4 | inamic oricare | 6 dmg × 2, fiecare lovitură Bleed 1 |

**Upgrade (+)**: damage / Block / vindecare +30% (rotunjit în sus, minim +2) sau cost −1, după carte.

## 7. Inamicii

Valori pentru etajul 1. Pe etajele 2 și 3 inamicii comuni se scalează: HP și damage ×1,25 / ×1,5.

| Inamic | Etaj | HP | Spd | Rang | Abilități |
|---|---|---|---|---|---|
| Fly | 1 | 6 | 7 | oricare | Buzz: 2 dmg oricui |
| Gaper | 1 | 18 | 3 | 1–2 | Grab: 5 dmg erou r1–2 |
| Pooter | 1 | 10 | 6 | 2–4 | Spit: 3 dmg oricui |
| Horf | 1 | 12 | 2 | 3–4 | Blood Shot: 6 dmg oricui |
| Clotty | 1 | 14 | 4 | 1–3 | Clot Burst: 3 dmg + Bleed 2 la 2 eroi random |
| Mulligan | 1 | 16 | 3 | 1–3 | Bump: 4 dmg; la moarte face 2 Fly |
| Hopper | 2 | 16 | 6 | oricare | Hop: 6 dmg erou random |
| Maw | 2 | 20 | 3 | 2–4 | Gore Spit: 4 dmg + Bleed 3 |
| Globin | 2 | 22 | 3 | 1–2 | Slam: 7 dmg; prima dată când moare se reface la 50% |
| Charger | 2 | 18 | 5 | 1–3 | Charge: 9 dmg erou r1, apoi trece pe rangul 1 |
| Spider | 2 | 8 | 8 | oricare | Bite: 2 dmg + Poison 3 |
| Knight | 3 | 30 | 3 | 1–2 | Shield Up: 10 Block; Lance: 8 dmg r1–2 |
| Bony | 3 | 18 | 5 | 2–4 | Bone Throw: 6 dmg oricui |
| Leaper | 3 | 24 | 4 | oricare | Leap: 8 dmg erou random |
| Host | 3 | 20 | 4 | 2–4 | închis: −75% damage primit; deschis (o tură din două): Triple Shot 3×3 |
| Nerve Ending | 3 | 25 | 1 | 1 | nu atacă; Taunt permanent (blochează) |

**Boss-ii** (o singură tură pe rundă; Mom are faze):
- **Monstro** (HP 120, Spd 2): Big Leap (10 dmg r1–2, 30% Stun) · Blood Spray (4 dmg toată echipa) · Spit Glob (7 dmg + Bleed 2 oricui).
- **Gurdy** (HP 180, Spd 1): Spawn (cheamă 2 Pooter/Fly, max 3 minioni) · Bile Rain (5 dmg toată echipa) · Mouth Beam (12 dmg r1).
- **Mom** (HP 300, Spd 3): Mom's Foot (15 dmg r1–2) · Mom's Hand (8 dmg rang 4 + îl trage pe rangul 1) · Door Summon (2 inamici din etajul 3) · Mom's Eye (Weak 2 toată echipa). **Faza 2 sub 50% HP**: două acțiuni pe tură, mai agresivă.

## 8. Ritmul unui run (ținta de 45–60 min)

- 3 etaje × (≈4 lupte în camere + ≈1–2 ambuscade + 1 boss) ≈ 18 lupte × ~2,5 min ≈ 45 min, plus explorare și meniuri.
- Curba de dificultate: etajul 1 te învață (fără ambuscade în primele 2 camere), etajul 2 te pedepsește dacă nu ai pachet bun, iar Mom e un test real.
- **Ținta de echilibru** (simulare cu un AI lacom): un jucător bun câștigă ~40–50% din run-uri.

## 9. Senzația jocului (juice) — obligatoriu, nu opțional

- **Hit-stop** 60–100 ms la lovitură, 150 ms la crit/kill.
- **Screen shake** bazat pe trauma: `shake = trauma²`, zgomot Perlin, trauma scade în timp.
- **Numere de damage** care sar cu bounce, colorate pe tip: alb dmg, galben crit, verde poison/vindecare, roșu bleed, albastru block.
- Sprite-ul **clipește alb** la hit, **lunge** (atacatorul sare spre țintă cu easing) și **squash & stretch**.
- **Sânge**: particule plus pete care rămân pe jos în cameră.
- **Respirație** idle: scale Y = sinus, cu fază diferită pentru fiecare personaj.
- Cărți: hover cu ridicare + scale 1.15 + înclinare spre mouse; drag urmărit lin, cu rotație după viteză.
- **Punch-in** cinematic (ca în DD): în timpul unei acțiuni camera face zoom ușor pe atacator și țintă, apoi revine.
- **Brimstone** = rază mare cu bloom și chromatic aberration.
- Post-procesare URP: Bloom, Vignette închisă, Film Grain, Color Adjustments (sepia-roșcat ca în Isaac).
- Torțe pâlpâitoare (Perlin), praf în aer pe hol, pași sincronizați cu animația de mers.
- Variație de pitch random la fiecare SFX.

## 10. Experiența jucătorului — reguli

1. Jucătorul trebuie să **înțeleagă** mereu de ce: intenții vizibile, motivul pentru care o carte nu se poate juca, tooltip pe fiecare status.
2. **Fără timp mort**: animațiile de luptă au viteză 1×/2× (tasta `F`). Holurile curățate se parcurg rapid.
3. Primele 2 minute **învață fără text lung**: prima luptă are un hint mic, contextual („Drag a card onto an enemy”).
4. Fiecare decizie contează: Skip la carte, Bedroom cu 3 opțiuni, alegerea drumului pe hartă.
5. Run-ul are un **seed** vizibil. Același seed dă același run (bun pentru teste și pentru a arăta profesorului).
6. Ecran final: etaje, kill-uri, timp, pachetele finale, seed.

## 11. Ce nu facem în v1

Salvare la mijlocul run-ului, meta-progresie, deblocări, alte personaje, setări avansate, controller. Se pot adăuga după.
