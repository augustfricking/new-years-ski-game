using System.Collections.Generic;
using System.Runtime.InteropServices;
using Ski.Core;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// Direct lines to the other players in the room (Plugins/WebGL/LastRunLink.jslib: WebRTC data
    /// channels through PeerJS), so they are seen about a tenth of a second late instead of the
    /// room's seconds: fast enough to race side by side and to punch someone who is really there.
    /// The room (<see cref="LiveRooms"/>) still says who is about and carries everything too, more
    /// slowly; a player whose line will not open (some networks block direct connections) is simply
    /// seen through the room. Nothing goes out from the Editor, where this does nothing.
    /// </summary>
    public static class LiveLink
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void LRLinkInit(string id, int echo);
        [DllImport("__Internal")] static extern void LRLinkDial(string id);
        [DllImport("__Internal")] static extern void LRLinkDrop(string id);
        [DllImport("__Internal")] static extern int LRLinkOpen(string id);
        [DllImport("__Internal")] static extern float LRLinkRtt(string id);
        [DllImport("__Internal")] static extern void LRLinkSend(string text);
        [DllImport("__Internal")] static extern void LRLinkPing();
        [DllImport("__Internal")] static extern string LRLinkPoll();
        [DllImport("__Internal")] static extern int LRLinkState();
        const bool Works = true;
#else
        static void LRLinkInit(string id, int echo) { }
        static void LRLinkDial(string id) { }
        static void LRLinkDrop(string id) { }
        static int LRLinkOpen(string id) => 0;
        static float LRLinkRtt(string id) => 0f;
        static void LRLinkSend(string text) { }
        static void LRLinkPing() { }
        static string LRLinkPoll() => null;
        static int LRLinkState() => 0;
        const bool Works = false;
#endif

        /// <summary>Moments sent a second on a direct line.</summary>
        public const float Rate = 20f;
        /// <summary>Seconds between new tries at a line that would not open (and later, after several, between rarer ones).</summary>
        const float RedialSeconds = 8f, RedialLater = 30f;
        /// <summary>How long the larger id waits for the smaller one to dial before dialling itself
        /// (the other side may be in a background tab, or an older version that never dials).</summary>
        const float AnswerWait = 10f;

        static string _me;
        static double _nextSend, _nextCheck, _nextPing;
        static int _seq;
        static readonly LivePacket One = new LivePacket();
        static readonly Dictionary<string, float> Dialled = new Dictionary<string, float>();
        static readonly Dictionary<string, int> Tries = new Dictionary<string, int>();
        static readonly Dictionary<string, float> FirstSeen = new Dictionary<string, float>();
        static readonly HashSet<string> Known = new HashSet<string>();
        static readonly List<string> Gone = new List<string>();
        static readonly Dictionary<string, LiveRooms.Remote> ByPeer = new Dictionary<string, LiveRooms.Remote>();

        /// <summary>This tab only answers, sending everyone's movement back beside them ("?echo=1": a stand-in for trying things).</summary>
        public static bool Echo => PageArgs.Get("echo") == "1";

        /// <summary>Lines open right now, and the average round trip on them (ms; 0 before it is measured).</summary>
        public static int OpenLines { get; private set; }
        public static float RoundTrip { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _me = null; _nextSend = _nextCheck = _nextPing = 0; _seq = 0;
            Dialled.Clear(); Tries.Clear(); FirstSeen.Clear(); Known.Clear(); ByPeer.Clear(); OpenLines = 0; RoundTrip = 0f;
        }

        static string PeerId(string playerId) => "lastrun-n-" + playerId;

        /// <summary>Whether the movement of this player is coming straight from their browser.</summary>
        public static bool Linked(LiveRooms.Remote r) => r != null && r.Timeline.Direct(LiveRooms.Now);

        /// <summary>Call every frame: opens our end, dials the others, reads what came in.</summary>
        public static void Tick()
        {
            if (!Works || !SaveData.Live || !LiveRooms.Connected) return;
            string me = LiveRooms.MyId;
            if (me == null) return;
            if (me != _me)
            {
                _me = me;
                LRLinkInit(PeerId(me), Echo ? 1 : 0);
                Dialled.Clear(); Tries.Clear(); FirstSeen.Clear();
            }
            double now = LiveRooms.Now;
            Read(now);
            if (now < _nextCheck) return;
            _nextCheck = now + 0.5;
            bool ready = LRLinkState() == 2;
            int open = 0; float rtt = 0f; int measured = 0;
            ByPeer.Clear();
            foreach (var r in LiveRooms.Others.Values)
            {
                string pid = PeerId(r.Id);
                ByPeer[pid] = r;
                Known.Add(pid);
                if (!FirstSeen.ContainsKey(pid)) FirstSeen[pid] = Time.unscaledTime;
                if (LRLinkOpen(pid) == 1)
                {
                    open++;
                    Tries[pid] = 0;
                    float ms = LRLinkRtt(pid);
                    if (ms > 0f) { rtt += ms; measured++; }
                    continue;
                }
                // The one with the smaller id dials; the other too if no line has opened after a while. Again
                // every few seconds until the line opens, and only now and then after several tries.
                bool mine = string.CompareOrdinal(me, r.Id) < 0 || Time.unscaledTime - FirstSeen[pid] > AnswerWait;
                Tries.TryGetValue(pid, out int tries);
                float wait = tries < 5 ? RedialSeconds : RedialLater;
                if (ready && mine && (!Dialled.TryGetValue(pid, out float t) || Time.unscaledTime - t > wait))
                {
                    Dialled[pid] = Time.unscaledTime;
                    Tries[pid] = tries + 1;
                    LRLinkDial(pid);
                }
            }
            OpenLines = open;
            RoundTrip = measured > 0 ? rtt / measured : 0f;
            // Lines to players who have left the room are closed.
            Gone.Clear();
            foreach (var pid in Known) if (!ByPeer.ContainsKey(pid)) Gone.Add(pid);
            foreach (var pid in Gone) { Known.Remove(pid); Dialled.Remove(pid); Tries.Remove(pid); FirstSeen.Remove(pid); LRLinkDrop(pid); }
            if (now >= _nextPing && open > 0) { _nextPing = now + 2.0; LRLinkPing(); }
        }

        static void Read(double now)
        {
            string all = LRLinkPoll();
            if (string.IsNullOrEmpty(all)) return;
            foreach (var line in all.Split('\n'))
            {
                int tab = line.IndexOf('\t');
                if (tab <= 0 || tab + 1 >= line.Length) continue;
                string pid = line.Substring(0, tab);
                if (!ByPeer.TryGetValue(pid, out var r))
                {
                    // Someone new since the last check: find them in the room.
                    foreach (var o in LiveRooms.Others.Values) if (PeerId(o.Id) == pid) { r = o; ByPeer[pid] = o; break; }
                    if (r == null) continue;
                }
                char kind = line[tab + 1];
                string body = line.Substring(tab + 2);
                if (kind == 'p') LiveRooms.TakeDirect(r, LivePacket.Decode(body), now);
                else if (kind == 'h') LiveRooms.TakePunch(r, body, now);
            }
        }

        /// <summary>Sends this moment of the player to every open line, twenty times a second.</summary>
        public static void Send(LiveRecorder rec, double now, LiveSample sample)
        {
            if (!Works || _me == null || OpenLines == 0 && Known.Count == 0) return;
            if (now < _nextSend) return;
            _nextSend = System.Math.Max(_nextSend + 1.0 / Rate, now - 0.5 / Rate);
            One.Samples.Clear();
            One.Samples.Add(sample);
            One.Mode = rec.Mode; One.Character = rec.Character; One.Place = rec.Place;
            One.Start = now; One.Seq = _seq = (_seq + 1) & 0xFFFF;
            LRLinkSend("p" + One.Encode());
        }

        /// <summary>A punch, on every open line (the room carries it too).</summary>
        public static void SendPunch(string text)
        {
            if (!Works || _me == null) return;
            LRLinkSend("h" + text);
        }
    }
}
