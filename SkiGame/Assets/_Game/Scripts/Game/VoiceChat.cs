using System.Collections.Generic;
using System.Runtime.InteropServices;
using Ski.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Ski.Game
{
    /// <summary>
    /// Talking to the other players: hold V and speak. Everyone in the same room (your slope, the
    /// tavern, the lift and map) can hear you, louder the closer they are; at a tavern table the
    /// people sitting there hear each other as if side by side. Peer to peer in the browser
    /// (Plugins/WebGL/LastRunVoice.jslib); in the Editor it does nothing.
    /// </summary>
    public sealed class VoiceChat : MonoBehaviour
    {
        public static VoiceChat I { get; private set; }

        /// <summary>Where the listener is, and what kind of place it is (set each frame by the scene).</summary>
        public static Vector3 MyPos;
        public static string Place = "";
        /// <summary>The tavern table the player sits at (-1: none).</summary>
        public static int MyTable = -1;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void LRVoiceInit(string id);
        [DllImport("__Internal")] static extern void LRVoiceCall(string id);
        [DllImport("__Internal")] static extern void LRVoiceHang(string id);
        [DllImport("__Internal")] static extern void LRVoiceGain(string id, float gain);
        [DllImport("__Internal")] static extern void LRVoiceTalk(int on);
        [DllImport("__Internal")] static extern float LRVoiceLevel(string id);
        [DllImport("__Internal")] static extern int LRVoiceState();
        const bool Works = true;
#else
        static void LRVoiceInit(string id) { }
        static void LRVoiceCall(string id) { }
        static void LRVoiceHang(string id) { }
        static void LRVoiceGain(string id, float gain) { }
        static void LRVoiceTalk(int on) { }
        static float LRVoiceLevel(string id) => 0f;
        static int LRVoiceState() => 0;
        const bool Works = false;
#endif

        readonly HashSet<string> _calling = new HashSet<string>();
        readonly List<string> _gone = new List<string>();
        readonly Dictionary<string, float> _level = new Dictionary<string, float>();
        string _me;
        bool _talking;
        float _poll;
        VisualElement _badge;

        public static VoiceChat Create(Transform parent)
        {
            var go = new GameObject("Voice");
            go.transform.SetParent(parent, false);
            return I = go.AddComponent<VoiceChat>();
        }

        static string PeerId(string playerId) => "lastrun-" + playerId;

        /// <summary>Whether this player is speaking right now (for a mark by their name).</summary>
        public static bool IsTalking(string playerId) => I != null && I._level.TryGetValue(playerId, out float l) && l > 0.02f;

        void Update()
        {
            if (!Works) return;
            bool on = SaveData.Voice && SaveData.Live;
            string me = LiveRooms.MyId;
            if (on && me != null && _me == null)
            {
                _me = me;
                LRVoiceInit(PeerId(me));
            }
            // Push to talk.
            var kb = Keyboard.current;
            bool talk = on && _me != null && kb != null && kb.vKey.isPressed && !Dialogs.Open;
            if (talk != _talking)
            {
                _talking = talk;
                LRVoiceTalk(talk ? 1 : 0);
                ShowBadge(talk);
            }
            if (_me == null) return;

            _poll -= Time.unscaledDeltaTime;
            if (_poll > 0f) return;
            _poll = 0.25f;
            double now = LiveRooms.Now;
            // Call everyone in the room (the one with the smaller id rings), hang up on those who left.
            foreach (var r in LiveRooms.Others.Values)
            {
                string pid = PeerId(r.Id);
                if (!on) continue;
                if (!_calling.Contains(pid))
                {
                    _calling.Add(pid);
                    if (string.CompareOrdinal(_me, r.Id) < 0) LRVoiceCall(pid);
                }
                LRVoiceGain(pid, Volume(r, now));
                _level[r.Id] = LRVoiceLevel(pid);
            }
            _gone.Clear();
            foreach (var pid in _calling)
            {
                bool here = false;
                foreach (var r in LiveRooms.Others.Values) if (PeerId(r.Id) == pid) { here = true; break; }
                if (!here || !on) _gone.Add(pid);
            }
            foreach (var pid in _gone) { _calling.Remove(pid); LRVoiceHang(pid); _level.Remove(pid.Substring(8)); }
        }

        /// <summary>How loud another player is here: full close by, fading with distance.</summary>
        static float Volume(LiveRooms.Remote r, double now)
        {
            if (!r.Timeline.Visible(now) || !r.Timeline.At(now, out var s, out _, out var mode)) return 1f;   // lift, map: everyone in the room
            float d = Vector2.Distance(new Vector2(s.Pos.x, s.Pos.z), new Vector2(MyPos.x, MyPos.z));
            if (Place == "tavern")
            {
                // At the same table: side by side. Elsewhere in the room: a murmur that grows as you come closer.
                if (MyTable >= 0 && (s.Flags & LiveFlags.Sit) != 0 && d < 3f) return 1f;
                return Mathf.Lerp(1f, 0.12f, Mathf.InverseLerp(2f, 11f, d));
            }
            return Mathf.Clamp01(1f - Mathf.InverseLerp(6f, 45f, d));
        }

        void ShowBadge(bool on)
        {
            var ui = Ui.Instance;
            if (ui == null) return;
            if (_badge == null)
            {
                _badge = Ui.Div(ui.TopLayer, "voice-badge");
                _badge.pickingMode = PickingMode.Ignore;
                Ui.Div(_badge, "voice-dot");
                Ui.Text(_badge, "TALKING", "voice-text");
            }
            _badge.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (on && LRVoiceState() == 5) Ui.Instance.Notice("NO MICROPHONE", "Allow it in the browser", 3f);
        }
    }
}
