using UnityEngine;
using UnityEngine.UIElements;

namespace Ski.Game
{
    /// <summary>
    /// Playing with fingers (phones and tablets, see <see cref="Device"/>). Dragging anywhere that is not
    /// a control looks around; several fingers at once, each on its own control.
    ///
    /// On a run, the left thumb rests on the steering box, nine zones like the keys around S: the left
    /// and right columns turn, the top row tucks and the bottom row brakes (so the corners do both), the
    /// middle does nothing. Its outline shows at the start of a run and after a while untouched. The right
    /// thumb pushes off with the poles (in time with the ring, as the click) and jumps; in the park a grab
    /// button joins them. Small buttons up top pause, change the camera and get back on course. In the
    /// air the same controls do the tricks, as the keys do.
    ///
    /// On foot (the tavern), the left thumb walks (any way, slower for a small push); the right uses
    /// what is in reach (sit, a beer, a song, the door...), punches and jumps.
    ///
    /// The run and the tavern read what the fingers say; nothing here moves anyone.
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
        public Vector2 Move { get; private set; }
        public bool Tuck => _steer != null && _steer.Tuck;
        public bool Brake => _steer != null && _steer.Brake;
        public bool Grab { get; private set; }
        public bool Talk { get; private set; }

        bool _push, _jump, _reset, _camera, _pause, _use, _punch;
        Vector2 _look;
        Mode _mode = Mode.Hidden;
        bool _modeSet;
        SteerBox _steer;
        object _run;      // the run the steering box last saw (a new one shows its outline again)
        VisualElement _layer, _lookArea, _skiing, _walking, _top, _turn, _grab, _talk, _walkTalk, _useBtn;

        /// <summary>A tap since the last time it was asked for (each is taken once).</summary>
        public bool TakePush() { bool b = _push; _push = false; return b; }
        public bool TakeJump() { bool b = _jump; _jump = false; return b; }
        public bool TakeReset() { bool b = _reset; _reset = false; return b; }
        public bool TakeCamera() { bool b = _camera; _camera = false; return b; }
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

        /// <summary>A pad for a thumb: it starts wherever the thumb lands, and reports how far it has moved from there.</summary>
        sealed class Pad
        {
            readonly VisualElement _box, _knob;
            Vector2 _start;
            int _pointer = -1;
            const float Reach = 110f;

            public Pad(VisualElement parent, string label, string classes, System.Action<Vector2> moved)
            {
                _box = Ui.Div(parent, classes);
                _box.pickingMode = PickingMode.Position;
                Ui.Text(_box, label, "touch-pad-label").pickingMode = PickingMode.Ignore;
                _knob = Ui.Div(_box, "touch-knob");
                _knob.pickingMode = PickingMode.Ignore;
                _box.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (_pointer >= 0) return;
                    _pointer = e.pointerId; _box.CapturePointer(e.pointerId);
                    _start = e.localPosition;
                    Place(_start);
                    e.StopPropagation();
                });
                _box.RegisterCallback<PointerMoveEvent>(e =>
                {
                    if (e.pointerId != _pointer) return;
                    Vector2 d = Vector2.ClampMagnitude((Vector2)e.localPosition - _start, Reach);
                    Place(_start + d);
                    moved(new Vector2(d.x, -d.y) / Reach);     // up the screen is forward
                });
                _box.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId == _pointer) { _box.ReleasePointer(e.pointerId); End(moved); } });
                _box.RegisterCallback<PointerCaptureOutEvent>(e => { if (e.pointerId == _pointer) End(moved); });
                End(moved);
            }

            void Place(Vector2 at)
            {
                _knob.style.left = at.x - 60f;
                _knob.style.top = at.y - 60f;
                _knob.style.opacity = 1f;
            }

            void End(System.Action<Vector2> moved)
            {
                _pointer = -1;
                moved(Vector2.zero);
                _knob.style.opacity = 0.35f;
                _knob.style.left = StyleKeyword.Null;     // back to the middle of the pad (the style sheet's place)
                _knob.style.top = StyleKeyword.Null;
            }
        }

        /// <summary>
        /// The steering box: the zone under the thumb decides (it may slide from one to the next), see
        /// <see cref="Ski.Core.TouchZones"/>. Its outline shows while it rests.
        /// </summary>
        sealed class SteerBox
        {
            public float Steer;
            public bool Tuck, Brake;
            readonly VisualElement _box, _frame;
            int _pointer = -1, _col, _row;
            float _idle = Rest, _shown = 1f;
            /// <summary>Seconds untouched before the outline comes back.</summary>
            const float Rest = 10f;

            public SteerBox(VisualElement parent)
            {
                _box = Ui.Div(parent, "touch-steer");
                _box.pickingMode = PickingMode.Position;
                _frame = Ui.Div(_box, "touch-steer-frame");
                _frame.pickingMode = PickingMode.Ignore;
                for (int i = 1; i <= 2; i++)
                {
                    var col = Ui.Div(_frame, "touch-steer-line touch-steer-col");
                    col.style.left = Length.Percent(i * 100f / 3f);
                    var row = Ui.Div(_frame, "touch-steer-line touch-steer-row");
                    row.style.top = Length.Percent(i * 100f / 3f);
                }
                Ui.Text(_frame, "TUCK", "touch-steer-label touch-steer-top");
                Ui.Text(_frame, "BRAKE", "touch-steer-label touch-steer-bottom");
                Ui.Text(_frame, "LEFT", "touch-steer-label touch-steer-left");
                Ui.Text(_frame, "RIGHT", "touch-steer-label touch-steer-right");
                foreach (var c in _frame.Children()) c.pickingMode = PickingMode.Ignore;

                _box.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (_pointer >= 0) return;
                    _pointer = e.pointerId; _box.CapturePointer(e.pointerId);
                    _col = _row = 0;
                    At(e.localPosition, 0f);
                    e.StopPropagation();
                });
                _box.RegisterCallback<PointerMoveEvent>(e => { if (e.pointerId == _pointer) At(e.localPosition, Ski.Core.TouchZones.Stick); });
                _box.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId == _pointer) { _box.ReleasePointer(e.pointerId); Lift(); } });
                _box.RegisterCallback<PointerCaptureOutEvent>(e => { if (e.pointerId == _pointer) Lift(); });
            }

            void At(Vector2 p, float stick)
            {
                var size = _box.layout.size;
                if (size.x < 1f || size.y < 1f) return;
                _col = Ski.Core.TouchZones.Zone(p.x / size.x, _col, stick);
                _row = Ski.Core.TouchZones.Zone(p.y / size.y, _row, stick);     // down the screen: top row first
                Ski.Core.TouchZones.Read(_col, _row, out Steer, out Tuck, out Brake);
            }

            void Lift()
            {
                _pointer = -1; _idle = 0f;
                Steer = 0f; Tuck = Brake = false;
            }

            /// <summary>Let go of everything (the controls went away), as a lifted thumb.</summary>
            public void Drop()
            {
                if (_pointer >= 0 && _box.HasPointerCapture(_pointer)) _box.ReleasePointer(_pointer);
                if (_pointer >= 0) Lift();
            }

            /// <summary>A new run: show the outline again.</summary>
            public void Fresh() { _idle = Rest; }

            public void Tick(float dt)
            {
                if (_pointer < 0) _idle += dt;
                float want = _pointer < 0 && _idle >= Rest ? 1f : 0f;
                // Fades away quickly under the thumb, comes back slowly.
                _shown = Mathf.MoveTowards(_shown, want, dt * (want > _shown ? 1.2f : 4f));
                _frame.style.opacity = _shown;
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

            // Looking around: anywhere that is not a control.
            _lookArea = Ui.Div(_layer, "touch-look");
            _lookArea.pickingMode = PickingMode.Position;
            int lookPointer = -1;
            _lookArea.RegisterCallback<PointerDownEvent>(e => { if (lookPointer >= 0) return; lookPointer = e.pointerId; _lookArea.CapturePointer(e.pointerId); });
            _lookArea.RegisterCallback<PointerMoveEvent>(e => { if (e.pointerId == lookPointer) _look += (Vector2)e.deltaPosition; });
            _lookArea.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId != lookPointer) return; _lookArea.ReleasePointer(e.pointerId); lookPointer = -1; });
            _lookArea.RegisterCallback<PointerCaptureOutEvent>(e => { if (e.pointerId == lookPointer) lookPointer = -1; });

            // On a run.
            _skiing = Ui.Div(_layer, "layer");
            _skiing.pickingMode = PickingMode.Ignore;
            _steer = new SteerBox(_skiing);
            Tap(_skiing, "PUSH", "touch-btn touch-push", () => _push = true);
            Tap(_skiing, "JUMP", "touch-btn touch-jump", () => _jump = true);
            _grab = Hold(_skiing, "GRAB", "touch-btn touch-grab", v => Grab = v);
            _talk = Hold(_skiing, "TALK", "touch-btn touch-talk", v => Talk = v);
            Tap(_skiing, "CAM", "touch-small touch-cam", () => _camera = true);
            Tap(_skiing, "RESET", "touch-small touch-reset", () => _reset = true);

            // On foot.
            _walking = Ui.Div(_layer, "layer");
            _walking.pickingMode = PickingMode.Ignore;
            new Pad(_walking, "WALK", "touch-pad", d => Move = d);
            _useBtn = Tap(_walking, "USE", "touch-btn touch-push", () => _use = true);
            Tap(_walking, "PUNCH", "touch-btn touch-punch", () => _punch = true);
            Tap(_walking, "JUMP", "touch-btn touch-jump", () => _jump = true);
            _walkTalk = Hold(_walking, "TALK", "touch-btn touch-talk", v => Talk = v);

            // Up top, always: pause.
            _top = Ui.Div(_layer, "touch-top");
            _top.pickingMode = PickingMode.Ignore;
            Tap(_top, "II", "touch-small", () => _pause = true);

            _turn = Ui.Text(ui.TopLayer, "TURN SIDEWAYS", "touch-turn");
            _turn.pickingMode = PickingMode.Ignore;
            _turn.style.display = DisplayStyle.None;
            SetMode(Mode.Hidden);
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

        static void Show(VisualElement e, bool on) => e.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;

        void SetMode(Mode m)
        {
            if (m == _mode && _modeSet) return;
            _mode = m; _modeSet = true;
            Show(_layer, m != Mode.Hidden);
            Show(_lookArea, m == Mode.Skiing || m == Mode.Walking);
            Show(_skiing, m == Mode.Skiing);
            Show(_walking, m == Mode.Walking);
            // Whatever was held or tapped belongs to the controls that went away.
            _steer?.Drop();
            Move = Vector2.zero; Grab = Talk = false;
            _push = _jump = _reset = _camera = _use = _punch = false; _look = Vector2.zero;
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
                // In the tavern: walking, unless the beer game or the song list has the screen (they have their own buttons).
                else if (app.Screen == GameScreen.Tavern && app.Tavern != null) want = app.Tavern.Busy ? Mode.Hidden : Mode.Walking;
            }
            if (Dialogs.Open && (want == Mode.Skiing || want == Mode.Walking)) want = Mode.PauseOnly;
            SetMode(want);
            bool voice = SaveData.Voice && SaveData.Live && LiveRooms.Others.Count > 0;
            if (_mode == Mode.Skiing)
            {
                bool park = app.Run.Session != null && app.Run.Session.Slope == Ski.Core.SlopeId.Blue;
                Show(_grab, park);
                Show(_talk, voice);
                if (!ReferenceEquals(app.Run.Session, _run)) { _run = app.Run.Session; _steer.Fresh(); }
                _steer.Tick(Time.unscaledDeltaTime);
            }
            else if (_mode == Mode.Walking)
            {
                Show(_useBtn, app.Tavern.CanUse);
                Show(_walkTalk, voice);
            }
            // Held upright the game cannot be played: ask for the phone to be turned.
            Show(_turn, Screen.height > Screen.width);
        }
    }
}
