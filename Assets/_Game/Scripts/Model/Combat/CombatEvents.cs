using System;
using System.Collections.Generic;

namespace DarkestBasement.Model.Combat
{
    public abstract class CombatEvent { }
    public sealed class RoundStarted : CombatEvent
    { public int Round { get; } public IReadOnlyList<string> Order { get; } public RoundStarted(int round, IEnumerable<Unit> order) { Round = round; var ids = new List<string>(); foreach (var u in order) ids.Add(u.Id); Order = ids.AsReadOnly(); } }
    public sealed class TurnStarted : CombatEvent
    { public string UnitId { get; } public TurnStarted(string unitId) { UnitId = unitId; } }
    public sealed class IntentChosen : CombatEvent
    {
        public string EnemyId { get; } public string AbilityId { get; } public IReadOnlyList<string> TargetIds { get; }
        public IntentKind Kind { get; } public int PredictedDamage { get; } public int ActionIndex { get; }
        public IntentChosen(string enemyId, EnemyIntent intent, int actionIndex = 0)
        { EnemyId = enemyId; AbilityId = intent.Ability.Id; TargetIds = Array.AsReadOnly(intent.TargetIds.ToArray()); Kind = intent.Kind; PredictedDamage = intent.PredictedDamage; ActionIndex = actionIndex; }
    }
    public sealed class CardsDrawn : CombatEvent
    { public string HeroId { get; } public IReadOnlyList<string> CardIds { get; } public CardsDrawn(string hero, IEnumerable<Core.CardInstance> cards) { HeroId = hero; var ids = new List<string>(); foreach (var c in cards) ids.Add(c.Uid); CardIds = ids.AsReadOnly(); } }
    public sealed class Shuffled : CombatEvent
    { public string HeroId { get; } public int CardCount { get; } public Shuffled(string hero, int count) { HeroId = hero; CardCount = count; } }
    public sealed class EnergyChanged : CombatEvent
    { public string HeroId { get; } public int Energy { get; } public int Delta { get; } public EnergyChanged(string hero, int energy, int delta) { HeroId = hero; Energy = energy; Delta = delta; } }
    public sealed class CardPlayed : CombatEvent
    { public string HeroId { get; } public string CardUid { get; } public string CardId { get; } public string TargetId { get; } public bool Upgraded { get; } public CardPlayed(string hero, Core.CardInstance card, string target) { HeroId = hero; CardUid = card.Uid; CardId = card.Def.Id; TargetId = target; Upgraded = card.Upgraded; } }
    public sealed class EnemyActed : CombatEvent
    { public string EnemyId { get; } public string AbilityId { get; } public IReadOnlyList<string> TargetIds { get; } public int ActionIndex { get; } public EnemyActed(string enemy, string ability, List<string> targets, int index) { EnemyId = enemy; AbilityId = ability; TargetIds = targets.AsReadOnly(); ActionIndex = index; } }
    public sealed class Damaged : CombatEvent
    {
        public string SourceId { get; } public string TargetId { get; } public int Amount { get; } public int Blocked { get; } public bool Crit { get; }
        public int HitIndex { get; } public int HpAfter { get; } public int RawAmount { get; } public string CardId { get; }
        public Damaged(string source, string target, int amount, int blocked, bool crit, int hitIndex, int hpAfter, int rawAmount, string cardId = null)
        { SourceId = source; TargetId = target; Amount = amount; Blocked = blocked; Crit = crit; HitIndex = hitIndex; HpAfter = hpAfter; RawAmount = rawAmount; CardId = cardId; }
    }
    public sealed class Healed : CombatEvent
    { public string SourceId { get; } public string TargetId { get; } public int Amount { get; } public int HpAfter { get; } public bool Revive { get; } public Healed(string source, string target, int amount, int hpAfter, bool revive = false) { SourceId = source; TargetId = target; Amount = amount; HpAfter = hpAfter; Revive = revive; } }
    public sealed class BlockGained : CombatEvent
    { public string SourceId { get; } public string TargetId { get; } public int Amount { get; } public int Total { get; } public BlockGained(string source, string target, int amount, int total) { SourceId = source; TargetId = target; Amount = amount; Total = total; } }
    public sealed class StatusApplied : CombatEvent
    { public string SourceId { get; } public string TargetId { get; } public Status Status { get; } public int Stacks { get; } public int Total { get; } public StatusApplied(string source, string target, Status status, int stacks, int total) { SourceId = source; TargetId = target; Status = status; Stacks = stacks; Total = total; } }
    public sealed class StatusTicked : CombatEvent
    { public string UnitId { get; } public Status Status { get; } public int Amount { get; } public int Remaining { get; } public StatusTicked(string unit, Status status, int amount, int remaining) { UnitId = unit; Status = status; Amount = amount; Remaining = remaining; } }
    public sealed class UnitMoved : CombatEvent
    { public string UnitId { get; } public int FromRank { get; } public int ToRank { get; } public UnitMoved(string unit, int from, int to) { UnitId = unit; FromRank = from; ToRank = to; } }
    public sealed class Summoned : CombatEvent
    { public string SourceId { get; } public string UnitId { get; } public string EnemyDefId { get; } public int Rank { get; } public int Hp { get; } public int JoinedRound { get; } public Summoned(string source, EnemyUnit unit) { SourceId = source; UnitId = unit.Id; EnemyDefId = unit.Def.Id; Rank = unit.Rank; Hp = unit.Hp; JoinedRound = unit.JoinedRound; } }
    public sealed class UnitDied : CombatEvent
    { public string UnitId { get; } public int Rank { get; } public bool IsHero { get; } public UnitDied(Unit unit) { UnitId = unit.Id; Rank = unit.Rank; IsHero = unit.IsHero; } }
    public sealed class TurnSkipped : CombatEvent
    { public string UnitId { get; } public Status Reason { get; } public TurnSkipped(string unit, Status reason) { UnitId = unit; Reason = reason; } }
    public sealed class CombatEnded : CombatEvent
    { public bool Victory { get; } public int Rounds { get; } public CombatEnded(bool victory, int rounds) { Victory = victory; Rounds = rounds; } }
}
