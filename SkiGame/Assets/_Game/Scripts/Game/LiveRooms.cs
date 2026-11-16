using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ski.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// The other people playing right now. Everyone on the same slope (or in the tavern) shares a
    /// room; each sends where they are every couple of seconds and sees the others played back a
    /// few seconds late, which is smooth and costs very little traffic. Nobody is in charge and
    /// nothing in a run depends on it: when the service is away the game simply plays alone.
    ///
    /// Rooms use Unity's Multiplayer sessions with no game networking, only player data. Nothing
    /// is sent from the Editor (tests, screenshots, the bot) unless <see cref="SendFromEditor"/> is
    /// switched on by hand, so real players never meet a developer's test run.
    /// </summary>
    public static class LiveRooms
    {
        public const int RoomSize = 16;
        const int RoomsPerPlace = 4;
        const float MinGap = 1.1f;      // the room service allows one player update a second

        public sealed class Remote
        {
            public string Id, Name = "Skier";
            public CharacterId Who = CharacterId.Erik;
            public readonly LiveTimeline Timeline = new LiveTimeline();
            public string PunchSeen;
            /// <summary>Their outfit from the shop (<see cref="Outfit.Code"/>).</summary>
            public string Outfit = "";
            public double Arrived;
        }

        /// <summary>Someone punched someone: (attacker, target player id, direction in degrees, attacker's clock).</summary>
        public static event Action<Remote, string, float, double> Punched;

        public static bool SendFromEditor;
        public static string Place { get; private set; }
        public static string RoomId => _session?.Id;
        public static string MyId => _session?.CurrentPlayer?.Id;
        public static bool Connected => _session != null && _session.State == SessionState.Connected;
        public static readonly Dictionary<string, Remote> Others = new Dictionary<string, Remote>();
        /// <summary>The local clock used for everything here.</summary>
        public static double Now => Time.realtimeSinceStartupAsDouble;

        static bool CanSend => !Application.isEditor || SendFromEditor;

        static ISession _session;
        static string _wanted;
        static bool _moving, _saving;
        static string _pendingState, _pendingPunch;
        static int _punchCount;
        static double _lastSave = double.NegativeInfinity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            SendFromEditor = false; Place = null; _session = null; _wanted = null; _moving = _saving = false;
            _pendingState = _pendingPunch = null; _punchCount = 0; _lastSave = double.NegativeInfinity;
            Others.Clear(); Punched = null;
        }

        /// <summary>Joins the room for a place ("green", "tavern"...), or leaves with null. Returns at once.</summary>
        public static void Enter(string place)
        {
            if (!SaveData.Live) place = null;
            _wanted = place;
            if (place == Place && (_session != null || _moving)) return;
            if (!_moving) Move();
        }

        static async void Move()
        {
            _moving = true;
            try
            {
                while (_wanted != Place || (_wanted != null && _session == null))
                {
                    string target = _wanted;
                    if (_session != null)
                    {
                        var old = _session;
                        Detach(old);
                        _session = null;
                        try { await old.LeaveAsync(); } catch (Exception e) { Debug.Log("[Live] leave: " + e.Message); }
                    }
                    Others.Clear();
                    _pendingState = _pendingPunch = null;
                    Place = target;
                    if (target == null) break;
                    await OnlineBoards.Connect();
                    if (OnlineBoards.Status != OnlineBoards.State.Online) { Debug.Log("[Live] offline: playing alone"); break; }
                    if (_wanted != target) continue;
                    _session = await Join(target);
                    if (_session == null) break;
                    if (_wanted != target) continue;
                }
            }
            finally { _moving = false; }
        }

        static async Task<ISession> Join(string place)
        {
            for (int n = 1; n <= RoomsPerPlace; n++)
            {
                string id = "lastrun-" + place + "-" + n;
                var options = new SessionOptions
                {
                    MaxPlayers = RoomSize,
                    PlayerProperties = new Dictionary<string, PlayerProperty>
                    {
                        { "n", new PlayerProperty(SafeName(SaveData.PlayerName), VisibilityPropertyOptions.Member) },
                        { "o", new PlayerProperty(SaveData.Outfit(SaveData.LastCharacter).Code, VisibilityPropertyOptions.Member) },
                    },
                };
                try
                {
                    var s = await MultiplayerService.Instance.CreateOrJoinSessionAsync(id, options);
                    Attach(s);
                    Debug.Log("[Live] in room " + id + " with " + (s.PlayerCount - 1) + " other(s)");
                    Scan(s);
                    return s;
                }
                catch (SessionException e) when (e.Message != null && e.Message.Contains("already a member"))
                {
                    // Still listed from before (a reload, a dropped tab): take the old place back.
                    try
                    {
                        var s = await MultiplayerService.Instance.ReconnectToSessionAsync(id);
                        Attach(s);
                        s.CurrentPlayer.SetProperty("n", new PlayerProperty(SafeName(SaveData.PlayerName), VisibilityPropertyOptions.Member));
                        Debug.Log("[Live] back in room " + id + " with " + (s.PlayerCount - 1) + " other(s)");
                        Scan(s);
                        return s;
                    }
                    catch (Exception re) { Debug.Log("[Live] room " + id + ": could not rejoin: " + re.Message); }
                }
                catch (SessionException e) { Debug.Log("[Live] room " + id + ": " + e.Error + " " + e.Message); }
                catch (Exception e) { Debug.Log("[Live] room " + id + ": " + e.Message); return null; }
                await Task.Delay(1100);   // one join a second
            }
            return null;
        }

        static string SafeName(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) return "Skier";
            return name.Length > 16 ? name.Substring(0, 16) : name;
        }

        static void Attach(ISession s)
        {
            s.PlayerPropertiesChanged += OnChanged;
            s.PlayerJoined += OnJoined;
            s.PlayerHasLeft += OnLeft;
            s.Changed += OnChanged;
            s.RemovedFromSession += OnRemoved;
            s.Deleted += OnRemoved;
        }

        static void Detach(ISession s)
        {
            s.PlayerPropertiesChanged -= OnChanged;
            s.PlayerJoined -= OnJoined;
            s.PlayerHasLeft -= OnLeft;
            s.Changed -= OnChanged;
            s.RemovedFromSession -= OnRemoved;
            s.Deleted -= OnRemoved;
        }

        static void OnChanged() { if (_session != null) Scan(_session); }
        static void OnJoined(string id) { if (_session != null) Scan(_session); }
        static void OnLeft(string id) { Others.Remove(id); }
        static void OnRemoved()
        {
            if (_session != null) Detach(_session);
            _session = null;
            Others.Clear();
            Place = null;
            if (_wanted != null && !_moving) Move();   // try again
        }

        /// <summary>Reads everybody's latest data.</summary>
        static void Scan(ISession s)
        {
            double now = Now;
            string me = s.CurrentPlayer?.Id;
            foreach (var p in s.Players)
            {
                if (p.Id == me) continue;
                if (!Others.TryGetValue(p.Id, out var r)) Others[p.Id] = r = new Remote { Id = p.Id };
                var props = p.Properties;
                if (props == null) continue;
                if (props.TryGetValue("n", out var n) && !string.IsNullOrEmpty(n.Value)) r.Name = SafeName(n.Value);
                if (props.TryGetValue("o", out var o) && o.Value != null && o.Value.Length < 64) r.Outfit = o.Value;
                if (props.TryGetValue("s", out var st))
                {
                    var packet = LivePacket.Decode(st.Value);
                    if (packet != null && r.Timeline.Add(packet, now))
                    {
                        r.Arrived = now;
                        if (packet.Character >= 0 && packet.Character < 4) r.Who = (CharacterId)packet.Character;
                    }
                }
                if (props.TryGetValue("h", out var h) && h.Value != r.PunchSeen)
                {
                    r.PunchSeen = h.Value;
                    // "target|count|yaw|time": only a punch from the last few seconds counts, not one left standing from before.
                    var parts = h.Value.Split('|');
                    var inv = System.Globalization.CultureInfo.InvariantCulture;
                    if (parts.Length == 4 && float.TryParse(parts[2], System.Globalization.NumberStyles.Float, inv, out float yaw)
                        && double.TryParse(parts[3], System.Globalization.NumberStyles.Float, inv, out double at)
                        && r.Timeline.SenderClock(now) - at < 8.0)
                        Punched?.Invoke(r, parts[0], yaw, at);
                }
            }
            // Anyone no longer listed has gone.
            List<string> gone = null;
            foreach (var id in Others.Keys)
            {
                bool there = false;
                foreach (var p in s.Players) if (p.Id == id) { there = true; break; }
                if (!there) (gone ??= new List<string>()).Add(id);
            }
            if (gone != null) foreach (var id in gone) Others.Remove(id);
        }

        /// <summary>Hands over this player's latest stretch of movement (an encoded <see cref="LivePacket"/>).</summary>
        public static void Send(string packet)
        {
            if (packet == null || !CanSend || !Connected) return;
            _pendingState = packet;
            Flush();
        }

        /// <summary>This player punched another one.</summary>
        public static void SendPunch(string targetId, float yaw)
        {
            if (!CanSend || !Connected || string.IsNullOrEmpty(targetId)) return;
            _punchCount++;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            _pendingPunch = targetId + "|" + _punchCount + "|" + yaw.ToString("0", inv) + "|" + Now.ToString("0.00", inv);
            Flush();
        }

        /// <summary>Call every frame: sends whatever is waiting once the service allows it.</summary>
        public static void Tick()
        {
            if (_pendingState != null || _pendingPunch != null) Flush();
        }

        static async void Flush()
        {
            if (_saving || _session == null || Now - _lastSave < MinGap) return;
            var s = _session;
            _saving = true;
            _lastSave = Now;
            try
            {
                if (!string.IsNullOrEmpty(_pendingState)) s.CurrentPlayer.SetProperty("s", new PlayerProperty(_pendingState, VisibilityPropertyOptions.Member));
                if (_pendingPunch != null) s.CurrentPlayer.SetProperty("h", new PlayerProperty(_pendingPunch, VisibilityPropertyOptions.Member));
                _pendingState = _pendingPunch = null;
                await s.SaveCurrentPlayerDataAsync();
            }
            catch (Exception e) { Debug.Log("[Live] update: " + e.Message); _lastSave = Now + 3.0; }
            finally { _saving = false; }
        }

        /// <summary>Call after the player changes their outfit (or character).</summary>
        public static void OutfitChanged()
        {
            if (!Connected) return;
            try { _session.CurrentPlayer.SetProperty("o", new PlayerProperty(SaveData.Outfit(SaveData.LastCharacter).Code, VisibilityPropertyOptions.Member)); _pendingState ??= ""; Flush(); }
            catch (Exception) { }
        }

        /// <summary>Call after the player changes their name.</summary>
        public static void NameChanged()
        {
            if (!Connected) return;
            try { _session.CurrentPlayer.SetProperty("n", new PlayerProperty(SafeName(SaveData.PlayerName), VisibilityPropertyOptions.Member)); }
            catch (Exception) { }
        }
    }
}
