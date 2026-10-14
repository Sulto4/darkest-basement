using System;
using System.Collections.Generic;
using DarkestBasement.Model.Core;
using DarkestBasement.Model.Data;
using DarkestBasement.Model.Combat;
using DarkestBasement.Model.Map;

namespace DarkestBasement.Model.Sim
{
    public sealed class AutoPlayer
    {
        public void PlayTurn(CombatEngine engine, Action<IReadOnlyList<CombatEvent>> consume = null)
        {
            var hero = engine.CurrentActor as HeroUnit;
            if (hero == null) throw new InvalidOperationException("AutoPlayer expects a hero turn.");
            // Limita aparține politicii AI: evită ciclurile de redraw, fără a schimba regulile jocului.
            for (int action = 0; action < 20 && !engine.IsOver && engine.CurrentActor == hero; action++)
            {
                CardInstance best = null; Unit target = null; double bestScore = 0.1;
                foreach (var card in hero.Deck.Hand)
                {
                    if (!engine.CanPlay(hero, card, out _)) continue;
                    var valid = engine.ValidTargets(hero, card);
                    foreach (var candidate in valid)
                    {
                        double score = Score(engine, hero, card, candidate);
                        if (score > bestScore) { bestScore = score; best = card; target = candidate; }
                    }
                }
                if (best != null) { engine.PlayCard(hero, best, target); consume?.Invoke(engine.Events); continue; }
                bool moved = false;
                if (hero.Energy >= 1)
                {
                    foreach (int direction in new[] { -1, 1 })
                    {
                        int rank = hero.Rank + direction;
                        if (rank < 1 || rank > engine.State.Living(true).Count) continue;
                        foreach (var card in hero.Deck.Hand)
                        {
                            if (card.Cost > hero.Energy - 1 || card.Def.CanUseRank(hero.Rank) || !card.Def.CanUseRank(rank)) continue;
                            engine.Move(hero, direction); consume?.Invoke(engine.Events); moved = true; break;
                        }
                        if (moved) break;
                    }
                }
                if (!moved) break;
            }
            if (!engine.IsOver && engine.CurrentActor == hero) { engine.EndTurn(); consume?.Invoke(engine.Events); }
        }
        public bool HasNoOffense(CombatState state)
        {
            int count = state.Living(true).Count;
            foreach (var hero in state.Heroes)
            {
                if (!hero.IsAlive) continue;
                var cards = new List<CardInstance>(hero.Deck.DrawPile); cards.AddRange(hero.Deck.Hand); cards.AddRange(hero.Deck.DiscardPile);
                foreach (var card in cards)
                {
                    bool reachable = false; for (int rank = 1; rank <= count; rank++) if (card.Def.CanUseRank(rank)) reachable = true;
                    if (!reachable) continue;
                    foreach (var effect in card.Def.Effects)
                        if (effect.Kind == EffectKind.Damage || (effect.Kind == EffectKind.ApplyStatus && (effect.Status == Status.Bleed || effect.Status == Status.Poison))) return false;
                }
            }
            return true;
        }
        private static double Score(CombatEngine engine, HeroUnit hero, CardInstance card, Unit candidate)
        {
            double score = 0; var targets = new List<Unit>();
            if (card.Def.TargetType == TargetType.AllEnemies || card.Def.TargetType == TargetType.AllAllies) targets.AddRange(engine.ValidTargets(hero, card)); else targets.Add(candidate);
            foreach (var effect in card.Def.Effects)
            {
                int value = card.Def.EffectValue(effect, card.Upgraded);
                if (effect.Kind == EffectKind.Draw || effect.Kind == EffectKind.DiscardHandDraw) { score += hero.Energy > 0 ? value * 1.5 : 0; continue; }
                if (effect.Kind == EffectKind.GainEnergy) { score += value * 4; continue; }
                if (effect.Kind == EffectKind.SelfDamage) { score -= value * (hero.Hp <= value + 5 ? 5 : 0.8); continue; }
                foreach (var target in targets)
                {
                    if (effect.Kind == EffectKind.Damage)
                    {
                        double damage = DamageCalculator.Calculate(hero, target, value, false).Raw * effect.Hits * (1 + 0.5 * effect.CritChance);
                        double effective = Math.Min(target.Hp, Math.Max(0, damage - target.Statuses[Status.Block]));
                        score += effective + (damage >= target.Hp + target.Statuses[Status.Block] ? 6 : 3.0 / Math.Max(1, target.Hp));
                    }
                    else if (effect.Kind == EffectKind.Heal) score += Math.Min(value, target.MaxHp - target.Hp) * (target.Hp < target.MaxHp * 0.45 ? 2.2 : 1.15);
                    else if (effect.Kind == EffectKind.Block) score += Math.Min(value, Math.Max(0, Incoming(engine.State, target) - target.Statuses[Status.Block])) * 0.9;
                    else if (effect.Kind == EffectKind.GainStrength) score += value * (engine.State.IsBoss ? 4 : 2.0);
                    else if (effect.Kind == EffectKind.ApplyStatus)
                    {
                        if (effect.Status == Status.Weak) score += Incoming(engine.State, target.IsHero ? target : hero) * 0.3 + (target.IsHero ? 0 : 1.5);
                        else if (effect.Status == Status.Stun) score += target.Statuses.Has(Status.Stun) ? 0.2 : (target is EnemyUnit enemy && enemy.IsBoss && card.Def.Id == "moms_eyeshadow" ? 2 : 6);
                        else if (effect.Status == Status.Mark) score += target.Statuses.Has(Status.Mark) ? 0.5 : Math.Min(6, target.Hp * 0.25);
                        else score += Math.Min(target.Hp * 0.4, value * 1.4 * (effect.PerHit ? 2 : 1));
                    }
                }
            }
            return score / Math.Max(1, card.Cost);
        }
        private static double Incoming(CombatState state, Unit target)
        {
            double result = 0;
            foreach (var enemy in state.Enemies)
            {
                if (!enemy.IsAlive || enemy.Intent == null) continue;
                if (enemy.Intent.TargetIds.Contains(target.Id)) result += enemy.Intent.PredictedDamage;
                if (enemy.Intent.SecondAction != null && enemy.Intent.SecondAction.TargetIds.Contains(target.Id)) result += enemy.Intent.SecondAction.PredictedDamage;
            }
            return result;
        }
        public bool ShouldTake(RunState state, CardDef card)
        {
            PartyHero owner = null; foreach (var hero in state.Party) if (hero.Def.Id == card.HeroId) owner = hero;
            if (owner == null || !owner.IsAlive) return false;
            int duplicates = 0; foreach (var c in owner.Deck) if (c.Def.Id == card.Id) duplicates++;
            return duplicates == 0 && owner.Deck.Count < 10 && CardValue(card) >= 5;
        }
        public double CardValue(CardDef card)
        {
            double value = 0;
            foreach (var e in card.Effects)
            {
                if (e.Kind == EffectKind.Damage) value += e.Amount * e.Hits * (card.TargetType == TargetType.AllEnemies ? 1.8 : 1);
                else if (e.Kind == EffectKind.Heal) value += e.Amount * 1.5;
                else if (e.Kind == EffectKind.Block) value += e.Amount * 0.65 * (card.TargetType == TargetType.AllAllies ? 4 : 1);
                else if (e.Kind == EffectKind.GainStrength) value += e.Amount * 4 * (card.TargetType == TargetType.AllAllies ? 4 : 1);
                else if (e.Kind == EffectKind.SelfDamage) value -= e.Amount;
                else if (e.Kind == EffectKind.Draw || e.Kind == EffectKind.DiscardHandDraw) value += e.Amount * 3;
                else if (e.Kind == EffectKind.GainEnergy) value += e.Amount * 4;
                else if (e.Kind == EffectKind.ApplyStatus && e.Status == Status.Stun) value += 6;
                else value += e.Amount * 2;
            }
            return value / Math.Max(1, card.Cost);
        }
        public int ChooseDestination(Run run)
        {
            var map = run.Map; int current = run.State.CurrentRoom;
            var distances = map.Distances(current); Room goal = null; double best = double.MaxValue;
            foreach (var room in map.Rooms)
            {
                if (room.Cleared || room.Id == map.BossId) continue;
                double priority = distances[room.Id];
                if (room.Revealed && room.Type == RoomType.Bedroom)
                {
                    double hp = 0, maxHp = 0; foreach (var hero in run.State.Party) if (hero.IsAlive) { hp += hero.Hp; maxHp += hero.Def.MaxHp; }
                    priority += hp / maxHp > 0.8 ? 8 : -1.5;
                }
                if (priority < best) { best = priority; goal = room; }
            }
            if (goal == null) goal = map.GetRoom(map.BossId);
            var toGoal = map.Distances(goal.Id); int next = -1, distance = int.MaxValue;
            foreach (int neighbor in map.GetRoom(current).Neighbors) if (toGoal[neighbor] < distance) { next = neighbor; distance = toGoal[neighbor]; }
            if (next < 0) throw new InvalidOperationException("No path to destination."); return next;
        }
    }
}
