using System.Collections.Generic;
using Ski.Core;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// The other skiers you share a run with. Every one of them is a real run by a real person,
    /// replayed as a ghost (ghosts cannot be hit): your own best on this slope, and other players'
    /// best runs from the world board, each with its name above it. There are no computer skiers.
    /// Never more than the cap from the settings.
    /// </summary>
    public sealed class GhostCrowd
    {
        sealed class Replay { public GhostView View; public GhostTrack Track; public bool Online; }

        static readonly Color Gold = new Color(1f, 0.86f, 0.45f), Ice = new Color(0.72f, 0.88f, 1f);

        readonly List<Replay> _replays = new List<Replay>();
        readonly RunSession _s;
        readonly Transform _parent;
        readonly int _cap;
        float _clock;
        bool _closed;

        public int Count => _replays.Count;

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
        }

        void AddOnline(List<OnlineBoards.Entry> list)
        {
            if (list == null) return;
            foreach (var e in list)
            {
                if (Count >= _cap) break;
                if (e.Me || string.IsNullOrEmpty(e.Ghost)) continue;
                bool have = false;
                foreach (var r in _replays) if (r.Online && r.View.Label == e.Name) { have = true; break; }
                if (have) continue;
                var track = GhostTrack.Decode(e.Ghost);
                if (track == null) continue;
                var who = e.Character >= 0 && e.Character < 4 ? (CharacterId)e.Character : CharacterId.Erik;
                _replays.Add(new Replay { Track = track, Online = true, View = GhostView.Create(_parent, e.Name, who, Ice) });
            }
        }

        /// <summary>One simulation step. Only called once the player has left the start gate.</summary>
        public void Step(float dt) { _clock += dt; }

        /// <summary>Places every ghost for this frame.</summary>
        public void Sync(Camera cam, float dt)
        {
            var c = _s.Course;
            Vector3 me = _s.Sim.Pos;
            foreach (var r in _replays)
            {
                bool live = r.Track.At(_clock, out Vector3 p, out float air, out float heading, out float speed);
                if (!live && _clock > r.Track.Duration + 6f || _clock <= 0f) { r.View.Hide(); continue; }
                p.y = c.Height(p.x, p.z) + air;
                // Nobody stands inside you: a ghost only appears once it is clear of the player.
                if ((p - me).sqrMagnitude < 9f) { r.View.Hide(); continue; }
                r.View.Show(p, heading, live ? speed : 0f, air, speed > 22f ? 1f : 0f, 0f, cam, dt);
            }
        }

        public void Close() { _closed = true; }
    }
}
