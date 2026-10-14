using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DarkestBasement.Model.Combat;

namespace DarkestBasement.Model.Data
{
    public enum Rarity { Starter, Common, Uncommon, Rare }
    public enum TargetType { Enemy, AllEnemies, Ally, Self, AllAllies }
    public enum EffectKind { Damage, Heal, Block, ApplyStatus, GainStrength, Draw, DiscardHandDraw, GainEnergy, SelfDamage, Taunt, Summon, Pull, MoveToFront }
    public enum UpgradeRule { Numbers, ReduceCost }
    public enum AbilityCondition { Always, HostOpen, HostClosed, MinionSpace }

    public sealed class EffectDef
    {
        public EffectKind Kind { get; }
        public int Amount { get; }
        public int Hits { get; }
        public double CritChance { get; }
        public Status Status { get; }
        public bool PerHit { get; }
        public bool OnCrit { get; }
        public double Chance { get; }
        public string SummonId { get; }
        public EffectDef(EffectKind kind, int amount, int hits = 1, double critChance = 0,
            Status status = Status.Bleed, bool perHit = false, bool onCrit = false, double chance = 1, string summonId = null)
        { Kind = kind; Amount = amount; Hits = hits; CritChance = critChance; Status = status; PerHit = perHit; OnCrit = onCrit; Chance = chance; SummonId = summonId; }
        public int Value(bool upgraded)
        {
            if (!upgraded || (Kind != EffectKind.Damage && Kind != EffectKind.Heal && Kind != EffectKind.Block)) return Amount;
            return Amount + Math.Max(2, (int)Math.Ceiling(Amount * 0.3));
        }
        public static EffectDef Damage(int amount, int hits = 1, double critChance = 0) { return new EffectDef(EffectKind.Damage, amount, hits, critChance); }
        public static EffectDef Heal(int amount) { return new EffectDef(EffectKind.Heal, amount); }
        public static EffectDef Block(int amount) { return new EffectDef(EffectKind.Block, amount); }
        public static EffectDef Apply(Status status, int stacks, bool perHit = false, bool onCrit = false, double chance = 1)
        { return new EffectDef(EffectKind.ApplyStatus, stacks, status: status, perHit: perHit, onCrit: onCrit, chance: chance); }
    }

    public sealed class CardDef
    {
        public string Id { get; }
        public string Name { get; }
        public string HeroId { get; }
        public Rarity Rarity { get; }
        public int Cost { get; }
        public IReadOnlyList<int> UsableRanks { get; }
        public TargetType TargetType { get; }
        public IReadOnlyList<int> TargetRanks { get; }
        public IReadOnlyList<EffectDef> Effects { get; }
        public UpgradeRule UpgradeRule { get; }
        public CardDef(string id, string name, string heroId, Rarity rarity, int cost, int[] ranks,
            TargetType targetType, int[] targetRanks, EffectDef[] effects, UpgradeRule upgradeRule = UpgradeRule.Numbers)
        { Id = id; Name = name; HeroId = heroId; Rarity = rarity; Cost = cost; UsableRanks = Array.AsReadOnly((int[])ranks.Clone()); TargetType = targetType;
          TargetRanks = Array.AsReadOnly((int[])targetRanks.Clone()); Effects = Array.AsReadOnly((EffectDef[])effects.Clone()); UpgradeRule = upgradeRule; }
        public int EnergyCost(bool upgraded) { return Math.Max(0, Cost - (upgraded && UpgradeRule == UpgradeRule.ReduceCost ? 1 : 0)); }
        public int EffectValue(EffectDef effect, bool upgraded)
        {
            bool numeric = upgraded && UpgradeRule == UpgradeRule.Numbers;
            // O carte deja gratuită primește o cifră utilă în plus, nu cost negativ.
            if (numeric && Cost == 0 && (effect.Kind == EffectKind.Draw || effect.Kind == EffectKind.DiscardHandDraw || effect.Kind == EffectKind.GainStrength)) return effect.Amount + 1;
            return effect.Value(numeric);
        }
        public bool CanUseRank(int rank) { return Contains(UsableRanks, rank); }
        public bool CanTargetRank(int rank) { return Contains(TargetRanks, rank); }
        internal static bool Contains(IReadOnlyList<int> ranks, int rank) { for (int i = 0; i < ranks.Count; i++) if (ranks[i] == rank) return true; return false; }
    }

    public sealed class HeroDef
    {
        public string Id { get; }
        public string Name { get; }
        public int MaxHp { get; }
        public int Speed { get; }
        public int StartingRank { get; }
        public IReadOnlyList<string> StartingDeck { get; }
        public HeroDef(string id, string name, int hp, int speed, int rank, params string[] deck)
        { Id = id; Name = name; MaxHp = hp; Speed = speed; StartingRank = rank; StartingDeck = Array.AsReadOnly(deck); }
    }

    public sealed class EnemyAbilityDef
    {
        public string Id { get; }
        public string Name { get; }
        public int Weight { get; }
        public AbilityCondition Condition { get; }
        public IntentKind IntentKind { get; }
        public IReadOnlyList<int> UsableRanks { get; }
        public IReadOnlyList<int> TargetRanks { get; }
        public TargetType TargetType { get; }
        public int TargetCount { get; }
        public IReadOnlyList<EffectDef> Effects { get; }
        public EnemyAbilityDef(string id, string name, IntentKind kind, int[] ranks, int[] targetRanks,
            EffectDef[] effects, int weight = 1, TargetType targetType = TargetType.Enemy, int targetCount = 1, AbilityCondition condition = AbilityCondition.Always)
        { Id = id; Name = name; IntentKind = kind; UsableRanks = Array.AsReadOnly(ranks); TargetRanks = Array.AsReadOnly(targetRanks);
          Effects = Array.AsReadOnly(effects); Weight = weight; TargetType = targetType; TargetCount = targetCount; Condition = condition; }
    }

    public sealed class EnemyDef
    {
        public string Id { get; }
        public string Name { get; }
        public int Floor { get; }
        public int MaxHp { get; }
        public int Speed { get; }
        public bool IsBoss { get; }
        public int Budget { get; }
        public IReadOnlyList<int> PreferredRanks { get; }
        public IReadOnlyList<EnemyAbilityDef> Abilities { get; }
        public EnemyDef(string id, string name, int floor, int hp, int speed, int budget, int[] ranks, bool boss, params EnemyAbilityDef[] abilities)
        { Id = id; Name = name; Floor = floor; MaxHp = hp; Speed = speed; Budget = budget; PreferredRanks = Array.AsReadOnly(ranks); IsBoss = boss; Abilities = Array.AsReadOnly(abilities); }
    }

    public sealed class FloorDef
    {
        public int Index { get; }
        public string Id { get; }
        public string Name { get; }
        public string BossId { get; }
        public double EnemyScale { get; }
        public IReadOnlyList<string> EnemyIds { get; }
        public FloorDef(int index, string id, string name, string boss, double scale, params string[] enemies)
        { Index = index; Id = id; Name = name; BossId = boss; EnemyScale = scale; EnemyIds = Array.AsReadOnly(enemies); }
    }
    public sealed class EncounterDef
    {
        public int Floor { get; }
        public bool IsBoss { get; }
        public bool IsAmbush { get; }
        public int Budget { get; }
        public IReadOnlyList<string> EnemyIds { get; }
        public EncounterDef(int floor, bool boss, bool ambush, int budget, params string[] enemies)
        { Floor = floor; IsBoss = boss; IsAmbush = ambush; Budget = budget; EnemyIds = Array.AsReadOnly(enemies); }
    }

    public static class CardText
    {
        public static string Describe(CardDef card, bool upgraded)
        {
            var parts = new List<string>();
            foreach (var e in card.Effects)
            {
                int v = card.EffectValue(e, upgraded);
                string text;
                switch (e.Kind)
                {
                    case EffectKind.Damage: text = "Deal " + v + " damage" + (e.Hits > 1 ? " " + e.Hits + " times" : "") + (e.CritChance > 0 ? " (" + (int)(e.CritChance * 100) + "% crit)" : ""); break;
                    case EffectKind.Heal: text = "Heal " + v + " HP"; break;
                    case EffectKind.Block: text = "Gain " + v + " Block"; break;
                    case EffectKind.ApplyStatus: text = "Apply " + v + " " + e.Status + (e.PerHit ? " per hit" : "") + (e.OnCrit ? " on crit" : ""); if (card.Id == "moms_eyeshadow") text += " (boss: 2 Weak instead)"; break;
                    case EffectKind.GainStrength: text = "Gain " + v + " Strength"; break;
                    case EffectKind.Draw: text = "Draw " + v + " cards"; break;
                    case EffectKind.DiscardHandDraw: text = "Discard your hand. Draw " + v + " cards"; break;
                    case EffectKind.GainEnergy: text = "Gain " + v + " energy"; break;
                    case EffectKind.SelfDamage: text = "Lose " + v + " HP"; break;
                    case EffectKind.Taunt: text = "Gain " + v + " Taunt"; break;
                    case EffectKind.Summon: text = "Summon " + v + " " + e.SummonId; break;
                    case EffectKind.Pull: text = "Pull to rank 1"; break;
                    default: text = "Move to rank 1"; break;
                }
                parts.Add(text + ".");
            }
            return string.Join(" ", parts);
        }
    }
}
