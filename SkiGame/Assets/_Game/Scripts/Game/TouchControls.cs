using UnityEngine;
using UnityEngine.UIElements;

namespace Ski.Game
{
    /// <summary>
    /// Skiing with fingers (phones and tablets, see <see cref="Device"/>). The left thumb steers on a pad
    /// that starts wherever it lands (slide left or right); the right thumb pushes off with the poles
    /// (in time with the ring, as the click), jumps, and holds tuck or brake; in the park a grab button
    /// joins them. Dragging anywhere else looks around. Small buttons up top pause, change the camera
    /// and get back on course. Several fingers at once, each on its own control. In the air the same
    /// controls do the tricks, as the keys do: steering spins, tuck and brake flip.
    /// The run reads what the fingers say (<see cref="RunController"/>); nothing here moves the skier.
    /// </summary>
    public sealed class TouchControls : MonoBehaviour
    {
        public static TouchControls I { get; private set; }
        /// <summary>The touch controls are up (a finger device, on a run).</summary>
        public static bool Active => I != null && I._mode == Mode.Skiing;

        enum Mode { Hidden, Skiing, PauseOnly }

        /// <summary>Steering: -1 left, 0, 1 right.</summary>
        public float Steer { get; private set; }
        public bool Tuck { get; private set; }
        public bool Brake { get; private set; }
        public bool Grab { get; private set; }
        public bool Talk { get; private set; }

        bool _push, _jump, _reset, _camera, _pause;
        Vector2 _look;
        Mode _mode = Mode.Hidden;
        bool _modeSet;
        VisualElement _layer, _skiing, _top, _turn, _knob, _pad, _grab, _talk;
        Vector2 _padStart;
        int _padPointer = -1, _lookPointer = -1;

        /// <summary>A tap since the last time the run asked (each is taken once).</summary>
        public bool TakePush() { bool b = _push; _push = false; return b; }
        public bool TakeJump() { bool b = _jump; _jump = false; return b; }
        public bool TakeReset() { bool b = _reset; _reset = false; return b; }
        public bool TakeCamera() { bool b = _camera; _camera = false; return b; }
        public bool TakePause() { bool b = _pause; _pause = false; return b; }
        /// <summary>How far the looking finger has moved since the last time (in interface units).</summary>
        public Vector2 TakeLook() { var d = _look; _look = Vector2.zero; return d; }

        public static TouchControls Create(Transform parent)
        {
            var go = new GameObject("TouchControls");
            go.transform.SetParent(parent, false);
            return I = go.AddComponent<TouchControls>();
        }

        void Build()
        {
            var ui = Ui.Instance;
            _layer = new VisualElement { pickingMode = PickingMode.Ignore };
            _layer.AddToClassList("layer");
            _layer.AddToClassList("touch-layer");
            // Under the dialogs (a dialog takes the fingers), over the HUD.
            ui.Root.Insert(ui.Root.IndexOf(ui.DialogLayer), _layer);

            _skiing = Ui.Div(_layer, "layer");
            _skiing.pickingMode = PickingMode.Ignore;

            // Looking around: anywhere that is not a control.
            var look = Ui.Div(_skiing, "touch-look");
            look.pickingMode = PickingMode.Position;
            look.RegisterCallback<PointerDownEvent>(e => { if (_lookPointer >= 0) return; _lookPointer = e.pointerId; look.CapturePointer(e.pointerId); });
            look.RegisterCallback<PointerMoveEvent>(e => { if (e.pointerId == _lookPointer) _look += (Vector2)e.deltaPosition; });
            look.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId != _lookPointer) return; look.ReleasePointer(e.pointerId); _lookPointer = -1; });
            look.RegisterCallback<PointerCaptureOutEvent>(e => { if (e.pointerId == _lookPointer) _lookPointer = -1; });

            // Steering: a pad for the left thumb, centred wherever it lands.
            _pad = Ui.Div(_skiing, "touch-pad");
            _pad.pickingMode = PickingMode.Position;
            Ui.Text(_pad, "STEER", "touch-pad-label").pickingMode = PickingMode.Ignore;
            _knob = Ui.Div(_pad, "touch-knob");
            _knob.pickingMode = PickingMode.Ignore;
            _pad.RegisterCallback<PointerDownEvent>(e =>
            {
                if (_padPointer >= 0) return;
                _padPointer = e.pointerId; _pad.CapturePointer(e.pointerId);
                _padStart = e.localPosition;
                PlaceKnob(_padStart);
                e.StopPropagation();
            });
            _pad.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (e.pointerId != _padPointer) return;
                float dx = e.localPosition.x - _padStart.x;
                Steer = dx > 28f ? 1f : dx < -28f ? -1f : 0f;
                PlaceKnob(new Vector2(_padStart.x + Mathf.Clamp(dx, -110f, 110f), _padStart.y));
            });
            _pad.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId == _padPointer) { _pad.ReleasePointer(e.pointerId); EndPad(); } });
            _pad.RegisterCallback<PointerCaptureOutEvent>(e => { if (e.pointerId == _padPointer) EndPad(); });
            EndPad();

            // The right thumb.
            Tap(_skiing, "PUSH", "touch-btn touch-push", () => _push = true);
            Tap(_skiing, "JUMP", "touch-btn touch-jump", () => _jump = true);
            Hold(_skiing, "TUCK", "touch-btn touch-tuck", v => Tuck = v);
            Hold(_skiing, "BRAKE", "touch-btn touch-brake", v => Brake = v);
            _grab = Hold(_skiing, "GRAB", "touch-btn touch-grab", v => Grab = v);
            _talk = Hold(_skiing, "TALK", "touch-btn touch-talk", v => Talk = v);

            // Up top: pause, camera, back on course.
            _top = Ui.Div(_layer, "touch-top");
            _top.pickingMode = PickingMode.Ignore;
            Tap(_top, "II", "touch-small", () => _pause = true);
            Tap(_skiing, "CAM", "touch-small touch-cam", () => _camera = true);
            Tap(_skiing, "RESET", "touch-small touch-reset", () => _reset = true);

            _turn = Ui.Text(ui.TopLayer, "TURN SIDEWAYS", "touch-turn");
            _turn.pickingMode = PickingMode.Ignore;
            _turn.style.display = DisplayStyle.None;
            SetMode(Mode.Hidden);
        }

        void PlaceKnob(Vector2 at)
        {
            _knob.style.left = at.x - 60f;
            _knob.style.top = at.y - 60f;
            _knob.style.opacity = 1f;
        }

        void EndPad()
        {
            _padPointer = -1;
            Steer = 0f;
            _knob.style.opacity = 0.35f;
            _knob.style.left = StyleKeyword.Null;     // back to its place in the middle of the pad (the style sheet's)
            _knob.style.top = StyleKeyword.Null;
        }

        /// <summary>A button that acts the moment it is touched.</summary>
        static VisualElement Tap(VisualElement parent, string label, string classes, System.Action act)
        {
            var b = Ui.Div(parent, classes);
            b.pickingMode = PickingMode.Position;
            Ui.Text(b, label, "touch-label").pickingMode = PickingMode.Ignore;
            b.RegisterCallback<PointerDownEvent>(e => { act(); b.AddToClassList("touch-down"); e.StopPropagation(); });
            b.RegisterCallback<PointerUpEvent>(_ => b.RemoveFromClassList("touch-down"));
            b.RegisterCallback<PointerLeaveEvent>(_ => b.RemoveFromClassList("touch-down"));
            return b;
        }

        /// <summary>A button that is on while a finger holds it.</summary>
        static VisualElement Hold(VisualElement parent, string label, string classes, System.Action<bool> set)
        {
            var b = Ui.Div(parent, classes);
            b.pickingMode = PickingMode.Position;
            Ui.Text(b, label, "touch-label").pickingMode = PickingMode.Ignore;
            int pointer = -1;
            void Release() { pointer = -1; set(false); b.RemoveFromClassList("touch-down"); }
            b.RegisterCallback<PointerDownEvent>(e =>
            {
                if (pointer >= 0) return;
                pointer = e.pointerId; b.CapturePointer(e.pointerId);
                set(true); b.AddToClassList("touch-down");
                e.StopPropagation();
            });
            b.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId != pointer) return; b.ReleasePointer(e.pointerId); Release(); });
            b.RegisterCallback<PointerCaptureOutEvent>(e => { if (e.pointerId == pointer) Release(); });
            return b;
        }

        void SetMode(Mode m)
        {
            if (m == _mode && _modeSet) return;
            _mode = m; _modeSet = true;
            _layer.style.display = m == Mode.Hidden ? DisplayStyle.None : DisplayStyle.Flex;
            _skiing.style.display = m == Mode.Skiing ? DisplayStyle.Flex : DisplayStyle.None;
            if (m != Mode.Skiing) { Steer = 0f; Tuck = Brake = Grab = Talk = false; _push = _jump = _reset = _camera = false; _look = Vector2.zero; }
        }

        void Update()
        {
            if (!Device.Touch) { if (_layer != null) SetMode(Mode.Hidden); return; }
            if (_layer == null) { if (Ui.Instance == null || Ui.Instance.Root == null) return; Build(); }
            var app = GameApp.Instance;
            Mode want = Mode.Hidden;
            if (app != null)
            {
                if (app.Screen == GameScreen.Run && app.Run != null) want = app.Run.Watching ? Mode.PauseOnly : Mode.Skiing;
                else if (app.Screen == GameScreen.Tavern) want = Mode.PauseOnly;
            }
            if (Dialogs.Open && want == Mode.Skiing) want = Mode.PauseOnly;
            SetMode(want);
            if (_mode == Mode.Skiing)
            {
                bool park = app.Run.Session != null && app.Run.Session.Slope == Ski.Core.SlopeId.Blue;
                _grab.style.display = park ? DisplayStyle.Flex : DisplayStyle.None;
                _talk.style.display = SaveData.Voice && SaveData.Live && LiveRooms.Others.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            // Held upright the game cannot be played: ask for the phone to be turned.
            _turn.style.display = Screen.height > Screen.width ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
