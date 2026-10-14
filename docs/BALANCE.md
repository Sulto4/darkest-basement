# Balans și contract de integrare — Darkest Basement

Verificat la 14 octombrie 2026. Comanda canonică: `tools/sim/run.sh`.

## Rezultatul final

| Măsură | Țintă | Seed-uri 1–500 |
|---|---:|---:|
| Victorii | 35–55% | **219/500 = 43,8%** |
| Runde / luptă normală, inclusiv ambuscade | 3–4 | **3,53** (8.300 lupte) |
| Runde / boss | 6–8 | **6,78** (1.385 lupte) |
| Monstro / Gurdy / Mom | fiecare 6–8 | **6,22 / 6,59 / 7,72** |
| Lupte / run câștigat | aproximativ 16–20 | **19,61** |
| Lupte / toate run-urile, inclusiv înfrângeri | informativ | **19,37** |
| Etaje terminate: Basement / Caves / Depths | informativ | **500 / 482 / 219** |

Raportul brut, inclusiv toate frecvențele cărților: [`../tools/sim/latest-report.txt`](../tools/sim/latest-report.txt).

Verificări suplimentare, fără schimbări ulterioare de valori:

| Seed-uri | Victorii | Runde normale | Runde boss | Monstro / Gurdy / Mom | Lupte / victorie |
|---|---:|---:|---:|---|---:|
| 1001–1500 | 255/500 = 51,0% | 3,56 | 6,77 | 6,29 / 6,61 / 7,59 | 19,85 |
| 3001–3500, lot nou după calibrare | 249/500 = 49,8% | 3,51 | 6,82 | 6,29 / 6,45 / 7,96 | 19,73 |

Surse: [`secondary-report.txt`](../tools/sim/secondary-report.txt), [`holdout-report.txt`](../tools/sim/holdout-report.txt). Aceste estimări caracterizează politica AutoPlayer, nu garantează rata de victorie a oamenilor sau durata în minute a View-ului.

## Ce a fost ajustat

Valorile inițiale au produs 0 victorii din 500, lupte normale de 2,33 runde și eșecuri concentrate în Caves. HP-ul inamicilor comuni a crescut aproximativ cu 50%, iar damage-ul și statusurile ofensive au scăzut. Astfel luptele au mai multe decizii fără a epuiza imediat HP-ul persistent. Tabelele din GDD conțin acum exact valorile de bază din `EnemyDatabase`.

- HP boss: Monstro 260, Gurdy 240, Mom 180. Mom are două acțiuni sub jumătate din HP, Weak pentru echipă, pull și invocări; HP-ul singur nu măsoară dificultatea.
- Scalarea inamicilor comuni: HP `ceil(base × 1 / 1.25 / 1.5)` și damage `floor(base × factor)`. Boss-ii au valori proprii, fără încă o scalare. Block și numărul de statusuri nu se scalează cu etajul.
- Buget normal: `8 + floor + min(4, combatsCleared / 2)`, maximum patru unități. Buget ambuscadă: `4 + floor`, două sau trei unități de cost cel mult 3. Costuri: Fly 1; Pooter/Horf/Spider 2; Gaper/Clotty/Mulligan/Hopper/Maw/Charger/Bony/Host/Nerve Ending 3; Globin/Knight/Leaper 4. Se aleg unități din pool-ul etajului, compatibile cu rangul inițial și bugetul rămas. Invocările boss-ilor au pool-urile tematice proprii.
- Harta are 9–11 camere uniform. Start, Boss, Treasure, Bedroom și Curio sunt obligatorii; celelalte sunt Combat. La hărțile cu 11 camere, una dintre camerele Combat devine Empty cu probabilitate 80%. Rezultă în medie aproximativ 3,7 lupte în camere pe etaj.
- Un segment nou are 25% șanse de eveniment. În acel caz: Ambush 1/7, Curio 2/7, Trap 4/7. Protecția de tutorial elimină ambuscadele pe deplasările care pleacă atunci când au fost vizitate cel mult două camere în Basement. Holurile curățate nu repetă evenimentele.
- Valorile de bază ale eroilor și cărților au rămas cele din GDD. Upgrade-ul mărește damage/Block/heal cu `max(2, ceil(base × .30))`. Utility cu cost pozitiv primește cost −1. Excepțiile necesare pentru utility deja gratuit sunt în coloana numerică din GDD: Abaddon +4 Strength, Lucky Penny draw 3, Dice Shard și The D6 redraw 4; celelalte efecte rămân identice. De exemplu Ceremonial Robes+ are 13 Block și tot 1 Strength.

## Decizii explicite unde GDD nu fixa detaliul

- Weak, Mark și Taunt au durată în turele purtătorului; scad la sfârșitul turei. Taunt aplicat în propria tură supraviețuiește acelei ture. Strength persistă până la sfârșitul luptei. Bleed/Poison aplică numărul curent de stack-uri la început, apoi pierd unul. Block expiră înaintea acestor tick-uri. Stun consumă un stack și sare întreaga tură, fără draw sau energie nouă.
- Taunt restrânge atacurile cu țintă individuală la purtătorii săi, inclusiv peste restricția de rang a țintei. Nu mută atacatorul și nu permite cărți din ranguri nevalide. Atacurile AoE își păstrează țintele și rangurile. Un atac la doi eroi folosește ținte distincte; dacă există doar un Taunt, lovește acel erou o singură dată.
- Intenția păstrează țintele de la începutul rundei când încă sunt legale; moartea, schimbarea rangurilor și Taunt pot impune retargetare la execuție. `EnemyActed.TargetIds` arată țintele efective. Predicția este damage brut, după modificatori și înainte de Block, pentru prima țintă și toate loviturile. Damage-ul efectiv al fiecărei ținte este în `Damaged`.
- Abilitățile au ponderi egale, cu excepția Mom's Foot/Hand: fiecare are pondere 2, Eye/Summon câte 1. În faza 2, ponderile atacurilor lui Mom se multiplică cu 3. Ambele intenții sunt vizibile la începutul rundei; dacă pragul de HP este trecut ulterior, a doua intenție apare când Mom acționează. Când nicio abilitate nu este legală, intenția `reposition` încearcă o deplasare cu un rang spre față, fără damage.
- Host este închis în rundele impare, deschis în cele pare, inclusiv dacă este Stunned. Reducerea de 75% se aplică înainte de rotunjire și Block. Globin se reface o singură dată cu `ceil(MaxHp / 2)` și păstrează statusurile.
- Maximum patru inamici simultan. Gurdy și Mom invocă doar în locurile libere. Cele două muște ale unui Mulligan rămân în coadă dacă nu încap, apoi apar când se eliberează un rang. Invocările acționează începând cu runda următoare. Victoria cere eliminarea boss-ului și a minionilor rămași.
- Un curio are trei rezultate echiprobabile: vindecă fiecare erou viu cu 12% din MaxHp; oferă o carte normală; declanșează o capcană. Capcana este evitată în 50% din cazuri de cel mai rapid supraviețuitor; altfel lovește un erou viu aleator cu `2 + floor` HP. `ExplorationOutcome.HpChanges` conține modificările reale pentru animații. Vindecările procentuale se rotunjesc în sus și nu învie eroi.
- Recompensele exclud Starter și eroii morți. Se aruncă întâi raritatea, apoi se alege uniform o definiție din pool-ul acelei rarități al supraviețuitorilor. Citirea recompensei deja generate nu mai consumă RNG. Treasure amestecă cele trei opțiuni, una fiind obligatoriu Uncommon+.
- Run-ul câștigă la folosirea trapei lui Mom după recompensa finală. HP, ranguri, cărți și upgrade-uri persistă între lupte; statusurile, energia și draw/hand/discard se reconstruiesc pentru fiecare luptă.

## Politica AutoPlayer și limitele măsurătorii

AI-ul preferă damage util și eliminarea unei ținte, heal la HP scăzut, Block împotriva intențiilor îndreptate către aliat, Strength mai ales la boss și mutări care deblochează o carte din mână. Explorează camere înaintea boss-ului și amână Bedroom când echipa este sănătoasă. Ia în principal cărți fără duplicate până la un pachet de zece; Treasure este obligatoriu o alegere din trei și poate introduce duplicate. Bedroom alege heal în aceste simulări; remove și upgrade sunt acoperite de teste, fără a influența cifrele de calibrare.

AI-ul joacă cel mult 20 de acțiuni într-o tură, pentru a nu repeta infinit combinații de cărți gratuite cu redraw. **Concedează explicit** dacă niciun supraviețuitor nu mai are atacuri utilizabile din rangurile accesibile sau după 40 de runde complete într-o singură luptă. Compactarea poate lăsa Lilith/Isaac fără rang de atac, iar un tank singur poate prelungi foarte mult o luptă cu invocări. Modelul nu omoară artificial eroii și nu impune această limită unui jucător: `Concede()` este o comandă distinctă, care produce înfrângere. Cele **30 de concesii** din lotul principal sunt incluse în cele 281 de înfrângeri și în statisticile de durată. Loturile secundar/nou au 30/38 concesii.

Frecvența pick = luări / oferte (la Treasure, toate cele trei sunt oferte). Frecvența play = utilizări / toate utilizările de cărți. Damage share folosește HP efectiv pierdut de inamici, fără overkill, Block sau self-damage; include Bleed/Poison, atribuite ultimei cărți care a aplicat acel status. Această atribuire a stack-urilor comune este o convenție de raportare; bonusul indirect din Strength/Mark nu este redistribuit către cartea de suport.

Pragul de alarmă al simulatorului pentru dominanță este 25% din damage sau din utilizări pentru o singură carte. Nu înlocuiește analiza strategică: cartea de start Lucky Punch are două copii în fiecare run și beneficiază de expunere garantată. În lotul principal nu există o carte care trece pragul; toate cele 40 sunt folosite cel puțin o dată. Tabelul complet de mai jos păstrează numerele măsurate, nu estimări.

| Card ID | Oferte | Luate | Pick % | Jucate | Play % | Damage HP | Damage % |
|---|---:|---:|---:|---:|---:|---:|---:|
| 20_20 | 415 | 231 | 55.7 | 3244 | 1.34 | 31750 | 2.51 |
| abaddon | 286 | 184 | 64.3 | 1915 | 0.79 | 0 | 0.00 |
| blood_bag | 1344 | 573 | 42.6 | 4875 | 2.01 | 0 | 0.00 |
| blood_beam | 0 | 0 | 0.0 | 6703 | 2.77 | 89902 | 7.10 |
| bomb_toss | 0 | 0 | 0.0 | 2799 | 1.16 | 28706 | 2.27 |
| book_of_belial | 1347 | 502 | 37.3 | 3050 | 1.26 | 0 | 0.00 |
| box_of_friends | 0 | 0 | 0.0 | 3069 | 1.27 | 31792 | 2.51 |
| brimstone_swipe | 0 | 0 | 0.0 | 21217 | 8.76 | 171353 | 13.54 |
| cambion_shield | 0 | 0 | 0.0 | 3933 | 1.62 | 0 | 0.00 |
| ceremonial_robes | 410 | 270 | 65.9 | 2644 | 1.09 | 0 | 0.00 |
| charged_brimstone | 409 | 202 | 49.4 | 1716 | 0.71 | 28382 | 2.24 |
| crickets_head | 466 | 208 | 44.6 | 2605 | 1.08 | 37804 | 2.99 |
| cursed_eye | 0 | 0 | 0.0 | 6216 | 2.57 | 0 | 0.00 |
| demon_wings | 0 | 0 | 0.0 | 4800 | 1.98 | 0 | 0.00 |
| dice_shard | 433 | 332 | 76.7 | 7670 | 3.17 | 0 | 0.00 |
| dr_fetus | 262 | 166 | 63.4 | 1499 | 0.62 | 27446 | 2.17 |
| goat_head | 1319 | 422 | 32.0 | 1133 | 0.47 | 0 | 0.00 |
| guardian_angel | 247 | 173 | 70.0 | 852 | 0.35 | 0 | 0.00 |
| holy_mantle | 267 | 197 | 73.8 | 44 | 0.02 | 0 | 0.00 |
| incubus_shot | 0 | 0 | 0.0 | 20283 | 8.38 | 116128 | 9.18 |
| ipecac | 370 | 217 | 58.6 | 1285 | 0.53 | 17419 | 1.38 |
| lil_brimstone | 360 | 186 | 51.7 | 788 | 0.33 | 11125 | 0.88 |
| lucky_foot | 0 | 0 | 0.0 | 14702 | 6.07 | 0 | 0.00 |
| lucky_penny | 1427 | 447 | 31.3 | 5578 | 2.30 | 0 | 0.00 |
| lucky_punch | 0 | 0 | 0.0 | 30026 | 12.40 | 184607 | 14.59 |
| maw_of_the_void | 253 | 171 | 67.6 | 1596 | 0.66 | 43569 | 3.44 |
| midas_touch | 244 | 164 | 67.2 | 2208 | 0.91 | 28531 | 2.25 |
| moms_eyeshadow | 1257 | 431 | 34.3 | 4628 | 1.91 | 0 | 0.00 |
| moms_knife | 412 | 301 | 73.1 | 5623 | 2.32 | 41127 | 3.25 |
| multidimensional_baby | 424 | 320 | 75.5 | 3696 | 1.53 | 33058 | 2.61 |
| pentagram | 415 | 225 | 54.2 | 1832 | 0.76 | 0 | 0.00 |
| polyphemus | 227 | 179 | 78.9 | 1420 | 0.59 | 27502 | 2.17 |
| spoon_bender | 0 | 0 | 0.0 | 13848 | 5.72 | 56413 | 4.46 |
| succubus | 431 | 345 | 80.0 | 4024 | 1.66 | 0 | 0.00 |
| tear | 0 | 0 | 0.0 | 18334 | 7.57 | 106175 | 8.39 |
| the_d6 | 412 | 255 | 61.9 | 1199 | 0.50 | 0 | 0.00 |
| the_poop | 1342 | 439 | 32.7 | 8787 | 3.63 | 29295 | 2.31 |
| the_sad_onion | 0 | 0 | 0.0 | 13007 | 5.37 | 94200 | 7.44 |
| twisted_pair | 225 | 192 | 85.3 | 2005 | 0.83 | 29189 | 2.31 |
| yum_heart | 0 | 0 | 0.0 | 7254 | 3.00 | 0 | 0.00 |

## Verificare și reproducere

`run.sh` folosește SDK-ul .NET 8 fixat în AGENTS, `LangVersion=9.0`, nullable off, implicit usings off și warnings-as-errors. Compilează întâi întregul Model ca **netstandard2.1**, apoi executabilul net8.0, apoi aceeași suită de teste folosită de NUnit, apoi 500 de simulări (seed 1). Nu există PackageReference sau pachete NuGet; configurația locală golește sursele de pachete. CLI home, pachetele și obiectele de compilare rămân sub `tools/sim`.

**35 teste, zero erori, zero warnings de compilator.** Suita verifică 1.000 de hărți, toate cele 40 de cărți prin API, multiset-ul pachetelor, formula damage, duratele statusurilor, stun, taunt, mișcare, spawn/revive, fazele boss-ilor, run flow, recompense, Bedroom, coborâri, determinism pe run-uri complete și terminarea a 200 de run-uri. Testele NUnit sunt același fișier sursă compilat de harness-ul offline cu un atribut minim compatibil. **Unity Editor / Unity Test Runner nu au fost executate în acest task.** Compatibilitatea API .NET Standard este verificată separat; aceasta nu este o probă a integrării View în Unity.

Comenzile pentru loturi suplimentare după build:

```sh
"/Volumes/Dock & Hub 2TB/DevStorage/dotnet/sdk8/dotnet" tools/sim/bin/Release/net8.0/Sim.dll sim --runs 500 --seed 1001
"/Volumes/Dock & Hub 2TB/DevStorage/dotnet/sdk8/dotnet" tools/sim/bin/Release/net8.0/Sim.dll sim --runs 500 --seed 3001
```

Pentru comanda canonică de 500 de seed-uri începând cu 1, simulatorul întoarce exit code nenul dacă o țintă de balans nu este atinsă. Pentru alte loturi afișează evaluarea fără a transforma variația statistică într-un eșec de build.

## API public pentru View

Tipurile sunt în namespace-urile din `ARCHITECTURE.md`; `Run`, `RunState`, `PartyHero`, `CardInstance` și `GameRng` sunt în `DarkestBasement.Model.Core`. Definițiile statice sunt readonly; colecțiile stării de joc sunt ale instanței și View trebuie să le trateze ca read-only, folosind comenzile pentru modificări.

```csharp
Run()
Run(int seed)
void StartRun(int seed)
// State, CurrentFloor, Map, Phase, AvailableDestinations,
// PendingEncounter, PendingCard, TreasureChoices, CanDescend, IsRunOver, IsVictory
Corridor BeginTravel(int roomId)
CorridorSegment AdvanceTravel() // null după toate cele trei
ExplorationOutcome ResolveCurio() // cameră sau segment activ
ExplorationOutcome ResolveTrap()     // segment activ
Room EnterRoom()                    // după toate segmentele rezolvate
CombatEngine CreateCombat(EncounterDef encounter) // PendingEncounter
void ApplyCombatResult(CombatEngine engine)     // o singură dată, după IsOver
CardDef RollCardReward()          // citire idempotentă
void TakeCard()
void SkipCard()
void ChooseTreasure(int index)
void UseBedroom(BedroomAction action, string heroId = null, string cardUid = null)
void Descend()
```

Fluxul efectiv: în `Map` există destinații doar dacă nu este un encounter în așteptare. `BeginTravel` → pentru fiecare segment `AdvanceTravel` → rezolvare eveniment/recompensă/luptă → `AdvanceTravel` până întoarce null → `EnterRoom`. Pentru Ambush, ca pentru cameră, se folosește `CreateCombat(run.PendingEncounter)`. O cameră poate trece în Treasure/Bedroom/Curio sau poate lăsa PendingEncounter. Un eveniment nerezolvat, o recompensă nealeasă sau o luptă neterminată blochează avansarea. `ApplyCombatResult` aplică HP/statistici și pune recompensa în așteptare; View nu trebuie să copieze manual starea eroilor.

```csharp
CombatEngine(CombatState state, GameRng rng)
List<CombatEvent> Events { get; }
CombatState State { get; }
Unit CurrentActor { get; }
bool IsOver { get; }
bool Victory { get; }
void StartCombat()
bool CanPlay(HeroUnit hero, CardInstance card, out string reason)
IReadOnlyList<Unit> ValidTargets(HeroUnit hero, CardInstance card)
void PlayCard(HeroUnit hero, CardInstance card, Unit target)
void Move(HeroUnit hero, int direction) // -1 sau +1
void EndTurn()
void Concede()
```

`StartCombat`, `PlayCard`, `Move`, `EndTurn`, `Concede` produc o listă nouă logic de evenimente: lista `Events` existentă este golită la fiecare comandă validă. View o consumă/copiază înaintea următoarei comenzi. `EndTurn` execută automat turele inamicilor și rundele necesare până la următorul erou viu sau final. `CanPlay`/`ValidTargets` nu consumă RNG. O comandă invalidă aruncă `InvalidOperationException` înainte să cheltuiască energie sau să mute cartea.

Toate evenimentele sunt clase simple derivate din `CombatEvent`, cu proprietăți snapshot:

| Eveniment | Date pentru View |
|---|---|
| `RoundStarted` | `Round`, `Order` (ID-uri în ordinea inițiativei) |
| `TurnStarted` | `UnitId` |
| `IntentChosen` | `EnemyId`, `AbilityId`, `TargetIds`, `Kind`, `PredictedDamage`, `ActionIndex` |
| `CardsDrawn` | `HeroId`, `CardIds` (UID-uri de instanță) |
| `Shuffled` | `HeroId`, `CardCount` |
| `EnergyChanged` | `HeroId`, `Energy`, `Delta` |
| `CardPlayed` | `HeroId`, `CardUid`, `CardId`, `TargetId`, `Upgraded` |
| `EnemyActed` | `EnemyId`, `AbilityId`, `TargetIds`, `ActionIndex` |
| `Damaged` | `SourceId`, `TargetId`, `Amount`, `RawAmount`, `Blocked`, `Crit`, `HitIndex`, `HpAfter`, `CardId` |
| `Healed` | `SourceId`, `TargetId`, `Amount`, `HpAfter`, `Revive` |
| `BlockGained` | `SourceId`, `TargetId`, `Amount`, `Total` |
| `StatusApplied` | `SourceId`, `TargetId`, `Status`, `Stacks`, `Total` |
| `StatusTicked` | `UnitId`, `Status`, `Amount`, `Remaining` |
| `UnitMoved` | `UnitId`, `FromRank`, `ToRank` |
| `Summoned` | `SourceId`, `UnitId`, `EnemyDefId`, `Rank`, `Hp`, `JoinedRound` |
| `UnitDied` | `UnitId`, `Rank`, `IsHero` |
| `TurnSkipped` | `UnitId`, `Reason` |
| `CombatEnded` | `Victory`, `Rounds` |

Indicii de hit și acțiune sunt 0-based; rangurile și rundele sunt 1-based. `Damaged.Amount` este HP efectiv pierdut, iar `RawAmount` este damage înainte de Block și de limitarea la HP. Tick-urile DOT au `StatusTicked` pentru status și `Damaged` pentru HP: View nu trebuie să afișeze aceeași pierdere de HP de două ori. La prima moarte a unui Globin, `UnitDied` este urmat de `Healed(Revive = true)`; View îl reactivează. O invocare nu intră retroactiv în `RoundStarted.Order` curent.
