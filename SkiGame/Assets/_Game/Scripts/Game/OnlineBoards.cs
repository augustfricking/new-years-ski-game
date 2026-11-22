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
            public bool Guest;        // no account (a browser of its own)
        }

        [Serializable]
        sealed class Meta { public string g; public int c = -1; }

        public const string ChugBoard = "chug";
        /// <summary>
        /// The world boards are for people playing the game. Nothing is ever sent from the Unity
        /// Editor (development, tests, screenshots) unless this is switched on by hand.
        /// </summary>
        public static bool SendFromEditor;
        /// <summary>A slope's world board. A slope whose course changed gets a fresh board (_v2...), since old times are not comparable.</summary>
        public static string BoardId(SlopeId slope) => "slope_" + slope.ToString().ToLowerInvariant() + (slope == SlopeId.Red ? "_v2" : "");

        static readonly Dictionary<string, List<Entry>> Cache = new Dictionary<string, List<Entry>>();
        static readonly HashSet<string> Missing = new HashSet<string>();
        static Task _connect;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { SendFromEditor = false; Status = State.Off; LastError = null; PlayerId = null; Cache.Clear(); Missing.Clear(); _connect = null; }

        /// <summary>The last list fetched for a board, or null.</summary>
        public static List<Entry> Cached(string board) => Cache.TryGetValue(board, out var l) ? l : null;

        public static Task Connect() => _connect ??= ConnectAsync();

        static async Task ConnectAsync()
        {
            Status = State.Connecting;
            try
            {
                // "?profile=name" in the page address signs in as a separate player (two tabs, two people).
                var options = new InitializationOptions();
                string profile = ProfileFromUrl();
                if (profile != null) options.SetProfile(profile);
                await UnityServices.InitializeAsync(options);
                // An account signed in without "remember me" is forgotten now: this browser starts as a guest again.
                if (PlayerPrefs.GetInt("forget_account", 0) == 1 && !AuthenticationService.Instance.IsSignedIn)
                {
                    if (AuthenticationService.Instance.SessionTokenExists) AuthenticationService.Instance.ClearSessionToken();
                    PlayerPrefs.SetInt("forget_account", 0);
                }
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
                PlayerId = AuthenticationService.Instance.PlayerId;
                // In the browser the sign-in is only kept if it is written out now: without this a
                // reload can come back as a new player (and a second row on every board).
                PlayerPrefs.Save();
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

        static string ProfileFromUrl() => PageArgs.Get("profile");

        static string CloudName(string name)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char ch in (name ?? "").Trim())
                if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_') sb.Append(ch); else if (ch == ' ') sb.Append('_');
            return sb.Length > 24 ? sb.ToString(0, 24) : sb.ToString();
        }

        /// <summary>Board names end in one of these (shown without it): an account, or a guest (a browser of its own).
        /// Names from before the marks carry neither, and are shown as they are, without a tag.</summary>
        const string AccountMark = "__a", GuestMark = "__g";

        static string Bare(string cloud)
        {
            if (string.IsNullOrEmpty(cloud)) return "";
            int hash = cloud.LastIndexOf('#');
            return hash > 0 ? cloud.Substring(0, hash) : cloud;
        }

        static bool IsGuest(string cloud) => Bare(cloud).EndsWith(GuestMark);

        static string DisplayName(string cloud)
        {
            string bare = Bare(cloud);
            if (bare.EndsWith(AccountMark) || bare.EndsWith(GuestMark)) bare = bare.Substring(0, bare.Length - AccountMark.Length);
            return bare.Length == 0 ? "Skier" : bare.Replace('_', ' ');
        }

        static async Task PushName()
        {
            string name = CloudName(SaveData.PlayerName);
            if (Status != State.Online || name.Length < 2) return;
            // The board says whether this is an account or a guest (guests have browsers of their own, and may share a name).
            bool account = !string.IsNullOrEmpty(AuthenticationService.Instance.PlayerInfo?.Username);
            name = (name.Length > 24 - AccountMark.Length ? name.Substring(0, 24 - AccountMark.Length) : name) + (account ? AccountMark : GuestMark);
            try { await AuthenticationService.Instance.UpdatePlayerNameAsync(name); }
            catch (Exception e) { Debug.Log("[Online] name not updated: " + e.Message); }
        }

        /// <summary>Signed in as someone else (an account, or back to this device): new id, fresh boards.</summary>
        public static async void SignedInAgain()
        {
            PlayerId = AuthenticationService.Instance.PlayerId;
            Cache.Clear(); Missing.Clear();
            Debug.Log("[Online] now signed in as " + PlayerId);
            await PushName();
        }

        /// <summary>Call after the player changes their name.</summary>
        public static async void NameChanged()
        {
            await Connect();
            await PushName();
        }

        /// <summary>What the world board says about one result.</summary>
        public sealed class WorldResult
        {
            public List<Entry> Rows;          // the top of the board and the rows around this player
            public int RunRank = -1;          // 1-based: where this result stands among everyone else's best
            public int BestRank = -1;         // 1-based: this player's own entry (their best), -1 when none
            public bool NewBest, First;       // beat this player's earlier best here / their first result here
        }

        /// <summary>
        /// A finished run (or stein) against the world: this player's earlier best is read, the result sent (when
        /// <paramref name="send"/>: the board keeps only each player's best, with the character it was made with),
        /// the board fetched, and the exact place this very result would take among everyone else. Null when offline.
        /// </summary>
        public static async void Report(string board, double score, string ghost, int character, bool higherBetter, bool send, Action<WorldResult> done)
        {
            await Connect();
            if (Status != State.Online || Missing.Contains(board)) { done?.Invoke(null); return; }
            double? before = null;
            try { before = (await LeaderboardsService.Instance.GetPlayerScoreAsync(board)).Score; }
            catch (Exception) { /* nothing here yet */ }
            // Nothing from the Editor, nor from a private test room ("?room=" in the address).
            bool sent = send && (!Application.isEditor || SendFromEditor) && PageArgs.TestRoom == null;
            if (send && !sent) Debug.Log("[Online] not sent (Editor or test room): " + board + " " + score.ToString("0.##"));
            if (sent)
            {
                try
                {
                    var options = new AddPlayerScoreOptions { Metadata = new Dictionary<string, object> { { "g", ghost ?? "" }, { "c", character } } };
                    await LeaderboardsService.Instance.AddPlayerScoreAsync(board, score, options);
                    Debug.Log("[Online] sent " + score.ToString("0.##") + " to " + board);
                }
                catch (Exception e) { Fail(board, e); done?.Invoke(null); return; }
            }
            var rows = await Rows(board);
            if (rows == null) { done?.Invoke(null); return; }
            var res = new WorldResult { Rows = rows, First = before == null };
            res.NewBest = sent && (before == null || (higherBetter ? score > before.Value : score < before.Value));
            foreach (var e in rows) if (e.Me) res.BestRank = e.Rank + 1;
            res.RunRank = await RankAmongOthers(board, score, higherBetter);
            done?.Invoke(res);
        }

        /// <summary>1 + how many other players' best beat this score (ties do not), -1 when it cannot be told.</summary>
        static async Task<int> RankAmongOthers(string board, double score, bool higherBetter)
        {
            try
            {
                int better = 0, offset = 0;
                const int Page = 500;
                while (true)
                {
                    var page = await LeaderboardsService.Instance.GetScoresAsync(board, new GetScoresOptions { Offset = offset, Limit = Page, IncludeMetadata = false });
                    foreach (var e in page.Results)
                    {
                        if (e.PlayerId == PlayerId || !Plausible(board, e.Score)) continue;
                        if (higherBetter ? e.Score > score : e.Score < score) better++;
                        else return better + 1;                     // best first: nobody after this one is better
                    }
                    offset += page.Results.Count;
                    if (page.Results.Count < Page || offset >= page.Total) return better + 1;
                }
            }
            catch (Exception) { return -1; }
        }

        /// <summary>The top of a board plus the rows around this device. The callback gets null when offline.</summary>
        public static async void Fetch(string board, Action<List<Entry>> done)
        {
            await Connect();
            if (Status != State.Online || Missing.Contains(board)) { done?.Invoke(null); return; }
            done?.Invoke(await Rows(board));
        }

        static async Task<List<Entry>> Rows(string board)
        {
            var list = new List<Entry>();
            try
            {
                var top = await LeaderboardsService.Instance.GetScoresAsync(board, new GetScoresOptions { Limit = 12, IncludeMetadata = true });
                foreach (var e in top.Results) Add(list, e);
            }
            catch (Exception e) { Fail(board, e); return null; }
            try
            {
                var near = await LeaderboardsService.Instance.GetPlayerRangeAsync(board, new GetPlayerRangeOptions { RangeLimit = 2, IncludeMetadata = true });
                foreach (var e in near.Results) Add(list, e);
            }
            catch (Exception) { /* no score on this board yet: nothing around us to show */ }
            list.Sort((a, b) => a.Rank.CompareTo(b.Rank));
            // Rows no run could have produced are not shown, and the places close up behind them.
            int dropped = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (!Plausible(board, list[i].Score)) { list.RemoveAt(i--); dropped++; continue; }
                var e = list[i]; e.Rank -= dropped; list[i] = e;
            }
            Cache[board] = list;
            return list;
        }

        static void Add(List<Entry> list, Unity.Services.Leaderboards.Models.LeaderboardEntry e)
        {
            foreach (var have in list) if (have.Rank == e.Rank) return;
            var entry = new Entry { Name = DisplayName(e.PlayerName), Score = e.Score, Rank = e.Rank, Me = e.PlayerId == PlayerId, Character = -1, Guest = IsGuest(e.PlayerName) };
            if (!string.IsNullOrEmpty(e.Metadata))
            {
                try { var m = JsonUtility.FromJson<Meta>(e.Metadata); if (m != null) { entry.Ghost = m.g; entry.Character = m.c; } }
                catch (Exception) { /* a row without a recording is still a row */ }
            }
            list.Add(entry);
        }

        static bool Plausible(string board, double score)
        {
            if (board == ChugBoard) return score > 4.0 && score < 600.0;
            for (int i = 0; i < 5; i++) if (BoardId((SlopeId)i) == board) return Courses.Plausible((SlopeId)i, (float)score);
            return true;
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
