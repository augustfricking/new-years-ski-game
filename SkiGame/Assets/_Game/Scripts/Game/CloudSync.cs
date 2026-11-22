using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// Keeps the save (progress, outfits, unlocks, best runs, settings) in the cloud as well as in
    /// the browser, with Unity Cloud Save (free tier). Signed in anonymously it is a backup for this
    /// browser; with an account (a username and password, also free) the same progress follows the
    /// player to any device. The newest save wins. Uploads are spaced out (at most one every 30 s),
    /// so a busy evening of skiing costs a few hundred writes, far inside the free allowance.
    /// When the service is away nothing changes: the browser's copy is the game's.
    /// </summary>
    public static class CloudSync
    {
        const string Key = "save";
        const float MinGap = 30f;
        /// <summary>What travels: everything except what belongs to this device (graphics, sign-in).</summary>
        static readonly string[] Floats = { "vol_master", "vol_music2", "vol_ambience", "vol_effects", "vol_voice", "mouse", "chug", "vol_theme", "vol_chat" };
        static readonly string[] Ints = { "subs", "invert", "name_asked", "others", "live", "walkman", "voice", "walkman_on", "walkman_repeat", "who", "walkman_shuffle" };
        static readonly string[] Strings =
        {
            "name", "walkman_song", "progress_0", "progress_1", "progress_2", "progress_3", "outfit_0", "outfit_1", "outfit_2", "outfit_3",
            "ghost_0", "ghost_1", "ghost_2", "ghost_3", "ghost_4", "board_0", "board_1", "board_2", "board_3", "board_4", "board_chug", "seen_unlocks",
        };

        public enum State { Local, Syncing, Saved, Failed }
        public static State Status { get; private set; } = State.Local;
        /// <summary>The account's username, or null while signed in only as this device.</summary>
        public static string Username { get; private set; }
        public static string LastError { get; private set; }

        /// <summary>Stay signed in to the account on this browser (otherwise it is forgotten when the game is next opened).</summary>
        public static bool Remember
        {
            get => PlayerPrefs.GetInt("remember", 1) == 1;
            set { PlayerPrefs.SetInt("remember", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>What an account is for, in a sentence (the welcome screen's info button).</summary>
        public const string Why = "Your progress, unlocks and outfits are saved online and follow you to any device.";

        static bool _dirty, _pulled, _busy;
        static float _lastPush = -999f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Status = State.Local; Username = null; LastError = null; _dirty = _pulled = _busy = false; _lastPush = -999f; }

        /// <summary>Something worth keeping changed: it goes up with the next upload.</summary>
        public static void MarkDirty()
        {
            _dirty = true;
            PlayerPrefs.SetString("save_time", Now().ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        static long LocalTime() => long.TryParse(PlayerPrefs.GetString("save_time", "0"), out long t) ? t : 0;

        /// <summary>Call every frame: fetches the cloud save once signed in, then uploads changes now and then.</summary>
        public static void Tick()
        {
            if (_busy || OnlineBoards.Status != OnlineBoards.State.Online || !AuthenticationService.Instance.IsSignedIn) return;
            if (!_pulled) { Pull(); return; }
            if (_dirty && Time.unscaledTime - _lastPush > MinGap) Push();
        }

        static async void Pull()
        {
            _busy = true; Status = State.Syncing;
            try
            {
                try { await AuthenticationService.Instance.GetPlayerInfoAsync(); } catch (Exception) { }
                Username = AuthenticationService.Instance.PlayerInfo?.Username;
                if (string.IsNullOrEmpty(Username)) Username = null;
                if (Username != null) OnlineBoards.NameChanged();          // the boards show the account (not a guest)
                var got = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { Key }, new Unity.Services.CloudSave.Models.Data.Player.LoadOptions());
                _pulled = true;
                if (got.TryGetValue(Key, out var item))
                {
                    var save = JsonUtility.FromJson<Payload>(item.Value.GetAs<string>());
                    if (save != null && save.t > LocalTime()) { Apply(save); Debug.Log("[Cloud] loaded the save from " + DateTimeOffset.FromUnixTimeSeconds(save.t)); }
                    else if (LocalTime() > (save?.t ?? 0)) _dirty = true;            // this browser's is newer: send it up
                }
                else _dirty = true;                                                  // nothing up there yet
                Status = State.Saved;
            }
            catch (Exception e) { Status = State.Failed; LastError = e.Message; _pulled = true; Debug.Log("[Cloud] could not load: " + e.Message); }
            finally { _busy = false; }
        }

        static async void Push()
        {
            _busy = true; _dirty = false; _lastPush = Time.unscaledTime; Status = State.Syncing;
            try
            {
                var save = Collect();
                await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { { Key, JsonUtility.ToJson(save) } },
                    new Unity.Services.CloudSave.Models.Data.Player.SaveOptions());
                Status = State.Saved;
            }
            catch (Exception e) { Status = State.Failed; LastError = e.Message; _dirty = true; Debug.Log("[Cloud] could not save: " + e.Message); }
            finally { _busy = false; }
        }

        [Serializable]
        sealed class Payload
        {
            public long t;
            public List<string> k = new List<string>();
            public List<string> v = new List<string>();
        }

        static Payload Collect()
        {
            var p = new Payload { t = LocalTime() };
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var k in Floats) if (PlayerPrefs.HasKey(k)) { p.k.Add("f:" + k); p.v.Add(PlayerPrefs.GetFloat(k).ToString("R", inv)); }
            foreach (var k in Ints) if (PlayerPrefs.HasKey(k)) { p.k.Add("i:" + k); p.v.Add(PlayerPrefs.GetInt(k).ToString(inv)); }
            foreach (var k in Strings) if (PlayerPrefs.HasKey(k)) { p.k.Add("s:" + k); p.v.Add(PlayerPrefs.GetString(k)); }
            return p;
        }

        static void Apply(Payload p)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            for (int i = 0; i < p.k.Count && i < p.v.Count; i++)
            {
                string k = p.k[i], v = p.v[i];
                if (k.Length < 3) continue;
                string name = k.Substring(2);
                switch (k[0])
                {
                    case 'f': if (float.TryParse(v, System.Globalization.NumberStyles.Float, inv, out float f)) PlayerPrefs.SetFloat(name, f); break;
                    case 'i': if (int.TryParse(v, System.Globalization.NumberStyles.Integer, inv, out int n)) PlayerPrefs.SetInt(name, n); break;
                    case 's': PlayerPrefs.SetString(name, v); break;
                }
            }
            PlayerPrefs.SetString("save_time", p.t.ToString(inv));
            PlayerPrefs.Save();
            SaveData.Reload();
        }

        // ---- the account -------------------------------------------------------------------------

        /// <summary>
        /// Turns this device's profile into an account (so it can be signed in to elsewhere), or
        /// signs in to an existing one (its save then replaces this browser's). Returns null when it
        /// worked, else what went wrong, in a few words.
        /// </summary>
        public static string Rules => Ski.Core.AccountRules.Rules;
        static string CheckName(string u) => Ski.Core.AccountRules.CheckName(u);
        static string CheckPassword(string p) => Ski.Core.AccountRules.CheckPassword(p);

        public static async Task<string> Account(string username, string password, bool create)
        {
            if (create)
            {
                string bad = CheckName(username) ?? CheckPassword(password);
                if (bad != null) return bad;
            }
            else if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)) return "Name and password";
            await OnlineBoards.Connect();
            if (OnlineBoards.Status != OnlineBoards.State.Online) return "Offline";
            try
            {
                var auth = AuthenticationService.Instance;
                if (create)
                {
                    // This browser's own player becomes the account (its progress comes along). If that player already
                    // has an account, the new one is a new player of its own.
                    if (auth.IsSignedIn) { try { await auth.GetPlayerInfoAsync(); } catch (Exception) { } }
                    bool linked = auth.IsSignedIn && !string.IsNullOrEmpty(auth.PlayerInfo?.Username);
                    if (!linked)
                    {
                        try { await auth.AddUsernamePasswordAsync(username, password); }
                        catch (AuthenticationException e) when (e.ErrorCode == AuthenticationErrorCodes.AccountLinkLimitExceeded) { linked = true; }
                    }
                    if (linked)
                    {
                        if (auth.IsSignedIn) auth.SignOut(true);
                        await auth.SignUpWithUsernamePasswordAsync(username, password);
                        OnlineBoards.SignedInAgain();
                    }
                }
                else
                {
                    if (auth.IsSignedIn) auth.SignOut(true);
                    await auth.SignInWithUsernamePasswordAsync(username, password);
                    PlayerPrefs.SetString("save_time", "0");                       // the account's save wins
                    OnlineBoards.SignedInAgain();
                }
                PlayerPrefs.SetInt("forget_account", Remember ? 0 : 1);           // not remembered: forgotten at the next start
                PlayerPrefs.Save();
                Username = username;
                OnlineBoards.NameChanged();
                _pulled = false;                                                    // fetch (or send) the save for this account
                return null;
            }
            catch (AuthenticationException e) { Debug.Log("[Cloud] account: " + e.ErrorCode + " " + e.Message); return Short(e.Message, create); }
            catch (RequestFailedException e) { Debug.Log("[Cloud] account: " + e.ErrorCode + " " + e.Message); return Short(e.Message, create); }
            catch (Exception e) { Debug.Log("[Cloud] account: " + e.Message); return Short(e.Message, create); }
        }

        /// <summary>A new password for the signed-in account (null when it worked). Username accounts have no
        /// e-mail, so a forgotten password cannot be reset: this is the way to keep it known.</summary>
        public static async Task<string> ChangePassword(string current, string next)
        {
            if (string.IsNullOrEmpty(current)) return "Current password";
            string bad = CheckPassword(next);
            if (bad != null) return bad;
            try
            {
                await AuthenticationService.Instance.UpdatePasswordAsync(current, next);
                return null;
            }
            catch (Exception e)
            {
                Debug.Log("[Cloud] password: " + e.Message);
                string m = e.Message.ToLowerInvariant();
                return m.Contains("network") || m.Contains("timeout") ? "Offline" : m.Contains("requirement") ? "Password too weak" : "Wrong password";
            }
        }

        /// <summary>Leaves the account: this browser goes back to being a device of its own.</summary>
        public static async Task SignOut()
        {
            try
            {
                var auth = AuthenticationService.Instance;
                auth.SignOut(true);
                PlayerPrefs.SetInt("forget_account", 0);
                await auth.SignInAnonymouslyAsync();
                PlayerPrefs.Save();
                OnlineBoards.SignedInAgain();
            }
            catch (Exception e) { Debug.Log("[Cloud] sign out: " + e.Message); }
            Username = null;
            _pulled = false;
        }

        static string Short(string message, bool create)
        {
            string m = (message ?? "").ToLowerInvariant();
            if (m.Contains("permission_denied") || m.Contains("provider is not available")) return "Accounts are off";
            if (m.Contains("network") || m.Contains("timeout") || m.Contains("cannot connect") || m.Contains("could not resolve")) return "Offline";
            if (!create) return "Wrong name or password";
            // The service words its refusals like "Password does not match requirements ..." and "Username does not ...".
            if (m.Contains("password") && m.Contains("requirement")) return "Password too weak";
            if (m.Contains("username") && m.Contains("requirement")) return "Name: letters, digits";
            if (m.Contains("already") || m.Contains("exist") || m.Contains("conflict") || m.Contains("taken")) return "Name taken";
            return "Try again";
        }
    }
}
