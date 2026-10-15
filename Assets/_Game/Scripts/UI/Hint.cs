using System.Collections.Generic;
using DarkestBasement.Core;
using TMPro;
using UnityEngine;

namespace DarkestBasement.UI
{
    // Indicii scurte și contextuale ("Hold W to walk", "Drag a card onto an enemy"). GDD §10: primele minute învață
    // fără text lung. Fiecare indiciu are un id și apare o singură dată pe sesiune; Hide(id) îl scoate când jucătorul a înțeles.
    //
    // Folosire (și pentru echipa C):  Hint.Show("drag_card", "Drag a card onto an enemy", new Vector2(320, 120));
    public static class Hint
    {
        static readonly HashSet<string> Seen = new HashSet<string>();
        static readonly Dictionary<string, GameObject> Active = new Dictionary<string, GameObject>();

        // arrow = săgeata mică de dedesubt, care arată spre lucrul despre care vorbește (fără ea, dacă dedesubt nu e nimic).
        public static bool Show(string id, string text, Vector2 uiPos, float seconds = 6f, bool arrow = true)
        {
            if (Seen.Contains(id) || UIRoot.Instance == null) return false;
            Seen.Add(id);

            var root = UiKit.Rect(UIRoot.Instance.Overlay, "Hint_" + id, uiPos, new Vector2(10, 10));
            var label = UiKit.Text(root, text, Vector2.zero, new Vector2(260, 20), UiFont.Body, Palette.Parchment);
            label.ForceMeshUpdate();
            float width = Mathf.Ceil(label.preferredWidth / 2f) * 2f + 16f;
            var panel = UiKit.Panel(root, "panel_dark", Vector2.zero, new Vector2(width, 20));
            panel.raycastTarget = false;
            panel.transform.SetAsFirstSibling();
            label.rectTransform.sizeDelta = new Vector2(width, 20);
            label.rectTransform.anchoredPosition = new Vector2(0f, 1f);
            // Săgeată mică ce sare în jos, spre lucrul despre care vorbește indiciul.
            var pointer = UiKit.Text(root, "v", new Vector2(0, -15), new Vector2(10, 10), UiFont.LabelBold, Palette.Gold);
            pointer.gameObject.SetActive(arrow);

            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            Active[id] = root.gameObject;
            float age = 0f;
            Ticker.Attach(root.gameObject, () =>
            {
                age += Core.GameTime.UnscaledDelta;
                group.alpha = Mathf.Clamp01(age / 0.3f) * Mathf.Clamp01((seconds - age) / 0.5f);
                pointer.rectTransform.anchoredPosition = new Vector2(0f, -15f + Mathf.Round(Mathf.Abs(Mathf.Sin(age * 5f)) * 3f));
                if (age >= seconds) Hide(id);
            });
            return true;
        }

        public static void Hide(string id)
        {
            if (!Active.TryGetValue(id, out var go)) return;
            Active.Remove(id);
            if (go != null) Object.Destroy(go);
        }

        public static void HideAll()
        {
            foreach (var go in Active.Values) if (go != null) Object.Destroy(go);
            Active.Clear();
        }

        // Pentru un run nou: indiciile pot apărea din nou.
        public static void ResetSeen() => Seen.Clear();
    }
}
