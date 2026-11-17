using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ski.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// The other people playing right now, all in one shared world: the five slopes, the village
    /// they all end in, the tavern and the lift. Everyone is in one room and says where they are;
    /// each sends where they are every couple of seconds and sees the others played back a few
    /// seconds late, which is smooth and costs very little traffic. Nobody is in charge and nothing
    /// in a run depends on it: when the service is away the game simply plays alone.
    ///
    /// The world holds <see cref="WorldSize"/> players (what a browser can draw and a slope can
    /// hold without becoming a crowd). When it is full, newcomers wait in line in a second room
    /// and are let in, first come first served, as places free up; they play alone meanwhile.
    ///
    /// Rooms use Unity's Multiplayer sessions (free tier) with no game networking, only player data.
    /// Nothing is sent from the Editor (tests, screenshots, the bot) unless <see cref="SendFromEditor"/>
    /// is switched on by hand, so real players never meet a developer's test run.
    /// </summary>
    public static class LiveRooms
    {
        public const int WorldSize = 24;
        /// <summary>The world real players share. The Editor never takes a place in it: it has its own
        /// rooms (and the private "test" one the live test tools use).</summary>
        static string WorldId => !Application.isEditor ? "lastrun-world-1" : Place == "test" ? "lastrun-test-1" : "lastrun-editor-1";
        static string QueueId => !Application.isEditor ? "lastrun-queue-1" : "lastrun-editor-queue-1";
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
        /// <summary>Where this player is ("green" .. "offpiste", "tavern", "lift"); the room is the same everywhere.</summary>
        public static string Place { get; private set; }
        public static string RoomId => _session?.Id;
        public static string MyId => _session?.CurrentPlayer?.Id;
        public static bool Connected => _session != null && _session.State == SessionState.Connected;
        /// <summary>Waiting in line for a place in the world (it is full).</summary>
        public static bool Queued => _queue != null && _session == null;
        /// <summary>1 = next in. 0 when not waiting.</summary>
        public static int QueuePosition { get; private set; }
        public static readonly Dictionary<string, Remote> Others = new Dictionary<string, Remote>();
        /// <summary>The local clock used for everything here.</summary>
        public static double Now => Time.realtimeSinceStartupAsDouble;

        static bool CanSend => !Application.isEditor || SendFromEditor;

        static ISession _session, _queue;
        static bool _wanted, _moving, _saving;
        static string _pendingState, _pendingPunch;
        static int _punchCount;
        static double _lastSave = double.NegativeInfinity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            SendFromEditor = false; Place = null; _session = _queue = null; _wanted = _moving = _saving = false;
            _pendingState = _pendingPunch = null; _punchCount = 0; _lastSave = double.NegativeInfinity;
            QueuePosition = 0;
            Others.Clear(); Punched = null;
        }

        /// <summary>Says where this player is; joins the world (or the line for it) the first time. Null leaves.</summary>
        public static void Enter(string place)
        {
            if (!SaveData.Live) place = null;
            Place = place;
            _wanted = place != null;
            if (!_moving) Move();
        }

        static async void Move()
        {
            _moving = true;
            try
            {
                while (true)
                {
                    if (!_wanted)
                    {
                        await Leave();
                        break;
                    }
                    if (_session != null) break;
                    await OnlineBoards.Connect();
                    if (OnlineBoards.Status != OnlineBoards.State.Online) { Debug.Log("[Live] offline: playing alone"); break; }
                    if (!_wanted) continue;
                    bool full;
                    (_session, full) = await JoinWorld();
                    if (_session != null)
                    {
                        if (_queue != null) { var q = _queue; _queue = null; QueuePosition = 0; try { await q.LeaveAsync(); } catch (Exception) { } }
                        break;
                    }
                    if (!full) { await Task.Delay(8000); continue; }          // a hiccup (rate limit, network): again shortly
                    // The world is full: wait in line, and try again when there is room for us.
                    if (_queue == null) _queue = await JoinRoom(QueueId, 100, false);
                    if (_queue == null) { await Task.Delay(6000); continue; }
                    await WaitForRoom();
                }
            }
            finally { _moving = false; }
        }

        static async Task Leave()
        {
            foreach (var s in new[] { _session, _queue })
            {
                if (s == null) continue;
                Detach(s);
                try { await s.LeaveAsync(); } catch (Exception e) { Debug.Log("[Live] leave: " + e.Message); }
            }
            _session = _queue = null;
            QueuePosition = 0;
            Others.Clear();
            _pendingState = _pendingPunch = null;
        }

        /// <summary>In line: our place in it, and every few seconds whether the world has room for us yet.</summary>
        static async Task WaitForRoom()
        {
            while (_wanted && _queue != null && _session == null)
            {
                string me = _queue.CurrentPlayer?.Id;
                int pos = 1;
                foreach (var p in _queue.Players) { if (p.Id == me) break; pos++; }   // the list is in order of joining
                QueuePosition = pos;
                int free = await FreeInWorld();
                if (free >= pos) return;
                await Task.Delay(6000);
            }
        }

        static async Task<int> FreeInWorld()
        {
            try
            {
                var q = new QuerySessionsOptions { Count = 5 };
                q.FilterOptions.Add(new FilterOption(FilterField.Name, WorldId, FilterOperation.Equal));
                var r = await MultiplayerService.Instance.QuerySessionsAsync(q);
                foreach (var s in r.Sessions) if (s.Name == WorldId) return s.AvailableSlots;
                return WorldSize;                                                      // nobody there: it is all ours
            }
            catch (Exception e) { Debug.Log("[Live] query: " + e.Message); return 0; }
        }

        static async Task<(ISession, bool)> JoinWorld()
        {
            try
            {
                var s = await JoinRoom(WorldId, WorldSize, true);
                return (s, false);
            }
            catch (SessionException e) when (IsFull(e))
            {
                Debug.Log("[Live] the world is full: waiting in line");
                return (null, true);
            }
        }

        static bool IsFull(SessionException e) =>
            (e.Message != null && e.Message.IndexOf("full", StringComparison.OrdinalIgnoreCase) >= 0) || e.Error.ToString().Contains("Full");

        /// <summary>Creates or joins a room by its name. Full rooms throw (for the world); other failures give null.</summary>
        static async Task<ISession> JoinRoom(string id, int size, bool world)
        {
            var options = new SessionOptions
            {
                Name = id,
                MaxPlayers = size,
                PlayerProperties = new Dictionary<string, PlayerProperty>
                {
                    { "n", new PlayerProperty(SafeName(SaveData.PlayerName), VisibilityPropertyOptions.Member) },
                    { "o", new PlayerProperty(SaveData.Outfit(SaveData.LastCharacter).Code, VisibilityPropertyOptions.Member) },
                },
            };
            try
            {
                var s = await MultiplayerService.Instance.CreateOrJoinSessionAsync(id, options);
                if (world) Attach(s);
                Debug.Log("[Live] in " + id + " with " + (s.PlayerCount - 1) + " other(s)");
                if (world) Scan(s);
                return s;
            }
            catch (SessionException e) when (e.Message != null && e.Message.Contains("already a member"))
            {
                // Still listed from before (a reload, a dropped tab): take the old place back.
                try
                {
                    var s = await MultiplayerService.Instance.ReconnectToSessionAsync(id);
                    if (world) Attach(s);
                    s.CurrentPlayer.SetProperty("n", new PlayerProperty(SafeName(SaveData.PlayerName), VisibilityPropertyOptions.Member));
                    Debug.Log("[Live] back in " + id + " with " + (s.PlayerCount - 1) + " other(s)");
                    if (world) Scan(s);
                    return s;
                }
                catch (Exception re) { Debug.Log("[Live] " + id + ": could not rejoin: " + re.Message); return null; }
            }
            catch (SessionException e) when (world && IsFull(e)) { throw; }
            catch (Exception e) { Debug.Log("[Live] " + id + ": " + e.Message); return null; }
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
            if (_wanted && !_moving) Move();   // try again (or wait in line)
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
