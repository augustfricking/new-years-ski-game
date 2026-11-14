using System.Collections.Generic;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// All sound goes through here: one-shot effects, the wind and ski loops that follow the
    /// simulation, ambience, music and voices. Effects are synthesised at start-up by
    /// <see cref="Synth"/>; voices are loaded from Resources/Voices.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        public static AudioDirector I { get; private set; }

        // Volumes (0..1), saved by Settings.
        public float Master = 0.85f, MusicVol = 0.6f, SfxVol = 0.9f, VoiceVol = 1f;

        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        readonly List<AudioSource> _pool = new List<AudioSource>();
        AudioSource _wind, _ski, _skid, _musicA, _musicB, _amb, _voice;
        string _music = "", _ambience = "";
        float _musicBlend = 1f;
        float _windT, _skiT, _skidT;
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
            _wind = Loop("wind"); _ski = Loop("ski"); _skid = Loop("skid");
            _musicA = Loop(null); _musicB = Loop(null); _amb = Loop(null);
            _voice = gameObject.AddComponent<AudioSource>();
            _voice.playOnAwake = false; _voice.spatialBlend = 0f;
            for (int i = 0; i < 10; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false; s.spatialBlend = 0f;
                _pool.Add(s);
            }
        }

        AudioSource Loop(string clip)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = true; s.playOnAwake = false; s.spatialBlend = 0f; s.volume = 0f;
            if (clip != null) { s.clip = Clip(clip); if (s.clip != null) s.Play(); }
            return s;
        }

        public AudioClip Clip(string id)
        {
            if (_clips.TryGetValue(id, out var c)) return c;
            c = Synth.Make(id);
            _clips[id] = c;
            return c;
        }

        // ---- one-shots ---------------------------------------------------------------------

        public static void Click() { if (I != null) I.Play("click", 0.5f); }
        public static void Hover() { if (I != null) I.Play("hover", 0.25f); }
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

        /// <summary>Feeds the wind and ski sounds from the simulation each frame.</summary>
        public void SkiState(float speed, float skid, float carve, bool contact, float storm = 0f)
        {
            float v = Mathf.Clamp01(speed / 34f);
            _windT = Mathf.Clamp01(v * v * 0.9f + 0.05f + storm * 0.6f);
            _wind.pitch = 0.7f + v * 0.9f + storm * 0.2f;
            _skiT = contact ? Mathf.Clamp01(v * 1.4f) * (0.35f + carve * 0.45f) : 0f;
            _ski.pitch = 0.75f + v * 0.7f;
            _skidT = contact ? Mathf.Clamp01(skid) * Mathf.Clamp01(speed / 8f) : 0f;
            _skid.pitch = 0.8f + v * 0.5f;
        }

        public void Silence() { _windT = _skiT = _skidT = 0f; }

        /// <summary>Wind only, for scenes that are not skiing (lift, storm).</summary>
        public void Wind(float amount) { _windT = amount; _wind.pitch = 0.75f + amount * 0.5f; _skiT = _skidT = 0f; }

        public void Music(string id)
        {
            if (id == _music) return;
            _music = id ?? "";
            // Swap the roles: the old track fades out on B, the new one fades in on A.
            var t = _musicA; _musicA = _musicB; _musicB = t;
            _musicBlend = 0f;
            _musicA.Stop();
            _musicA.clip = string.IsNullOrEmpty(id) ? null : Clip(id);
            if (_musicA.clip != null) _musicA.Play();
        }

        public void Ambience(string id)
        {
            if (id == _ambience) return;
            _ambience = id ?? "";
            _amb.Stop();
            _amb.clip = string.IsNullOrEmpty(id) ? null : Clip(id);
            if (_amb.clip != null) _amb.Play();
        }

        public float AmbienceVolume = 0.6f;
        public float MusicDuck = 1f; // lowered while voices speak

        void Update()
        {
            AudioListener.volume = Master;
            float dt = Time.unscaledDeltaTime, k = 1f - Mathf.Exp(-6f * dt);
            _wind.volume = Mathf.Lerp(_wind.volume, _windT * 0.55f * SfxVol, k);
            _ski.volume = Mathf.Lerp(_ski.volume, _skiT * 0.5f * SfxVol, k);
            _skid.volume = Mathf.Lerp(_skid.volume, _skidT * 0.6f * SfxVol, k);
            _musicBlend = Mathf.MoveTowards(_musicBlend, 1f, dt / 1.2f);
            float duck = _voice.isPlaying ? 0.45f : 1f;
            MusicDuck = Mathf.Lerp(MusicDuck, duck, k);
            _musicA.volume = _musicBlend * MusicVol * MusicDuck;
            _musicB.volume = (1f - _musicBlend) * MusicVol * MusicDuck;
            if (_musicBlend >= 1f && _musicB.isPlaying) _musicB.Stop();
            _amb.volume = Mathf.Lerp(_amb.volume, AmbienceVolume * SfxVol, k);
            _voice.volume = VoiceVol;
        }

        void OnDestroy() { if (I == this) I = null; }
    }
}
