using System;
using System.Collections.Generic;
using Ski.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ski.Game
{
    /// <summary>
    /// In-run heads-up display: timer or score, speed, the pole-timing ring, the edge meter,
    /// pop-ups for tricks, penalties and crashes, and key hints at the start.
    /// </summary>
    public sealed class Hud
    {
        readonly RunController _run;
        readonly VisualElement _root;
        readonly Label _timer, _penalty, _speed, _extra, _toast, _toastSub, _score, _air;
        readonly PoleRing _ring;
        readonly CarveMeter _carve;
        readonly VisualElement _hints, _ringBox, _carveBox;
        readonly Action<float> _ticker;
        readonly Queue<(string title, string sub, bool bad, float time)> _queue = new Queue<(string, string, bool, float)>();
        float _toastLeft, _hintTime;
        bool _visible = true;

        public Hud(RunController run)
        {
            _run = run;
            var ui = Ui.Instance;
            var s = run.Session;
            _root = Ui.Div(ui.HudLayer, "layer");

            var tl = Ui.Div(_root, "hud-corner hud-tl");
            var badge = Ui.Text(tl, s.Course.Name.ToUpperInvariant(), "hud-badge");
            badge.style.backgroundColor = Palette.Slope(s.Slope);
            if (s.ScoreBased)
            {
                _score = Ui.Text(tl, "0", "hud-score");
                _timer = Ui.Text(tl, "", "hint");
            }
            else _timer = Ui.Text(tl, "0.00", "hud-timer");
            _penalty = Ui.Text(tl, "", "hud-penalty");

            var tr = Ui.Div(_root, "hud-corner hud-tr");
            var speedRow = Ui.Div(tr, "row");
            _speed = Ui.Text(speedRow, "0", "hud-speed");
            Ui.Text(speedRow, "KM/H", "hud-unit");
            _extra = Ui.Text(tr, "", "hint");

            var centre = Ui.Div(_root, "hud-centre");
            _toast = Ui.Text(centre, "", "toast toast-hidden");
            _toastSub = Ui.Text(centre, "", "toast-sub toast-hidden");
            _air = Ui.Text(centre, "", "toast-sub");

            var bottom = Ui.Div(_root, "hud-bottom");
            _ringBox = Ui.Div(bottom, "center");
            _ring = new PoleRing();
            _ringBox.Add(_ring);
            Ui.Keys(_ringBox, "SHIFT", SkierInfo.Get(s.Character).Gear == Gear.Snowboard ? "push off in time" : "pole in time");
            _carveBox = Ui.Div(bottom, "center");
            _carve = new CarveMeter { CarveMin = s.Sim.P.CarveMin, CarveMax = s.Sim.P.CarveMax };
            _carveBox.Add(_carve);

            _hints = Ui.Div(_root, "hud-corner hud-bl");
            Ui.Keys(_hints, "A D", "turn - gently to carve, hard to brake");
            Ui.Keys(_hints, "W", "hold to tuck");
            Ui.Keys(_hints, "S", "snowplough brake");
            Ui.Keys(_hints, "SPACE", "jump");
            if (s.ScoreBased) Ui.Keys(_hints, "1 2 3 4 5", "tricks in the air (+ W A S D)");
            Ui.Keys(_hints, "R", "back on course");
            Ui.Keys(_hints, "MOUSE", "look around (click first)");

            run.OnEvent += OnEvent;
            _ticker = ui.AddTicker(Tick);
        }

        public void Dispose()
        {
            _run.OnEvent -= OnEvent;
            Ui.Instance?.RemoveTicker(_ticker);
            _root.RemoveFromHierarchy();
        }

        public void SetVisible(bool on)
        {
            _visible = on;
            _root.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Shows a pop-up in the middle of the screen.</summary>
        public void Toast(string title, string sub = "", bool bad = false, float seconds = 1.7f)
        {
            if (_queue.Count > 3) _queue.Dequeue();
            _queue.Enqueue((title, sub, bad, seconds));
        }

        void OnEvent(RunEvent e)
        {
            switch (e.Type)
            {
                case RunEventType.Started: Toast("GO!", "", false, 0.9f); break;
                case RunEventType.TrickScored:
                    Toast(e.Trick.Name.ToUpperInvariant() + (e.Trick.Clean ? "!" : ""), "+" + e.Trick.Points + (e.Trick.Clean ? "  CLEAN" : e.Trick.Sketchy ? "  SKETCHY" : ""));
                    break;
                case RunEventType.GateMissed: Toast("MISSED GATE", "+" + RunSession.GatePenalty.ToString("0") + " s", true, 1.2f); break;
                case RunEventType.PersonHit: Toast("WATCH OUT!", "+" + RunSession.PersonPenalty.ToString("0") + " s", true); break;
                case RunEventType.DeadEnd:
                case RunEventType.Respawned: Toast(e.Text.ToUpperInvariant(), "", true, 2.2f); break;
                case RunEventType.Crash:
                    if (e.Reason != CrashReason.Person) Toast(CrashText(e.Reason), "", true, 1.3f);
                    break;
                case RunEventType.PolePush:
                    var q = (PoleQuality)(int)e.Value;
                    _ring.Flash = 1f;
                    _ring.FlashColour = q == PoleQuality.Perfect ? Palette.UiGood : q == PoleQuality.Ok ? Palette.UiAccent : Palette.UiBad;
                    break;
            }
        }

        static string CrashText(CrashReason r)
        {
            switch (r)
            {
                case CrashReason.Trees: return "INTO THE TREES!";
                case CrashReason.HardLanding: return "OUCH!";
                case CrashReason.BadLanding: return "WIPEOUT!";
                case CrashReason.Cliff: return "CLIFF!";
                default: return "CRASH!";
            }
        }

        void Tick(float dt)
        {
            if (!_visible || _run == null) return;
            var s = _run.Session; var sim = s.Sim;

            string time = FormatTime(s.Time);
            if (s.ScoreBased) { _score.text = s.Score.ToString("N0", System.Globalization.CultureInfo.InvariantCulture); _timer.text = time; }
            else _timer.text = time;
            _penalty.text = s.Penalty > 0f ? "+" + s.Penalty.ToString("0") + " s penalty" : "";
            _speed.text = Mathf.RoundToInt(sim.Speed * 3.6f).ToString();
            if (s.Gates.Count > 0) _extra.text = "GATE " + Mathf.Min(s.NextGate + 1, s.Gates.Count) + " / " + s.Gates.Count + (s.GatesMissed > 0 ? "   MISSED " + s.GatesMissed : "");
            else if (s.Crowd != null && s.PeopleHit > 0) _extra.text = "BUMPS: " + s.PeopleHit;

            // Pole ring: only while pushing still helps.
            bool showRing = sim.PoleAvailable && sim.Contact && !sim.Crashed && sim.Plough < 0.3f;
            _ringBox.style.display = showRing ? DisplayStyle.Flex : DisplayStyle.None;
            if (showRing)
            {
                _ring.Phase = sim.PolePhase; _ring.SweetSpot = sim.P.PoleSweetSpot; _ring.Window = sim.P.PoleWindow; _ring.OkWindow = sim.P.PoleOkWindow;
                _ring.Flash = Mathf.MoveTowards(_ring.Flash, 0f, dt * 2.5f);
                _ring.MarkDirtyRepaint();
            }
            bool showCarve = !showRing && sim.Contact && !sim.Crashed && sim.Speed > 4f && sim.Surface == SurfaceKind.Snow;
            _carveBox.style.display = showCarve ? DisplayStyle.Flex : DisplayStyle.None;
            if (showCarve) { _carve.Lean = sim.Lean; _carve.Quality = sim.CarveQuality; _carve.MarkDirtyRepaint(); }

            // What is happening in the air right now.
            string air = "";
            if (sim.RealAir && !sim.Crashed)
            {
                if (sim.ActiveGrab != 0) air = TrickScorer.GrabName(sim.ActiveGrab, sim.ActiveGrabVariant).ToUpperInvariant();
                int halves = Mathf.FloorToInt(Mathf.Abs(sim.AirYaw) / Mathf.PI + 0.15f);
                if (halves > 0) air = (air.Length > 0 ? air + " " : "") + (halves * 180);
                if (Mathf.Abs(sim.Flip) > 0.6f) air = (air.Length > 0 ? air + " + " : "") + "FLIP";
                if (Mathf.Abs(sim.Cork) > 0.6f) air = (air.Length > 0 ? air + " + " : "") + "CORK";
            }
            else if (sim.Surface == SurfaceKind.Box && sim.GrindDistance > 1f) air = "GRIND " + sim.GrindDistance.ToString("0") + " m";
            _air.text = air;

            // Key hints fade once the run is properly under way.
            _hintTime += dt;
            bool hints = s.Phase == RunPhase.Ready || s.Time < 7f && s.Phase == RunPhase.Running;
            _hints.style.display = hints ? DisplayStyle.Flex : DisplayStyle.None;

            // Pop-ups.
            if (_toastLeft > 0f)
            {
                _toastLeft -= dt;
                if (_toastLeft <= 0f) { _toast.AddToClassList("toast-hidden"); _toastSub.AddToClassList("toast-hidden"); _toastLeft = -0.25f; }
            }
            else if (_toastLeft < 0f) _toastLeft = Mathf.Min(_toastLeft + dt, 0f);
            else if (_queue.Count > 0)
            {
                var t = _queue.Dequeue();
                _toast.text = t.title; _toastSub.text = t.sub;
                _toast.EnableInClassList("toast-bad", t.bad);
                _toast.RemoveFromClassList("toast-hidden"); _toastSub.RemoveFromClassList("toast-hidden");
                _toastLeft = t.time;
            }
        }

        public static string FormatTime(float t)
        {
            int min = (int)(t / 60f);
            float sec = t - min * 60f;
            return min > 0 ? min + ":" + sec.ToString("00.00", System.Globalization.CultureInfo.InvariantCulture) : sec.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
