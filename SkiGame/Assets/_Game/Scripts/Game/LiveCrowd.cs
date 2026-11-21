using System.Collections.Generic;
using Ski.Core;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// The other people on the mountain right now, from <see cref="LiveRooms"/>: pale ghosts while
    /// they ski (they cannot be hit) on this slope, and far off on the other runs across the flanks;
    /// their own solid selves once they are down in the village, which every run shares. There are
    /// no computer skiers and no replays here: replays are watched from the boards. Never more than
    /// the cap from the settings, nearest first.
    /// </summary>
    public sealed class LiveCrowd
    {
        readonly Dictionary<string, GhostView> _views = new Dictionary<string, GhostView>();
        readonly List<string> _drop = new List<string>();
        readonly List<(float, GhostView)> _order = new List<(float, GhostView)>();
        readonly RunSession _s;
        readonly PisteCourse _pc;
        readonly Transform _parent;

        public int Shown { get; private set; }

        public LiveCrowd(RunSession session, Transform parent)
        {
            _s = session;
            _pc = session.Course as PisteCourse;
            var root = new GameObject("LivePlayers");
            root.transform.SetParent(parent, false);
            _parent = root.transform;
        }

        /// <summary>How far past their finish someone from another run takes to ride over into the village.</summary>
        const float HandOff = 45f;

        /// <summary>Places everyone for this frame.</summary>
        public void Sync(Camera cam, float dt)
        {
            var c = _s.Course;
            Vector3 me = _s.Sim.Pos;
            double now = LiveRooms.Now;
            _order.Clear();
            foreach (var r in LiveRooms.Others.Values)
            {
                byte place = r.Timeline.Place;
                bool onMountain = place == LivePlace.Unknown || LivePlace.IsSlope(place);   // unknown: an older version, on this slope
                bool there = onMountain && r.Timeline.Visible(now) && r.Timeline.At(now, out var s, out float speed, out var mode) && mode != LiveMode.Tavern;
                if (_views.TryGetValue(r.Id, out var v) && v.OutfitCode != r.Outfit) { Object.Destroy(v.gameObject); _views.Remove(r.Id); v = null; }
                if (v == null)
                {
                    if (!there) continue;
                    _views[r.Id] = v = GhostView.Create(_parent, r.Name, r.Who, false, false, r.Outfit);
                }
                v.Rename(r.Name);
                if (!there || !r.Timeline.At(now, out s, out speed, out mode)) { v.Hide(); continue; }
                // Where they are in this world: on this slope as they are; in the village (shared by
                // all the runs) by where they stand in it; on another run, far off across the flanks.
                int theirs = LivePlace.IsSlope(place) ? (int)LivePlace.Slope(place) : (int)_s.Slope;
                float yaw = s.Yaw * Mathf.Deg2Rad;
                Vector3 p;
                if (theirs == (int)_s.Slope) p = new Vector3(s.Pos.x, c.Height(s.Pos.x, s.Pos.z) + s.Pos.y, s.Pos.z);
                else if (mode == LiveMode.Finished || s.Pos.z > Courses.FinishZ[theirs])
                {
                    float past = Mathf.Max(0f, s.Pos.z - Courses.FinishZ[theirs]);
                    float z = past + c.FinishZ;
                    float x = s.Pos.x;
                    // Out of their own run: over the first metres past their finish they ride in from where that
                    // run meets the plaza (where they were drawn a moment ago) instead of jumping across.
                    if (_pc != null && past < HandOff)
                    {
                        Vector3 end = MountainBuilder.OnOtherRun(_pc, theirs, s.Pos.x, Courses.FinishZ[theirs], out _);
                        x = Mathf.Lerp(end.x, s.Pos.x, M.Smooth(past / HandOff));
                    }
                    p = new Vector3(x, c.Height(x, z) + s.Pos.y, z);
                }
                else if (_pc != null)
                {
                    p = MountainBuilder.OnOtherRun(_pc, theirs, s.Pos.x, s.Pos.z, out float along);
                    p.y += s.Pos.y;
                    yaw += along;
                }
                else { v.Hide(); continue; }
                bool solid = mode == LiveMode.Finished;
                if (v.Solid != solid) v.SetSolid(solid);
                // A ghost only appears once it is clear of the player: nobody stands inside you.
                if (!solid && (p - me).sqrMagnitude < 9f) { v.Hide(); continue; }
                float tuck = (s.Flags & LiveFlags.Tuck) != 0 ? 1f : 0f, plough = (s.Flags & LiveFlags.Plough) != 0 ? 1f : 0f;
                v.Show(p, yaw, speed, s.Pos.y, tuck, plough, cam, dt, (s.Flags & LiveFlags.Crashed) != 0);
                v.SetTalking(VoiceChat.IsTalking(r.Id));
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
