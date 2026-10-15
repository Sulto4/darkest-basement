using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkestBasement.Audio
{
    // Toate sunetele jocului trec pe aici. Datele (volum, buclă, variație de pitch) vin din
    // Resources/Audio/audio_manifest.json, generat de tools/audio/generate.py.
    //
    // Folosire:
    //   AudioManager.Play("hit_flesh");         // un sunet din grupul "hit_flesh", ales random
    //   AudioManager.PlayMusic("music_combat"); // trece lin de la muzica curentă la cea nouă
    //   AudioManager.PlayAmbience("basement_ambience");
    public sealed class AudioManager : MonoBehaviour
    {
        [Serializable]
        sealed class SoundEntry
        {
            public string id;
            public string path;
            public string type;
            public string category;
            public string group;
            public float volume = 1f;
            public bool loop;
            public float pitchVariance;
            public float duration;
        }

        [Serializable]
        sealed class Manifest { public List<SoundEntry> sounds = new List<SoundEntry>(); }

        const int SfxVoices = 16;
        const float MusicFade = 1.2f;

        static AudioManager _instance;

        public static float MasterVolume = 1f;
        public static float MusicVolume = 0.8f;
        public static float SfxVolume = 1f;

        readonly Dictionary<string, SoundEntry> _byId = new Dictionary<string, SoundEntry>();
        readonly Dictionary<string, List<SoundEntry>> _byGroup = new Dictionary<string, List<SoundEntry>>();
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();
        AudioSource[] _sfx;
        int _nextVoice;

        // Două surse pentru muzică: una se stinge în timp ce cealaltă crește (crossfade).
        AudioSource _musicA, _musicB, _ambience;
        SoundEntry _musicEntry, _ambienceEntry;
        float _musicMix = 1f; // 1 = A se aude, 0 = B se aude
        float _musicTarget = 1f;

        public static AudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[Audio]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<AudioManager>();
                }
                return _instance;
            }
        }

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            LoadManifest();

            _sfx = new AudioSource[SfxVoices];
            for (int i = 0; i < SfxVoices; i++) _sfx[i] = NewSource("sfx_" + i, false);
            _musicA = NewSource("music_a", true);
            _musicB = NewSource("music_b", true);
            _ambience = NewSource("ambience", true);
        }

        AudioSource NewSource(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f; // sunet 2D: nu depinde de poziția camerei
            return s;
        }

        void LoadManifest()
        {
            var text = Resources.Load<TextAsset>("Audio/audio_manifest");
            if (text == null) { Debug.LogWarning("[Audio] lipsește audio_manifest.json"); return; }
            var manifest = JsonUtility.FromJson<Manifest>(text.text);
            foreach (var e in manifest.sounds)
            {
                _byId[e.id] = e;
                var g = string.IsNullOrEmpty(e.group) ? e.id : e.group;
                if (!_byGroup.TryGetValue(g, out var list)) _byGroup[g] = list = new List<SoundEntry>();
                list.Add(e);
            }
        }

        AudioClip Clip(SoundEntry e)
        {
            if (_clips.TryGetValue(e.id, out var c)) return c;
            c = Resources.Load<AudioClip>(e.path);
            if (c == null) Debug.LogWarning("[Audio] lipsește clipul " + e.path);
            _clips[e.id] = c;
            return c;
        }

        // Primește fie un id exact ("footstep_stone_2"), fie un grup ("footstep_stone").
        SoundEntry Resolve(string idOrGroup)
        {
            if (_byGroup.TryGetValue(idOrGroup, out var list) && list.Count > 0)
                return list[UnityEngine.Random.Range(0, list.Count)];
            _byId.TryGetValue(idOrGroup, out var e);
            return e;
        }

        public static void Play(string idOrGroup, float volumeScale = 1f, float pitch = 1f) => Instance.PlayInternal(idOrGroup, volumeScale, pitch);

        // Fiecare sunet care chiar pornește (QA-ul verifică așa că butoanele „sună” la hover și la click).
        public static event Action<string> Played;

        void PlayInternal(string idOrGroup, float volumeScale, float pitch)
        {
            var e = Resolve(idOrGroup);
            if (e == null) { Debug.LogWarning("[Audio] sunet necunoscut: " + idOrGroup); return; }
            var clip = Clip(e);
            if (clip == null) return;

            // Același sunet de 5 ori în același frame sună ca un zgomot, nu ca 5 lovituri.
            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(e.group ?? e.id, out var last) && now - last < 0.03f) return;
            _lastPlayed[e.group ?? e.id] = now;

            var src = _sfx[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _sfx.Length;
            src.clip = clip;
            src.volume = e.volume * volumeScale * SfxVolume * MasterVolume;
            // Variația mică de pitch face ca lovitura a zecea să nu sune identic cu prima.
            src.pitch = pitch * (1f + UnityEngine.Random.Range(-e.pitchVariance, e.pitchVariance));
            src.Play();
            Played?.Invoke(idOrGroup);
        }

        public static void PlayMusic(string id) => Instance.PlayMusicInternal(id);

        void PlayMusicInternal(string id)
        {
            var e = Resolve(id);
            if (e == null || e == _musicEntry) return;
            _musicEntry = e;
            var clip = Clip(e);
            // Pornim pe sursa care acum e stinsă și mutăm mixul spre ea.
            var incoming = _musicTarget > 0.5f ? _musicB : _musicA;
            incoming.clip = clip;
            incoming.time = 0f;
            incoming.Play();
            _musicTarget = incoming == _musicA ? 1f : 0f;
        }

        public static void StopMusic() { Instance._musicEntry = null; Instance._musicA.Stop(); Instance._musicB.Stop(); }

        public static void PlayAmbience(string id)
        {
            var m = Instance;
            var e = m.Resolve(id);
            if (e == null || e == m._ambienceEntry) return;
            m._ambienceEntry = e;
            m._ambience.clip = m.Clip(e);
            m._ambience.Play();
        }

        void Update()
        {
            _musicMix = Mathf.MoveTowards(_musicMix, _musicTarget, Time.unscaledDeltaTime / MusicFade);
            float baseVol = (_musicEntry != null ? _musicEntry.volume : 1f) * MusicVolume * MasterVolume;
            _musicA.volume = baseVol * _musicMix;
            _musicB.volume = baseVol * (1f - _musicMix);
            if (_ambienceEntry != null) _ambience.volume = _ambienceEntry.volume * MusicVolume * MasterVolume;
        }
    }
}
