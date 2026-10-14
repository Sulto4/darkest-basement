using System;
using System.Collections.Generic;
using System.Text;
using DarkestBasement.Model.Core;
using DarkestBasement.Model.Data;
using DarkestBasement.Model.Combat;
using DarkestBasement.Model.Map;

namespace DarkestBasement.Model.Sim
{
    public sealed class CardMetrics
    {
        public int Offered { get; set; }
        public int Picked { get; set; }
        public int Played { get; set; }
        public long Damage { get; set; }
    }
    public sealed class CombatSample
    {
        public int Floor { get; }
        public bool IsBoss { get; }
        public bool IsAmbush { get; }
        public int Rounds { get; }
        public bool Victory { get; }
        public CombatSample(CombatEngine engine)
        { Floor = engine.State.Floor; IsBoss = engine.State.IsBoss; IsAmbush = engine.State.IsAmbush; Rounds = engine.State.Round; Victory = engine.Victory; }
    }
    public sealed class SimulationResult
    {
        public List<CombatSample> Samples { get; } = new List<CombatSample>();
        public int Concessions { get; internal set; }
        public int Seed { get; internal set; }
        public bool Victory { get; internal set; }
        public int Combats { get; internal set; }
        public int NormalCombats { get; internal set; }
        public int BossCombats { get; internal set; }
        public int NormalRounds { get; internal set; }
        public int BossRounds { get; internal set; }
        public int FloorsCleared { get; internal set; }
        public int Kills { get; internal set; }
        public int CardsTaken { get; internal set; }
        public string Fingerprint { get; internal set; }
        public Dictionary<string, CardMetrics> Cards { get; } = new Dictionary<string, CardMetrics>();
        internal CardMetrics Metric(string id) { if (!Cards.TryGetValue(id, out var metric)) { metric = new CardMetrics(); Cards.Add(id, metric); } return metric; }
    }
    public sealed class RunSimulator
    {
        public SimulationResult Play(int seed)
        {
            var run = new Run(seed); var player = new AutoPlayer(); var result = new SimulationResult { Seed = seed }; ulong trace = 14695981039346656037UL;
            void Hash(string value) { foreach (char c in value) { trace ^= c; trace = unchecked(trace * 1099511628211UL); } }
            void Consume(IReadOnlyList<CombatEvent> events)
            {
                foreach (var ev in events)
                {
                    if (ev is CardPlayed card) { result.Metric(card.CardId).Played++; Hash(card.HeroId + card.CardUid + card.CardId + card.TargetId); }
                    if (ev is Damaged hit) { if (hit.CardId != null && hit.SourceId != hit.TargetId) result.Metric(hit.CardId).Damage += hit.Amount; Hash((hit.SourceId ?? "dot") + hit.TargetId + ":" + hit.Amount + ":" + hit.Blocked + ":" + hit.Crit); }
                    if (ev is EnemyActed enemy) Hash(enemy.AbilityId + string.Join(",", enemy.TargetIds));
                    if (ev is UnitDied death) Hash(death.UnitId);
                }
            }
            int steps = 0;
            while (!run.IsRunOver)
            {
                if (++steps > 3000) throw new InvalidOperationException("Run did not terminate, seed " + seed + ", phase " + run.Phase);
                if (run.PendingEncounter != null && run.Phase != RunPhase.Combat)
                {
                    var engine = run.CreateCombat(run.PendingEncounter); engine.StartCombat(); Consume(engine.Events);
                    while (!engine.IsOver)
                    {
                        // Politica renunță la lupte fără atacuri sau cu progres prea lent.
                        if (player.HasNoOffense(engine.State) || engine.State.Round > 40) { engine.Concede(); Consume(engine.Events); result.Concessions++; break; }
                        player.PlayTurn(engine, Consume);
                    }
                    result.Samples.Add(new CombatSample(engine));
                    if (engine.State.IsBoss) { result.BossCombats++; result.BossRounds += engine.State.Round; }
                    else { result.NormalCombats++; result.NormalRounds += engine.State.Round; }
                    run.ApplyCombatResult(engine); continue;
                }
                switch (run.Phase)
                {
                    case RunPhase.Map:
                        if (run.CanDescend) { run.Descend(); break; }
                        int destination = player.ChooseDestination(run); Hash("room:" + run.State.FloorIndex + ":" + destination); run.BeginTravel(destination); break;
                    case RunPhase.Travel:
                        var segment = run.AdvanceTravel();
                        if (segment == null) { run.EnterRoom(); break; }
                        if (segment.Resolved) break;
                        Hash("segment:" + segment.Index + ":" + segment.Event);
                        if (segment.Event == CorridorEventKind.Trap) run.ResolveTrap();
                        else if (segment.Event == CorridorEventKind.Curio) run.ResolveCurio();
                        break;
                    case RunPhase.CardReward:
                        var reward = run.RollCardReward(); result.Metric(reward.Id).Offered++;
                        if (player.ShouldTake(run.State, reward)) { result.Metric(reward.Id).Picked++; Hash("take:" + reward.Id); run.TakeCard(); } else run.SkipCard(); break;
                    case RunPhase.Treasure:
                        int chosen = 0; double value = double.MinValue;
                        for (int i = 0; i < run.TreasureChoices.Count; i++) { var card = run.TreasureChoices[i]; result.Metric(card.Id).Offered++; double score = player.CardValue(card); if (score > value) { chosen = i; value = score; } }
                        result.Metric(run.TreasureChoices[chosen].Id).Picked++; run.ChooseTreasure(chosen); break;
                    case RunPhase.Bedroom: run.UseBedroom(BedroomAction.Heal); break;
                    case RunPhase.Curio: run.ResolveCurio(); break;
                    default: throw new InvalidOperationException("Unhandled run phase " + run.Phase);
                }
            }
            result.Victory = run.IsVictory; result.Combats = run.State.Stats.Combats; result.FloorsCleared = run.State.Stats.FloorsCleared; result.Kills = run.State.Stats.Kills; result.CardsTaken = run.State.Stats.CardsTaken;
            var final = new StringBuilder(); final.Append(seed).Append(':').Append(result.Victory).Append(':').Append(result.Combats).Append(':').Append(result.NormalRounds).Append(':').Append(result.BossRounds).Append(':').Append(result.Kills).Append(':').Append(trace);
            foreach (var hero in run.State.Party) { final.Append('|').Append(hero.Def.Id).Append(':').Append(hero.Hp).Append(':').Append(hero.Rank); foreach (var card in hero.Deck) final.Append(',').Append(card.Uid).Append(':').Append(card.Def.Id).Append(':').Append(card.Upgraded); }
            result.Fingerprint = final.ToString(); return result;
        }
    }
}
