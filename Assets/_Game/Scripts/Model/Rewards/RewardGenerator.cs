using System;
using System.Collections.Generic;
using DarkestBasement.Model.Core;
using DarkestBasement.Model.Data;

namespace DarkestBasement.Model.Rewards
{
    public static class RewardGenerator
    {
        public static CardDef RollCard(RunState state, GameRng rng, bool boss = false, bool atLeastUncommon = false)
        {
            int roll = rng.Range(0, 100);
            Rarity rarity = boss ? Rarity.Rare : atLeastUncommon ? (roll < 85 ? Rarity.Uncommon : Rarity.Rare) : roll < 65 ? Rarity.Common : roll < 95 ? Rarity.Uncommon : Rarity.Rare;
            var pool = new List<CardDef>();
            foreach (var hero in state.Party) if (hero.IsAlive) foreach (var card in CardDatabase.All.Values) if (card.HeroId == hero.Def.Id && card.Rarity == rarity) pool.Add(card);
            // Ordinea bazei de date nu trebuie să depindă de implementarea Dictionary din runtime.
            pool.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            if (pool.Count == 0) throw new InvalidOperationException("No living hero reward pool.");
            return rng.Pick(pool);
        }
        public static IReadOnlyList<CardDef> Treasure(RunState state, GameRng rng)
        {
            var cards = new List<CardDef> { RollCard(state, rng, atLeastUncommon: true) };
            for (int i = 1; i < 3; i++)
            {
                CardDef card; int tries = 0; do { card = RollCard(state, rng); tries++; } while (cards.Contains(card) && tries < 50);
                cards.Add(card);
            }
            rng.Shuffle(cards); return cards.AsReadOnly();
        }
        public static void HealParty(RunState state, double fraction)
        { foreach (var hero in state.Party) if (hero.IsAlive) hero.Hp = Math.Min(hero.Def.MaxHp, hero.Hp + (int)Math.Ceiling(hero.Def.MaxHp * fraction)); }
    }
    public static class EncounterGenerator
    {
        public static EncounterDef Generate(int floor, int combatsCleared, GameRng rng, bool boss = false, bool ambush = false)
        {
            var def = FloorDatabase.Get(floor);
            if (boss) return new EncounterDef(floor, true, false, 0, def.BossId);
            int budget = ambush ? 4 + floor : 8 + floor + Math.Min(4, combatsCleared / 2);
            var ids = new List<string>(); int spent = 0, count = ambush ? rng.Range(2, 4) : 4;
            for (int i = 0; i < count; i++)
            {
                var pool = new List<EnemyDef>();
                foreach (string id in def.EnemyIds)
                {
                    var enemy = EnemyDatabase.Get(id);
                    if (!CardDef.Contains(enemy.PreferredRanks, i + 1)) continue;
                    if (ambush && enemy.Budget > 3) continue;
                    if (enemy.Budget <= budget - spent || ids.Count == 0) pool.Add(enemy);
                }
                if (pool.Count == 0) break;
                EnemyDef chosen = rng.Pick(pool); ids.Add(chosen.Id); spent += chosen.Budget;
            }
            return new EncounterDef(floor, false, ambush, budget, ids.ToArray());
        }
    }
}
