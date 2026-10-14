using System;
using System.Collections.Generic;
using NUnit.Framework;
using DarkestBasement.Model.Core;
using DarkestBasement.Model.Data;
using DarkestBasement.Model.Map;
using DarkestBasement.Model.Combat;
using DarkestBasement.Model.Rewards;
using DarkestBasement.Model.Sim;

namespace DarkestBasement.Tests
{
    public class ModelRulesTests
    {
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Equal<T>(T expected, T actual) { Check(EqualityComparer<T>.Default.Equals(expected, actual), "Expected " + expected + ", got " + actual); }
        private static CombatEngine Engine(string heroId, params string[] enemies)
        {
            var party = new List<PartyHero> { new PartyHero(HeroDatabase.Get(heroId)) { Rank = 1 } };
            var rng = new GameRng(17); return new CombatEngine(new CombatState(party, new EncounterDef(1, false, false, 0, enemies), rng), rng);
        }
        private static CardInstance Give(CombatEngine engine, string cardId)
        {
            var hero = (HeroUnit)engine.CurrentActor; var card = new CardInstance(CardDatabase.Get(cardId), "test_" + hero.Deck.Hand.Count); hero.Deck.Hand.Add(card); return card;
        }
        private static void NextRound(CombatEngine engine)
        { int round = engine.State.Round; int count = 0; while (!engine.IsOver && engine.State.Round == round && count++ < 10) engine.EndTurn(); }
        [Test] public static void DatabaseHasEveryDefinition()
        {
            Equal(40, CardDatabase.All.Count); Equal(19, EnemyDatabase.All.Count); Equal(4, HeroDatabase.All.Count); Equal(3, FloorDatabase.All.Count);
            foreach (var card in CardDatabase.All.Values)
            { Check(card.Cost >= 0 && card.Cost <= 2, card.Id); Check(CardText.Describe(card, false).Length > 5, card.Id); Check(CardText.Describe(card, true).Length > 5, card.Id); Equal(card.Id, card.Id.ToLowerInvariant()); }
            foreach (var hero in HeroDatabase.All) { Equal(5, hero.StartingDeck.Count); foreach (string id in hero.StartingDeck) Equal(hero.Id, CardDatabase.Get(id).HeroId); }
        }
        [Test] public static void MapsConnectedAndBossFarthestFor1000Seeds()
        {
            for (int seed = 0; seed < 1000; seed++)
            {
                var map = MapGenerator.Generate(new GameRng(seed), seed % 3 + 1); var distances = map.Distances(map.StartId);
                Check(map.Rooms.Count >= 9 && map.Rooms.Count <= 11, "Room bounds seed " + seed); Equal(map.Rooms.Count, distances.Count);
                int max = 0, treasure = 0, bedroom = 0, curio = 0;
                foreach (var room in map.Rooms)
                {
                    max = Math.Max(max, distances[room.Id]); Check(room.X >= 0 && room.X < 5 && room.Y >= 0 && room.Y < 4, "Grid");
                    if (room.Type == RoomType.Treasure) { treasure++; Equal(1, room.Neighbors.Count); }
                    if (room.Type == RoomType.Bedroom) bedroom++; if (room.Type == RoomType.Curio) curio++;
                    foreach (int n in room.Neighbors) { var other = map.GetRoom(n); Equal(1, Math.Abs(room.X - other.X) + Math.Abs(room.Y - other.Y)); Check(other.Neighbors.Contains(room.Id), "Undirected"); }
                }
                Equal(max, distances[map.BossId]); Equal(1, treasure); Equal(1, bedroom); Check(curio >= 1 && curio <= 2, "Curio bounds");
                Check(map.Corridors.Count >= map.Rooms.Count && map.Corridors.Count <= map.Rooms.Count + 1, "Extra edge count");
                Check(map.GetRoom(map.BossId).Revealed, "Boss visible");
                foreach (var corridor in map.Corridors) Equal(3, corridor.Segments.Count);
            }
        }
        [Test] public static void ScoutingRevealsNeighborsOnly()
        {
            var map = MapGenerator.Generate(new GameRng(3), 1);
            foreach (var room in map.Rooms) Equal(room.Id == 0 || room.Id == map.BossId || map.GetRoom(0).Neighbors.Contains(room.Id), room.Revealed);
            int next = map.GetRoom(0).Neighbors[0]; map.Visit(next); foreach (int id in map.GetRoom(next).Neighbors) Check(map.GetRoom(id).Revealed, "Scouting");
        }
        [Test] public static void RngForksAreStableAndIndependent()
        {
            var root = new GameRng(987); var first = root.Fork("map"); var second = root.Fork("map");
            for (int i = 0; i < 100; i++) { root.Fork("combat").Range(0, 100); Equal(first.Range(-3, 9), second.Range(-3, 9)); }
            bool differs = false; first = root.Fork("map"); second = root.Fork("loot"); for (int i = 0; i < 20; i++) if (first.Range(0, 100) != second.Range(0, 100)) differs = true; Check(differs, "Distinct streams");
        }
        [Test] public static void ShuffleAndDeckPreserveMultiset()
        {
            var cards = new List<CardInstance>(); for (int i = 0; i < 20; i++) cards.Add(new CardInstance(CardDatabase.Get("tear"), "c" + i));
            var deck = new Deck(cards, new GameRng(8)); deck.InitialShuffle();
            for (int cycle = 0; cycle < 20; cycle++)
            { deck.Draw(20); var ids = new HashSet<string>(); foreach (var c in deck.Hand) ids.Add(c.Uid); Equal(20, ids.Count); Equal(0, deck.DrawPile.Count); Equal(0, deck.DiscardPile.Count); deck.DiscardHand(); }
        }
        [Test] public static void DamageFormulaWeakMarkCritBlockStrength()
        {
            var e = Engine("azazel", "gaper"); var h = e.State.Heroes[0]; var target = e.State.Enemies[0];
            Equal(6, DamageCalculator.Calculate(h, target, 6, false).Amount);
            h.Statuses[Status.Strength] = 2; Equal(8, DamageCalculator.Calculate(h, target, 6, false).Amount);
            h.Statuses[Status.Weak] = 1; Equal(6, DamageCalculator.Calculate(h, target, 6, false).Amount);
            target.Statuses[Status.Mark] = 1; Equal(9, DamageCalculator.Calculate(h, target, 6, false).Amount);
            target.Statuses[Status.Block] = 4; var hit = DamageCalculator.Calculate(h, target, 6, true); Equal(13, hit.Raw); Equal(4, hit.Blocked); Equal(9, hit.Amount);
            Equal(4, target.Statuses[Status.Block]); Equal(target.MaxHp, target.Hp);
        }
        [Test] public static void DotsTickDecayAndBypassBlock()
        {
            var e = Engine("azazel", "nerve_ending"); var hero = e.State.Heroes[0]; hero.Statuses[Status.Bleed] = 3; hero.Statuses[Status.Poison] = 2; hero.Statuses[Status.Block] = 10;
            e.StartCombat(); Equal(29, hero.Hp); Equal(2, hero.Statuses[Status.Bleed]); Equal(1, hero.Statuses[Status.Poison]); Equal(0, hero.Statuses[Status.Block]);
            NextRound(e); Equal(26, hero.Hp); Equal(1, hero.Statuses[Status.Bleed]); Equal(0, hero.Statuses[Status.Poison]);
        }
        [Test] public static void StunSkipsExactlyOneTurn()
        {
            var e = Engine("azazel", "nerve_ending"); e.State.Heroes[0].Statuses[Status.Stun] = 1; e.StartCombat();
            int skipped = 0; foreach (var ev in e.Events) if (ev is TurnSkipped) skipped++; Equal(1, skipped); Equal(2, e.State.Round); Equal(0, e.State.Heroes[0].Statuses[Status.Stun]);
        }
        [Test] public static void TauntForcesBothSidesAndPersistsOnNerve()
        {
            var e = Engine("azazel", "gaper", "nerve_ending"); e.StartCombat(); var hero = (HeroUnit)e.CurrentActor; var card = Give(e, "brimstone_swipe");
            Equal(1, e.ValidTargets(hero, card).Count); Equal("nerve_ending", ((EnemyUnit)e.ValidTargets(hero, card)[0]).Def.Id);
            var state = new RunState(4); var rng = new GameRng(4); var combat = new CombatState(state.Party, new EncounterDef(1, false, false, 0, "gaper"), rng);
            combat.Heroes[3].Statuses[Status.Taunt] = 1;
            var intent = EnemyAI.Choose(combat, combat.Enemies[0], rng); Equal("lilith", intent.TargetIds[0]);
            NextRound(e); Equal(1, e.State.Enemies[1].Statuses[Status.Taunt]);
        }
        [Test] public static void DeathCompactsRanksAndEmitsMovement()
        {
            var e = Engine("azazel", "fly", "gaper", "pooter"); e.StartCombat(); var hero = (HeroUnit)e.CurrentActor; var card = Give(e, "brimstone_swipe");
            e.State.Enemies[0].Hp = 6; e.PlayCard(hero, card, e.State.Enemies[0]); Check(!e.State.Enemies[0].IsAlive, "Fly dead"); Equal(1, e.State.Enemies[1].Rank); Equal(2, e.State.Enemies[2].Rank);
            int moved = 0; foreach (var ev in e.Events) if (ev is UnitMoved) moved++; Equal(2, moved);
        }
        [Test] public static void MulliganSpawnsTwoFliesThatWaitUntilNextRound()
        {
            var e = Engine("azazel", "mulligan"); e.StartCombat(); e.State.Enemies[0].Hp = 1; var hero = (HeroUnit)e.CurrentActor; var card = Give(e, "brimstone_swipe"); e.PlayCard(hero, card, e.State.Enemies[0]);
            Equal(2, e.State.Living(false).Count); foreach (var enemy in e.State.Enemies) if (enemy.IsAlive) { Equal("fly", enemy.Def.Id); Equal(e.State.Round, enemy.JoinedRound); }
            foreach (var unit in TurnOrder.Roll(e.State, new GameRng(1))) Check(unit.IsHero, "New summon cannot act this round");
        }
        [Test] public static void GlobinRevivesOnlyOnce()
        {
            var e = Engine("azazel", "globin"); e.StartCombat(); var hero = (HeroUnit)e.CurrentActor; var enemy = e.State.Enemies[0]; enemy.Hp = 1;
            var card = Give(e, "brimstone_swipe"); e.PlayCard(hero, card, enemy); Equal((enemy.MaxHp + 1) / 2, enemy.Hp); Check(enemy.HasRevived, "Revive");
            enemy.Hp = 1; card = Give(e, "brimstone_swipe"); e.PlayCard(hero, card, enemy); Check(e.IsOver && e.Victory, "Second death is final"); Equal(1, e.State.Kills);
        }
        [Test] public static void HostReductionAndAlternatingOpenState()
        {
            var e = Engine("azazel", "host"); var hero = e.State.Heroes[0]; var enemy = e.State.Enemies[0]; Equal(2, DamageCalculator.Calculate(hero, enemy, 8, false).Raw);
            e.StartCombat(); Check(!enemy.HostOpen, "Closed odd round"); NextRound(e); Check(enemy.HostOpen, "Open even round"); Equal(8, DamageCalculator.Calculate(hero, enemy, 8, false).Raw);
        }
        [Test] public static void MomPhaseTwoHasTwoActionsBelowHalfOnly()
        {
            var e = Engine("azazel", "mom"); var enemy = e.State.Enemies[0]; enemy.Hp = enemy.MaxHp / 2; Equal(1, enemy.Phase); enemy.Hp--; Equal(2, enemy.Phase); e.StartCombat();
            Check(enemy.Intent.SecondAction != null, "Second intent visible"); e.EndTurn(); int actions = 0; bool sawTurn = false; foreach (var ev in e.Events) { if (ev is TurnStarted turn && turn.UnitId == enemy.Id) { if (sawTurn) break; sawTurn = true; } if (ev is EnemyActed acted && acted.EnemyId == enemy.Id) actions++; } Check(sawTurn, "Mom acted"); Equal(2, actions);
        }
        [Test] public static void BossEyeshadowWeakInsteadOfStun()
        {
            var state = new RunState(2); var rng = new GameRng(2); var e = new CombatEngine(new CombatState(state.Party, new EncounterDef(1, true, false, 0, "monstro"), rng), rng); e.StartCombat();
            int guard = 0; while (e.CurrentActor.Id != "lilith" && guard++ < 20) e.EndTurn(); var hero = (HeroUnit)e.CurrentActor; var card = Give(e, "moms_eyeshadow"); e.PlayCard(hero, card, e.State.Enemies[0]); Equal(2, e.State.Enemies[0].Statuses[Status.Weak]); Equal(0, e.State.Enemies[0].Statuses[Status.Stun]);
        }
        [Test] public static void RewardsUseLivingPoolsAndBossRares()
        {
            var state = new RunState(3); foreach (var hero in state.Party) if (hero.Def.Id != "isaac") hero.Hp = 0; var rng = new GameRng(3);
            int common = 0, uncommon = 0, rare = 0;
            for (int i = 0; i < 10000; i++) { var card = RewardGenerator.RollCard(state, rng); Equal("isaac", card.HeroId); if (card.Rarity == Rarity.Common) common++; if (card.Rarity == Rarity.Uncommon) uncommon++; if (card.Rarity == Rarity.Rare) rare++; }
            Check(common > 6300 && common < 6700 && uncommon > 2800 && uncommon < 3200 && rare > 400 && rare < 600, "Rarity proportions");
            for (int i = 0; i < 100; i++) { Equal(Rarity.Rare, RewardGenerator.RollCard(state, rng, true).Rarity); bool higher = false; foreach (var card in RewardGenerator.Treasure(state, rng)) if (card.Rarity >= Rarity.Uncommon) higher = true; Check(higher, "Treasure guarantee"); }
        }
        private static void Throws(Action action)
        { bool threw = false; try { action(); } catch (InvalidOperationException) { threw = true; } Check(threw, "Expected rejected action"); }
        [Test] public static void MoveSwapsRanksAndCostsOneEnergy()
        {
            var state = new RunState(8); var rng = new GameRng(8); var e = new CombatEngine(new CombatState(state.Party, new EncounterDef(1, false, false, 0, "nerve_ending"), rng), rng); e.StartCombat();
            var hero = (HeroUnit)e.CurrentActor; Equal(3, hero.Deck.Hand.Count); Equal(2, hero.Energy); int old = hero.Rank; int direction = old == 1 ? 1 : -1;
            Unit neighbor = null; foreach (var unit in e.State.Heroes) if (unit.Rank == old + direction) neighbor = unit;
            e.Move(hero, direction); Equal(1, hero.Energy); Equal(old + direction, hero.Rank); Equal(old, neighbor.Rank);
            Throws(() => e.Move(hero, 2)); Equal(1, hero.Energy);
            e.Move(hero, -direction); Equal(0, hero.Energy); Throws(() => e.Move(hero, direction));
        }
        [Test] public static void EndTurnDiscardsAndEventsBelongToLatestCommand()
        {
            var rng = new GameRng(8); var e = new CombatEngine(new CombatState(new RunState(8).Party, new EncounterDef(1, false, false, 0, "nerve_ending"), rng), rng); e.StartCombat();
            var hero = (HeroUnit)e.CurrentActor; var oldHand = new List<CardInstance>(hero.Deck.Hand); Equal(3, oldHand.Count); e.EndTurn();
            Equal(0, hero.Deck.Hand.Count); Equal(0, hero.Energy); foreach (var card in oldHand) Check(hero.Deck.DiscardPile.Contains(card), "Old hand discarded");
            foreach (var ev in e.Events) Check(!(ev is RoundStarted), "No previous command events");
        }
        [Test] public static void InvalidCardCommandsDoNotConsumeEnergyOrHand()
        {
            var e = Engine("azazel", "gaper"); e.StartCombat(); var hero = (HeroUnit)e.CurrentActor; var card = Give(e, "brimstone_swipe");
            int energy = hero.Energy, count = hero.Deck.Hand.Count;
            Throws(() => e.PlayCard(hero, card, hero)); Equal(energy, hero.Energy); Equal(count, hero.Deck.Hand.Count);
            var wrong = Give(e, "tear"); Check(!e.CanPlay(hero, wrong, out string reason) && reason.Length > 0, "Explain wrong hero");
        }
        [Test] public static void EveryCardResolvesThroughPublicEngine()
        {
            foreach (var def in CardDatabase.All.Values)
            {
                var state = new RunState(11); foreach (var member in state.Party) member.Hp -= 5;
                var rng = new GameRng(11); var e = new CombatEngine(new CombatState(state.Party, new EncounterDef(1, false, false, 0, "nerve_ending", "nerve_ending", "nerve_ending", "nerve_ending"), rng), rng); e.StartCombat();
                int turns = 0; while (e.CurrentActor.Id != def.HeroId && turns++ < 20) e.EndTurn();
                var hero = (HeroUnit)e.CurrentActor; var card = Give(e, def.Id); Check(e.CanPlay(hero, card, out string reason), def.Id + ": " + reason);
                var targets = e.ValidTargets(hero, card); e.PlayCard(hero, card, targets[0]);
                Check(hero.Deck.DiscardPile.Contains(card), def.Id + " resolved into discard");
            }
        }
        [Test] public static void UpgradesUseCeilingMinimumTwoOrCostReduction()
        {
            Equal(8, CardDatabase.Get("brimstone_swipe").Effects[0].Value(true));
            Equal(10, CardDatabase.Get("demon_wings").Effects[0].Value(true));
            Equal(0, CardDatabase.Get("pentagram").EnergyCost(true));
            var penny = CardDatabase.Get("lucky_penny"); Equal(3, penny.EffectValue(penny.Effects[0], true));
            var abaddon = CardDatabase.Get("abaddon"); Equal(4, abaddon.EffectValue(abaddon.Effects[0], true));
            var robes = CardDatabase.Get("ceremonial_robes"); Equal(1, robes.EffectValue(robes.Effects[1], true));
            Equal(5, CardDatabase.Get("abaddon").Effects[1].Value(true));
        }
        [Test] public static void MultiHitStrengthBleedAndCritStun()
        {
            var state = new RunState(12); var rng = new GameRng(12); var e = new CombatEngine(new CombatState(state.Party, new EncounterDef(1, false, false, 0, "monstro"), rng), rng); e.StartCombat();
            int guard = 0; while (e.CurrentActor.Id != "lilith" && guard++ < 20) e.EndTurn();
            var hero = (HeroUnit)e.CurrentActor; hero.Statuses[Status.Strength] = 2; int hp = e.State.Enemies[0].Hp; var card = Give(e, "twisted_pair"); e.PlayCard(hero, card, e.State.Enemies[0]);
            Equal(hp - 16, e.State.Enemies[0].Hp); Equal(2, e.State.Enemies[0].Statuses[Status.Bleed]); int hits = 0;
            foreach (var ev in e.Events) if (ev is Damaged damage && damage.CardId == "twisted_pair") { Equal(hits, damage.HitIndex); hits++; } Equal(2, hits);
            bool sawCrit = false;
            for (int seed = 0; seed < 30 && !sawCrit; seed++)
            {
                rng = new GameRng(seed); e = new CombatEngine(new CombatState(new RunState(seed).Party, new EncounterDef(1, false, false, 0, "monstro"), rng), rng); e.StartCombat();
                guard = 0; while (e.CurrentActor.Id != "cain" && guard++ < 20) e.EndTurn(); hero = (HeroUnit)e.CurrentActor; card = Give(e, "midas_touch"); e.PlayCard(hero, card, e.State.Enemies[0]);
                foreach (var ev in e.Events) if (ev is Damaged damage && damage.Crit) sawCrit = true;
                Equal(sawCrit ? 1 : 0, e.State.Enemies[0].Statuses[Status.Stun]);
            }
            Check(sawCrit, "Crit sampled");
        }
        [Test] public static void WeakMarkExpireButStrengthAndNewTauntPersist()
        {
            var e = Engine("azazel", "nerve_ending"); var hero = e.State.Heroes[0]; hero.Statuses[Status.Weak] = 2; hero.Statuses[Status.Mark] = 2; hero.Statuses[Status.Strength] = 2; e.StartCombat();
            var card = Give(e, "goat_head"); e.PlayCard(hero, card, hero); Equal(1, hero.Statuses[Status.Taunt]); e.EndTurn();
            Equal(1, hero.Statuses[Status.Weak]); Equal(1, hero.Statuses[Status.Mark]); Equal(2, hero.Statuses[Status.Strength]); Equal(1, hero.Statuses[Status.Taunt]);
            e.EndTurn(); Equal(0, hero.Statuses[Status.Weak]); Equal(0, hero.Statuses[Status.Mark]); Equal(0, hero.Statuses[Status.Taunt]);
        }
        [Test] public static void GurdyNeverExceedsThreeMinions()
        {
            var rng = new GameRng(4); var e = new CombatEngine(new CombatState(new RunState(4).Party, new EncounterDef(2, true, false, 0, "gurdy"), rng), rng); e.StartCombat();
            int steps = 0; bool summoned = false;
            while (!e.IsOver && e.State.Round < 12 && steps++ < 100)
            { e.EndTurn(); foreach (var ev in e.Events) if (ev is Summoned) summoned = true; Check(e.State.Living(false).Count <= 4, "Three minions plus Gurdy"); }
            Check(summoned, "Gurdy used Spawn");
        }
        [Test] public static void ChargerAdvancesToFrontAndMomHandPullsBackHero()
        {
            var rng = new GameRng(7); var e = new CombatEngine(new CombatState(new RunState(7).Party, new EncounterDef(2, false, false, 0, "gaper", "charger"), rng), rng); e.StartCombat();
            int guard = 0; while (e.State.Enemies[1].Rank != 1 && guard++ < 20) e.EndTurn(); Equal(1, e.State.Enemies[1].Rank); Equal(2, e.State.Enemies[0].Rank);
            bool pulled = false;
            for (int seed = 0; seed < 100 && !pulled; seed++)
            {
                rng = new GameRng(seed); e = new CombatEngine(new CombatState(new RunState(seed).Party, new EncounterDef(3, true, false, 0, "mom"), rng), rng); e.StartCombat();
                if (e.State.Enemies[0].Intent.Ability.Id != "moms_hand") continue;
                guard = 0; while (e.State.Heroes[3].Rank == 4 && !e.IsOver && guard++ < 20) e.EndTurn(); Equal(1, e.State.Heroes[3].Rank); pulled = true;
            }
            Check(pulled, "Mom hand sampled");
        }
        [Test] public static void EnemyIntentsRespectAbilityRanksAndConditions()
        {
            var rng = new GameRng(1); var state = new CombatState(new RunState(1).Party, new EncounterDef(1, false, false, 0, "horf"), rng);
            Equal("reposition", EnemyAI.Choose(state, state.Enemies[0], rng).Ability.Id);
            state.Enemies[0].Rank = 3; Equal("blood_shot", EnemyAI.Choose(state, state.Enemies[0], rng).Ability.Id);
            state = new CombatState(new RunState(1).Party, new EncounterDef(3, false, false, 0, "host"), rng); state.Enemies[0].Rank = 2;
            Equal("closed", EnemyAI.Choose(state, state.Enemies[0], rng).Ability.Id);
        }
        [Test] public static void ConcessionIsAnExplicitDefeat()
        { var e = Engine("lilith", "nerve_ending"); e.StartCombat(); Check(new AutoPlayer().HasNoOffense(e.State), "Compacted Lilith cannot attack from rank one"); e.Concede(); Check(e.IsOver && !e.Victory, "Defeat"); Equal(1, e.Events.Count); Check(e.Events[0] is CombatEnded, "Defeat event"); }
        private static void WinPending(Run run, bool take = false)
        {
            var engine = run.CreateCombat(run.PendingEncounter); engine.StartCombat();
            // Izolăm tranzițiile run-ului; regulile damage sunt verificate separat.
            foreach (var enemy in engine.State.Enemies) enemy.Hp = 0;
            if (!engine.IsOver) engine.EndTurn();
            Check(engine.IsOver && engine.Victory, "Fixture victory"); run.ApplyCombatResult(engine);
            if (take) run.TakeCard(); else run.SkipCard();
        }
        private static void WalkTo(Run run, int goal)
        {
            int steps = 0;
            while (run.State.CurrentRoom != goal)
            {
                Check(steps++ < 100, "Test route bound"); var distances = run.Map.Distances(goal); int next = -1, distance = int.MaxValue;
                foreach (int id in run.Map.GetRoom(run.State.CurrentRoom).Neighbors) if (distances[id] < distance) { next = id; distance = distances[id]; }
                run.BeginTravel(next);
                while (true)
                {
                    var segment = run.AdvanceTravel(); if (segment == null) break;
                    if (segment.Resolved) continue;
                    if (segment.Event == CorridorEventKind.Ambush) WinPending(run);
                    else if (segment.Event == CorridorEventKind.Trap) run.ResolveTrap();
                    else if (segment.Event == CorridorEventKind.Curio) { run.ResolveCurio(); if (run.Phase == RunPhase.CardReward) run.SkipCard(); }
                }
                var room = run.EnterRoom();
                if (next == goal) return;
                if (run.PendingEncounter != null) WinPending(run);
                if (run.Phase == RunPhase.Treasure) run.ChooseTreasure(0);
                if (run.Phase == RunPhase.Bedroom) run.UseBedroom(BedroomAction.Heal);
                if (run.Phase == RunPhase.Curio) { run.ResolveCurio(); if (run.Phase == RunPhase.CardReward) run.SkipCard(); }
                Check(room.Visited, "Entered room visited");
            }
        }
        [Test] public static void RunTravelEnforcesSegmentOrderAndEarlySafety()
        {
            for (int seed = 1; seed <= 100; seed++)
            {
                var run = new Run(seed); int destination = run.AvailableDestinations[0].Id; var corridor = run.BeginTravel(destination); Throws(() => run.EnterRoom());
                for (int i = 0; i < 3; i++)
                {
                    var segment = run.AdvanceTravel(); Check(segment.Event != CorridorEventKind.Ambush, "First corridor safe");
                    if (!segment.Resolved)
                    { Throws(() => run.AdvanceTravel()); if (segment.Event == CorridorEventKind.Curio) { run.ResolveCurio(); if (run.Phase == RunPhase.CardReward) run.SkipCard(); } else run.ResolveTrap(); }
                }
                Equal(null, run.AdvanceTravel()); run.EnterRoom(); Check(corridor.Cleared, "Corridor cleared"); Equal(3, corridor.TravelSpeedMultiplier);
                if (run.PendingEncounter != null) { Equal(0, run.AvailableDestinations.Count); Throws(() => run.BeginTravel(0)); }
            }
        }
        [Test] public static void TreasureChoicePersistsExactlyOneOwnedCard()
        {
            var run = new Run(24); Room treasure = null; foreach (var room in run.Map.Rooms) if (room.Type == RoomType.Treasure) treasure = room;
            WalkTo(run, treasure.Id); Equal(RunPhase.Treasure, run.Phase); var chosen = run.TreasureChoices[1]; int count = run.State.Stats.CardsTaken;
            run.ChooseTreasure(1); Equal(count + 1, run.State.Stats.CardsTaken); Check(treasure.Cleared, "Treasure consumed");
            bool owned = false; foreach (var hero in run.State.Party) foreach (var card in hero.Deck) if (card.Def == chosen) { owned = true; Equal(hero.Def.Id, chosen.HeroId); }
            Check(owned, "Chosen card in persistent deck"); Throws(() => run.ChooseTreasure(0));
        }
        [Test] public static void BedroomSupportsHealRemoveAndUpgrade()
        {
            foreach (BedroomAction action in new[] { BedroomAction.Heal, BedroomAction.RemoveCard, BedroomAction.UpgradeCard })
            {
                var run = new Run(31); Room bedroom = null; foreach (var room in run.Map.Rooms) if (room.Type == RoomType.Bedroom) bedroom = room;
                WalkTo(run, bedroom.Id); var hero = run.State.Party[0]; hero.Hp = 10; var card = hero.Deck[0]; int cards = hero.Deck.Count;
                run.UseBedroom(action, hero.Def.Id, card.Uid); Equal(RunPhase.Map, run.Phase); Check(bedroom.Cleared, "Bedroom consumed");
                if (action == BedroomAction.Heal) Equal(22, hero.Hp);
                if (action == BedroomAction.RemoveCard) { Equal(cards - 1, hero.Deck.Count); Check(!hero.Deck.Contains(card), "Removed"); }
                if (action == BedroomAction.UpgradeCard) Check(card.Upgraded, "Persistent upgrade");
                Throws(() => run.UseBedroom(action, hero.Def.Id, card.Uid));
            }
        }
        [Test] public static void CombatResultPersistsDeathsDecksAndStatistics()
        {
            var run = new Run(1); Room combatRoom = null; foreach (var room in run.Map.Rooms) if (room.Type == RoomType.Combat) { combatRoom = room; break; }
            WalkTo(run, combatRoom.Id); var engine = run.CreateCombat(run.PendingEncounter); engine.StartCombat();
            engine.State.Heroes[0].Hp = 0; engine.State.Heroes[1].Hp = 7;
            foreach (var enemy in engine.State.Enemies) enemy.Hp = 0; engine.EndTurn();
            int previous = run.State.Stats.Combats; run.ApplyCombatResult(engine); Equal(previous + 1, run.State.Stats.Combats); Equal(0, run.State.Party[0].Hp); Equal(7, run.State.Party[1].Hp);
            var card = run.RollCardReward(); Check(card.HeroId != "azazel", "Dead hero excluded"); int taken = run.State.Stats.CardsTaken; run.TakeCard(); Equal(taken + 1, run.State.Stats.CardsTaken);
            Equal(1, run.State.Party[1].Rank); Throws(() => run.ApplyCombatResult(engine));
        }
        [Test] public static void BossRewardAndDescentAdvanceAllThreeFloors()
        {
            var run = new Run(52); Throws(() => run.Descend());
            for (int floor = 1; floor <= 3; floor++)
            {
                Equal(floor, run.CurrentFloor.Index); WalkTo(run, run.Map.BossId); var engine = run.CreateCombat(run.PendingEncounter); engine.StartCombat(); foreach (var enemy in engine.State.Enemies) enemy.Hp = 0; engine.EndTurn(); run.ApplyCombatResult(engine);
                Equal(Rarity.Rare, run.RollCardReward().Rarity); run.SkipCard(); Check(run.CanDescend, "Trapdoor unlocked");
                run.State.Party[0].Hp = 10; run.State.Party[3].Hp = 0; run.Descend();
                if (floor < 3) { Equal(19, run.State.Party[0].Hp); Equal(0, run.State.Party[3].Hp); Equal(floor + 1, run.Map.Floor); }
            }
            Check(run.IsRunOver && run.IsVictory, "Final victory"); Equal(3, run.State.Stats.FloorsCleared);
        }
        [Test] public static void EncounterBudgetsGrowAndRespectFloorPools()
        {
            var rng = new GameRng(13);
            for (int floor = 1; floor <= 3; floor++)
            {
                var first = EncounterGenerator.Generate(floor, 0, rng); var later = EncounterGenerator.Generate(floor, 8, rng); Check(later.Budget > first.Budget, "Difficulty growth");
                foreach (var encounter in new[] { first, later, EncounterGenerator.Generate(floor, 2, rng, ambush: true) })
                {
                    Check(encounter.EnemyIds.Count >= 2 && encounter.EnemyIds.Count <= 4, "Group size"); int budget = 0;
                    foreach (string id in encounter.EnemyIds) { Equal(floor, EnemyDatabase.Get(id).Floor); budget += EnemyDatabase.Get(id).Budget; }
                    Check(budget <= encounter.Budget, "Within budget");
                }
            }
        }
        [Test] public static void FullRunsAreDeterministic()
        { var sim = new RunSimulator(); for (int seed = 1; seed <= 5; seed++) Equal(sim.Play(seed).Fingerprint, sim.Play(seed).Fingerprint); }
        [Test] public static void TwoHundredFullRunsTerminate()
        { var sim = new RunSimulator(); for (int seed = 1; seed <= 200; seed++) { var r = sim.Play(seed); Check(r.Combats > 0, "No combats"); Check(r.Fingerprint.Length > 20, "Result fingerprint"); } }
    }
}
