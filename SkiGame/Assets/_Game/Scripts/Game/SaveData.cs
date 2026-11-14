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

        public static float MasterVolume = 0.85f, MusicVolume = 0.6f, VoiceVolume = 1f, MouseSensitivity = 0.12f;
        public static bool Subtitles = true, InvertLook;
        public static float BestChug; // seconds, 0 = none yet
        public static CharacterId LastCharacter = CharacterId.Erik;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _boards = null; }

        public static void Load()
        {
            MasterVolume = PlayerPrefs.GetFloat("vol_master", 0.85f);
            MusicVolume = PlayerPrefs.GetFloat("vol_music", 0.6f);
            VoiceVolume = PlayerPrefs.GetFloat("vol_voice", 1f);
            MouseSensitivity = PlayerPrefs.GetFloat("mouse", 0.12f);
            Subtitles = PlayerPrefs.GetInt("subs", 1) == 1;
            InvertLook = PlayerPrefs.GetInt("invert", 0) == 1;
            BestChug = PlayerPrefs.GetFloat("chug", 0f);
            LastCharacter = (CharacterId)Mathf.Clamp(PlayerPrefs.GetInt("who", 2), 0, 3);
            Apply();
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat("vol_master", MasterVolume);
            PlayerPrefs.SetFloat("vol_music", MusicVolume);
            PlayerPrefs.SetFloat("vol_voice", VoiceVolume);
            PlayerPrefs.SetFloat("mouse", MouseSensitivity);
            PlayerPrefs.SetInt("subs", Subtitles ? 1 : 0);
            PlayerPrefs.SetInt("invert", InvertLook ? 1 : 0);
            PlayerPrefs.SetFloat("chug", BestChug);
            PlayerPrefs.SetInt("who", (int)LastCharacter);
            PlayerPrefs.Save();
            Apply();
        }

        public static void Apply()
        {
            var a = AudioDirector.I;
            if (a == null) return;
            a.Master = MasterVolume; a.MusicVol = MusicVolume; a.VoiceVol = VoiceVolume;
        }
    }
}
