using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Ski.Game
{
    /// <summary>
    /// The DJ's songs (made with YuE2, see tools/music/make_songs_yue2.py). They live in StreamingAssets/Music
    /// and are fetched only when played, so they cost nothing at start-up. A browser decodes a
    /// whole song into memory, so only the two most recent are kept.
    /// </summary>
    public static class Songs
    {
        [Serializable] public sealed class Song { public string id, title, style; }
        [Serializable] sealed class SongList { public Song[] songs; }

        public static readonly List<Song> All = new List<Song>();
        public static bool Loaded { get; private set; }
        static bool _loading;
        static readonly List<(string id, AudioClip clip)> Cache = new List<(string, AudioClip)>();
        static readonly Dictionary<string, List<Action<AudioClip>>> Waiting = new Dictionary<string, List<Action<AudioClip>>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { All.Clear(); Loaded = false; _loading = false; Cache.Clear(); Waiting.Clear(); }

        public static string Url(string file)
        {
            string path = Application.streamingAssetsPath + "/Music/" + file;
            return path.Contains("://") ? path : "file://" + path;
        }

        public static Song Get(string id) => All.Find(s => s.id == id);
        public static int IndexOf(string id) => All.FindIndex(s => s.id == id);

        /// <summary>Reads the list of songs once (call early; harmless to call again).</summary>
        public static void LoadList(MonoBehaviour host, Action done = null)
        {
            if (Loaded) { done?.Invoke(); return; }
            if (_loading) return;
            _loading = true;
            host.StartCoroutine(ReadList(done));
        }

        static IEnumerator ReadList(Action done)
        {
            using (var req = UnityWebRequest.Get(Url("songs.json")))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var list = JsonUtility.FromJson<SongList>(req.downloadHandler.text);
                        if (list?.songs != null) All.AddRange(list.songs);
                    }
                    catch (Exception e) { Debug.Log("[Songs] list: " + e.Message); }
                }
                else Debug.Log("[Songs] no song list: " + req.error);
            }
            Loaded = true;
            _loading = false;
            done?.Invoke();
        }

        /// <summary>Hands over a song's clip, fetching it if needed (null when it cannot be had).</summary>
        public static void Clip(MonoBehaviour host, string id, Action<AudioClip> done)
        {
            foreach (var c in Cache) if (c.id == id && c.clip != null) { done(c.clip); return; }
            if (Waiting.TryGetValue(id, out var list)) { list.Add(done); return; }
            Waiting[id] = new List<Action<AudioClip>> { done };
            host.StartCoroutine(Fetch(id));
        }

        static IEnumerator Fetch(string id)
        {
            AudioClip clip = null;
            using (var req = UnityWebRequestMultimedia.GetAudioClip(Url(id + ".mp3"), AudioType.MPEG))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    clip = DownloadHandlerAudioClip.GetContent(req);
                    if (clip != null) clip.name = id;
                }
                else Debug.Log("[Songs] " + id + ": " + req.error);
            }
            if (clip != null)
            {
                Cache.Add((id, clip));
                while (Cache.Count > 2) { var old = Cache[0]; Cache.RemoveAt(0); if (old.clip != null) UnityEngine.Object.Destroy(old.clip); }
            }
            var waiting = Waiting[id];
            Waiting.Remove(id);
            foreach (var w in waiting) w(clip);
        }
    }

    /// <summary>
    /// The walkman from the tavern table: once picked up, your own choice of the DJ's songs plays
    /// wherever you are (the DJ still has the floor in the tavern). M skips to the next song,
    /// N switches it on and off; both are in the settings too.
    /// </summary>
    public static class Walkman
    {
        public static bool Active => SaveData.HasWalkman && SaveData.WalkmanOn && Songs.All.Count > 0;
        public static string Current => Songs.All.Count == 0 ? null : Songs.All[Mathf.Clamp(Songs.IndexOf(SaveData.WalkmanSong), 0, Songs.All.Count - 1)].id;

        public static void Next(int step = 1)
        {
            if (Songs.All.Count == 0) return;
            int i = Songs.IndexOf(SaveData.WalkmanSong);
            i = ((i < 0 ? 0 : i + step) % Songs.All.Count + Songs.All.Count) % Songs.All.Count;
            SaveData.WalkmanSong = Songs.All[i].id;
            SaveData.WalkmanOn = true;
            SaveData.Save();
            AudioDirector.I?.RefreshMusic(true);
            Announce();
        }

        public static void Toggle()
        {
            SaveData.WalkmanOn = !SaveData.WalkmanOn;
            SaveData.Save();
            AudioDirector.I?.RefreshMusic(true);
            if (SaveData.WalkmanOn) Announce(); else Ui.Instance?.Notice("WALKMAN OFF", null, 1.6f);
        }

        public static void Announce()
        {
            var s = Songs.Get(Current);
            if (s != null) Ui.Instance?.Notice("NOW PLAYING", s.title + "  -  " + s.style, 2.6f);
        }
    }
}
