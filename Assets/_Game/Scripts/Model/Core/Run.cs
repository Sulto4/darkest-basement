using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DarkestBasement.Model.Combat;
using DarkestBasement.Model.Data;
using DarkestBasement.Model.Map;
using DarkestBasement.Model.Rewards;

namespace DarkestBasement.Model.Core
{
    public enum RunPhase { Map, Travel, Combat, CardReward, Treasure, Bedroom, Curio, Over }
    public enum BedroomAction { Heal, RemoveCard, UpgradeCard }
    public enum ExplorationOutcomeKind { None, Heal, Card, Trap, Avoided }
    public sealed class ExplorationOutcome
    {
        public ExplorationOutcomeKind Kind { get; }
        public string HeroId { get; }
        public int Amount { get; }
        public IReadOnlyDictionary<string, int> HpChanges { get; }
        public ExplorationOutcome(ExplorationOutcomeKind kind, string hero = null, int amount = 0, IDictionary<string, int> hpChanges = null)
        {
            Kind = kind; HeroId = hero; Amount = amount;
            var changes = hpChanges == null ? new Dictionary<string, int>() : new Dictionary<string, int>(hpChanges);
            if (hpChanges == null && hero != null && amount != 0) changes[hero] = kind == ExplorationOutcomeKind.Trap ? -amount : amount;
            HpChanges = new ReadOnlyDictionary<string, int>(changes);
        }
    }
    public sealed class Run
    {
        private GameRng mapRng, combatRng, lootRng;
        private Corridor travel;
        private int destination, segmentIndex, floorCombats;
        private bool reverseTravel, bossDefeated, victory, fightingSegment;
        private CorridorSegment activeSegment;
        private CombatEngine activeCombat;
        public RunState State { get; private set; }
        public FloorMap Map { get; private set; }
        public FloorDef CurrentFloor { get { return FloorDatabase.Get(State.FloorIndex); } }
        public RunPhase Phase { get; private set; }
        public EncounterDef PendingEncounter { get; private set; }
        public CardDef PendingCard { get; private set; }
        public IReadOnlyList<CardDef> TreasureChoices { get; private set; }
        public bool IsRunOver { get { return State != null && (victory || !State.AnyAlive || Phase == RunPhase.Over); } }
        public bool IsVictory { get { return victory; } }
        public bool CanDescend { get { return Phase == RunPhase.Map && bossDefeated && State.CurrentRoom == Map.BossId; } }
        public IReadOnlyList<Room> AvailableDestinations
        {
            get { var result = new List<Room>(); if (Phase == RunPhase.Map && !IsRunOver && PendingEncounter == null) foreach (int id in Map.GetRoom(State.CurrentRoom).Neighbors) result.Add(Map.GetRoom(id)); return result.AsReadOnly(); }
        }
        public Run() { }
        public Run(int seed) { StartRun(seed); }
        public void StartRun(int seed)
        {
            State = new RunState(seed); var root = new GameRng(seed); mapRng = root.Fork("map"); combatRng = root.Fork("combat"); lootRng = root.Fork("loot");
            victory = false; bossDefeated = false; floorCombats = 0; travel = null; activeSegment = null; activeCombat = null; PendingEncounter = null; PendingCard = null; TreasureChoices = null;
            Map = MapGenerator.Generate(mapRng, 1); State.CurrentRoom = Map.StartId; Phase = RunPhase.Map;
        }
        public Corridor BeginTravel(int roomId)
        {
            Require(RunPhase.Map); if (PendingEncounter != null) throw new InvalidOperationException("Resolve this room encounter first."); if (IsRunOver) throw new InvalidOperationException("Run ended.");
            travel = Map.GetCorridor(State.CurrentRoom, roomId); destination = roomId; reverseTravel = travel.To == State.CurrentRoom; segmentIndex = 0; activeSegment = null; Phase = RunPhase.Travel; return travel;
        }
        public CorridorSegment AdvanceTravel()
        {
            Require(RunPhase.Travel);
            if (activeSegment != null && !activeSegment.Resolved) throw new InvalidOperationException("Resolve the current segment first.");
            if (segmentIndex >= 3) return null;
            int index = reverseTravel ? 2 - segmentIndex : segmentIndex; segmentIndex++;
            activeSegment = travel.Segments[index]; activeSegment.Revealed = true;
            int visited = 0; foreach (var room in Map.Rooms) if (room.Visited) visited++;
            if (State.FloorIndex == 1 && visited <= 2 && activeSegment.Event == CorridorEventKind.Ambush) activeSegment.Event = CorridorEventKind.None;
            if (activeSegment.Event == CorridorEventKind.None || activeSegment.Resolved) activeSegment.Resolved = true;
            else if (activeSegment.Event == CorridorEventKind.Ambush) PendingEncounter = EncounterGenerator.Generate(State.FloorIndex, floorCombats, combatRng, ambush: true);
            return activeSegment;
        }
        public ExplorationOutcome ResolveTrap()
        {
            Require(RunPhase.Travel); RequireSegment(CorridorEventKind.Trap);
            ExplorationOutcome result = Trap(); activeSegment.Resolved = true; return result;
        }
        private ExplorationOutcome Trap()
        {
            var living = new List<PartyHero>(); foreach (var hero in State.Party) if (hero.IsAlive) living.Add(hero);
            PartyHero fastest = living[0]; foreach (var hero in living) if (hero.Def.Speed > fastest.Def.Speed) fastest = hero;
            if (lootRng.Chance(0.5)) return new ExplorationOutcome(ExplorationOutcomeKind.Avoided, fastest.Def.Id);
            PartyHero target = lootRng.Pick(living); int damage = Math.Min(target.Hp, 2 + State.FloorIndex); target.Hp -= damage; CompactParty();
            if (!State.AnyAlive) Phase = RunPhase.Over;
            return new ExplorationOutcome(ExplorationOutcomeKind.Trap, target.Def.Id, damage);
        }
        public ExplorationOutcome ResolveCurio()
        {
            bool corridor = Phase == RunPhase.Travel;
            if (corridor) RequireSegment(CorridorEventKind.Curio); else Require(RunPhase.Curio);
            int roll = lootRng.Range(0, 3); ExplorationOutcome outcome;
            if (roll == 0)
            {
                var previous = new Dictionary<string, int>(); foreach (var hero in State.Party) if (hero.IsAlive) previous[hero.Def.Id] = hero.Hp;
                RewardGenerator.HealParty(State, 0.12); var changes = new Dictionary<string, int>(); int total = 0;
                foreach (var hero in State.Party) if (hero.IsAlive) { int amount = hero.Hp - previous[hero.Def.Id]; changes.Add(hero.Def.Id, amount); total += amount; }
                outcome = new ExplorationOutcome(ExplorationOutcomeKind.Heal, amount: total, hpChanges: changes);
            }
            else if (roll == 1) { PendingCard = RewardGenerator.RollCard(State, lootRng); outcome = new ExplorationOutcome(ExplorationOutcomeKind.Card); }
            else outcome = Trap();
            if (corridor) activeSegment.Resolved = true; else Map.GetRoom(State.CurrentRoom).Cleared = true;
            if (PendingCard != null) Phase = RunPhase.CardReward;
            else if (!IsRunOver) Phase = corridor ? RunPhase.Travel : RunPhase.Map;
            return outcome;
        }
        public Room EnterRoom()
        {
            Require(RunPhase.Travel);
            if (segmentIndex != 3 || (activeSegment != null && !activeSegment.Resolved)) throw new InvalidOperationException("Finish all three corridor segments.");
            State.CurrentRoom = destination; Room room = Map.GetRoom(destination); Map.Visit(destination); travel = null; activeSegment = null;
            Phase = RunPhase.Map;
            if (room.Cleared) return room;
            if (room.Type == RoomType.Combat || room.Type == RoomType.Boss) PendingEncounter = EncounterGenerator.Generate(State.FloorIndex, floorCombats, combatRng, room.Type == RoomType.Boss);
            else if (room.Type == RoomType.Treasure) { TreasureChoices = RewardGenerator.Treasure(State, lootRng); Phase = RunPhase.Treasure; }
            else if (room.Type == RoomType.Bedroom) Phase = RunPhase.Bedroom;
            else if (room.Type == RoomType.Curio) Phase = RunPhase.Curio;
            else room.Cleared = true;
            return room;
        }
        public CombatEngine CreateCombat(EncounterDef encounter)
        {
            if (activeCombat != null || PendingEncounter == null || !ReferenceEquals(encounter, PendingEncounter) || (Phase != RunPhase.Map && Phase != RunPhase.Travel)) throw new InvalidOperationException("No pending encounter.");
            fightingSegment = Phase == RunPhase.Travel;
            activeCombat = new CombatEngine(new CombatState(State.Party, encounter, combatRng), combatRng); Phase = RunPhase.Combat; return activeCombat;
        }
        public void ApplyCombatResult(CombatEngine engine)
        {
            Require(RunPhase.Combat); if (engine != activeCombat || !engine.IsOver) throw new InvalidOperationException("Combat is not finished.");
            foreach (var hero in engine.State.Heroes) foreach (var member in State.Party) if (hero.Def.Id == member.Def.Id) { member.Hp = hero.Hp; member.Rank = hero.Rank; }
            State.Stats.Combats++; State.Stats.Kills += engine.State.Kills; State.Stats.ElapsedRounds += engine.State.Round; floorCombats++; CompactParty();
            activeCombat = null; PendingEncounter = null;
            if (!engine.Victory) { Phase = RunPhase.Over; return; }
            if (fightingSegment) activeSegment.Resolved = true; else Map.GetRoom(State.CurrentRoom).Cleared = true;
            if (engine.State.IsBoss) { bossDefeated = true; State.Stats.FloorsCleared++; }
            PendingCard = RewardGenerator.RollCard(State, lootRng, engine.State.IsBoss); Phase = RunPhase.CardReward;
        }
        public CardDef RollCardReward() { Require(RunPhase.CardReward); return PendingCard; }
        public void TakeCard()
        {
            Require(RunPhase.CardReward); AddCard(PendingCard); PendingCard = null; FinishReward();
        }
        public void SkipCard() { Require(RunPhase.CardReward); PendingCard = null; FinishReward(); }
        private void FinishReward() { Phase = travel == null ? RunPhase.Map : RunPhase.Travel; }
        private void AddCard(CardDef card)
        { foreach (var hero in State.Party) if (hero.IsAlive && hero.Def.Id == card.HeroId) { hero.Deck.Add(State.NewCard(card)); State.Stats.CardsTaken++; return; } throw new InvalidOperationException("Reward owner is dead."); }
        public void ChooseTreasure(int index)
        {
            Require(RunPhase.Treasure); if (index < 0 || index >= TreasureChoices.Count) throw new ArgumentOutOfRangeException(nameof(index));
            AddCard(TreasureChoices[index]); TreasureChoices = null; Map.GetRoom(State.CurrentRoom).Cleared = true; Phase = RunPhase.Map;
        }
        public void UseBedroom(BedroomAction action, string heroId = null, string cardUid = null)
        {
            Require(RunPhase.Bedroom);
            if (action == BedroomAction.Heal) RewardGenerator.HealParty(State, 0.35);
            else
            {
                PartyHero owner = null; foreach (var hero in State.Party) if (hero.Def.Id == heroId && hero.IsAlive) owner = hero;
                CardInstance card = null; if (owner != null) foreach (var instance in owner.Deck) if (instance.Uid == cardUid) card = instance;
                if (card == null) throw new InvalidOperationException("Card is not owned by a living hero.");
                if (action == BedroomAction.RemoveCard) { if (owner.Deck.Count <= 1) throw new InvalidOperationException("Cannot remove last card."); owner.Deck.Remove(card); }
                else if (action == BedroomAction.UpgradeCard) { if (card.Upgraded) throw new InvalidOperationException("Already upgraded."); card.Upgraded = true; }
                else throw new ArgumentOutOfRangeException(nameof(action));
            }
            Map.GetRoom(State.CurrentRoom).Cleared = true; Phase = RunPhase.Map;
        }
        public void Descend()
        {
            if (!CanDescend) throw new InvalidOperationException("Defeat this floor's boss and use its trapdoor.");
            if (State.FloorIndex == 3) { victory = true; Phase = RunPhase.Over; return; }
            RewardGenerator.HealParty(State, 0.25); State.FloorIndex++; bossDefeated = false; floorCombats = 0;
            Map = MapGenerator.Generate(mapRng, State.FloorIndex); State.CurrentRoom = Map.StartId; Phase = RunPhase.Map;
        }
        private void CompactParty()
        {
            var living = new List<PartyHero>(); foreach (var hero in State.Party) if (hero.IsAlive) living.Add(hero);
            living.Sort((a, b) => a.Rank.CompareTo(b.Rank)); for (int i = 0; i < living.Count; i++) living[i].Rank = i + 1;
        }
        private void Require(RunPhase phase) { if (State == null || Phase != phase || IsRunOver) throw new InvalidOperationException("Action unavailable in phase " + Phase + "."); }
        private void RequireSegment(CorridorEventKind kind) { if (activeSegment == null || activeSegment.Resolved || activeSegment.Event != kind) throw new InvalidOperationException("Wrong corridor event."); }
    }
}
