using System;
using System.Collections.Generic;
using DarkestBasement.Model.Core;
using DarkestBasement.Model.Data;

namespace DarkestBasement.Model.Combat
{
    public enum Status { Block, Bleed, Poison, Stun, Weak, Mark, Strength, Taunt }
    public enum IntentKind { Attack, AttackAll, Block, Debuff, Buff, Summon, Special, Unknown }
    public sealed class StatusSet
    {
        private readonly int[] stacks = new int[8];
        public int this[Status status] { get { return stacks[(int)status]; } set { stacks[(int)status] = Math.Max(0, value); } }
        public void Add(Status status, int amount) { this[status] += amount; }
        public bool Has(Status status) { return this[status] > 0; }
        public void Decay(Status status) { this[status]--; }
    }
    public abstract class Unit
    {
        public string Id { get; }
        public string Name { get; }
        public int Hp { get; set; }
        public int MaxHp { get; }
        public int Speed { get; }
        public int Rank { get; set; }
        public int JoinedRound { get; set; }
        public StatusSet Statuses { get; } = new StatusSet();
        public bool IsAlive { get { return Hp > 0; } }
        public abstract bool IsHero { get; }
        protected Unit(string id, string name, int hp, int maxHp, int speed, int rank)
        { Id = id; Name = name; Hp = hp; MaxHp = maxHp; Speed = speed; Rank = rank; }
    }
    public sealed class HeroUnit : Unit
    {
        public HeroDef Def { get; }
        public Deck Deck { get; }
        public int Energy { get; internal set; }
        public override bool IsHero { get { return true; } }
        public HeroUnit(PartyHero hero, GameRng rng) : base(hero.Def.Id, hero.Def.Name, hero.Hp, hero.Def.MaxHp, hero.Def.Speed, hero.Rank)
        { Def = hero.Def; Deck = new Deck(hero.Deck, rng); }
    }
    public sealed class EnemyUnit : Unit
    {
        public EnemyDef Def { get; }
        public EnemyIntent Intent { get; internal set; }
        public bool HasRevived { get; internal set; }
        public bool HostOpen { get; internal set; }
        public double DamageScale { get; }
        public bool IsBoss { get { return Def.IsBoss; } }
        public int Phase { get { return Def.Id == "mom" && Hp * 2 < MaxHp ? 2 : 1; } }
        public override bool IsHero { get { return false; } }
        public EnemyUnit(EnemyDef def, string id, int rank, int floor) : base(id, def.Name,
            def.IsBoss ? def.MaxHp : (int)Math.Ceiling(def.MaxHp * FloorDatabase.Get(floor).EnemyScale),
            def.IsBoss ? def.MaxHp : (int)Math.Ceiling(def.MaxHp * FloorDatabase.Get(floor).EnemyScale), def.Speed, rank)
        { Def = def; DamageScale = def.IsBoss ? 1 : FloorDatabase.Get(floor).EnemyScale; if (def.Id == "nerve_ending") Statuses[Status.Taunt] = 1; }
    }
    public sealed class Deck
    {
        private readonly GameRng rng;
        public List<CardInstance> DrawPile { get; } = new List<CardInstance>();
        public List<CardInstance> Hand { get; } = new List<CardInstance>();
        public List<CardInstance> DiscardPile { get; } = new List<CardInstance>();
        public Deck(IEnumerable<CardInstance> cards, GameRng rng) { this.rng = rng; DrawPile.AddRange(cards); }
        public void InitialShuffle() { rng.Shuffle(DrawPile); }
        public List<CardInstance> Draw(int amount, Action shuffled = null)
        {
            var drawn = new List<CardInstance>();
            for (int i = 0; i < amount; i++)
            {
                if (DrawPile.Count == 0)
                {
                    if (DiscardPile.Count == 0) break;
                    DrawPile.AddRange(DiscardPile); DiscardPile.Clear(); rng.Shuffle(DrawPile); shuffled?.Invoke();
                }
                int last = DrawPile.Count - 1; CardInstance card = DrawPile[last]; DrawPile.RemoveAt(last); Hand.Add(card); drawn.Add(card);
            }
            return drawn;
        }
        public void DiscardHand() { DiscardPile.AddRange(Hand); Hand.Clear(); }
    }
    public sealed class CombatState
    {
        private int nextEnemy;
        public List<HeroUnit> Heroes { get; } = new List<HeroUnit>();
        public List<EnemyUnit> Enemies { get; } = new List<EnemyUnit>();
        public int Floor { get; }
        public bool IsBoss { get; }
        public bool IsAmbush { get; }
        public int Round { get; internal set; }
        public int Kills { get; internal set; }
        public CombatState(IEnumerable<PartyHero> party, EncounterDef encounter, GameRng rng)
        {
            Floor = encounter.Floor; IsBoss = encounter.IsBoss; IsAmbush = encounter.IsAmbush;
            foreach (var hero in party) if (hero.IsAlive) Heroes.Add(new HeroUnit(hero, rng));
            int rank = 1; foreach (string id in encounter.EnemyIds) AddEnemy(id, rank++, 0);
            CompactInitialRanks();
        }
        private void CompactInitialRanks() { Heroes.Sort((a, b) => a.Rank.CompareTo(b.Rank)); for (int i = 0; i < Heroes.Count; i++) Heroes[i].Rank = i + 1; }
        public EnemyUnit AddEnemy(string defId, int rank, int joinedRound)
        { var enemy = new EnemyUnit(EnemyDatabase.Get(defId), "enemy_" + nextEnemy++, rank, Floor) { JoinedRound = joinedRound }; Enemies.Add(enemy); return enemy; }
        public IEnumerable<Unit> Units { get { foreach (var h in Heroes) yield return h; foreach (var e in Enemies) yield return e; } }
        public List<Unit> Living(bool heroes)
        { var result = new List<Unit>(); foreach (var unit in Units) if (unit.IsHero == heroes && unit.IsAlive) result.Add(unit); result.Sort((a, b) => a.Rank.CompareTo(b.Rank)); return result; }
    }
    public sealed class DamageResult
    {
        public int Raw { get; }
        public int Amount { get; }
        public int Blocked { get; }
        public bool Crit { get; }
        public DamageResult(int raw, int amount, int blocked, bool crit) { Raw = raw; Amount = amount; Blocked = blocked; Crit = crit; }
    }
    public static class DamageCalculator
    {
        public static DamageResult Calculate(Unit source, Unit target, int baseDamage, bool crit)
        {
            double damage = Math.Max(0, baseDamage + source.Statuses[Status.Strength]);
            if (source.Statuses.Has(Status.Weak)) damage *= 0.75;
            if (target.Statuses.Has(Status.Mark)) damage *= 1.5;
            if (crit) damage *= 1.5;
            if (target is EnemyUnit enemy && enemy.Def.Id == "host" && !enemy.HostOpen) damage *= 0.25;
            int raw = (int)Math.Floor(damage), blocked = Math.Min(raw, target.Statuses[Status.Block]);
            return new DamageResult(raw, Math.Min(target.Hp, raw - blocked), blocked, crit);
        }
    }
    public static class TurnOrder
    {
        public static List<Unit> Roll(CombatState state, GameRng rng)
        {
            var entries = new List<KeyValuePair<Unit, int>>();
            foreach (var unit in state.Units) if (unit.IsAlive && unit.JoinedRound < state.Round) entries.Add(new KeyValuePair<Unit, int>(unit, unit.Speed + rng.Range(0, 4)));
            entries.Sort((a, b) => { int cmp = b.Value.CompareTo(a.Value); if (cmp != 0) return cmp; cmp = b.Key.IsHero.CompareTo(a.Key.IsHero); return cmp != 0 ? cmp : string.CompareOrdinal(a.Key.Id, b.Key.Id); });
            var order = new List<Unit>(); foreach (var entry in entries) order.Add(entry.Key); return order;
        }
    }
}
