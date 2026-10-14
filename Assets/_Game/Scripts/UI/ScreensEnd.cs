using System;
using DarkestBasement.Audio;
using DarkestBasement.Core;
using DarkestBasement.Model.Core;
using DarkestBasement.Model.Data;
using TMPro;
using UnityEngine;

namespace DarkestBasement.UI
{
    // Ecranul de final (GDD §10.6): etaje, kill-uri, timp, pachetele finale și seed-ul,
    // ca run-ul să poată fi rejucat sau arătat altcuiva. Ca "biletul de moarte" din Isaac, pe pergament.
    public sealed class EndScreen
    {
        public RectTransform Root { get; }

        public EndScreen(Transform parent, Run run, bool victory, float seconds, Action onRetry, Action onNewRun, Action onMenu)
        {
            Root = UiKit.Stretch(parent, "EndScreen");
            var dim = UiKit.Dim(Root, 0f);
            Tween.Value(0.8f, k => { if (dim != null) dim.color = new Color(0.02f, 0.005f, 0.005f, 0.75f * k); }, Ease.OutQuad, null, 0f, true, dim);

            var panel = UiKit.Panel(Root, "panel_paper", new Vector2(320, 188), new Vector2(580, 320)).rectTransform;
            Color titleColor = victory ? Palette.GoldDark : Palette.BloodDark;
            UiKit.Text(panel, victory ? "VICTORY" : "YOU DIED", new Vector2(290, 286), new Vector2(500, 40), UiFont.Title, titleColor, TextAlignmentOptions.Center, 3);
            string epitaph = victory ? "MOM IS DEAD. THE BASEMENT IS QUIET." : DeathLine(run);
            UiKit.Text(panel, epitaph, new Vector2(290, 258), new Vector2(520, 12), UiFont.Label, Palette.Ink);
            UiKit.Fill(panel, Palette.WithAlpha(Palette.Ink, 0.5f), new Vector2(290, 248), new Vector2(480, 1));

            var st = run.State.Stats;
            int floors = victory ? 3 : st.FloorsCleared;
            var t = TimeSpan.FromSeconds(seconds);
            string time = (int)t.TotalMinutes + ":" + t.Seconds.ToString("00");
            string stats = "FLOORS " + floors + "/3    KILLS " + st.Kills + "    COMBATS " + st.Combats + "    CARDS TAKEN " + st.CardsTaken
                           + "    TIME " + time + "    SEED " + run.State.Seed;
            UiKit.Text(panel, stats, new Vector2(290, 236), new Vector2(540, 10), UiFont.Label, Palette.Ink);

            // Pachetele finale, câte o coloană pe erou.
            for (int i = 0; i < run.State.Party.Count; i++)
            {
                var hero = run.State.Party[i];
                float x = 74 + i * 144;
                var portrait = UiKit.Image(panel, SpriteLibrary.Get("Sprites/Heroes/" + hero.Def.Id + "/portrait"), new Vector2(x - 40, 206));
                portrait.raycastTarget = false;
                if (!hero.IsAlive) portrait.color = new Color(0.35f, 0.3f, 0.3f);
                UiKit.Text(panel, hero.Def.Name.ToUpperInvariant(), new Vector2(x + 14, 212), new Vector2(80, 10), UiFont.Label, Palette.Ink, TextAlignmentOptions.Left);
                UiKit.Text(panel, hero.IsAlive ? hero.Hp + "/" + hero.Def.MaxHp + " HP" : "DEAD", new Vector2(x + 14, 201), new Vector2(80, 10), UiFont.Label,
                    hero.IsAlive ? Palette.WithAlpha(Palette.Ink, 0.7f) : Palette.BloodDark, TextAlignmentOptions.Left);

                var lines = new System.Text.StringBuilder();
                int shown = 0;
                foreach (var card in hero.Deck)
                {
                    if (shown == 13) { lines.Append("<color=#2a1b1399>+" + (hero.Deck.Count - 13) + " more</color>"); break; }
                    string color = card.Def.Rarity == Rarity.Rare ? "#a8782a" : card.Def.Rarity == Rarity.Uncommon ? "#2f5f8f" : "#2a1b13";
                    lines.Append("<color=" + color + ">" + card.Def.Name + (card.Upgraded ? "+" : "") + "</color>\n");
                    shown++;
                }
                var list = UiKit.Text(panel, lines.ToString(), new Vector2(x, 128), new Vector2(124, 128), UiFont.Body, Palette.Ink, TextAlignmentOptions.Top);
                list.lineSpacing = -8f;
            }

            UiKit.BigButton(Root, "RETRY SEED", new Vector2(196, 16), new Vector2(130, 24), onRetry);
            UiKit.BigButton(Root, "NEW RUN", new Vector2(338, 16), new Vector2(130, 24), onNewRun);
            UiKit.Button(Root, "MAIN MENU", new Vector2(470, 16), new Vector2(110, 22), onMenu);

            panel.localScale = Vector3.one * 0.9f;
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            Tween.Value(0.6f, k =>
            {
                if (panel == null) return;
                group.alpha = k;
                panel.localScale = Vector3.one * Mathf.LerpUnclamped(0.9f, 1f, k);
            }, Ease.OutBack, null, 0.4f, true, panel);
            AudioManager.Play(victory ? "victory_sting" : "defeat_sting");
        }

        static string DeathLine(Run run)
        {
            string floor = FloorTheme.ForFloor(run.State.FloorIndex).Title;
            return "THE " + floor + " CLAIMED THEM ALL.";
        }

        public void Close() { if (Root != null) UnityEngine.Object.Destroy(Root.gameObject); }
    }
}
