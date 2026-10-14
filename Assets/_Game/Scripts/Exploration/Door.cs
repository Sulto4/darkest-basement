using System.Collections;
using DarkestBasement.Audio;
using DarkestBasement.Core;
using DarkestBasement.FX;
using UnityEngine;

namespace DarkestBasement.Exploration
{
    // Ușa de la capătul holului, în stil Isaac. Textura spune ce te așteaptă: door_boss (dinți și cranii),
    // door_treasure (aurie), door_bedroom, door_normal. La deschidere: tremură, se dă de perete, iar din cameră
    // iese lumina camerei (roșie la boss, aurie la comoară).
    public sealed class Door : MonoBehaviour
    {
        public string Type { get; private set; }
        public bool IsOpen { get; private set; }

        Material _material;
        Texture2D _open;
        Light _spill;
        Transform _panel;
        Color _spillColor;

        public static string TypeFor(DarkestBasement.Model.Map.RoomType room)
        {
            switch (room)
            {
                case DarkestBasement.Model.Map.RoomType.Boss: return "boss";
                case DarkestBasement.Model.Map.RoomType.Treasure: return "treasure";
                case DarkestBasement.Model.Map.RoomType.Bedroom: return "bedroom";
                default: return "normal";
            }
        }

        public static Door Create(Transform parent, MaterialSet materials, FloorTheme theme, string type, Vector3 localPos, float size)
        {
            var go = new GameObject("Door_" + type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var door = go.AddComponent<Door>();
            door.Build(materials, theme, type, size);
            return door;
        }

        void Build(MaterialSet materials, FloorTheme theme, string type, float size)
        {
            Type = type;
            var closed = SpriteLibrary.Texture(theme.Texture("door_" + type));
            _open = SpriteLibrary.Texture(theme.Texture("door_" + type + "_open"));
            _material = materials.Unique(MaterialKind.LitCutout, closed);

            // Panoul ușii: un quad vertical cu fața spre -Z (spre echipa care vine).
            var panel = new GameObject("Panel");
            panel.transform.SetParent(transform, false);
            _panel = panel.transform;
            _panel.localScale = new Vector3(size, size, 1f);
            panel.AddComponent<MeshFilter>().sharedMesh = MeshKit.BillboardQuad;
            var r = panel.AddComponent<MeshRenderer>();
            r.sharedMaterial = _material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            _spillColor = type == "boss" ? new Color(1f, 0.25f, 0.18f)
                : type == "treasure" ? new Color(1f, 0.82f, 0.4f)
                : type == "bedroom" ? new Color(0.75f, 0.8f, 1f)
                : new Color(1f, 0.62f, 0.35f);

            var lightGo = new GameObject("Spill");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, size * 0.45f, -0.6f);
            _spill = lightGo.AddComponent<Light>();
            _spill.type = LightType.Point;
            _spill.color = _spillColor;
            _spill.range = 7f;
            _spill.intensity = type == "boss" ? 0.8f : 0f; // ușa boss-ului strălucește amenințător și închisă
            _spill.shadows = LightShadows.None;
            LocalLight.Register(_spill);
        }

        public IEnumerator Open()
        {
            if (IsOpen) yield break;
            IsOpen = true;

            // 1. Anticipare: ușa se zgâlțâie, de parcă cineva împinge din partea cealaltă.
            Vector3 home = _panel.localPosition;
            var shake = Tween.Value(0.32f, k =>
            {
                float amp = 0.05f * (1f - k);
                _panel.localPosition = home + new Vector3(Mathf.Sin(k * 60f) * amp, Mathf.Abs(Mathf.Sin(k * 45f)) * amp * 0.5f, 0f);
            }, Ease.Linear, null, 0f, false, _panel);
            yield return shake.Wait();
            _panel.localPosition = home;

            // 2. Se deschide: altă textură + un "pop" de scară.
            AudioManager.Play(Type == "boss" ? "door_boss_open" : "door_open");
            if (_open != null) _material.SetTexture(Materials.BaseMap, _open);
            Vector3 size = _panel.localScale;
            _panel.localScale = new Vector3(size.x * 1.08f, size.y * 1.08f, 1f);
            Tween.Scale(_panel, size, 0.3f, Ease.OutBack);
            if (Type == "boss") ScreenShake.Add(0.45f);
            else ScreenShake.Add(0.12f);

            // 3. Lumina camerei se revarsă pe hol.
            float from = _spill.intensity, to = Type == "boss" ? 3.5f : 2.6f;
            yield return Tween.Value(0.55f, k => _spill.intensity = Mathf.Lerp(from, to, k), Ease.OutCubic, null, 0f, false, _spill).Wait();
        }

        void OnDestroy() => LocalLight.Unregister(_spill);
    }
}
