using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ski.Core;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// The world boards, on Unity Gaming Services (free tier). The player is signed in anonymously:
    /// the service remembers this browser or device, so there is exactly one profile, and one row
    /// per board, for each device. No account, no password. Each score carries a small recording of
    /// the run, which other players see as a ghost. If the service cannot be reached, or the boards
    /// have not been created in the cloud project, everything quietly stays local.
    /// </summary>
    public static class OnlineBoards
    {
        public enum State { Off, Connecting, Online, Failed }
        public static State Status { get; private set; }
        public static string LastError { get; private set; }
        public static string PlayerId { get; private set; }

        public struct Entry
        {
            public string Name;
            public double Score;
            public int Rank;          // 0-based
            public bool Me;
            public string Ghost;      // encoded GhostTrack, may be empty
            public int Character;     // CharacterId, or -1
        }

        [Serializable]
        sealed class Meta { public string g; public int c = -1; }

        public const string ChugBoard = "chug";
        public static string BoardId(SlopeId slope) => "slope_" + slope.ToString().ToLowerInvariant();

        static readonly Dictionary<string, List<Entry>> Cache = new Dictionary<string, List<Entry>>();
        static readonly HashSet<string> Missing = new HashSet<string>();
        static Task _connect;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Status = State.Off; LastError = null; PlayerId = null; Cache.Clear(); Missing.Clear(); _connect = null; }

        /// <summary>The last list fetched for a board, or null.</summary>
        public static List<Entry> Cached(string board) => Cache.TryGetValue(board, out var l) ? l : null;

        public static Task Connect() => _connect ??= ConnectAsync();

        static async Task ConnectAsync()
        {
            Status = State.Connecting;
            try
            {
                await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
                PlayerId = AuthenticationService.Instance.PlayerId;
                Status = State.Online;
                Debug.Log("[Online] signed in as device profile " + PlayerId);
                await PushName();
            }
            catch (Exception e)
            {
                Status = State.Failed;
                LastError = e.Message;
                Debug.Log("[Online] not connected, boards stay local: " + e.Message);
            }
        }

        static string CloudName(string name)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch in (name ?? "").Trim())
                if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_') sb.Append(ch); else if (ch == ' ') sb.Append('_');
            return sb.Length > 24 ? sb.ToString(0, 24) : sb.ToString();
        }

        static string DisplayName(string cloud)
        {
            if (string.IsNullOrEmpty(cloud)) return "Skier";
            int hash = cloud.LastIndexOf('#');
            if (hash > 0) cloud = cloud.Substring(0, hash);
            return cloud.Replace('_', ' ');
        }

        static async Task PushName()
        {
            string name = CloudName(SaveData.PlayerName);
            if (Status != State.Online || name.Length < 2) return;
            try { await AuthenticationService.Instance.UpdatePlayerNameAsync(name); }
            catch (Exception e) { Debug.Log("[Online] name not updated: " + e.Message); }
        }

        /// <summary>Call after the player changes their name.</summary>
        public static async void NameChanged()
        {
            await Connect();
            await PushName();
        }

        /// <summary>Sends a result (the service keeps the best per device), then refreshes the list.</summary>
        public static async void Submit(string board, double score, string ghost, int character, Action<List<Entry>> done = null)
        {
            await Connect();
            if (Status != State.Online || Missing.Contains(board)) { done?.Invoke(null); return; }
            try
            {
                var options = new AddPlayerScoreOptions { Metadata = new Dictionary<string, object> { { "g", ghost ?? "" }, { "c", character } } };
                await LeaderboardsService.Instance.AddPlayerScoreAsync(board, score, options);
                Debug.Log("[Online] sent " + score.ToString("0.##") + " to " + board);
            }
            catch (Exception e) { Fail(board, e); done?.Invoke(null); return; }
            Fetch(board, done);
        }

        /// <summary>The top of a board plus the rows around this device. The callback gets null when offline.</summary>
        public static async void Fetch(string board, Action<List<Entry>> done)
        {
            await Connect();
            if (Status != State.Online || Missing.Contains(board)) { done?.Invoke(null); return; }
            var list = new List<Entry>();
            try
            {
                var top = await LeaderboardsService.Instance.GetScoresAsync(board, new GetScoresOptions { Limit = 12, IncludeMetadata = true });
                foreach (var e in top.Results) Add(list, e);
            }
            catch (Exception e) { Fail(board, e); done?.Invoke(null); return; }
            try
            {
                var near = await LeaderboardsService.Instance.GetPlayerRangeAsync(board, new GetPlayerRangeOptions { RangeLimit = 2, IncludeMetadata = true });
                foreach (var e in near.Results) Add(list, e);
            }
            catch (Exception) { /* no score on this board yet: nothing around us to show */ }
            list.Sort((a, b) => a.Rank.CompareTo(b.Rank));
            Cache[board] = list;
            done?.Invoke(list);
        }

        static void Add(List<Entry> list, Unity.Services.Leaderboards.Models.LeaderboardEntry e)
        {
            foreach (var have in list) if (have.Rank == e.Rank) return;
            var entry = new Entry { Name = DisplayName(e.PlayerName), Score = e.Score, Rank = e.Rank, Me = e.PlayerId == PlayerId, Character = -1 };
            if (!string.IsNullOrEmpty(e.Metadata))
            {
                try { var m = JsonUtility.FromJson<Meta>(e.Metadata); if (m != null) { entry.Ghost = m.g; entry.Character = m.c; } }
                catch (Exception) { /* a row without a recording is still a row */ }
            }
            list.Add(entry);
        }

        static void Fail(string board, Exception e)
        {
            LastError = e.Message;
            // A board that does not exist in the cloud project is not asked for again this session.
            if (e.Message != null && (e.Message.Contains("not found") || e.Message.Contains("could not be found") || e.Message.Contains("NotFound") || e.Message.Contains("404"))) Missing.Add(board);
            Debug.Log("[Online] " + board + ": " + e.Message);
        }
    }
}
