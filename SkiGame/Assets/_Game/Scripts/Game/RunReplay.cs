using Ski.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ski.Game
{
    /// <summary>
    /// Watching someone's best run from the board: their skier, solid and named, skis the
    /// recording while the camera follows from behind. Lives inside a run's world, so the slope,
    /// the village and the people on it are all there.
    /// </summary>
    public sealed class RunReplay
    {
        const float LeadIn = 1.2f, TailOut = 2.5f;

        readonly GhostTrack _track;
        readonly GhostView _view;
        readonly Course _c;
        readonly VisualElement _ui;
        readonly Label _clockLabel;
        readonly SlopeId _slope;
        float _clock = -LeadIn;
        Vector3 _camPos, _dir = Vector3.forward;
        bool _init;

        public bool Done => _clock > _track.Duration + TailOut;
        /// <summary>Where the replayed skier is now.</summary>
        public Vector3 Position { get; private set; }
        float _crash;

        /// <summary>The replayed skier ran into someone: down they go, tumbling, for a moment.</summary>
        public void Crash() { _crash = 1.4f; }
        public bool Crashed => _crash > 0f;

        public RunReplay(Transform parent, Course course, SlopeId slope, GhostTrack track, string name, CharacterId who)
        {
            _track = track; _c = course; _slope = slope;
            _view = GhostView.Create(parent, name, who, true);
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

        public void Tick(Camera cam, float dt)
        {
            _clock += dt;
            float t = Mathf.Max(_clock, 0f);
            _track.At(t, out Vector3 flat, out float air, out float heading, out float speed);
            if (_clock < 0f) speed = 0f;
            Vector3 p = new Vector3(flat.x, _c.Height(flat.x, flat.z) + air, flat.z);
            Position = p;
            _crash = Mathf.Max(0f, _crash - dt);
            _view.Show(p, heading, speed, air, speed > 22f ? 1f : 0f, 0f, cam, dt, _crash > 0f);
            if (_clockLabel != null) _clockLabel.text = Hud.FormatTime(Mathf.Min(t, _track.Duration));

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

        public void Dispose()
        {
            _ui?.RemoveFromHierarchy();
            if (_view != null) Object.Destroy(_view.gameObject);
        }
    }
}
