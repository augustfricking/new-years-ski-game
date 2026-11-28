using Ski.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ski.Game
{
    /// <summary>
    /// Playing with fingers (phones and tablets, see <see cref="Device"/>). Dragging anywhere that is not
    /// a control looks around; several fingers at once, each on its own control.
    ///
    /// On a run, the left thumb steers with the knob of a small round stick: left of the middle turns left,
    /// right of it turns right, and only letting go goes straight (<see cref="TouchZones"/>). Its ring shows
    /// at the start of a run and after a while untouched; the knob always. The right thumb pushes off with
    /// the poles (in time with the ring, as the click), jumps, and holds tuck or brake, with grab (in the
    /// park) and talk (when others are about) beside them. A row of small buttons along the top edge: back
    /// on course, the camera, and settings (the pause menu). In the air the same controls do the tricks, as the keys do.
    ///
    /// One touch is one press. Unity tells the interface a finger pressed again each time the finger shifts
    /// on the glass (the touch goes from began to moved or still), so a finger already on a button counts
    /// again only after it lifts (<see cref="Press"/>); otherwise the camera switched and switched back. A
    /// phone's browser may also follow a tap with a pretend mouse click: that copy is dropped everywhere.
    ///
    /// On foot (the tavern), the same stick walks (any way, slower for a small push); the right thumb uses
    /// what is in reach (sit, a beer, a song, the door...), punches and jumps.
    ///
    /// Everything keeps to the corners and the top edge, off the middle of the screen, the speed, the time
    /// and the push ring. The run and the tavern read what the fingers say; nothing here moves anyone.
    /// </summary>
    public sealed class TouchControls : MonoBehaviour
    {
        public static TouchControls I { get; private set; }
        /// <summary>The skiing controls are up (a finger device, on a run).</summary>
        public static bool Active => I != null && I._mode == Mode.Skiing;
        /// <summary>The walking controls are up (a finger device, in the tavern).</summary>
        public static bool OnFoot => I != null && I._mode == Mode.Walking;

        enum Mode { Hidden, Skiing, Walking, PauseOnly }

        /// <summary>Steering: -1 left, 0, 1 right.</summary>
        public float Steer => _steer != null ? _steer.Steer : 0f;
        /// <summary>Walking: x sideways, y forward, each -1..1 (shorter for a small push).</summary>
        public Vector2 Move => _walk != null ? _walk.Analog : Vector2.zero;
        public bool Tuck { get; private set; }
        public bool Brake { get; private set; }
        public bool Grab { get; private set; }
        public bool Talk { get; private set; }

        bool _push, _jump, _reset, _camera, _pause, _use, _punch;
        Vector2 _look;
        Mode _mode = Mode.Hidden;
        bool _modeSet;
        Stick _steer, _walk;
        object _run;      // the run the stick last saw (a new one shows its ring again)
        VisualElement _layer, _lookArea, _skiing, _walking, _right, _top, _turn, _grab, _talk, _useBtn, _resetBtn, _camBtn, _settingsBtn,
            _backBtn, _tuckBtn, _brakeBtn;
        /// <summary>When a finger last touched the interface (a mouse right after it is the browser's copy).</summary>
        static float _lastFinger = -10f;

        /// <summary>A tap since the last time it was asked for (each is taken once).</summary>
        public bool TakePush() { bool b = _push; _push = false; return b; }
        public bool TakeJump() { bool b = _jump; _jump = false; return b; }
        public bool TakeReset() { bool b = _reset; _reset = false; return b; }
        public bool TakeCamera() { bool b = _camera; _camera = false; return b; }
        /// <summary>Settings (the pause menu, with the settings in it), or back out of a replay: as Esc.</summary>
        public bool TakePause() { bool b = _pause; _pause = false; return b; }
        public bool TakeUse() { bool b = _use; _use = false; return b; }
        public bool TakePunch() { bool b = _punch; _punch = false; return b; }
        /// <summary>How far the looking finger has moved since the last time (in interface units).</summary>
        public Vector2 TakeLook() { var d = _look; _look = Vector2.zero; return d; }

        public static TouchControls Create(Transform parent)
        {
            var go = new GameObject("TouchControls");
            go.transform.SetParent(parent, false);
            return I = go.AddComponent<TouchControls>();
        }

        /// <summary>
        /// A small round stick: the knob follows the thumb out from the middle of the ring (as far as the
        /// ring), and springs back when it lets go. Steering reads which half the knob is in; walking reads
        /// how far and which way. The ring (and its line) shows only while the stick rests.
        /// </summary>
        sealed class Stick
        {
            public float Steer;
            public Vector2 Analog;
            readonly bool _halves;
            readonly VisualElement _area, _guide, _knob;
            int _pointer = -1, _side;
            float _idle = Rest, _shown = 1f;
            /// <summary>Seconds untouched before the ring comes back.</summary>
            const float Rest = 10f;
            /// <summary>How far the knob goes from the middle, in interface units (the ring's radius less a little).</summary>
            const float Reach = 100f;

            public Stick(VisualElement parent, bool halves, string label)
            {
                _halves = halves;
                _area = Ui.Div(parent, "touch-stick");     // takes the thumb a little outside the ring too
                _area.pickingMode = PickingMode.Position;
                _guide = Ui.Div(_area, "touch-stick-guide");
                var ring = Ui.Div(_guide, "touch-stick-ring");
                if (halves) Ui.Div(ring, "touch-stick-split");     // the line between left and right
                if (label != null) Ui.Text(_guide, label, "touch-stick-label touch-stick-up");
                _knob = Ui.Div(_area, "touch-stick-knob");
                Ignore(_guide); Ignore(_knob);

                _area.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (_pointer >= 0) return;
                    _pointer = e.pointerId; _area.CapturePointer(e.pointerId);
                    _side = 0;
                    At(e.localPosition, true);
                    e.StopPropagation();
                });
                _area.RegisterCallback<PointerMoveEvent>(e => { if (e.pointerId == _pointer) At(e.localPosition, false); });
                _area.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId == _pointer) { _area.ReleasePointer(e.pointerId); Lift(); } });
                _area.RegisterCallback<PointerCaptureOutEvent>(e => { if (e.pointerId == _pointer) Lift(); });
            }

            static void Ignore(VisualElement e)
            {
                e.pickingMode = PickingMode.Ignore;
                foreach (var c in e.Children()) Ignore(c);
            }

            void At(Vector2 p, bool first)
            {
                Vector2 d = p - _area.layout.size * 0.5f;          // from the middle; down the screen is +y
                Vector2 k = Vector2.ClampMagnitude(d, Reach);
                _knob.style.translate = new Translate(k.x, k.y);
                Vector2 v = new Vector2(d.x, -d.y) / Reach;         // up the screen is forward
                if (_halves) Steer = _side = TouchZones.Side(v.x, _side, !first);
                else Analog = v.magnitude < TouchZones.WalkDead ? Vector2.zero : Vector2.ClampMagnitude(v, 1f);
            }

            void Lift()
            {
                _pointer = -1; _idle = 0f; _side = 0;
                Steer = 0f; Analog = Vector2.zero;
                _knob.style.translate = new Translate(0, 0);
            }

            /// <summary>Let go of everything (the controls went away), as a lifted thumb.</summary>
            public void Drop()
            {
                if (_pointer >= 0 && _area.HasPointerCapture(_pointer)) _area.ReleasePointer(_pointer);
                if (_pointer >= 0) Lift();
            }

            /// <summary>A new run: show the ring again.</summary>
            public void Fresh() { _idle = Rest; }

            public void Tick(float dt)
            {
                if (_pointer < 0) _idle += dt;
                float want = _pointer < 0 && _idle >= Rest ? 1f : 0f;
                // Fades away quickly under the thumb, comes back slowly.
                _shown = Mathf.MoveTowards(_shown, want, dt * (want > _shown ? 1.2f : 4f));
                _guide.style.opacity = _shown;
            }
        }

        void Build()
        {
            var ui = Ui.Instance;
            _layer = new VisualElement { pickingMode = PickingMode.Ignore };
            _layer.AddToClassList("layer");
            _layer.AddToClassList("touch-layer");
            // Under the dialogs (a dialog takes the fingers), over the HUD.
            ui.Root.Insert(ui.Root.IndexOf(ui.DialogLayer), _layer);

            // The browser's pretend mouse after a tap: dropped before it reaches anything (see the class notes).
            ui.Root.RegisterCallback<PointerDownEvent>(e => DropCopy(e, e.pointerType), TrickleDown.TrickleDown);
            ui.Root.RegisterCallback<PointerUpEvent>(e => DropCopy(e, e.pointerType), TrickleDown.TrickleDown);
            ui.Root.RegisterCallback<PointerMoveEvent>(e => DropCopy(e, e.pointerType), TrickleDown.TrickleDown);
            ui.Root.RegisterCallback<ClickEvent>(e => DropCopy(e, e.pointerType), TrickleDown.TrickleDown);

            // Looking around: anywhere that is not a control.
            _lookArea = Ui.Div(_layer, "touch-look");
            _lookArea.pickingMode = PickingMode.Position;
            int lookPointer = -1;
            _lookArea.RegisterCallback<PointerDownEvent>(e => { if (lookPointer >= 0) return; lookPointer = e.pointerId; _lookArea.CapturePointer(e.pointerId); });
            _lookArea.RegisterCallback<PointerMoveEvent>(e => { if (e.pointerId == lookPointer) _look += (Vector2)e.deltaPosition; });
            _lookArea.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId != lookPointer) return; _lookArea.ReleasePointer(e.pointerId); lookPointer = -1; });
            _lookArea.RegisterCallback<PointerCaptureOutEvent>(e => { if (e.pointerId == lookPointer) lookPointer = -1; });

            // Left thumb: on a run the steering stick, on foot the walking one.
            _skiing = Ui.Div(_layer, "layer");
            _skiing.pickingMode = PickingMode.Ignore;
            _steer = new Stick(_skiing, true, "STEER");
            _walking = Ui.Div(_layer, "layer");
            _walking.pickingMode = PickingMode.Ignore;
            _walk = new Stick(_walking, false, "WALK");

            // Right thumb: the same corner on a run and on foot, the buttons that do not apply hidden.
            _right = Ui.Div(_layer, "layer");
            _right.pickingMode = PickingMode.Ignore;
            _pushBtn = Tap(_right, "PUSH", "touch-btn touch-main", () => _push = true);
            _useBtn = Tap(_right, "USE", "touch-btn touch-main", () => _use = true);
            Tap(_right, "JUMP", "touch-btn touch-jump", () => _jump = true);
            _tuckBtn = Hold(_right, "TUCK", "touch-btn touch-side", v => Tuck = v);
            _brakeBtn = Hold(_right, "BRAKE", "touch-btn touch-brake", v => Brake = v);
            _grab = Hold(_right, "GRAB", "touch-btn touch-grab", v => Grab = v);
            _punchBtn = Tap(_right, "PUNCH", "touch-btn touch-side", () => _punch = true);
            _talk = Hold(_right, "TALK", "touch-btn touch-talk", v => Talk = v);

            // Along the top edge, between the time and the speed.
            _top = Ui.Div(_layer, "touch-top");
            _top.pickingMode = PickingMode.Ignore;
            _resetBtn = Tap(_top, "RESET", "touch-small", () => _reset = true);
            _camBtn = Tap(_top, "CAM", "touch-small", () => _camera = true);
            // The settings are in the pause menu (with restart and leave), so one button opens both.
            _settingsBtn = Tap(_top, "SETTINGS", "touch-small", () => _pause = true);
            _backBtn = Tap(_top, "BACK", "touch-small", () => _pause = true);     // out of a replay

            _turn = Ui.Text(ui.TopLayer, "TURN SIDEWAYS", "touch-turn");
            _turn.pickingMode = PickingMode.Ignore;
            _turn.style.display = DisplayStyle.None;
            SetMode(Mode.Hidden);
        }

        VisualElement _pushBtn, _punchBtn;

        /// <summary>A finger was here: remember when. A mouse within a second of it is the browser's copy of the tap: stopped.</summary>
        static void DropCopy(EventBase e, string pointerType)
        {
            if (pointerType == UnityEngine.UIElements.PointerType.touch) { _lastFinger = Time.unscaledTime; return; }
            if (pointerType == UnityEngine.UIElements.PointerType.mouse && Device.Touch && Time.unscaledTime - _lastFinger < 1f) e.StopImmediatePropagation();
        }

        /// <summary>A button that acts the moment it is touched.</summary>
        static VisualElement Tap(VisualElement parent, string label, string classes, System.Action act)
        {
            var b = Ui.Div(parent, classes);
            b.pickingMode = PickingMode.Position;
            Ui.Text(b, label, "touch-label").pickingMode = PickingMode.Ignore;
            new Press(b, () => { act(); b.AddToClassList("touch-down"); }, () => b.RemoveFromClassList("touch-down"), true);
            return b;
        }

        /// <summary>A button that is on while a finger holds it.</summary>
        static VisualElement Hold(VisualElement parent, string label, string classes, System.Action<bool> set)
        {
            var b = Ui.Div(parent, classes);
            b.pickingMode = PickingMode.Position;
            Ui.Text(b, label, "touch-label").pickingMode = PickingMode.Ignore;
            new Press(b, () => { set(true); b.AddToClassList("touch-down"); }, () => { set(false); b.RemoveFromClassList("touch-down"); }, false);
            return b;
        }

        /// <summary>Calls <paramref name="act"/> once for each new touch of <paramref name="el"/> (see <see cref="Press"/>).</summary>
        public static void OnPress(VisualElement el, System.Action act) => new Press(el, act, null, true);

        /// <summary>
        /// One press per touch. Unity's interface input hears a finger's press again every time the finger's
        /// touch changes phase (began, then moved or still: any shift of the thumb), with the same pointer, so
        /// a pointer already down here is not a new press; the next one counts after it lifts. A press right
        /// after a lift is the browser's copy of the tap. A tap whose lift was never heard (the button was
        /// hidden under the finger) is let go once no finger or mouse button is down any more (not a hold:
        /// a held tuck must never drop by itself).
        /// </summary>
        sealed class Press
        {
            static readonly System.Collections.Generic.List<Press> Live = new System.Collections.Generic.List<Press>();
            readonly System.Action _down, _up;
            int _held = -1;
            float _downAt, _upAt = -10f;

            public Press(VisualElement el, System.Action down, System.Action up, bool settles)
            {
                _down = down; _up = up;
                if (settles)
                {
                    if (el.panel != null) Live.Add(this);
                    el.RegisterCallback<AttachToPanelEvent>(_ => { if (!Live.Contains(this)) Live.Add(this); });
                }
                el.RegisterCallback<DetachFromPanelEvent>(_ => { Live.Remove(this); Lift(); });
                el.RegisterCallback<PointerDownEvent>(e =>
                {
                    e.StopPropagation();
                    if (_held >= 0) return;                              // this finger again (or another one on it already)
                    if (Time.unscaledTime - _upAt < 0.1f) return;        // the copy of the tap that just lifted
                    _held = e.pointerId; _downAt = Time.unscaledTime;
                    el.CapturePointer(e.pointerId);
                    _down();
                });
                el.RegisterCallback<PointerUpEvent>(e =>
                {
                    if (e.pointerId != _held) return;
                    if (el.HasPointerCapture(e.pointerId)) el.ReleasePointer(e.pointerId);
                    Lift();
                });
                el.RegisterCallback<PointerCaptureOutEvent>(e => { if (e.pointerId == _held) Lift(); });
            }

            void Lift()
            {
                if (_held < 0) return;
                _held = -1; _upAt = Time.unscaledTime;
                _up?.Invoke();
            }

            /// <summary>Lets go of presses whose lift went unheard. Run once a frame.</summary>
            public static void Settle()
            {
                if (Live.Count == 0 || AnyDown()) return;
                for (int i = Live.Count - 1; i >= 0; i--)
                    if (Live[i]._held >= 0 && Time.unscaledTime - Live[i]._downAt > 0.25f) Live[i].Lift();
            }

            static bool AnyDown()
            {
                var ts = UnityEngine.InputSystem.Touchscreen.current;
                if (ts != null) foreach (var t in ts.touches) if (t.press.isPressed) return true;
                var m = UnityEngine.InputSystem.Mouse.current;
                return m != null && m.leftButton.isPressed;
            }
        }

        static void Show(VisualElement e, bool on) => e.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;

        void SetMode(Mode m)
        {
            if (m == _mode && _modeSet) return;
            _mode = m; _modeSet = true;
            bool ski = m == Mode.Skiing, foot = m == Mode.Walking;
            Show(_layer, m != Mode.Hidden);
            Show(_lookArea, ski || foot);
            Show(_skiing, ski);
            Show(_walking, foot);
            Show(_right, ski || foot);
            Show(_pushBtn, ski);
            Show(_tuckBtn, ski);
            Show(_brakeBtn, ski);
            Show(_punchBtn, foot);
            Show(_resetBtn, ski);
            Show(_camBtn, ski);
            Show(_settingsBtn, ski || foot);     // not over a dialog (it has its own buttons)
            // Whatever was held or tapped belongs to the controls that went away.
            _steer?.Drop(); _walk?.Drop();
            Tuck = Brake = Grab = Talk = false;
            _push = _jump = _reset = _camera = _use = _punch = false; _look = Vector2.zero;
        }

        void Update()
        {
            if (!Device.Touch) { if (_layer != null) SetMode(Mode.Hidden); return; }
            Press.Settle();
            if (_layer == null) { if (Ui.Instance == null || Ui.Instance.Root == null) return; Build(); }
            var app = GameApp.Instance;
            Mode want = Mode.Hidden;
            if (app != null)
            {
                if (app.Screen == GameScreen.Run && app.Run != null) want = app.Run.Watching ? Mode.PauseOnly : Mode.Skiing;
                // In the tavern: walking, unless the beer game or the song list has the screen (they have their own buttons).
                else if (app.Screen == GameScreen.Tavern && app.Tavern != null) want = app.Tavern.Busy ? Mode.Hidden : Mode.Walking;
            }
            if (Dialogs.Open && (want == Mode.Skiing || want == Mode.Walking)) want = Mode.PauseOnly;
            SetMode(want);
            Show(_backBtn, _mode == Mode.PauseOnly && app != null && app.Run != null && app.Run.Watching && !Dialogs.Open);
            bool voice = SaveData.Voice && SaveData.Live && LiveRooms.Others.Count > 0;
            float dt = Time.unscaledDeltaTime;
            if (_mode == Mode.Skiing)
            {
                bool park = app.Run.Session != null && app.Run.Session.Slope == SlopeId.Blue;
                Show(_grab, park);
                Show(_useBtn, false);
                Show(_talk, voice);
                if (!ReferenceEquals(app.Run.Session, _run)) { _run = app.Run.Session; _steer.Fresh(); }
                _steer.Tick(dt);
            }
            else if (_mode == Mode.Walking)
            {
                Show(_grab, false);
                Show(_useBtn, app.Tavern.CanUse);
                Show(_talk, voice);
                _walk.Tick(dt);
            }
            // Held upright the game cannot be played: ask for the phone to be turned.
            Show(_turn, Screen.height > Screen.width);
        }
    }
}
