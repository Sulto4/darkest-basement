# Arhitectura codului

Ideea centrală: **logica jocului e separată de ce vezi pe ecran.**

```
┌─────────────────────────────┐        evenimente         ┌─────────────────────────────┐
│  MODEL (C# pur, fără Unity) │ ────────────────────────▶ │  VIEW (MonoBehaviour-uri)   │
│  reguli, numere, hartă,     │                           │  sprite-uri, cameră, UI,    │
│  cărți, AI, RNG cu seed     │ ◀──────────────────────── │  animații, sunet, juice     │
└─────────────────────────────┘     comenzi (PlayCard)    └─────────────────────────────┘
```

- **Model** (`Assets/_Game/Scripts/Model`, assembly `DarkestBasement.Model`, `noEngineReferences: true`): nu știe că există Unity. Se poate testa în milisecunde și rula în simulări de mii de run-uri (`tools/sim`).
- **View** (`Assets/_Game/Scripts/{Core,Exploration,Combat,UI,FX,Audio}`, assembly `DarkestBasement`): citește starea modelului, îi trimite comenzi și **redă secvențial evenimentele** pe care le produce modelul (coada de animații).
- **Editor** (`Assets/_Game/Editor`): generează scena, setările de import pentru pixel art și build-urile.
- **Tests** (`Assets/_Game/Tests/EditMode`): NUnit prin Unity Test Framework.

## Model

| Namespace | Conține |
|---|---|
| `DarkestBasement.Model.Core` | `GameRng` (seed, fluxuri separate: map / combat / loot), `RunState` (eroi, HP persistent, pachete, etaj, statistici) |
| `DarkestBasement.Model.Data` | definiții: `CardDef`, `EffectDef`, `HeroDef`, `EnemyDef`, `EnemyAbilityDef`, `FloorDef`, `EncounterDef` + bazele `CardDatabase`, `HeroDatabase`, `EnemyDatabase`, `FloorDatabase` (date scrise în C#, verificate de compilator) |
| `DarkestBasement.Model.Map` | `FloorMap`, `Room`, `Corridor`, `CorridorSegment`, `MapGenerator` (arbore de acoperire aleatoriu + bucle, BFS pentru boss) |
| `DarkestBasement.Model.Combat` | `CombatState`, `Unit` / `HeroUnit` / `EnemyUnit`, `Deck` (draw/hand/discard, Fisher–Yates), `StatusSet`, `DamageCalculator`, `TurnOrder`, `EnemyAI` (intenții), `CombatEngine`, `CombatEvent` (+ subtipuri) |
| `DarkestBasement.Model.Rewards` | `RewardGenerator` (carte random pe raritate, treasure 1 din 3, upgrade/remove) |
| `DarkestBasement.Model.Sim` | `AutoPlayer` (politică euristică pentru eroi), `RunSimulator` (run complet fără grafică) |

### Contractul `CombatEngine`

```csharp
var engine = new CombatEngine(state, rng);
engine.Events          // List<CombatEvent> produse de ultima comandă; View le consumă și le golește
engine.StartCombat();  // RoundStarted, IntentChosen…, TurnStarted
engine.CurrentActor    // Unit
engine.CanPlay(hero, card, out string reason)
engine.ValidTargets(hero, card)   // IReadOnlyList<Unit>
engine.PlayCard(hero, card, target)
engine.Move(hero, -1 / +1)
engine.EndTurn();      // continuă automat prin turele inamicilor până la următorul erou sau finalul luptei
engine.IsOver, engine.Victory
```

Evenimente (fiecare devine o animație în View): `RoundStarted(order)`, `TurnStarted(unit)`, `IntentChosen(enemy, ability, target)`, `CardsDrawn`, `Shuffled`, `EnergyChanged`, `CardPlayed(hero, card, target)`, `EnemyActed(enemy, ability, targets)`, `Damaged(src, target, amount, blocked, crit)`, `Healed`, `BlockGained`, `StatusApplied`, `StatusTicked`, `UnitMoved(unit, fromRank, toRank)`, `Summoned`, `UnitDied`, `TurnSkipped(stun)`, `CombatEnded(victory)`.

Reguli pentru Model: C# 9 fără `record` / `init` (Unity), fără `UnityEngine`, fără LINQ în bucle fierbinți, fără `static` mutabil. Toată aleatoritatea trece prin `GameRng`.

## View

| Folder | Rol |
|---|---|
| `Core/` | `GameBootstrap` (singurul obiect din scenă; construiește restul), `GameFlow` (mașina de stări: Menu → Map → Corridor → Room → Combat → Reward → … → GameOver/Victory), `Tween` + `Easing` (bibliotecă proprie, fără pachete externe), `SpriteLibrary`, `Services` |
| `Exploration/` | `CorridorBuilder` (hol 3D procedural), `RoomBuilder`, `PartyFormation`, `PartyWalker`, `CameraRig` (urmărire din spate, tranziție spre vedere laterală), `Billboard`, `Door`, `TorchFlicker` |
| `Combat/` | `CombatController` (pod Model ↔ View, coada de evenimente), `CombatStage` (pozițiile rangurilor), `UnitView` (sprite, animații, flash, lunge, respirație), `IntentView` |
| `UI/` | `UIRoot` (Canvas pixel-perfect 640×360 ×3), `HandView`, `CardView`, `TurnOrderBar`, `UnitHud` (HP, block, statusuri), `MapPanel`, `RewardScreen`, `BedroomScreen`, `TreasureScreen`, `MainMenu`, `EndScreen`, `Tooltip`, `Hint` |
| `FX/` | `ScreenShake` (trauma²·Perlin), `HitStop`, `DamageNumbers`, `Particles`, `Decals`, `BeamFx` |
| `Audio/` | `AudioManager` (pool de surse, pitch random, crossfade muzică) |

Totul se construiește **din cod** (obiecte, materiale, UI). Scena `Main.unity` are un singur `GameBootstrap`. Motivul: proiectul se poate reconstrui și verifica fără clicuri în editor.

## Resurse (convenții de nume, încărcate prin `Resources.Load`)

```
Assets/_Game/Resources/
  Sprites/Heroes/<heroId>/back_idle_0..1, back_walk_0..3, side_idle_0..1, side_attack_0..1, side_hurt_0, side_dead_0, portrait
  Sprites/Enemies/<enemyId>/idle_0..1, attack_0..1, hurt_0, dead_0, portrait
  Sprites/Bosses/<bossId>/…  (aceleași nume)
  Sprites/Familiars/incubus/idle_0..1, attack_0
  Sprites/Props/<propId>_<n>
  Sprites/Cards/<cardId>          (ilustrația cărții, 64×40)
  Sprites/UI/…                    (rame de cărți pe erou, iconițe status/intenții, butoane, panouri 9-slice, iconițe hartă)
  Textures/Env/<floorId>/wall, floor, ceiling, beam, door_<type>
  Fonts/…
  Audio/Sfx/<id>, Audio/Music/<id>
```

ID-urile (`isaac`, `cain`, `lilith`, `azazel`, `gaper`, `monstro`, `brimstone_swipe` …) sunt aceleași în Model și în numele fișierelor.

## Unelte

- `tools/unity.sh` — rulează Unity în batchmode (compilare, teste, build), cu cache-urile pe SSD.
- `tools/sim/` — proiect .NET care compilează sursele Model și rulează teste + simulări de echilibru fără Unity.
- `tools/art/` — generatoarele Python de pixel art (sursa de adevăr pentru sprite-uri).
- `tools/audio/` — scripturi ElevenLabs pentru SFX și muzică.
