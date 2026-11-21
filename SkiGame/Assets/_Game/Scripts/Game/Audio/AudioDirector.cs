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
        /// <summary>MusicVol: the background theme; SongVol: the songs (walkman, the DJ).</summary>
        public float Master = 0.85f, MusicVol = 0.15f, SongVol = 0.5f, AmbienceVol = 0.3f, SfxVol = 0.7f, VoiceVol = 1f;
        /// <summary>Set while the game is paused: skis and wind fall silent at once.</summary>
        public bool Paused;

        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        readonly List<AudioSource> _pool = new List<AudioSource>();
        AudioSource _windA, _windB, _carve, _skid, _musicA, _musicB, _amb, _amb2, _voice;
        string _music = "", _ambience = "", _ambience2 = "";
        float _musicBlend = 1f, _musicLevel = 1f;
        bool _songA, _songB;          // whether each music source plays a song (its volume is SongVol, else MusicVol)

        // The background theme (StreamingAssets/Music/theme.mp3, made by tools/music/make_theme_yue2.py): one
        // seamless loop for the menus and the runs, fetched once at start. Until it is here, or if it cannot be
        // had, the synthesised loops play.
        public const string Theme = "theme";
        AudioClip _theme;
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
            StartCoroutine(FetchTheme());
        }

        System.Collections.IEnumerator FetchTheme()
        {
            // A few tries: the host can answer "busy" for a moment (GitHub Pages does while a new version goes up).
            float[] waits = { 0f, 3f, 8f, 20f, 45f };
            foreach (float wait in waits)
            {
                if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
                using (var req = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(Songs.Url(Theme + ".mp3"), AudioType.MPEG))
                {
                    yield return req.SendWebRequest();
                    if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                    {
                        _theme = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(req);
                        if (_theme != null) { _theme.name = Theme; break; }
                    }
                    else Debug.Log("[Audio] theme: " + req.error);
                }
            }
            if (_theme != null && IsThemeScene(_sceneMusic)) Route();
        }

        /// <summary>The scenes whose music is the theme (their own synthesised loop is the fallback).</summary>
        static bool IsThemeScene(string id) => id == "music_ski" || id == "music_menu";

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

        // The interface's ticks only ever answer the player: a click needs a button or key pressed (or
        // let go) a moment ago, a hover needs the pointer to have moved since the last one. Without
        // this, anything that re-sends a hover or click by itself (a panel laid out again under a still
        // pointer, the browser's pointer lock coming and going, a map run flickering under the cursor)
        // ticks on and on like a stuck button, until a real click changes what is under the pointer.
        float _lastInput = -10f;
        Vector2 _lastHoverAt = new Vector2(-9999f, -9999f);

        public static void Click()
        {
            if (I == null) return;
            I.NoteInput();                                               // this very frame's press counts too
            if (Time.unscaledTime - I._lastInput > 0.6f) return;
            I.Play("click", volume: 0.35f);
        }

        public static void Hover()
        {
            if (I == null) return;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;
            Vector2 at = mouse.position.ReadValue();
            if ((at - I._lastHoverAt).sqrMagnitude < 9f) return;         // the pointer has not moved: not a real hover
            I._lastHoverAt = at;
            I.Play("hover", volume: 0.12f);
        }

        void NoteInput()
        {
            var m = UnityEngine.InputSystem.Mouse.current;
            var k = UnityEngine.InputSystem.Keyboard.current;
            bool any = (m != null && (m.leftButton.isPressed || m.leftButton.wasReleasedThisFrame || m.rightButton.wasPressedThisFrame))
                    || (k != null && k.anyKey.wasPressedThisFrame);
            if (any) _lastInput = Time.unscaledTime;
        }

        // A sound asked for over and over (a UI event firing every frame) must never turn into a buzz:
        // each sound has a short gap it cannot be repeated within, and a burst is cut off.
        readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();
        readonly Dictionary<string, int> _burst = new Dictionary<string, int>();
        readonly HashSet<string> _warned = new HashSet<string>();

        bool Allowed(string id)
        {
            float now = Time.unscaledTime;
            float gap = id == "hover" ? 0.09f : id == "click" ? 0.06f : 0.03f;
            if (_lastPlayed.TryGetValue(id, out float last))
            {
                if (now - last < gap) return false;
                int n = now - last < 0.5f ? (_burst.TryGetValue(id, out int b) ? b + 1 : 1) : 0;
                _burst[id] = n;
                if (n > 6)
                {
                    if (_warned.Add(id)) Debug.Log("[Audio] '" + id + "' asked for again and again; held back");
                    _lastPlayed[id] = now;
                    return false;
                }
            }
            _lastPlayed[id] = now;
            return true;
        }
        public static void Sfx(string id, float volume = 1f, float pitch = 1f) { if (I != null) I.Play(id, volume, pitch); }

        public void Play(string id, float volume = 1f, float pitch = 1f, float pan = 0f)
        {
            if (!Allowed(id)) return;
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

        string _sceneMusic = "";
        float _sceneLevel = 1f;
        bool _songPlaying;
        float _songStart;
        /// <summary>In the tavern the DJ plays, and the walkman waits.</summary>
        public bool DjHasFloor;
        /// <summary>Called when a song (not a synthesised loop) has played to its end, with its id.</summary>
        public event System.Action<string> SongEnded;
        /// <summary>Called when a song starts playing (its clip loaded), with its id: the "now playing" card.</summary>
        public static event System.Action<string> SongStarted;
        /// <summary>The song playing now (its id), or null.</summary>
        public string CurrentSong => _music.StartsWith("song:") ? _music.Substring(5) : null;

        /// <summary>
        /// The music a scene wants: a synthesised loop by name, or "song:&lt;id&gt;" for one of the DJ's
        /// songs. With the walkman on, your song plays instead (outside the tavern).
        /// </summary>
        public void Music(string id, float level = 1f)
        {
            _sceneMusic = id ?? "";
            _sceneLevel = level;
            Route();
        }

        /// <summary>Re-reads the walkman: call after it is switched or skips a song.</summary>
        public void RefreshMusic(bool restart = false)
        {
            if (restart && _music.StartsWith("song:")) _music = "";
            Route();
        }

        /// <summary>Plays the scene's song again from the start (a request for the song that is on).</summary>
        public void RestartMusic()
        {
            _music = "";
            Route();
        }

        void Route()
        {
            bool walkman = Walkman.Active && !DjHasFloor;
            string id = walkman ? "song:" + Walkman.Current : _sceneMusic;
            // Menus and runs share one theme, so going from one to the other does not start it again.
            if (!walkman && IsThemeScene(id) && _theme != null) id = Theme;
            PlayMusic(id, walkman ? 0.9f : _sceneLevel);
        }

        // Its own name: as a second "Play(string, float)" it was the better match for Play("click", 0.35f), so every
        // button click and hover replaced the music (or the song) with a looping click.
        void PlayMusic(string id, float level)
        {
            _musicLevel = level;
            if (id == _music) return;
            Debug.Log("[Audio] music " + (string.IsNullOrEmpty(_music) ? "-" : _music) + " -> " + (string.IsNullOrEmpty(id) ? "-" : id));
            _music = id ?? "";
            // Swap the roles: the old track fades out on B, the new one fades in on A.
            var t = _musicA; _musicA = _musicB; _musicB = t;
            _songB = _songA; _songA = id != null && id.StartsWith("song:");
            _musicBlend = 0f;
            _musicA.Stop();
            _musicA.clip = null;
            _songPlaying = false;
            if (string.IsNullOrEmpty(id)) return;
            if (id.StartsWith("song:"))
            {
                // A whole song: fetched when first played, and played once (the end picks the next).
                _musicA.loop = false;
                var src = _musicA;
                Songs.Clip(this, id.Substring(5), clip =>
                {
                    if (_music != id || clip == null || src != _musicA) return;
                    src.clip = clip;
                    src.Play();
                    _songPlaying = true;
                    _songStart = Time.unscaledTime;
                    SongStarted?.Invoke(id.Substring(5));
                });
                return;
            }
            _musicA.loop = true;
            _musicA.clip = id == Theme ? _theme : Clip(id);
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
            NoteInput();
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

            // A song has finished: the walkman moves on by itself, the DJ picks the next one.
            // (By the clock as well: a browser can report a song as stopped while it is still being decoded.)
            // A clip still loading can report no length at all: then it has not ended, whatever it says.
            float songLen = _musicA.clip != null ? _musicA.clip.length : 0f;
            if (_songPlaying && songLen > 5f && !_musicA.isPlaying && Time.unscaledTime - _songStart > Mathf.Max(songLen - 1f, 10f))
            {
                _songPlaying = false;
                string ended = CurrentSong;
                if (Walkman.Active && !DjHasFloor)
                {
                    if (SaveData.WalkmanRepeat) RestartMusic(); else Walkman.Next();
                }
                SongEnded?.Invoke(ended);
            }
            // A loop (the theme, a synthesised piece) never stops by itself: if the browser dropped it, start it again.
            if (!_songA && _musicA.loop && _musicA.clip != null && !_musicA.isPlaying && _musicBlend > 0.5f && _musicA.clip.loadState == AudioDataLoadState.Loaded)
            {
                Debug.Log("[Audio] " + _music + " had stopped: playing it again");
                _musicA.Play();
            }
            _musicBlend = Mathf.MoveTowards(_musicBlend, 1f, dt / 1.2f);
            float duck = _voice.isPlaying ? 0.5f : 1f;
            _musicA.volume = Mathf.Lerp(_musicA.volume, _musicBlend * (_songA ? SongVol : MusicVol) * _musicLevel * duck, slow * 2f);
            _musicB.volume = (1f - _musicBlend) * (_songB ? SongVol : MusicVol) * _musicLevel * duck;
            if (_musicBlend >= 1f && _musicB.isPlaying) _musicB.Stop();
            _amb.volume = Mathf.Lerp(_amb.volume, AmbienceLevel * AmbienceVol, slow);
            _amb2.volume = Mathf.Lerp(_amb2.volume, AmbienceLevel * 0.8f * AmbienceVol, slow);
            _voice.volume = VoiceVol;
        }

        void OnDestroy() { if (I == this) I = null; }
    }
}
