using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DarkestBasement.Model.Combat;

namespace DarkestBasement.Model.Data
{
    public static class CardDatabase
    {
        public static readonly IReadOnlyDictionary<string, CardDef> All = Build();
        public static CardDef Get(string id) { return All[id]; }
        private static IReadOnlyDictionary<string, CardDef> Build()
        {
            var d = new Dictionary<string, CardDef>();
            int[] any = { 1, 2, 3, 4 }, front = { 1, 2 }, three = { 1, 2, 3 }, back = { 3, 4 }, ranged = { 2, 3, 4 };
            void Add(string id, string name, string hero, Rarity rarity, int cost, int[] ranks, TargetType target, int[] targets, params EffectDef[] effects)
            {
                bool numeric = cost == 0;
                foreach (var effect in effects) if (effect.Kind == EffectKind.Damage || effect.Kind == EffectKind.Heal || effect.Kind == EffectKind.Block) numeric = true;
                d.Add(id, new CardDef(id, name, hero, rarity, cost, ranks, target, targets, effects, numeric ? UpgradeRule.Numbers : UpgradeRule.ReduceCost));
            }
            var s = Rarity.Starter; var c = Rarity.Common; var u = Rarity.Uncommon; var r = Rarity.Rare;
            var enemy = TargetType.Enemy; var aoe = TargetType.AllEnemies; var self = TargetType.Self; var ally = TargetType.Ally; var allies = TargetType.AllAllies;
            Add("brimstone_swipe", "Brimstone Swipe", "azazel", s, 1, front, enemy, front, EffectDef.Damage(6));
            Add("demon_wings", "Demon Wings", "azazel", s, 1, any, self, any, EffectDef.Block(7));
            Add("blood_beam", "Blood Beam", "azazel", s, 2, front, aoe, front, EffectDef.Damage(5), EffectDef.Apply(Status.Bleed, 2));
            Add("goat_head", "Goat Head", "azazel", c, 1, front, self, any, EffectDef.Block(6), new EffectDef(EffectKind.Taunt, 1));
            Add("blood_bag", "Blood Bag", "azazel", c, 1, any, self, any, EffectDef.Heal(7));
            Add("pentagram", "Pentagram", "azazel", u, 1, any, self, any, new EffectDef(EffectKind.GainStrength, 2));
            Add("charged_brimstone", "Charged Brimstone", "azazel", u, 2, front, enemy, three, EffectDef.Damage(14));
            Add("ceremonial_robes", "Ceremonial Robes", "azazel", u, 1, any, self, any, EffectDef.Block(10), new EffectDef(EffectKind.GainStrength, 1));
            Add("abaddon", "Abaddon", "azazel", r, 0, any, self, any, new EffectDef(EffectKind.GainStrength, 3), new EffectDef(EffectKind.SelfDamage, 5));
            Add("maw_of_the_void", "Maw of the Void", "azazel", r, 2, front, aoe, any, EffectDef.Damage(8), EffectDef.Apply(Status.Bleed, 3));
            Add("lucky_punch", "Lucky Punch", "cain", s, 1, three, enemy, front, EffectDef.Damage(5, critChance: 0.25));
            Add("lucky_foot", "Lucky Foot", "cain", s, 1, any, self, any, EffectDef.Block(5), new EffectDef(EffectKind.Draw, 1));
            Add("bomb_toss", "Bomb Toss", "cain", s, 2, three, aoe, ranged, EffectDef.Damage(4));
            Add("the_poop", "The Poop", "cain", c, 1, three, enemy, any, EffectDef.Damage(3), EffectDef.Apply(Status.Weak, 2));
            Add("lucky_penny", "Lucky Penny", "cain", c, 0, any, self, any, new EffectDef(EffectKind.Draw, 2));
            Add("moms_knife", "Mom's Knife", "cain", u, 1, three, enemy, any, EffectDef.Damage(7), EffectDef.Apply(Status.Mark, 2));
            Add("crickets_head", "Cricket's Head", "cain", u, 2, front, enemy, front, EffectDef.Damage(12, critChance: 0.3));
            Add("dice_shard", "Dice Shard", "cain", u, 0, any, self, any, new EffectDef(EffectKind.DiscardHandDraw, 3), new EffectDef(EffectKind.GainEnergy, 1));
            Add("midas_touch", "Midas' Touch", "cain", r, 2, front, enemy, front, EffectDef.Damage(10, critChance: 0.5), EffectDef.Apply(Status.Stun, 1, onCrit: true));
            Add("dr_fetus", "Dr. Fetus", "cain", r, 2, three, aoe, any, EffectDef.Damage(6, critChance: 0.25));
            Add("tear", "Tear", "isaac", s, 1, ranged, enemy, any, EffectDef.Damage(5));
            Add("yum_heart", "Yum Heart", "isaac", s, 1, any, ally, any, EffectDef.Heal(5));
            Add("the_sad_onion", "The Sad Onion", "isaac", s, 1, ranged, enemy, any, EffectDef.Damage(3, 2));
            Add("spoon_bender", "Spoon Bender", "isaac", s, 1, ranged, enemy, any, EffectDef.Damage(4), EffectDef.Apply(Status.Mark, 2));
            Add("book_of_belial", "Book of Belial", "isaac", c, 1, any, ally, any, new EffectDef(EffectKind.GainStrength, 2));
            Add("20_20", "20/20", "isaac", u, 1, ranged, enemy, any, EffectDef.Damage(4, 2));
            Add("ipecac", "Ipecac", "isaac", u, 2, ranged, enemy, any, EffectDef.Damage(8), EffectDef.Apply(Status.Poison, 4));
            Add("the_d6", "The D6", "isaac", u, 0, any, self, any, new EffectDef(EffectKind.DiscardHandDraw, 3));
            Add("holy_mantle", "Holy Mantle", "isaac", r, 2, any, allies, any, EffectDef.Block(6));
            Add("polyphemus", "Polyphemus", "isaac", r, 2, ranged, enemy, any, EffectDef.Damage(18));
            Add("incubus_shot", "Incubus Shot", "lilith", s, 1, back, enemy, any, EffectDef.Damage(5));
            Add("cambion_shield", "Cambion Shield", "lilith", s, 1, any, ally, any, EffectDef.Block(6));
            Add("box_of_friends", "Box of Friends", "lilith", s, 2, back, enemy, any, EffectDef.Damage(3, 3));
            Add("cursed_eye", "Cursed Eye", "lilith", s, 1, back, aoe, any, EffectDef.Apply(Status.Weak, 2));
            Add("moms_eyeshadow", "Mom's Eyeshadow", "lilith", c, 1, back, enemy, any, EffectDef.Apply(Status.Stun, 1));
            Add("lil_brimstone", "Lil' Brimstone", "lilith", u, 2, back, aoe, front, EffectDef.Damage(7));
            Add("succubus", "Succubus", "lilith", u, 1, any, allies, any, new EffectDef(EffectKind.GainStrength, 1));
            Add("multidimensional_baby", "Multidimensional Baby", "lilith", u, 1, back, enemy, any, EffectDef.Damage(4, 2), EffectDef.Apply(Status.Mark, 2));
            Add("guardian_angel", "Guardian Angel", "lilith", r, 2, any, ally, any, EffectDef.Heal(10), EffectDef.Block(5));
            Add("twisted_pair", "Twisted Pair", "lilith", r, 1, back, enemy, any, EffectDef.Damage(6, 2), EffectDef.Apply(Status.Bleed, 1, perHit: true));
            return new ReadOnlyDictionary<string, CardDef>(d);
        }
    }

    public static class HeroDatabase
    {
        public static readonly IReadOnlyList<HeroDef> All = Array.AsReadOnly(new[] {
            new HeroDef("azazel", "Azazel", 34, 4, 1, "brimstone_swipe", "brimstone_swipe", "demon_wings", "demon_wings", "blood_beam"),
            new HeroDef("cain", "Cain", 28, 6, 2, "lucky_punch", "lucky_punch", "lucky_foot", "lucky_foot", "bomb_toss"),
            new HeroDef("isaac", "Isaac", 26, 5, 3, "tear", "tear", "yum_heart", "the_sad_onion", "spoon_bender"),
            new HeroDef("lilith", "Lilith", 22, 4, 4, "incubus_shot", "incubus_shot", "cambion_shield", "box_of_friends", "cursed_eye") });
        public static HeroDef Get(string id) { foreach (var hero in All) if (hero.Id == id) return hero; throw new KeyNotFoundException(id); }
    }

    public static class EnemyDatabase
    {
        public static readonly IReadOnlyDictionary<string, EnemyDef> All = Build();
        public static EnemyDef Get(string id) { return All[id]; }
        private static IReadOnlyDictionary<string, EnemyDef> Build()
        {
            var d = new Dictionary<string, EnemyDef>();
            int[] any = { 1, 2, 3, 4 }, front = { 1, 2 }, three = { 1, 2, 3 }, back = { 3, 4 }, ranged = { 2, 3, 4 };
            EnemyAbilityDef A(string id, string name, int damage, int[] ranks, int[] targets, params EffectDef[] extra)
            { var effects = new List<EffectDef> { EffectDef.Damage(damage) }; effects.AddRange(extra); return new EnemyAbilityDef(id, name, IntentKind.Attack, ranks, targets, effects.ToArray(), weight: id == "moms_foot" || id == "moms_hand" ? 2 : 1); }
            void Add(string id, string name, int floor, int hp, int speed, int budget, int[] ranks, params EnemyAbilityDef[] abilities)
            { d.Add(id, new EnemyDef(id, name, floor, hp, speed, budget, ranks, false, abilities)); }
            Add("fly", "Fly", 1, 9, 7, 1, any, A("buzz", "Buzz", 1, any, any));
            Add("gaper", "Gaper", 1, 27, 3, 3, front, A("grab", "Grab", 3, front, front));
            Add("pooter", "Pooter", 1, 15, 6, 2, ranged, A("spit", "Spit", 2, ranged, any));
            Add("horf", "Horf", 1, 18, 2, 2, back, A("blood_shot", "Blood Shot", 3, back, any));
            Add("clotty", "Clotty", 1, 21, 4, 3, three, new EnemyAbilityDef("clot_burst", "Clot Burst", IntentKind.Attack, three, any,
                new[] { EffectDef.Damage(2), EffectDef.Apply(Status.Bleed, 1) }, targetCount: 2));
            Add("mulligan", "Mulligan", 1, 24, 3, 3, three, A("bump", "Bump", 2, three, any));
            Add("hopper", "Hopper", 2, 24, 6, 3, any, A("hop", "Hop", 3, any, any));
            Add("maw", "Maw", 2, 30, 3, 3, ranged, A("gore_spit", "Gore Spit", 2, ranged, any, EffectDef.Apply(Status.Bleed, 1)));
            Add("globin", "Globin", 2, 33, 3, 4, front, A("slam", "Slam", 4, front, any));
            Add("charger", "Charger", 2, 27, 5, 3, three, A("charge", "Charge", 5, three, new[] { 1 }, new EffectDef(EffectKind.MoveToFront, 1)));
            Add("spider", "Spider", 2, 12, 8, 2, any, A("bite", "Bite", 1, any, any, EffectDef.Apply(Status.Poison, 1)));
            Add("knight", "Knight", 3, 45, 3, 4, front,
                new EnemyAbilityDef("shield_up", "Shield Up", IntentKind.Block, any, any, new[] { EffectDef.Block(10) }, targetType: TargetType.Self), A("lance", "Lance", 4, front, front));
            Add("bony", "Bony", 3, 27, 5, 3, ranged, A("bone_throw", "Bone Throw", 3, ranged, any));
            Add("leaper", "Leaper", 3, 36, 4, 4, any, A("leap", "Leap", 4, any, any));
            Add("host", "Host", 3, 30, 4, 3, ranged,
                new EnemyAbilityDef("closed", "Closed Shell", IntentKind.Special, any, any, Array.Empty<EffectDef>(), targetType: TargetType.Self, condition: AbilityCondition.HostClosed),
                new EnemyAbilityDef("triple_shot", "Triple Shot", IntentKind.Attack, ranged, any, new[] { EffectDef.Damage(2, 3) }, condition: AbilityCondition.HostOpen));
            Add("nerve_ending", "Nerve Ending", 3, 38, 1, 3, new[] { 1 }, new EnemyAbilityDef("taunt", "Permanent Taunt", IntentKind.Buff, any, any, Array.Empty<EffectDef>(), targetType: TargetType.Self));
            d.Add("monstro", new EnemyDef("monstro", "Monstro", 1, 260, 2, 0, any, true,
                A("big_leap", "Big Leap", 4, any, front, EffectDef.Apply(Status.Stun, 1, chance: 0.3)),
                new EnemyAbilityDef("blood_spray", "Blood Spray", IntentKind.AttackAll, any, any, new[] { EffectDef.Damage(2) }, targetType: TargetType.AllEnemies),
                A("spit_glob", "Spit Glob", 3, any, any, EffectDef.Apply(Status.Bleed, 1))));
            d.Add("gurdy", new EnemyDef("gurdy", "Gurdy", 2, 240, 1, 0, any, true,
                new EnemyAbilityDef("spawn", "Spawn", IntentKind.Summon, any, any, new[] { new EffectDef(EffectKind.Summon, 2, summonId: "pooter_fly") }, targetType: TargetType.Self, condition: AbilityCondition.MinionSpace),
                new EnemyAbilityDef("bile_rain", "Bile Rain", IntentKind.AttackAll, any, any, new[] { EffectDef.Damage(3) }, targetType: TargetType.AllEnemies),
                A("mouth_beam", "Mouth Beam", 7, any, new[] { 1 })));
            d.Add("mom", new EnemyDef("mom", "Mom", 3, 180, 3, 0, any, true,
                A("moms_foot", "Mom's Foot", 12, any, front),
                A("moms_hand", "Mom's Hand", 8, any, new[] { 4 }, new EffectDef(EffectKind.Pull, 1)),
                new EnemyAbilityDef("door_summon", "Door Summon", IntentKind.Summon, any, any, new[] { new EffectDef(EffectKind.Summon, 2, summonId: "depths") }, targetType: TargetType.Self, condition: AbilityCondition.MinionSpace),
                new EnemyAbilityDef("moms_eye", "Mom's Eye", IntentKind.Debuff, any, any, new[] { EffectDef.Apply(Status.Weak, 2) }, targetType: TargetType.AllEnemies)));
            return new ReadOnlyDictionary<string, EnemyDef>(d);
        }
    }
    public static class FloorDatabase
    {
        public static readonly IReadOnlyList<FloorDef> All = Array.AsReadOnly(new[] {
            new FloorDef(1, "basement", "Basement", "monstro", 1, "fly", "gaper", "pooter", "horf", "clotty", "mulligan"),
            new FloorDef(2, "caves", "Caves", "gurdy", 1.25, "hopper", "maw", "globin", "charger", "spider"),
            new FloorDef(3, "depths", "Depths", "mom", 1.5, "knight", "bony", "leaper", "host", "nerve_ending") });
        public static FloorDef Get(int floor) { if (floor < 1 || floor > 3) throw new ArgumentOutOfRangeException(nameof(floor)); return All[floor - 1]; }
    }
}
