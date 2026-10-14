using System;
using System.Collections.Generic;
using DarkestBasement.Model.Core;
using DarkestBasement.Model.Data;

namespace DarkestBasement.Model.Combat
{
    public sealed class EnemyIntent
    {
        public EnemyAbilityDef Ability { get; }
        public List<string> TargetIds { get; }
        public IntentKind Kind { get { return Ability.IntentKind; } }
        public int PredictedDamage { get; }
        public EnemyIntent SecondAction { get; internal set; }
        public EnemyIntent(EnemyAbilityDef ability, List<string> targets, int predicted) { Ability = ability; TargetIds = targets; PredictedDamage = predicted; }
    }
    public static class EnemyAI
    {
        public static EnemyIntent Choose(CombatState state, EnemyUnit enemy, GameRng rng)
        {
            var choices = new List<EnemyAbilityDef>(); int total = 0;
            foreach (var ability in enemy.Def.Abilities)
            {
                if (!CardDef.Contains(ability.UsableRanks, enemy.Rank)) continue;
                if (ability.Condition == AbilityCondition.HostOpen && !enemy.HostOpen) continue;
                if (ability.Condition == AbilityCondition.HostClosed && enemy.HostOpen) continue;
                if (ability.Condition == AbilityCondition.MinionSpace && state.Living(false).Count >= 4) continue;
                if (Targets(state, enemy, ability, null, false).Count == 0) continue;
                choices.Add(ability); total += Weight(enemy, ability);
            }
            if (choices.Count == 0)
            {
                // Unele formații nu permit atacul; inamicul avansează, fără damage inventat.
                var wait = new EnemyAbilityDef("reposition", "Reposition", IntentKind.Special, new[] { 1, 2, 3, 4 }, new[] { 1, 2, 3, 4 }, Array.Empty<EffectDef>(), targetType: TargetType.Self);
                return new EnemyIntent(wait, new List<string> { enemy.Id }, 0);
            }
            int roll = rng.Range(0, total); EnemyAbilityDef selected = choices[0];
            foreach (var ability in choices) { roll -= Weight(enemy, ability); if (roll < 0) { selected = ability; break; } }
            var targets = Targets(state, enemy, selected, rng, true); var ids = new List<string>(); foreach (var t in targets) ids.Add(t.Id);
            int damage = 0;
            foreach (var effect in selected.Effects) if (effect.Kind == EffectKind.Damage)
            {
                if (targets.Count > 0) damage += DamageCalculator.Calculate(enemy, targets[0], (int)Math.Floor(effect.Amount * enemy.DamageScale), false).Raw * effect.Hits;
            }
            return new EnemyIntent(selected, ids, damage);
        }
        private static int Weight(EnemyUnit enemy, EnemyAbilityDef ability)
        { return ability.Weight * (enemy.Phase == 2 && (ability.IntentKind == IntentKind.Attack || ability.IntentKind == IntentKind.AttackAll) ? 3 : 1); }
        public static List<Unit> Targets(CombatState state, EnemyUnit source, EnemyAbilityDef ability, GameRng rng, bool select, IReadOnlyList<string> preferred = null)
        {
            if (ability.TargetType == TargetType.Self) return new List<Unit> { source };
            var candidates = new List<Unit>();
            foreach (var target in state.Living(true)) if (CardDef.Contains(ability.TargetRanks, target.Rank)) candidates.Add(target);
            if (ability.TargetType == TargetType.AllEnemies) return candidates;
            // Taunt are prioritate față de restricția rangului atacului.
            var taunts = new List<Unit>(); foreach (var target in state.Living(true)) if (target.Statuses.Has(Status.Taunt)) taunts.Add(target);
            if (taunts.Count > 0) candidates = taunts;
            if (!select) return candidates;
            var result = new List<Unit>();
            if (preferred != null) foreach (var id in preferred) foreach (var target in candidates) if (target.Id == id && !result.Contains(target) && result.Count < ability.TargetCount) result.Add(target);
            var pool = new List<Unit>(candidates); foreach (var target in result) pool.Remove(target);
            while (result.Count < ability.TargetCount && pool.Count > 0)
            { Unit target = rng.Pick(pool); result.Add(target); pool.Remove(target); }
            return result;
        }
    }
}
