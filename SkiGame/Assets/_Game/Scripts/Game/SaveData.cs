using Ski.Core;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>PlayerPrefs-backed storage (IndexedDB in the browser) for leaderboards and settings.</summary>
    public sealed class PrefsStore : IKeyValueStore
    {
        public string Get(string key) => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
        public void Set(string key, string value) { PlayerPrefs.SetString(key, value); PlayerPrefs.Save(); }
    }

    /// <summary>Player settings and progress that survive between sessions.</summary>
    public static class SaveData
    {
        static Leaderboards _boards;
        public static Leaderboards Boards => _boards ??= new Leaderboards(new PrefsStore());
        static ChugBoard _chug;
        public static ChugBoard Chug => _chug ??= new ChugBoard(new PrefsStore());

        public static float MasterVolume = 0.85f, MusicVolume = 0.4f, AmbienceVolume = 0.4f, EffectsVolume = 0.6f, VoiceVolume = 1f, MouseSensitivity = 0.12f;
        public static bool Subtitles = true, InvertLook;
        public static float BestChug; // seconds, 0 = none yet
        /// <summary>What this device is called on the boards. Empty: the character's name is shown.</summary>
        public static string PlayerName = "";
        /// <summary>Whether the first-launch question for a name has been answered (or skipped).</summary>
        public static bool NameAsked;
        /// <summary>The most other skiers (ghosts) shown on a slope at once. 0 switches them off.</summary>
        public static int MaxGhosts = 6;

        /// <summary>The recording of this device's best run on a slope, or null.</summary>
        public static string Ghost(SlopeId slope) => PlayerPrefs.GetString("ghost_" + (int)slope, null);
        public static void SetGhost(SlopeId slope, string encoded) { PlayerPrefs.SetString("ghost_" + (int)slope, encoded); PlayerPrefs.Save(); }

        /// <summary>How a board row of this device is labelled.</summary>
        public static string BoardName(BoardEntry e) => !e.IsPlayer ? e.Name : PlayerName.Length > 0 ? PlayerName + " (" + e.Name + ")" : e.Name + " (you)";
        public static CharacterId LastCharacter = CharacterId.Erik;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _boards = null; _chug = null; }

        public static void Load()
        {
            MasterVolume = PlayerPrefs.GetFloat("vol_master", 0.85f);
            MusicVolume = PlayerPrefs.GetFloat("vol_music2", 0.4f);
            AmbienceVolume = PlayerPrefs.GetFloat("vol_ambience", 0.4f);
            EffectsVolume = PlayerPrefs.GetFloat("vol_effects", 0.6f);
            VoiceVolume = PlayerPrefs.GetFloat("vol_voice", 1f);
            MouseSensitivity = PlayerPrefs.GetFloat("mouse", 0.12f);
            Subtitles = PlayerPrefs.GetInt("subs", 1) == 1;
            InvertLook = PlayerPrefs.GetInt("invert", 0) == 1;
            BestChug = PlayerPrefs.GetFloat("chug", 0f);
            PlayerName = PlayerPrefs.GetString("name", "");
            NameAsked = PlayerPrefs.GetInt("name_asked", 0) == 1 || PlayerName.Length > 0;
            MaxGhosts = PlayerPrefs.GetInt("ghosts", 6);
            LastCharacter = (CharacterId)Mathf.Clamp(PlayerPrefs.GetInt("who", 2), 0, 3);
            Apply();
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat("vol_master", MasterVolume);
            PlayerPrefs.SetFloat("vol_music2", MusicVolume);
            PlayerPrefs.SetFloat("vol_ambience", AmbienceVolume);
            PlayerPrefs.SetFloat("vol_effects", EffectsVolume);
            PlayerPrefs.SetFloat("vol_voice", VoiceVolume);
            PlayerPrefs.SetFloat("mouse", MouseSensitivity);
            PlayerPrefs.SetInt("subs", Subtitles ? 1 : 0);
            PlayerPrefs.SetInt("invert", InvertLook ? 1 : 0);
            PlayerPrefs.SetFloat("chug", BestChug);
            PlayerPrefs.SetString("name", PlayerName);
            PlayerPrefs.SetInt("name_asked", NameAsked ? 1 : 0);
            PlayerPrefs.SetInt("ghosts", MaxGhosts);
            PlayerPrefs.SetInt("who", (int)LastCharacter);
            PlayerPrefs.Save();
            Apply();
        }

        public static void Apply()
        {
            var a = AudioDirector.I;
            if (a == null) return;
            a.Master = MasterVolume; a.MusicVol = MusicVolume; a.AmbienceVol = AmbienceVolume; a.SfxVol = EffectsVolume; a.VoiceVol = VoiceVolume;
        }
    }
}
