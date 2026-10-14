using System.Collections.Generic;
using DarkestBasement.Model.Combat;
using DarkestBasement.Model.Data;

namespace DarkestBasement.Combat
{
    // „Ce vede jucătorul”: starea luptei reconstruită NUMAI din evenimente.
    // Modelul rulează înaintea animațiilor (o comandă produce toată lista deodată), deci View-ul
    // nu are voie să citească HP-ul direct din model: ar arăta cifra finală înainte de lovitură.
    // Clasa e C# pur (fără UnityEngine), ca s-o putem verifica în teste: după fiecare comandă,
    // starea de aici trebuie să fie identică cu cea din model.
    public sealed class CombatViewState
    {
        public sealed class UnitInfo
        {
            public string Id;
            public string DefId;
            public string Name;
            public bool IsHero;
            public bool IsBoss;
            public int Hp;
            public int MaxHp;
            public int Rank;
            public bool Dead;
            public bool HostOpen;
            public readonly int[] Statuses = new int[8];
            public int Block => Statuses[(int)Status.Block];
            public int Get(Status s) => Statuses[(int)s];
            // Mom intră în faza 2 sub jumătate din HP (aceeași regulă ca în EnemyUnit.Phase).
            public int Phase => DefId == "mom" && Hp * 2 < MaxHp ? 2 : 1;
        }

        public sealed class HeroCards
        {
            public int Draw;
            public int Discard;
            public int Energy;
            public readonly List<string> Hand = new List<string>();
            // Cartea jucată ajunge în discard abia după ce efectele ei s-au rezolvat (ca în model).
            public int InFlight;
        }

        public readonly Dictionary<string, UnitInfo> Units = new Dictionary<string, UnitInfo>();
        public readonly Dictionary<string, HeroCards> Cards = new Dictionary<string, HeroCards>();
        public readonly List<string> Order = new List<string>();
        public int Round { get; private set; }
        public string Active { get; private set; }
        public bool Over { get; private set; }
        public bool Victory { get; private set; }

        public CombatViewState(CombatState state)
        {
            foreach (var h in state.Heroes)
            {
                var u = new UnitInfo { Id = h.Id, DefId = h.Def.Id, Name = h.Name, IsHero = true, Hp = h.Hp, MaxHp = h.MaxHp, Rank = h.Rank, Dead = !h.IsAlive };
                Units[h.Id] = u;
                // Draw pornește de la 0: primul Shuffled din StartCombat pune tot pachetul acolo.
                Cards[h.Id] = new HeroCards();
            }
            foreach (var e in state.Enemies) AddEnemy(e.Id, e.Def.Id, e.Name, e.Hp, e.MaxHp, e.Rank, e.IsBoss, e.Statuses);
        }

        void AddEnemy(string id, string defId, string name, int hp, int maxHp, int rank, bool boss, StatusSet statuses)
        {
            var u = new UnitInfo { Id = id, DefId = defId, Name = name, IsHero = false, IsBoss = boss, Hp = hp, MaxHp = maxHp, Rank = rank, Dead = hp <= 0 };
            // Nerve Ending pornește cu Taunt permanent, pus de constructorul modelului (fără eveniment).
            if (statuses != null) for (int i = 0; i < 8; i++) u.Statuses[i] = statuses[(Status)i];
            Units[id] = u;
        }

        public UnitInfo Unit(string id) => id != null && Units.TryGetValue(id, out var u) ? u : null;
        public HeroCards HeroCardsOf(string id) => id != null && Cards.TryGetValue(id, out var c) ? c : null;

        // Unitatea vie de pe un anumit rang (sau null).
        public UnitInfo AtRank(bool heroes, int rank)
        {
            foreach (var u in Units.Values) if (u.IsHero == heroes && !u.Dead && u.Rank == rank) return u;
            return null;
        }

        public int LivingCount(bool heroes)
        {
            int n = 0; foreach (var u in Units.Values) if (u.IsHero == heroes && !u.Dead) n++;
            return n;
        }

        // Mâna eroului se golește la sfârșitul turei lui. Modelul nu emite eveniment pentru asta,
        // deci o facem explicit: la următorul TurnStarted și la cărțile de tip „D6”.
        public void DiscardHand(string heroId)
        {
            var c = HeroCardsOf(heroId); if (c == null) return;
            c.Discard += c.Hand.Count; c.Hand.Clear();
        }

        // Cărțile jucate „în zbor” intră în discard la sfârșitul comenzii.
        public void SettleInFlight()
        {
            foreach (var c in Cards.Values) { c.Discard += c.InFlight; c.InFlight = 0; }
        }

        public void Apply(CombatEvent e)
        {
            switch (e)
            {
                case RoundStarted ev:
                    Round = ev.Round; Order.Clear(); Order.AddRange(ev.Order);
                    // Host e deschis în rundele pare (regula din model, aplicată la începutul rundei).
                    foreach (var u in Units.Values) if (u.DefId == "host" && !u.Dead) u.HostOpen = ev.Round % 2 == 0;
                    break;
                case TurnStarted ev:
                    EndTurnOf(Active);
                    Active = ev.UnitId;
                    break;
                case CardsDrawn ev:
                {
                    var c = HeroCardsOf(ev.HeroId); if (c == null) break;
                    c.Hand.AddRange(ev.CardIds); c.Draw -= ev.CardIds.Count;
                    break;
                }
                case Shuffled ev:
                {
                    var c = HeroCardsOf(ev.HeroId); if (c == null) break;
                    // Cărțile trase înainte de amestec sunt încă „în draw” vizual până vine CardsDrawn,
                    // de aceea adunăm în loc să suprascriem.
                    c.Draw += ev.CardCount; c.Discard = 0;
                    break;
                }
                case EnergyChanged ev:
                {
                    var c = HeroCardsOf(ev.HeroId); if (c != null) c.Energy = ev.Energy;
                    break;
                }
                case CardPlayed ev:
                {
                    var c = HeroCardsOf(ev.HeroId); if (c == null) break;
                    c.Hand.Remove(ev.CardUid); c.InFlight++;
                    if (DiscardsHand(ev.CardId)) DiscardHand(ev.HeroId);
                    break;
                }
                case Damaged ev:
                {
                    var u = Unit(ev.TargetId); if (u == null) break;
                    u.Hp = ev.HpAfter; u.Statuses[(int)Status.Block] -= ev.Blocked;
                    if (u.Statuses[(int)Status.Block] < 0) u.Statuses[(int)Status.Block] = 0;
                    break;
                }
                case Healed ev:
                {
                    var u = Unit(ev.TargetId); if (u == null) break;
                    u.Hp = ev.HpAfter; if (ev.Revive) u.Dead = false;
                    break;
                }
                case BlockGained ev:
                {
                    var u = Unit(ev.TargetId); if (u != null) u.Statuses[(int)Status.Block] = ev.Total;
                    break;
                }
                case StatusApplied ev:
                {
                    var u = Unit(ev.TargetId); if (u != null) u.Statuses[(int)ev.Status] = ev.Total;
                    break;
                }
                case StatusTicked ev:
                {
                    var u = Unit(ev.UnitId); if (u != null) u.Statuses[(int)ev.Status] = ev.Remaining;
                    break;
                }
                case UnitMoved ev:
                {
                    var u = Unit(ev.UnitId); if (u != null) u.Rank = ev.ToRank;
                    break;
                }
                case Summoned ev:
                {
                    var def = EnemyDatabase.Get(ev.EnemyDefId);
                    AddEnemy(ev.UnitId, ev.EnemyDefId, def.Name, ev.Hp, ev.Hp, ev.Rank, def.IsBoss, null);
                    if (ev.EnemyDefId == "nerve_ending") Units[ev.UnitId].Statuses[(int)Status.Taunt] = 1;
                    break;
                }
                case UnitDied ev:
                {
                    var u = Unit(ev.UnitId); if (u != null) u.Dead = true;
                    break;
                }
                case CombatEnded ev:
                    // La final modelul nu mai golește mâna (lupta pur și simplu se oprește).
                    Active = null; Over = true; Victory = ev.Victory;
                    break;
            }
        }

        void EndTurnOf(string unitId)
        {
            var u = Unit(unitId);
            if (u != null && u.IsHero) DiscardHand(unitId);
        }

        public static bool DiscardsHand(string cardId)
        {
            if (cardId == null || !CardDatabase.All.TryGetValue(cardId, out var def)) return false;
            foreach (var fx in def.Effects) if (fx.Kind == EffectKind.DiscardHandDraw) return true;
            return false;
        }

        // Compară cu modelul; întoarce null dacă totul e identic, altfel prima diferență (pentru teste).
        public string Diff(CombatEngine engine)
        {
            foreach (var unit in engine.State.Units)
            {
                var v = Unit(unit.Id);
                if (v == null) return "missing unit " + unit.Id;
                if (v.Hp != unit.Hp) return unit.Id + " hp " + v.Hp + " != " + unit.Hp;
                if (v.Dead == unit.IsAlive) return unit.Id + " dead flag " + v.Dead;
                if (unit.IsAlive && v.Rank != unit.Rank) return unit.Id + " rank " + v.Rank + " != " + unit.Rank;
                for (int i = 0; i < 8; i++)
                    if (unit.IsAlive && v.Statuses[i] != unit.Statuses[(Status)i]) return unit.Id + " " + (Status)i + " " + v.Statuses[i] + " != " + unit.Statuses[(Status)i];
                if (unit is HeroUnit h && h.IsAlive)
                {
                    var c = Cards[h.Id];
                    if (c.Energy != h.Energy) return h.Id + " energy " + c.Energy + " != " + h.Energy;
                    if (c.Draw != h.Deck.DrawPile.Count) return h.Id + " draw " + c.Draw + " != " + h.Deck.DrawPile.Count;
                    if (c.Discard != h.Deck.DiscardPile.Count) return h.Id + " discard " + c.Discard + " != " + h.Deck.DiscardPile.Count;
                    if (c.Hand.Count != h.Deck.Hand.Count) return h.Id + " hand " + c.Hand.Count + " != " + h.Deck.Hand.Count;
                    foreach (var card in h.Deck.Hand) if (!c.Hand.Contains(card.Uid)) return h.Id + " hand misses " + card.Uid;
                }
            }
            if (Over != engine.IsOver) return "over " + Over + " != " + engine.IsOver;
            return null;
        }
    }
}
