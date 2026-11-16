using System.Collections.Generic;
using Ski.Core;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// The other people on this slope right now, from <see cref="LiveRooms"/>: pale ghosts while
    /// they ski (they cannot be hit), their own solid selves once they are past the finish line.
    /// There are no computer skiers and no replays here: replays are watched from the boards.
    /// Never more than the cap from the settings, nearest first.
    /// </summary>
    public sealed class LiveCrowd
    {
        readonly Dictionary<string, GhostView> _views = new Dictionary<string, GhostView>();
        readonly List<string> _drop = new List<string>();
        readonly List<(float, GhostView)> _order = new List<(float, GhostView)>();
        readonly RunSession _s;
        readonly Transform _parent;

        public int Shown { get; private set; }

        public LiveCrowd(RunSession session, Transform parent)
        {
            _s = session;
            var root = new GameObject("LivePlayers");
            root.transform.SetParent(parent, false);
            _parent = root.transform;
        }

        /// <summary>Places everyone for this frame.</summary>
        public void Sync(Camera cam, float dt)
        {
            var c = _s.Course;
            Vector3 me = _s.Sim.Pos;
            double now = LiveRooms.Now;
            _order.Clear();
            foreach (var r in LiveRooms.Others.Values)
            {
                bool there = r.Timeline.Visible(now) && r.Timeline.At(now, out var s, out float speed, out var mode) && mode != LiveMode.Tavern;
                if (!_views.TryGetValue(r.Id, out var v))
                {
                    if (!there) continue;
                    _views[r.Id] = v = GhostView.Create(_parent, r.Name, r.Who, false);
                }
                v.Rename(r.Name);
                if (!there || !r.Timeline.At(now, out s, out speed, out mode)) { v.Hide(); continue; }
                Vector3 p = new Vector3(s.Pos.x, c.Height(s.Pos.x, s.Pos.z) + s.Pos.y, s.Pos.z);
                bool solid = mode == LiveMode.Finished;
                if (v.Solid != solid) v.SetSolid(solid);
                // A ghost only appears once it is clear of the player: nobody stands inside you.
                if (!solid && (p - me).sqrMagnitude < 9f) { v.Hide(); continue; }
                float tuck = (s.Flags & LiveFlags.Tuck) != 0 ? 1f : 0f, plough = (s.Flags & LiveFlags.Plough) != 0 ? 1f : 0f;
                v.Show(p, s.Yaw * Mathf.Deg2Rad, speed, s.Pos.y, tuck, plough, cam, dt, (s.Flags & LiveFlags.Crashed) != 0);
                _order.Add(((p - me).sqrMagnitude, v));
            }
            // The cap: the nearest are kept.
            Shown = _order.Count;
            if (_order.Count > SaveData.MaxGhosts)
            {
                _order.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                for (int i = SaveData.MaxGhosts; i < _order.Count; i++) _order[i].Item2.Hide();
                Shown = SaveData.MaxGhosts;
            }
            // People who left the room.
            _drop.Clear();
            foreach (var id in _views.Keys) if (!LiveRooms.Others.ContainsKey(id)) _drop.Add(id);
            foreach (var id in _drop) { Object.Destroy(_views[id].gameObject); _views.Remove(id); }
        }
    }
}
