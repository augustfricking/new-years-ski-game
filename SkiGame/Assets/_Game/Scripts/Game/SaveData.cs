using Ski.Core;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>PlayerPrefs-backed storage (IndexedDB in the browser) for leaderboards and settings.</summary>
    public sealed class PrefsStore : IKeyValueStore
    {
        public string Get(string key) => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
        public void Set(string key, string value) { PlayerPrefs.SetString(key, value); CloudSync.MarkDirty(); PlayerPrefs.Save(); }
    }

    /// <summary>Player settings and progress that survive between sessions.</summary>
    public static class SaveData
    {
        static Leaderboards _boards;
        public static Leaderboards Boards => _boards ??= new Leaderboards(new PrefsStore());
        static ChugBoard _chug;
        public static ChugBoard Chug => _chug ??= new ChugBoard(new PrefsStore());

        /// <summary>MusicVolume: the background theme (low: it loops all the time); SongVolume: the walkman and the DJ;
        /// VoiceChatVolume: other players' microphones.</summary>
        public static float MasterVolume = 0.85f, MusicVolume = 0.15f, SongVolume = 0.5f, AmbienceVolume = 0.3f, EffectsVolume = 0.7f,
                            VoiceVolume = 1f, VoiceChatVolume = 1f, MouseSensitivity = 0.12f;
        public static bool Subtitles = true, InvertLook;
        public static float BestChug; // seconds, 0 = none yet
        /// <summary>What this device is called on the boards. Empty: the character's name is shown.</summary>
        public static string PlayerName = "";
        /// <summary>Whether the first-launch question for a name has been answered (or skipped).</summary>
        public static bool NameAsked;
        /// <summary>The most other skiers (ghosts) shown on a slope at once. 0 switches them off.</summary>
        public static int MaxGhosts = 8;
        /// <summary>Meet the other people playing right now (on by default).</summary>
        public static bool Live = true;
        /// <summary>The walkman from the tavern: picked up, playing, and which song.</summary>
        public static bool HasWalkman, WalkmanOn = true, WalkmanRepeat;
        /// <summary>Voice chat with the other players (hold V to talk).</summary>
        public static bool Voice = true;
        /// <summary>Graphics: 0 low, 1 medium, 2 high; and the share of the screen resolution the 3D view is drawn at.</summary>
        public static int Quality = 1;
        public static float RenderScale = 1f;
        public static string WalkmanSong = "";

        /// <summary>The recording of this device's best run on a slope, or null.</summary>
        public static string Ghost(SlopeId slope) => PlayerPrefs.GetString("ghost_" + (int)slope, null);
        public static void SetGhost(SlopeId slope, string encoded) { PlayerPrefs.SetString("ghost_" + (int)slope, encoded); CloudSync.MarkDirty(); PlayerPrefs.Save(); }

        /// <summary>How a board row of this device is labelled.</summary>
        public static string BoardName(BoardEntry e) => !e.IsPlayer ? e.Name : PlayerName.Length > 0 ? PlayerName + " (" + e.Name + ")" : e.Name + " (you)";
        public static CharacterId LastCharacter = CharacterId.Erik;

        // ---- the shop: outfits and what has been earned --------------------------------------

        static readonly Progress[] _progress = new Progress[4];
        static readonly Outfit[] _outfits = new Outfit[4];
        /// <summary>Called with each thing newly earned.</summary>
        public static event System.Action<Unlock> Unlocked;

        /// <summary>What one character has done on this device (each earns their own things).</summary>
        public static Progress ProgressOf(CharacterId who)
        {
            int i = (int)who;
            if (_progress[i] != null) return _progress[i];
            string json = PlayerPrefs.GetString("progress_" + i, "");
            // Before items belonged to one friend, progress was shared: each friend starts from it.
            if (json.Length == 0) json = PlayerPrefs.GetString("progress", "");
            try { _progress[i] = JsonUtility.FromJson<Progress>(json); } catch (System.Exception) { }
            return _progress[i] ??= new Progress();
        }

        /// <summary>Changes one character's progress, saves it and announces anything newly earned.</summary>
        public static void Record(CharacterId who, System.Action<Progress> change)
        {
            SeenSet();          // read before the change, so the first time marks only older things seen
            var p = ProgressOf(who);
            var before = JsonUtility.FromJson<Progress>(JsonUtility.ToJson(p));
            change(p);
            PlayerPrefs.SetString("progress_" + (int)who, JsonUtility.ToJson(p));
            CloudSync.MarkDirty();
            PlayerPrefs.Save();
            foreach (var u in Unlocks.NewlyEarned(who, before, p)) Unlocked?.Invoke(u);
        }

        // ---- what the shop has shown ----------------------------------------------------------------
        static System.Collections.Generic.HashSet<string> _seen;

        static System.Collections.Generic.HashSet<string> SeenSet()
        {
            if (_seen != null) return _seen;
            _seen = new System.Collections.Generic.HashSet<string>();
            if (!PlayerPrefs.HasKey("seen_unlocks"))
            {
                // The first time: what was earned before the NEW tags existed counts as seen.
                foreach (var u in Unlocks.All) if (u.Earned(ProgressOf(u.Who))) _seen.Add(u.Id);
                PlayerPrefs.SetString("seen_unlocks", string.Join(",", _seen));
            }
            else foreach (var id in PlayerPrefs.GetString("seen_unlocks", "").Split(',')) if (id.Length > 0) _seen.Add(id);
            return _seen;
        }

        /// <summary>Earned by its friend and not yet seen in the shop.</summary>
        public static bool IsNew(Unlock u) => u != null && u.Earned(ProgressOf(u.Who)) && !SeenSet().Contains(u.Id);

        /// <summary>How many of this friend's things wait in the shop, unseen.</summary>
        public static int NewCount(CharacterId who)
        {
            int n = 0;
            foreach (var u in Unlocks.All) if (u.Who == who && IsNew(u)) n++;
            return n;
        }

        /// <summary>The shop has shown this friend everything they have earned.</summary>
        public static void MarkSeen(CharacterId who)
        {
            bool changed = false;
            foreach (var u in Unlocks.All) if (u.Who == who && u.Earned(ProgressOf(who)) && SeenSet().Add(u.Id)) changed = true;
            if (!changed) return;
            PlayerPrefs.SetString("seen_unlocks", string.Join(",", _seen));
            CloudSync.MarkDirty();
            PlayerPrefs.Save();
        }

        /// <summary>How a character is dressed on this device (a copy: change it with <see cref="SetOutfit"/>).</summary>
        public static Outfit Outfit(CharacterId who)
        {
            int i = (int)who;
            if (_outfits[i] == null)
            {
                try { _outfits[i] = JsonUtility.FromJson<Outfit>(PlayerPrefs.GetString("outfit_" + i, "")); } catch (System.Exception) { }
                _outfits[i] ??= new Outfit();
            }
            return Unlocks.Allowed(who, ProgressOf(who), _outfits[i]);
        }

        public static void SetOutfit(CharacterId who, Outfit o)
        {
            _outfits[(int)who] = o.Copy();
            PlayerPrefs.SetString("outfit_" + (int)who, JsonUtility.ToJson(o));
            CloudSync.MarkDirty();
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _boards = null; _chug = null; System.Array.Clear(_progress, 0, 4); System.Array.Clear(_outfits, 0, 4); Unlocked = null; }

        public static void Load()
        {
            MasterVolume = PlayerPrefs.GetFloat("vol_master", 0.85f);
            // "vol_music2" was the one music level for everything; it now sets the songs, and the theme starts low.
            MusicVolume = PlayerPrefs.GetFloat("vol_theme", 0.15f);
            SongVolume = PlayerPrefs.GetFloat("vol_music2", 0.5f);
            AmbienceVolume = PlayerPrefs.GetFloat("vol_ambience", 0.3f);
            EffectsVolume = PlayerPrefs.GetFloat("vol_effects", 0.7f);
            VoiceVolume = PlayerPrefs.GetFloat("vol_voice", 1f);
            VoiceChatVolume = PlayerPrefs.GetFloat("vol_chat", 1f);
            MouseSensitivity = PlayerPrefs.GetFloat("mouse", 0.12f);
            Subtitles = PlayerPrefs.GetInt("subs", 1) == 1;
            InvertLook = PlayerPrefs.GetInt("invert", 0) == 1;
            BestChug = PlayerPrefs.GetFloat("chug", 0f);
            PlayerName = PlayerPrefs.GetString("name", "");
            NameAsked = PlayerPrefs.GetInt("name_asked", 0) == 1 || PlayerName.Length > 0;
            MaxGhosts = PlayerPrefs.GetInt("others", 8);
            Live = PlayerPrefs.GetInt("live", 1) == 1;
            HasWalkman = PlayerPrefs.GetInt("walkman", 0) == 1;
            Voice = PlayerPrefs.GetInt("voice", 1) == 1;
            Quality = PlayerPrefs.GetInt("quality", 1);
            RenderScale = PlayerPrefs.GetFloat("render_scale", 1f);
            WalkmanOn = PlayerPrefs.GetInt("walkman_on", 1) == 1;
            WalkmanRepeat = PlayerPrefs.GetInt("walkman_repeat", 0) == 1;
            WalkmanSong = PlayerPrefs.GetString("walkman_song", "");
            LastCharacter = (CharacterId)Mathf.Clamp(PlayerPrefs.GetInt("who", 2), 0, 3);
            Apply();
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat("vol_master", MasterVolume);
            PlayerPrefs.SetFloat("vol_theme", MusicVolume);
            PlayerPrefs.SetFloat("vol_music2", SongVolume);
            PlayerPrefs.SetFloat("vol_chat", VoiceChatVolume);
            PlayerPrefs.SetFloat("vol_ambience", AmbienceVolume);
            PlayerPrefs.SetFloat("vol_effects", EffectsVolume);
            PlayerPrefs.SetFloat("vol_voice", VoiceVolume);
            PlayerPrefs.SetFloat("mouse", MouseSensitivity);
            PlayerPrefs.SetInt("subs", Subtitles ? 1 : 0);
            PlayerPrefs.SetInt("invert", InvertLook ? 1 : 0);
            PlayerPrefs.SetFloat("chug", BestChug);
            PlayerPrefs.SetString("name", PlayerName);
            PlayerPrefs.SetInt("name_asked", NameAsked ? 1 : 0);
            PlayerPrefs.SetInt("others", MaxGhosts);
            PlayerPrefs.SetInt("live", Live ? 1 : 0);
            PlayerPrefs.SetInt("walkman", HasWalkman ? 1 : 0);
            PlayerPrefs.SetInt("voice", Voice ? 1 : 0);
            PlayerPrefs.SetInt("quality", Quality);
            PlayerPrefs.SetFloat("render_scale", RenderScale);
            PlayerPrefs.SetInt("walkman_on", WalkmanOn ? 1 : 0);
            PlayerPrefs.SetInt("walkman_repeat", WalkmanRepeat ? 1 : 0);
            PlayerPrefs.SetString("walkman_song", WalkmanSong ?? "");
            PlayerPrefs.SetInt("who", (int)LastCharacter);
            CloudSync.MarkDirty();
            PlayerPrefs.Save();
            Apply();
        }

        /// <summary>Everything read again from storage (after a save came down from the cloud).</summary>
        public static void Reload()
        {
            _boards = null; _chug = null;
            System.Array.Clear(_progress, 0, 4); System.Array.Clear(_outfits, 0, 4);
            _seen = null;
            Load();
            GraphicsQuality.Apply();
        }

        public static void Apply()
        {
            var a = AudioDirector.I;
            if (a == null) return;
            a.Master = MasterVolume; a.MusicVol = MusicVolume; a.SongVol = SongVolume; a.AmbienceVol = AmbienceVolume; a.SfxVol = EffectsVolume; a.VoiceVol = VoiceVolume;
        }
    }
}
