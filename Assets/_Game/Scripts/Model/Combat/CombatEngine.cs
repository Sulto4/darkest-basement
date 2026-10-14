using System;
using System.Collections.Generic;
using DarkestBasement.Model.Core;
using DarkestBasement.Model.Data;

namespace DarkestBasement.Model.Combat
{
    public sealed class CombatEngine
    {
        private readonly GameRng rng;
        private readonly Dictionary<string, KeyValuePair<string, string>> dotOrigins = new Dictionary<string, KeyValuePair<string, string>>();
        private readonly HashSet<string> dead = new HashSet<string>();
        private readonly Queue<KeyValuePair<string, string>> pendingSpawns = new Queue<KeyValuePair<string, string>>();
        private List<Unit> order = new List<Unit>();
        private int orderIndex;
        private bool started;
        private int[] turnDurations;
        public CombatState State { get; }
        public List<CombatEvent> Events { get; } = new List<CombatEvent>();
        public Unit CurrentActor { get; private set; }
        public bool IsOver { get; private set; }
        public bool Victory { get; private set; }
        public CombatEngine(CombatState state, GameRng rng) { State = state; this.rng = rng; }
        public void StartCombat()
        {
            if (started) throw new InvalidOperationException("Combat already started.");
            Events.Clear(); started = true;
            foreach (var hero in State.Heroes) { hero.Deck.InitialShuffle(); Events.Add(new Shuffled(hero.Id, hero.Deck.DrawPile.Count)); }
            if (!CheckOver()) { StartRound(); Advance(); }
        }
        public bool CanPlay(HeroUnit hero, CardInstance card, out string reason)
        {
            reason = "";
            if (!started || IsOver || CurrentActor != hero || !hero.IsAlive) reason = "Not this hero's turn";
            else if (card == null || !hero.Deck.Hand.Contains(card)) reason = "Card is not in hand";
            else if (card.Def.HeroId != hero.Def.Id) reason = "Wrong hero";
            else if (!card.Def.CanUseRank(hero.Rank)) reason = "Needs rank " + string.Join("–", card.Def.UsableRanks);
            else if (card.Cost > hero.Energy) reason = "Not enough energy";
            else if (ValidTargets(hero, card).Count == 0) reason = "No valid targets";
            return reason.Length == 0;
        }
        public IReadOnlyList<Unit> ValidTargets(HeroUnit hero, CardInstance card)
        {
            var result = new List<Unit>(); if (card == null || hero == null || !hero.IsAlive) return result;
            var def = card.Def;
            if (def.TargetType == TargetType.Self) { result.Add(hero); return result.AsReadOnly(); }
            bool allies = def.TargetType == TargetType.Ally || def.TargetType == TargetType.AllAllies;
            foreach (var unit in State.Living(allies)) if (def.CanTargetRank(unit.Rank)) result.Add(unit);
            if (def.TargetType == TargetType.Enemy)
            {
                var taunts = new List<Unit>(); foreach (var unit in State.Living(false)) if (unit.Statuses.Has(Status.Taunt)) taunts.Add(unit);
                if (taunts.Count > 0) result = taunts;
            }
            return result.AsReadOnly();
        }
        public void PlayCard(HeroUnit hero, CardInstance card, Unit target)
        {
            if (!CanPlay(hero, card, out string reason)) throw new InvalidOperationException(reason);
            var valid = ValidTargets(hero, card);
            if (target == null || !Contains(valid, target)) throw new InvalidOperationException("Invalid target.");
            Events.Clear(); hero.Deck.Hand.Remove(card); ChangeEnergy(hero, -card.Cost);
            Events.Add(new CardPlayed(hero.Id, card, target.Id));
            var targets = new List<Unit>();
            if (card.Def.TargetType == TargetType.AllAllies || card.Def.TargetType == TargetType.AllEnemies) targets.AddRange(valid); else targets.Add(target);
            ResolveEffects(hero, targets, card.Def.Effects, card);
            // Cartea jucată nu poate fi retrasa în timpul propriei rezolvări.
            hero.Deck.DiscardPile.Add(card);
            FinishAction();
        }
        public void Move(HeroUnit hero, int direction)
        {
            if (!started || IsOver || CurrentActor != hero || hero.Energy < 1 || (direction != -1 && direction != 1)) throw new InvalidOperationException("Invalid move.");
            Unit neighbor = null; foreach (var unit in State.Living(true)) if (unit.Rank == hero.Rank + direction) neighbor = unit;
            if (neighbor == null) throw new InvalidOperationException("No adjacent ally.");
            Events.Clear(); ChangeEnergy(hero, -1); int rank = hero.Rank; SetRank(hero, neighbor.Rank); SetRank(neighbor, rank);
        }
        public void EndTurn()
        {
            if (!started || IsOver || !(CurrentActor is HeroUnit)) throw new InvalidOperationException("No active hero turn.");
            Events.Clear(); EndActor(); Advance();
        }
        public void Concede()
        {
            if (!started || IsOver) throw new InvalidOperationException("Combat is not active.");
            Events.Clear(); IsOver = true; Victory = false; CurrentActor = null; Events.Add(new CombatEnded(false, State.Round));
        }
        private static bool Contains(IReadOnlyList<Unit> units, Unit target) { foreach (var unit in units) if (unit == target) return true; return false; }
        private void ChangeEnergy(HeroUnit hero, int delta) { hero.Energy += delta; Events.Add(new EnergyChanged(hero.Id, hero.Energy, delta)); }
        private void Draw(HeroUnit hero, int count)
        { var cards = hero.Deck.Draw(count, () => Events.Add(new Shuffled(hero.Id, hero.Deck.DrawPile.Count))); Events.Add(new CardsDrawn(hero.Id, cards)); }
        private void StartRound()
        {
            State.Round++;
            foreach (var enemy in State.Enemies) if (enemy.IsAlive && enemy.Def.Id == "host") enemy.HostOpen = State.Round % 2 == 0;
            order = TurnOrder.Roll(State, rng); orderIndex = 0; Events.Add(new RoundStarted(State.Round, order));
            foreach (var enemy in State.Enemies)
            {
                if (!enemy.IsAlive) continue;
                enemy.Intent = EnemyAI.Choose(State, enemy, rng); Events.Add(new IntentChosen(enemy.Id, enemy.Intent));
                if (enemy.Phase == 2) { enemy.Intent.SecondAction = EnemyAI.Choose(State, enemy, rng); Events.Add(new IntentChosen(enemy.Id, enemy.Intent.SecondAction, 1)); }
            }
        }
        private void Advance()
        {
            while (!IsOver)
            {
                if (CheckOver()) return;
                if (orderIndex >= order.Count) StartRound();
                CurrentActor = order[orderIndex++];
                if (!CurrentActor.IsAlive) continue;
                Events.Add(new TurnStarted(CurrentActor.Id));
                turnDurations = new int[8]; for (int i = 0; i < 8; i++) turnDurations[i] = CurrentActor.Statuses[(Status)i];
                int block = CurrentActor.Statuses[Status.Block]; CurrentActor.Statuses[Status.Block] = 0;
                if (block > 0) Events.Add(new StatusTicked(CurrentActor.Id, Status.Block, block, 0));
                TickDot(CurrentActor, Status.Bleed); if (CurrentActor.IsAlive) TickDot(CurrentActor, Status.Poison);
                if (CheckOver()) return;
                if (!CurrentActor.IsAlive) continue;
                if (CurrentActor.Statuses.Has(Status.Stun))
                { CurrentActor.Statuses.Decay(Status.Stun); Events.Add(new StatusTicked(CurrentActor.Id, Status.Stun, 0, CurrentActor.Statuses[Status.Stun])); Events.Add(new TurnSkipped(CurrentActor.Id, Status.Stun)); EndActor(); continue; }
                if (CurrentActor is HeroUnit hero)
                { ChangeEnergy(hero, 2 - hero.Energy); Draw(hero, Math.Max(0, 3 - hero.Deck.Hand.Count)); return; }
                var enemy = (EnemyUnit)CurrentActor;
                var first = enemy.Intent ?? EnemyAI.Choose(State, enemy, rng);
                EnemyAct(enemy, first, 0);
                if (!IsOver && enemy.IsAlive && enemy.Phase == 2)
                {
                    var second = first.SecondAction;
                    if (second == null) { second = EnemyAI.Choose(State, enemy, rng); Events.Add(new IntentChosen(enemy.Id, second, 1)); }
                    EnemyAct(enemy, second, 1);
                }
                EndActor();
            }
        }
        private void EndActor()
        {
            if (CurrentActor == null) return;
            if (CurrentActor is HeroUnit hero) { hero.Deck.DiscardHand(); ChangeEnergy(hero, -hero.Energy); }
            foreach (var status in new[] { Status.Weak, Status.Mark, Status.Taunt })
            {
                if (turnDurations != null && turnDurations[(int)status] > 0 && CurrentActor.Statuses.Has(status))
                {
                    if (CurrentActor is EnemyUnit enemy && enemy.Def.Id == "nerve_ending" && status == Status.Taunt) continue;
                    CurrentActor.Statuses.Decay(status); Events.Add(new StatusTicked(CurrentActor.Id, status, 0, CurrentActor.Statuses[status]));
                }
            }
            CurrentActor = null;
        }
        private void TickDot(Unit unit, Status status)
        {
            int stacks = unit.Statuses[status]; if (stacks <= 0) return;
            int amount = Math.Min(stacks, unit.Hp); unit.Hp -= amount; unit.Statuses.Decay(status);
            Events.Add(new StatusTicked(unit.Id, status, amount, unit.Statuses[status]));
            dotOrigins.TryGetValue(unit.Id + ":" + status, out var origin);
            Events.Add(new Damaged(origin.Key, unit.Id, amount, 0, false, 0, unit.Hp, stacks, origin.Value)); HandleDeath(unit);
        }
        private void EnemyAct(EnemyUnit enemy, EnemyIntent intent, int index)
        {
            var targets = EnemyAI.Targets(State, enemy, intent.Ability, rng, true, intent.TargetIds); var ids = new List<string>(); foreach (var t in targets) ids.Add(t.Id);
            Events.Add(new EnemyActed(enemy.Id, intent.Ability.Id, ids, index));
            if (intent.Ability.Id == "reposition")
            {
                if (enemy.Rank > 1) { foreach (var ally in State.Living(false)) if (ally.Rank == enemy.Rank - 1) { int rank = enemy.Rank; SetRank(enemy, ally.Rank); SetRank(ally, rank); break; } }
            }
            else ResolveEffects(enemy, targets, intent.Ability.Effects, null);
            CheckOver();
        }
        private void ResolveEffects(Unit source, List<Unit> targets, IReadOnlyList<EffectDef> effects, CardInstance card)
        {
            var critTargets = new HashSet<string>();
            foreach (var effect in effects)
            {
                if (effect.PerHit) continue;
                int value = card == null ? effect.Amount : card.Def.EffectValue(effect, card.Upgraded);
                if (effect.Kind == EffectKind.Draw) { Draw((HeroUnit)source, value); continue; }
                if (effect.Kind == EffectKind.DiscardHandDraw) { var hero = (HeroUnit)source; hero.Deck.DiscardHand(); Draw(hero, value); continue; }
                if (effect.Kind == EffectKind.GainEnergy) { ChangeEnergy((HeroUnit)source, value); continue; }
                if (effect.Kind == EffectKind.SelfDamage) { DirectDamage(source, source, value, card?.Def.Id); continue; }
                if (effect.Kind == EffectKind.Summon) { Summon(source, effect.SummonId, value); continue; }
                if (effect.Kind == EffectKind.MoveToFront) { PullToFront(source); continue; }
                foreach (var target in targets)
                {
                    if (!target.IsAlive || (effect.OnCrit && !critTargets.Contains(target.Id))) continue;
                    if (effect.Kind == EffectKind.Damage)
                    {
                        int baseDamage = source is EnemyUnit enemy ? (int)Math.Floor(value * enemy.DamageScale) : value;
                        for (int hit = 0; hit < effect.Hits && target.IsAlive; hit++)
                        {
                            bool crit = rng.Chance(effect.CritChance); if (crit) critTargets.Add(target.Id);
                            DamageResult result = DamageCalculator.Calculate(source, target, baseDamage, crit);
                            target.Statuses[Status.Block] -= result.Blocked; target.Hp -= result.Amount;
                            Events.Add(new Damaged(source.Id, target.Id, result.Amount, result.Blocked, crit, hit, target.Hp, result.Raw, card?.Def.Id));
                            HandleDeath(target);
                            if (target.IsAlive) foreach (var onHit in effects) if (onHit.PerHit && (!onHit.OnCrit || crit)) ApplyStatus(source, target, onHit, onHit.Amount, card);
                        }
                    }
                    else if (effect.Kind == EffectKind.Heal)
                    { int heal = Math.Min(value, target.MaxHp - target.Hp); target.Hp += heal; Events.Add(new Healed(source.Id, target.Id, heal, target.Hp)); }
                    else if (effect.Kind == EffectKind.Block)
                    { target.Statuses.Add(Status.Block, value); Events.Add(new BlockGained(source.Id, target.Id, value, target.Statuses[Status.Block])); }
                    else if (effect.Kind == EffectKind.ApplyStatus) ApplyStatus(source, target, effect, value, card);
                    else if (effect.Kind == EffectKind.GainStrength) AddStatus(source, target, Status.Strength, value);
                    else if (effect.Kind == EffectKind.Taunt) AddStatus(source, target, Status.Taunt, value);
                    else if (effect.Kind == EffectKind.Pull) PullToFront(target);
                }
            }
        }
        private void ApplyStatus(Unit source, Unit target, EffectDef effect, int value, CardInstance card)
        {
            if (!rng.Chance(effect.Chance)) return;
            Status status = effect.Status;
            if (card != null && card.Def.Id == "moms_eyeshadow" && target is EnemyUnit enemy && enemy.IsBoss) { status = Status.Weak; value = 2; }
            AddStatus(source, target, status, value);
            if (status == Status.Bleed || status == Status.Poison) dotOrigins[target.Id + ":" + status] = new KeyValuePair<string, string>(source.Id, card?.Def.Id);
        }
        private void AddStatus(Unit source, Unit target, Status status, int value)
        { target.Statuses.Add(status, value); Events.Add(new StatusApplied(source.Id, target.Id, status, value, target.Statuses[status])); }
        private void DirectDamage(Unit source, Unit target, int amount, string cardId)
        { int actual = Math.Min(target.Hp, amount); target.Hp -= actual; Events.Add(new Damaged(source.Id, target.Id, actual, 0, false, 0, target.Hp, amount, cardId)); HandleDeath(target); }
        private void PullToFront(Unit unit)
        {
            if (!unit.IsAlive || unit.Rank == 1) return;
            int rank = unit.Rank; foreach (var other in State.Living(unit.IsHero)) if (other != unit && other.Rank < rank) SetRank(other, other.Rank + 1);
            SetRank(unit, 1);
        }
        private void SetRank(Unit unit, int rank) { if (unit.Rank == rank) return; int old = unit.Rank; unit.Rank = rank; Events.Add(new UnitMoved(unit.Id, old, rank)); }
        private void HandleDeath(Unit unit)
        {
            if (unit.IsAlive || dead.Contains(unit.Id)) return;
            Events.Add(new UnitDied(unit));
            if (unit is EnemyUnit globin && globin.Def.Id == "globin" && !globin.HasRevived)
            { globin.HasRevived = true; globin.Hp = (globin.MaxHp + 1) / 2; Events.Add(new Healed(globin.Id, globin.Id, globin.Hp, globin.Hp, true)); return; }
            dead.Add(unit.Id);
            if (!unit.IsHero) State.Kills++;
            var living = State.Living(unit.IsHero); for (int i = 0; i < living.Count; i++) SetRank(living[i], i + 1);
            if (unit is EnemyUnit enemy && enemy.Def.Id == "mulligan")
                for (int i = 0; i < 2; i++) pendingSpawns.Enqueue(new KeyValuePair<string, string>(enemy.Id, "fly"));
            FlushSpawns();
        }
        private void Summon(Unit source, string id, int count)
        {
            for (int i = 0; i < count && State.Living(false).Count < 4; i++)
            {
                string defId = id;
                if (id == "pooter_fly") defId = rng.Chance(0.5) ? "pooter" : "fly";
                if (id == "depths") defId = rng.Pick(FloorDatabase.Get(3).EnemyIds);
                Spawn(source.Id, defId);
            }
        }
        private void Spawn(string source, string id)
        { EnemyUnit enemy = State.AddEnemy(id, State.Living(false).Count + 1, State.Round); Events.Add(new Summoned(source, enemy)); }
        private void FlushSpawns() { while (pendingSpawns.Count > 0 && State.Living(false).Count < 4) { var next = pendingSpawns.Dequeue(); Spawn(next.Key, next.Value); } }
        private void FinishAction()
        { if (CheckOver()) return; if (CurrentActor != null && !CurrentActor.IsAlive) { EndActor(); Advance(); } }
        private bool CheckOver()
        {
            if (IsOver) return true;
            if (State.Living(true).Count > 0 && (State.Living(false).Count > 0 || pendingSpawns.Count > 0)) return false;
            IsOver = true; Victory = State.Living(true).Count > 0; CurrentActor = null; Events.Add(new CombatEnded(Victory, State.Round)); return true;
        }
    }
}
