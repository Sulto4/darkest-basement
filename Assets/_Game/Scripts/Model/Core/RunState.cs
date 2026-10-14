using System.Collections.Generic;
using DarkestBasement.Model.Data;

namespace DarkestBasement.Model.Core
{
    public sealed class CardInstance
    {
        public CardDef Def { get; }
        public bool Upgraded { get; set; }
        public string Uid { get; }
        public CardInstance(CardDef def, string uid, bool upgraded = false) { Def = def; Uid = uid; Upgraded = upgraded; }
        public int Cost { get { return Def.EnergyCost(Upgraded); } }
    }
    public sealed class PartyHero
    {
        public HeroDef Def { get; }
        public int Hp { get; set; }
        public int Rank { get; set; }
        public bool IsAlive { get { return Hp > 0; } }
        public List<CardInstance> Deck { get; } = new List<CardInstance>();
        public PartyHero(HeroDef def) { Def = def; Hp = def.MaxHp; Rank = def.StartingRank; }
    }
    public sealed class RunStats
    {
        public int Kills { get; set; }
        public int Combats { get; set; }
        public int CardsTaken { get; set; }
        public int ElapsedRounds { get; set; }
        public int FloorsCleared { get; set; }
    }
    public sealed class RunState
    {
        private int nextCard;
        public int Seed { get; }
        public List<PartyHero> Party { get; } = new List<PartyHero>();
        public int FloorIndex { get; set; } = 1;
        public int CurrentRoom { get; set; }
        public RunStats Stats { get; } = new RunStats();
        public RunState(int seed)
        {
            Seed = seed;
            foreach (var hero in HeroDatabase.All)
            { var member = new PartyHero(hero); foreach (var id in hero.StartingDeck) member.Deck.Add(NewCard(CardDatabase.Get(id))); Party.Add(member); }
        }
        public CardInstance NewCard(CardDef def) { return new CardInstance(def, "card_" + nextCard++); }
        public bool AnyAlive { get { foreach (var hero in Party) if (hero.IsAlive) return true; return false; } }
    }
}
