using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// All sound goes through here. Four groups with their own volume: music, ambience (wind,
    /// lift hum, crowd, fire), effects (skis, poles, jumps, crashes, UI) and voices.
    /// Effects are synthesised by <see cref="Synth"/>; voices are loaded from Resources/Voices.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        public static AudioDirector I { get; private set; }

        // Volumes (0..1), saved by SaveData.
        public float Master = 0.85f, MusicVol = 0.4f, AmbienceVol = 0.4f, SfxVol = 0.6f, VoiceVol = 1f;
        /// <summary>Set while the game is paused: skis and wind fall silent at once.</summary>
        public bool Paused;

        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        readonly List<AudioSource> _pool = new List<AudioSource>();
        AudioSource _windA, _windB, _carve, _skid, _musicA, _musicB, _amb, _amb2, _voice;
        string _music = "", _ambience = "", _ambience2 = "";
        float _musicBlend = 1f, _musicLevel = 1f;
        float _windT, _carveT, _skidT, _windPitch = 1f, _time;
        int _next;

        public static AudioDirector Create(Transform parent)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<AudioDirector>();
            I.Build();
            return I;
        }

        void Build()
        {
            _windA = Loop(); _windB = Loop(); _carve = Loop(); _skid = Loop();
            _musicA = Loop(); _musicB = Loop(); _amb = Loop(); _amb2 = Loop();
            _voice = gameObject.AddComponent<AudioSource>();
            _voice.playOnAwake = false; _voice.spatialBlend = 0f;
            for (int i = 0; i < 10; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false; s.spatialBlend = 0f;
                _pool.Add(s);
            }
            StartCoroutine(Warm());
        }

        AudioSource Loop()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = true; s.playOnAwake = false; s.spatialBlend = 0f; s.volume = 0f;
            return s;
        }

        /// <summary>Generates the common sounds a few per frame, so nothing hitches when first played.</summary>
        IEnumerator Warm()
        {
            yield return null;
            foreach (var id in Synth.WarmList)
            {
                Clip(id);
                yield return null;
            }
            Begin(_windA, "wind_a"); Begin(_windB, "wind_b"); Begin(_carve, "carve"); Begin(_skid, "skid");
        }

        static void Begin(AudioSource s, string id)
        {
            if (s.clip != null) return;
            s.clip = I.Clip(id);
            if (s.clip != null) { s.time = 0f; s.Play(); }
        }

        public AudioClip Clip(string id)
        {
            if (_clips.TryGetValue(id, out var c)) return c;
            c = Synth.Make(id);
            _clips[id] = c;
            return c;
        }

        // ---- one-shots ---------------------------------------------------------------------

        public static void Click() { if (I != null) I.Play("click", 0.6f); }
        public static void Hover() { if (I != null) I.Play("hover", 0.5f); }
        public static void Sfx(string id, float volume = 1f, float pitch = 1f) { if (I != null) I.Play(id, volume, pitch); }

        public void Play(string id, float volume = 1f, float pitch = 1f, float pan = 0f)
        {
            var c = Clip(id);
            if (c == null) return;
            var s = _pool[_next++ % _pool.Count];
            s.pitch = pitch; s.panStereo = pan;
            s.PlayOneShot(c, volume * SfxVol);
        }

        public AudioSource Voice(AudioClip clip, float pitch = 1f)
        {
            if (clip == null) return null;
            _voice.Stop();
            _voice.clip = clip; _voice.pitch = pitch; _voice.volume = VoiceVol;
            _voice.Play();
            return _voice;
        }

        public void StopVoice() { _voice.Stop(); }
        public bool VoicePlaying => _voice.isPlaying;

        // ---- loops -------------------------------------------------------------------------

        /// <summary>
        /// Feeds the ski and wind sounds from the simulation each frame.
        /// carve: 0..1 clean edge; skid: 0..1 scraping (over-edged turn, snowplough, side slip).
        /// </summary>
        public void SkiState(float speed, float skid, float carve, bool contact, float storm = 0f)
        {
            float v = Mathf.Clamp01(speed / 34f);
            _windT = Mathf.Clamp01(0.06f + v * v * 0.7f + storm * 0.7f);
            _windPitch = 0.75f + v * 0.6f + storm * 0.2f;
            float moving = Mathf.Clamp01(speed / 6f);
            _carveT = contact ? moving * (0.25f + 0.45f * carve + 0.3f * v) * (1f - Mathf.Clamp01(skid)) : 0f;
            _skidT = contact ? Mathf.Clamp01(skid) * moving : 0f;
            _carve.pitch = 0.8f + v * 0.7f;
            _skid.pitch = 0.75f + v * 0.5f + skid * 0.15f;
        }

        public void Silence() { _windT = _carveT = _skidT = 0f; }

        /// <summary>Wind only, for scenes that are not skiing (lift, storm, map).</summary>
        public void Wind(float amount) { _windT = amount; _windPitch = 0.8f + amount * 0.4f; _carveT = _skidT = 0f; }

        public void Music(string id, float level = 1f)
        {
            _musicLevel = level;
            if (id == _music) return;
            _music = id ?? "";
            // Swap the roles: the old track fades out on B, the new one fades in on A.
            var t = _musicA; _musicA = _musicB; _musicB = t;
            _musicBlend = 0f;
            _musicA.Stop();
            _musicA.clip = string.IsNullOrEmpty(id) ? null : Clip(id);
            if (_musicA.clip != null) _musicA.Play();
        }

        public void Ambience(string id, string second = null)
        {
            if (id != _ambience)
            {
                _ambience = id ?? "";
                _amb.Stop();
                _amb.clip = string.IsNullOrEmpty(id) ? null : Clip(id);
                if (_amb.clip != null) _amb.Play();
            }
            if (second != _ambience2)
            {
                _ambience2 = second ?? "";
                _amb2.Stop();
                _amb2.clip = string.IsNullOrEmpty(second) ? null : Clip(second);
                if (_amb2.clip != null) _amb2.Play();
            }
        }

        public float AmbienceLevel = 0.7f;

        void Update()
        {
            AudioListener.volume = Master;
            float dt = Time.unscaledDeltaTime;
            _time += dt;
            float slow = 1f - Mathf.Exp(-5f * dt), fast = 1f - Mathf.Exp(-28f * dt);
            float live = Paused ? 0f : 1f;

            // Wind: two layers of different length, each wandering in level and pitch, so the
            // result never settles into a pattern.
            float g1 = 0.55f + 0.45f * Mathf.PerlinNoise(_time * 0.21f, 3.1f), g2 = 0.35f + 0.65f * Mathf.PerlinNoise(_time * 0.13f, 7.7f);
            _windA.volume = Mathf.Lerp(_windA.volume, _windT * g1 * 0.5f * AmbienceVol * live, Paused ? fast : slow);
            _windB.volume = Mathf.Lerp(_windB.volume, _windT * g2 * 0.38f * AmbienceVol * live, Paused ? fast : slow);
            _windA.pitch = _windPitch * (0.92f + 0.16f * Mathf.PerlinNoise(_time * 0.09f, 1.3f));
            _windB.pitch = _windPitch * (0.85f + 0.3f * Mathf.PerlinNoise(_time * 0.07f, 5.9f));

            // Skis: the scrape answers at once, so you hear the moment a carve turns into a skid.
            _carve.volume = Mathf.Lerp(_carve.volume, _carveT * 0.5f * SfxVol * live, fast);
            _skid.volume = Mathf.Lerp(_skid.volume, _skidT * 0.6f * SfxVol * live, fast);

            _musicBlend = Mathf.MoveTowards(_musicBlend, 1f, dt / 1.2f);
            float duck = _voice.isPlaying ? 0.5f : 1f;
            _musicA.volume = Mathf.Lerp(_musicA.volume, _musicBlend * MusicVol * _musicLevel * duck, slow * 2f);
            _musicB.volume = (1f - _musicBlend) * MusicVol * _musicLevel * duck;
            if (_musicBlend >= 1f && _musicB.isPlaying) _musicB.Stop();
            _amb.volume = Mathf.Lerp(_amb.volume, AmbienceLevel * AmbienceVol, slow);
            _amb2.volume = Mathf.Lerp(_amb2.volume, AmbienceLevel * 0.8f * AmbienceVol, slow);
            _voice.volume = VoiceVol;
        }

        void OnDestroy() { if (I == this) I = null; }
    }
}
