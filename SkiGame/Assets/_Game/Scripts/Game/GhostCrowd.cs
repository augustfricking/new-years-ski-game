using System.Collections.Generic;
using Ski.Core;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// The other skiers you share a run with, all of them ghosts (they cannot be hit):
    ///  - your own best run on this slope, replayed;
    ///  - other players' runs from the world board, replayed with their names;
    ///  - rivals skiing live, to fill the slope when few players are about.
    /// Never more than the cap from the settings; real players take precedence over rivals.
    /// </summary>
    public sealed class GhostCrowd
    {
        sealed class Replay { public GhostView View; public GhostTrack Track; public bool Online; }
        sealed class Rival { public GhostView View; public RunSession Session; public Bot Bot; public float Done, Delay; }

        static readonly Color Gold = new Color(1f, 0.86f, 0.45f), Ice = new Color(0.72f, 0.88f, 1f), Mist = new Color(0.9f, 0.86f, 1f);

        readonly List<Replay> _replays = new List<Replay>();
        readonly List<Rival> _rivals = new List<Rival>();
        readonly RunSession _s;
        readonly Transform _parent;
        readonly int _cap;
        float _clock;
        bool _closed;

        public int Count => _replays.Count + _rivals.Count;

        public GhostCrowd(RunSession session, Transform parent)
        {
            _s = session;
            _cap = SaveData.MaxGhosts;
            var root = new GameObject("Ghosts");
            root.transform.SetParent(parent, false);
            _parent = root.transform;
            if (_cap <= 0) return;

            var mine = GhostTrack.Decode(SaveData.Ghost(session.Slope));
            if (mine != null) _replays.Add(new Replay { Track = mine, View = GhostView.Create(_parent, "YOUR BEST", session.Character, Gold) });

            string board = OnlineBoards.BoardId(session.Slope);
            AddOnline(OnlineBoards.Cached(board));
            OnlineBoards.Fetch(board, list => { if (!_closed) AddOnline(list); });

            // Rivals from the local board ski live beside you.
            int want = Mathf.Min(_cap - Count, 3);
            var names = new List<string>();
            foreach (var e in SaveData.Boards.Board(session.Slope)) if (!e.IsPlayer) names.Add(e.Name);
            for (int i = 0; i < want && i < names.Count; i++)
            {
                var who = (CharacterId)(((int)session.Character + 1 + i) % 4);
                var rs = new RunSession(session.Slope, who, session.Course, false);
                var bot = new Bot(rs) { Skill = 0.8f - i * 0.12f };
                bot.SpeedLimit *= 1.03f - i * 0.06f;
                bot.Lane = (i % 2 == 0 ? 1f : -1f) * (2.2f + i * 0.8f);
                // They leave the gate one after another, like an interval start.
                _rivals.Add(new Rival { Session = rs, Bot = bot, Delay = 1.2f + i * 1.6f, View = GhostView.Create(_parent, names[i], who, Mist) });
            }
        }

        void AddOnline(List<OnlineBoards.Entry> list)
        {
            if (list == null) return;
            foreach (var e in list)
            {
                if (e.Me || string.IsNullOrEmpty(e.Ghost)) continue;
                bool have = false;
                foreach (var r in _replays) if (r.Online && r.View.Label == e.Name) { have = true; break; }
                if (have) continue;
                var track = GhostTrack.Decode(e.Ghost);
                if (track == null) continue;
                // A real player always gets a place: a rival makes way if the slope is full.
                if (Count >= _cap)
                {
                    if (_rivals.Count == 0) break;
                    var last = _rivals[_rivals.Count - 1];
                    Object.Destroy(last.View.gameObject);
                    _rivals.RemoveAt(_rivals.Count - 1);
                }
                var who = e.Character >= 0 && e.Character < 4 ? (CharacterId)e.Character : CharacterId.Erik;
                _replays.Add(new Replay { Track = track, Online = true, View = GhostView.Create(_parent, e.Name, who, Ice) });
            }
        }

        /// <summary>One simulation step. Only called once the player has left the start gate.</summary>
        public void Step(float dt)
        {
            _clock += dt;
            foreach (var r in _rivals)
            {
                if (r.Done > 8f || _clock < r.Delay) continue;
                r.Session.Step(r.Bot.Decide(), dt);
                r.Session.Events.Clear();
                if (r.Session.Phase == RunPhase.Finished) r.Done += dt;
            }
        }

        /// <summary>Places every ghost for this frame.</summary>
        public void Sync(Camera cam, float dt)
        {
            var c = _s.Course;
            foreach (var r in _replays)
            {
                bool live = r.Track.At(_clock, out Vector3 p, out float air, out float heading, out float speed);
                if (!live && _clock > r.Track.Duration + 6f) { r.View.Hide(); continue; }
                p.y = c.Height(p.x, p.z) + air;
                r.View.Show(p, heading, live ? speed : 0f, air, speed > 22f ? 1f : 0f, 0f, cam, dt);
            }
            foreach (var r in _rivals)
            {
                if (r.Done > 8f) { r.View.Hide(); continue; }
                var sim = r.Session.Sim;
                r.View.Show(sim.Pos, sim.Heading, sim.Speed, sim.Contact ? 0f : 1f, sim.Tuck, sim.Plough, cam, dt);
            }
        }

        public void Close() { _closed = true; }
    }
}
