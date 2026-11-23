using Ski.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ski.Game
{
    /// <summary>
    /// Watching someone's best run from the board: their skier, solid and named, while the camera
    /// follows from behind. A run recorded with its inputs is played again exactly: its own session on
    /// this slope, fed the same keys step by step, so the line, every crash and every child on the
    /// kids' slope are as they were. An older recording (positions twice a second) is followed as a
    /// path; its children are not the ones from that run, so nobody is run into. Lives inside a run's
    /// world, so the slope, the village and the people on it are all there.
    /// </summary>
    public sealed class RunReplay
    {
        const float LeadIn = 1.5f, TailOut = 2.5f;

        readonly GhostTrack _track;
        readonly RunSession _run;
        readonly RunInputs.Reader _reader;
        readonly KidView _kids;
        readonly GhostView _view;
        readonly Course _c;
        readonly VisualElement _ui;
        readonly Label _clockLabel;
        float _clock = -LeadIn, _acc, _tail;
        Vector3 _camPos, _dir = Vector3.forward;
        bool _init;

        /// <summary>Whether this is the run itself played again (not a path followed).</summary>
        public bool Exact => _run != null;
        public bool Done => _run != null ? _reader.Done && _tail > TailOut : _clock > _track.Duration + TailOut;
        /// <summary>Where the replayed skier is now.</summary>
        public Vector3 Position { get; private set; }
        /// <summary>The replayed session (null for a path).</summary>
        public RunSession Session => _run;

        public RunReplay(Transform parent, Course course, SlopeId slope, GhostTrack track, RunInputs inputs, string name, CharacterId who)
        {
            _track = track; _c = course;
            if (inputs != null && inputs.Playable)
            {
                _run = new RunSession(slope, who, course);
                _reader = new RunInputs.Reader(inputs);
                // Waiting at the gate is skipped: played quickly up to a moment before the start line.
                int start = FirstRunningStep(slope, who, course, inputs);
                int skip = Mathf.Max(0, start - Mathf.RoundToInt(LeadIn / SkiSim.FixedDt));
                while (_reader.Step < skip && RunInputs.StepInto(_run, _reader)) _run.Events.Clear();
                if (_run.Crowd != null && course is PisteCourse pc) _kids = KidView.Create(_run.Crowd, pc, parent);
            }
            _view = GhostView.Create(parent, name, who, true);
            if (_run != null) _view.CrashCourse = course;      // the run's own crashes, taken by the body as they were
            if (Ui.Instance != null)
            {
                _ui = Ui.Div(Ui.Instance.ScreenLayer, "replay-bar");
                _ui.pickingMode = PickingMode.Ignore;
                Ui.Text(_ui, "REPLAY", "replay-chip");
                Ui.Text(_ui, name.ToUpperInvariant(), "h2");
                _clockLabel = Ui.Text(_ui, "", "replay-clock");
                Ui.Keys(_ui, "ESC", "back");
            }
        }

        /// <summary>The step at which the recorded run left the start gate (found on a scratch session).</summary>
        static int FirstRunningStep(SlopeId slope, CharacterId who, Course course, RunInputs inputs)
        {
            var s = new RunSession(slope, who, course, false);
            var r = new RunInputs.Reader(inputs);
            while (s.Phase == RunPhase.Ready && RunInputs.StepInto(s, r)) s.Events.Clear();
            return r.Step;
        }

        public void Tick(Camera cam, float dt)
        {
            Vector3 p; float heading, speed, air, tuck = 0f, plough = 0f; bool crashed = false;
            if (_run != null)
            {
                // The run itself, a step of the simulation at a time, as it was played.
                _acc += dt;
                int guard = 0;
                while (_acc >= SkiSim.FixedDt && guard++ < 24)
                {
                    _acc -= SkiSim.FixedDt;
                    if (!RunInputs.StepInto(_run, _reader)) { _run.Step(default, SkiSim.FixedDt); _tail += SkiSim.FixedDt; }
                    Sound();
                }
                if (guard >= 24) _acc = 0f;
                var sim = _run.Sim;
                air = sim.Contact ? 0f : sim.AirHeight;
                p = new Vector3(sim.Pos.x, _c.Height(sim.Pos.x, sim.Pos.z) + air, sim.Pos.z);
                heading = sim.Heading; speed = sim.HorizontalSpeed; tuck = sim.Tuck; plough = sim.Plough; crashed = sim.Crashed;
                if (_kids != null) { _kids.ViewerZ = sim.Pos.z; _kids.Sync(dt); }
                if (_clockLabel != null) _clockLabel.text = Hud.FormatTime(_run.Phase == RunPhase.Ready ? 0f : _run.Time);
            }
            else
            {
                // An older recording: the path, smoothly between its samples.
                _clock += dt;
                float t = Mathf.Max(_clock, 0f);
                _track.At(t, out Vector3 flat, out air, out heading, out speed);
                if (_clock < 0f) speed = 0f;
                p = new Vector3(flat.x, _c.Height(flat.x, flat.z) + air, flat.z);
                tuck = speed > 22f ? 1f : 0f;
                if (_clockLabel != null) _clockLabel.text = Hud.FormatTime(Mathf.Min(t, _track.Duration));
            }
            Position = p;
            _view.Show(p, heading, speed, air, tuck, plough, cam, dt, crashed, _run != null ? _run.Sim.LastCrash : CrashReason.Cliff);

            // Behind and above, following the line rather than every wiggle.
            Vector3 d = new Vector3(Mathf.Sin(heading), 0f, Mathf.Cos(heading));
            if (speed > 2f || !_init) _dir = Vector3.Slerp(_dir, d, _init ? 1f - Mathf.Exp(-2.5f * dt) : 1f).normalized;
            Vector3 focus = p + Vector3.up * 1.1f;
            Vector3 want = focus - _dir * 6.5f + Vector3.up * 2.4f;
            want.y = Mathf.Max(want.y, _c.Height(want.x, want.z) + 1.2f);
            _camPos = _init ? Vector3.Lerp(_camPos, want, 1f - Mathf.Exp(-6f * dt)) : want;
            _init = true;
            cam.transform.SetPositionAndRotation(_camPos, Quaternion.LookRotation(focus + _dir * 3f - _camPos));
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 60f + Mathf.Clamp01(speed / 36f) * 8f, 1f - Mathf.Exp(-3f * dt));
        }

        /// <summary>What the replayed run sounded like where it mattered: crashes and the children it ran into.</summary>
        void Sound()
        {
            foreach (var e in _run.Events)
                if (e.Type == RunEventType.Crash || e.Type == RunEventType.PersonHit) AudioDirector.Sfx("crash", 0.8f);
            _run.Events.Clear();
        }

        public void Dispose()
        {
            _ui?.RemoveFromHierarchy();
            if (_view != null) Object.Destroy(_view.gameObject);
            if (_kids != null) Object.Destroy(_kids.gameObject);
        }
    }
}
