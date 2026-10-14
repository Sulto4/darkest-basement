using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DarkestBasement.Audio;
using DarkestBasement.Core;
using DarkestBasement.Exploration;
using DarkestBasement.FX;
using DarkestBasement.Model.Combat;
using DarkestBasement.Model.Core;
using DarkestBasement.Model.Data;
using DarkestBasement.Model.Map;
using DarkestBasement.Model.Rewards;
using DarkestBasement.Model.Sim;
using DarkestBasement.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkestBasement.Combat
{
    // Lupta pornită direct, fără meniu, hartă sau hol: pentru iterații rapide pe animații și juice.
    // În joc, "-combat-sandbox [id]" trece prin GameFlow (echipa W), care cheamă tot CombatController.Begin.
    // Run(...) de aici e intrarea statică independentă: își face singur lumea (WorldView + RoomBuilder ale lui W).
    //
    // encounterId: null / "floor2" / "ambush3" / "boss2" / "monstro" / "gaper,fly,horf" / "showcase".
    // Opțiuni din linia de comandă: -combat-autoplay (joacă AutoPlayer), -combat-fast (viteză ×2),
    // -combat-showcase (eroii primesc cărți spectaculoase: Charged Brimstone, Dr. Fetus, Polyphemus...).
    public static class CombatSandbox
    {
        static Transform _root;

        public static CombatController Run(int seed, string encounterIdOrNull)
        {
            var ui = UIRoot.Create();
            _ = AudioManager.Instance;
            if (CommandLine.Has("-combat-autoplay")) CombatController.AutoPlay = true;
            if (CommandLine.Has("-combat-fast")) CombatController.Fast = true;

            var run = new Run(seed);
            var rng = new GameRng(seed).Fork("sandbox");
            var encounter = Encounter(encounterIdOrNull, rng);
            if (CommandLine.Has("-combat-showcase") || encounterIdOrNull == "showcase") AddShowcaseCards(run.State);
            Debug.Log("[CombatSandbox] seed " + seed + ": " + string.Join(",", encounter.EnemyIds) + " (etaj " + encounter.Floor + ")");

            var stage = BuildStage(ui, encounter, seed);
            var engine = new CombatEngine(new CombatState(run.State.Party, encounter, rng), rng);
            return CombatController.Begin(engine, stage, victory => FxHost.Run(After(run, engine, victory, seed, encounterIdOrNull)));
        }

        // După luptă: recompensa (la victorie), apoi o luptă nouă cu seed-ul următor.
        static IEnumerator After(Run run, CombatEngine engine, bool victory, int seed, string id)
        {
            if (victory)
            {
                var card = RewardGenerator.RollCard(run.State, new GameRng(seed).Fork("sandbox_loot"), engine.State.IsBoss);
                bool done = false;
                RewardScreen.Show(card, card.Rarity == Rarity.Rare, _ => done = true);
                while (!done) yield return null;
            }
            float t = 0f;
            while (t < 1.2f) { t += Time.unscaledDeltaTime; yield return null; }
            if (!CommandLine.Has("-combat-once")) Run(seed + 1, id);
        }

        public static EncounterDef Encounter(string id, GameRng rng)
        {
            if (string.IsNullOrEmpty(id)) return EncounterGenerator.Generate(1, 2, rng);
            id = id.Trim().ToLowerInvariant();
            int digit = 1;
            foreach (char c in id) if (char.IsDigit(c)) digit = Mathf.Clamp(c - '0', 1, 3);
            if (id == "showcase") return new EncounterDef(2, false, false, 0, "globin", "host", "mulligan", "spider");
            if (id.StartsWith("floor")) return EncounterGenerator.Generate(digit, 2, rng);
            if (id.StartsWith("ambush")) return EncounterGenerator.Generate(digit, 0, rng, ambush: true);
            if (id.StartsWith("boss")) return EncounterGenerator.Generate(digit, 0, rng, boss: true);
            var ids = new List<string>();
            int floor = 1; bool boss = false;
            foreach (var raw in id.Split(',', '+'))
            {
                string e = raw.Trim();
                if (!EnemyDatabase.All.TryGetValue(e, out var def)) { Debug.LogWarning("[CombatSandbox] inamic necunoscut: " + e); continue; }
                floor = Math.Max(floor, def.Floor);
                boss |= def.IsBoss;
                ids.Add(e);
            }
            return ids.Count == 0 ? EncounterGenerator.Generate(1, 2, rng) : new EncounterDef(floor, boss, false, 0, ids.ToArray());
        }

        // Cărțile cu cele mai multe efecte vizuale, ca să le vezi pe toate fără să joci un run întreg.
        static void AddShowcaseCards(RunState state)
        {
            var extra = new Dictionary<string, string[]>
            {
                { "azazel", new[] { "charged_brimstone", "maw_of_the_void", "pentagram" } },
                { "cain", new[] { "moms_knife", "dr_fetus", "midas_touch", "crickets_head" } },
                { "isaac", new[] { "polyphemus", "ipecac", "20_20", "holy_mantle" } },
                { "lilith", new[] { "lil_brimstone", "twisted_pair", "guardian_angel", "moms_eyeshadow" } },
            };
            foreach (var hero in state.Party)
            {
                if (!extra.TryGetValue(hero.Def.Id, out var ids)) continue;
                foreach (var id in ids) hero.Deck.Add(state.NewCard(CardDatabase.Get(id)));
                // O carte îmbunătățită, ca să se vadă și aspectul „+”.
                if (hero.Deck.Count > 0) hero.Deck[0].Upgraded = true;
            }
        }

        static CombatStageInfo BuildStage(UIRoot ui, EncounterDef enc, int seed)
        {
            if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
            _root = new GameObject("[Combat Sandbox]").transform;
            var theme = FloorTheme.ForFloor(enc.Floor);
            try
            {
                // Lumea adevărată a jocului (echipa W): RenderTexture 640×360, post-procesare, camera, camera de luptă.
                var world = WorldView.Create(ui);
                theme.Apply();
                var room = RoomBuilder.Build(_root, theme, enc.IsBoss ? RoomType.Boss : RoomType.Combat, false, false, seed);
                var stage = room.StageInfo(enc.Floor, enc.IsBoss);
                stage.IsAmbush = enc.IsAmbush;
                room.SetCutaway(true);
                var pose = CameraRig.SidePose(stage);
                world.Rig.SetManualPose(pose.position, pose.rotation, CameraRig.SideFov);
                AudioManager.PlayAmbience(theme.Ambience);
                if (ui.CurtainAlpha > 0.01f) ui.FadeIn(0.3f);
                return stage;
            }
            catch (Exception e)
            {
                // Plasă de siguranță: dacă lumea lui W lipsește sau crapă, scena minimă a noastră.
                Debug.LogException(e);
                var stage = new CombatStageInfo { Center = Vector3.zero, Right = Vector3.right, Floor = enc.Floor, IsBoss = enc.IsBoss, IsAmbush = enc.IsAmbush };
                var cam = CombatSandboxCamera.Create(stage);
                cam.transform.SetParent(_root, true);
                Backdrop(stage, theme);
                if (ui.CurtainAlpha > 0.01f) ui.FadeIn(0.3f);
                return stage;
            }
        }

        // Podea și perete din texturile etajului, ca sprite-uri repetate (fără lumini: doar pentru plasa de siguranță).
        static void Backdrop(CombatStageInfo stage, FloorTheme theme)
        {
            Vector3 right = CombatStage.Right(stage), depth = CombatStage.Depth(stage);
            var floor = Tiled("Floor", SpriteLibrary.Texture(theme.Texture("floor")), new Vector2(34f, 14f), -100);
            floor.transform.position = stage.Center + depth * 2f;
            floor.transform.rotation = Quaternion.LookRotation(Vector3.down, depth);
            floor.color = new Color(0.55f, 0.5f, 0.48f);
            var wall = Tiled("Wall", SpriteLibrary.Texture(theme.Texture("wall")), new Vector2(34f, 9f), -101);
            wall.transform.position = stage.Center + depth * 4f;
            wall.transform.rotation = Quaternion.LookRotation(depth, Vector3.up);
            wall.color = new Color(0.42f, 0.38f, 0.36f);
        }

        static SpriteRenderer Tiled(string name, Texture2D tex, Vector2 size, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            if (tex == null) return sr;
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), 32f, 0, SpriteMeshType.FullRect);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = size;
            sr.sortingOrder = order;
            return sr;
        }

        // ---------- verificare logică în batchmode ----------
        // tools/forge.sh method DarkestBasement.Combat.CombatSandbox.SelfTest
        // 1) fiecare carte și abilitate de inamic are stil vizual, fiecare unitate are cadre, fiecare carte are ilustrație;
        // 2) 300 de lupte jucate de AutoPlayer: starea reconstruită din evenimente (CombatViewState) == starea modelului,
        //    după fiecare comandă. Asta e garanția că HUD-ul nu minte niciodată.
        public static void SelfTest()
        {
            var problems = new List<string>();
            foreach (var card in CardDatabase.All.Values)
            {
                if (!SpriteLibrary.Exists("Sprites/Cards/" + card.Id)) problems.Add("ilustrație lipsă: " + card.Id);
                if (AbilityFx.ForCard(card.Id).Fallback) problems.Add("carte fără stil vizual: " + card.Id);
            }
            foreach (var enemy in EnemyDatabase.All.Values)
                foreach (var ability in enemy.Abilities)
                    if (AbilityFx.ForEnemyAbility(ability.Id).Fallback) problems.Add("abilitate fără stil vizual: " + enemy.Id + "/" + ability.Id);
            foreach (var hero in HeroDatabase.All)
                if (SpriteLibrary.TryFrames("Sprites/Heroes/" + hero.Id + "/side_idle") == null) problems.Add("cadre lipsă: erou " + hero.Id);
            foreach (var enemy in EnemyDatabase.All.Values)
            {
                var f = UnitFrames.ForEnemy(enemy.Id, enemy.IsBoss);
                if (f.Idle[0].name.StartsWith("placeholder")) problems.Add("cadre lipsă: inamic " + enemy.Id);
            }

            int combats = 0, batches = 0, failures = 0;
            var kinds = new HashSet<string>();
            string[] bosses = { "monstro", "gurdy", "mom" };
            for (int seed = 1; seed <= 300; seed++)
            {
                var run = new Run(seed);
                var rng = new GameRng(seed * 31 + 7);
                EncounterDef enc = seed % 5 == 0 ? new EncounterDef(EnemyDatabase.Get(bosses[seed % 3]).Floor, true, false, 0, bosses[seed % 3])
                    : EncounterGenerator.Generate(1 + seed % 3, seed % 6, rng, ambush: seed % 7 == 0);
                if (seed % 2 == 0) AddShowcaseCards(run.State);
                var engine = new CombatEngine(new CombatState(run.State.Party, enc, rng), rng);
                var view = new CombatViewState(engine.State);
                string error = null;
                void Consume(IReadOnlyList<CombatEvent> events)
                {
                    if (error != null) return;
                    foreach (var e in events) { view.Apply(e); kinds.Add(e.GetType().Name); }
                    view.SettleInFlight();
                    batches++;
                    error = view.Diff(engine);
                }
                engine.StartCombat(); Consume(engine.Events);
                var ai = new AutoPlayer();
                int guard = 0;
                while (!engine.IsOver && error == null && guard++ < 300)
                {
                    if (ai.HasNoOffense(engine.State) || engine.State.Round > 40) { engine.Concede(); Consume(engine.Events); break; }
                    ai.PlayTurn(engine, Consume);
                }
                combats++;
                if (error != null) { failures++; if (failures <= 5) problems.Add("seed " + seed + " [" + string.Join(",", enc.EnemyIds) + "]: " + error); }
            }

            var report = new StringBuilder();
            report.AppendLine("[CombatSandbox.SelfTest] " + combats + " lupte, " + batches + " comenzi, " + failures + " diferențe view/model, " + kinds.Count + "/18 tipuri de evenimente");
            foreach (var p in problems) report.AppendLine("  - " + p);
            Debug.Log(report.ToString());
            if (failures > 0 || problems.Count > 0) throw new Exception("CombatSandbox.SelfTest a găsit " + problems.Count + " probleme.");
        }
    }
}
