using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ski.Game
{
    /// <summary>
    /// Root of all screen UI (UI Toolkit). Owns the layers, the fade, the goggle frame and
    /// small factory helpers so screens are built in code with classes from Game.uss.
    /// </summary>
    public sealed class Ui : MonoBehaviour
    {
        public static Ui Instance { get; private set; }

        public VisualElement Root { get; private set; }
        /// <summary>Layers, back to front.</summary>
        public VisualElement GoggleLayer, HudLayer, ScreenLayer, DialogLayer, TopLayer;

        VisualElement _fade, _goggles, _barTop, _barBottom;
        readonly List<Action<float>> _tickers = new List<Action<float>>();

        public static Ui Create(Transform parent)
        {
            var go = new GameObject("UI");
            go.transform.SetParent(parent, false);
            var ui = go.AddComponent<Ui>();
            ui.Build();
            Instance = ui;
            return ui;
        }

        void Build()
        {
            var doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = Resources.Load<PanelSettings>("UI/Panel");
            if (doc.panelSettings == null) Debug.LogError("Missing Resources/UI/Panel (run ProjectSetup).");
            Root = doc.rootVisualElement;
            Root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Game"));
            Root.AddToClassList("root");
            Root.pickingMode = PickingMode.Ignore;

            GoggleLayer = Layer("goggle-layer");
            HudLayer = Layer("hud-layer");
            ScreenLayer = Layer("screen-layer");
            DialogLayer = Layer("dialog-layer");
            TopLayer = Layer("top-layer");

            _goggles = new VisualElement { pickingMode = PickingMode.Ignore };
            _goggles.AddToClassList("goggles");
            _goggles.style.backgroundImage = GoggleTexture.Get();
            _goggles.style.display = DisplayStyle.None;
            GoggleLayer.Add(_goggles);

            _barTop = Div(TopLayer, "letterbox letterbox-top letterbox-hidden");
            _barBottom = Div(TopLayer, "letterbox letterbox-bottom letterbox-hidden");
            _fade = Div(TopLayer, "fade");
            _fade.style.opacity = 0f;
            _fade.pickingMode = PickingMode.Ignore;
        }

        /// <summary>A note that shows beside <paramref name="target"/> while the pointer is over it (on the top
        /// layer, so nothing covers it): an info button's text, how to earn a locked item.</summary>
        public static void Tooltip(VisualElement target, string text)
        {
            var top = Instance.TopLayer;
            Label tip = null;
            target.RegisterCallback<PointerEnterEvent>(_ =>
            {
                tip?.RemoveFromHierarchy();
                tip = Text(top, text, "info-tip");
                tip.pickingMode = PickingMode.Ignore;
                var r = target.worldBound;
                var at = top.WorldToLocal(new Vector2(r.xMax + 8f, r.yMin - 4f));
                // Kept on the screen: flipped to the left of the target near the right edge.
                if (at.x > top.layout.width - 360f) at = top.WorldToLocal(new Vector2(r.xMin - 8f - 340f, r.yMin - 4f));
                tip.style.left = Mathf.Max(8f, at.x); tip.style.top = at.y;
            });
            target.RegisterCallback<PointerLeaveEvent>(_ => { tip?.RemoveFromHierarchy(); tip = null; });
            target.RegisterCallback<DetachFromPanelEvent>(_ => { tip?.RemoveFromHierarchy(); tip = null; });
        }

        /// <summary>Whether the mouse is over something of the interface (a card, a button, the shop): a click
        /// there is for the interface, not for grabbing the mouse to look around. The layers themselves let clicks through.</summary>
        public bool PointerOverUi()
        {
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m == null || Root == null || Root.panel == null) return false;
            Vector2 p = m.position.ReadValue();
            Vector2 at = RuntimePanelUtils.ScreenToPanel(Root.panel, new Vector2(p.x, UnityEngine.Screen.height - p.y));
            return Root.panel.Pick(at) != null;
        }

        VisualElement Layer(string name)
        {
            var l = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            l.AddToClassList("layer");
            Root.Add(l);
            return l;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = _tickers.Count - 1; i >= 0; i--) _tickers[i](dt);
        }

        /// <summary>Registers a per-frame callback (for gauges and timed pop-ups). Returns it for removal.</summary>
        public Action<float> AddTicker(Action<float> tick) { _tickers.Add(tick); return tick; }
        public void RemoveTicker(Action<float> tick) { _tickers.Remove(tick); }

        public void ShowGoggles(bool on) { _goggles.style.display = on ? DisplayStyle.Flex : DisplayStyle.None; }

        /// <summary>A short note at the top of the screen for a few seconds ("UNLOCKED", "Goalie mask").</summary>
        public void Notice(string title, string sub, float seconds = 4f)
        {
            var box = Div(TopLayer, "notice notice-hidden");
            box.pickingMode = PickingMode.Ignore;
            Text(box, title, "notice-title");
            if (!string.IsNullOrEmpty(sub)) Text(box, sub, "notice-sub");
            float t = 0f;
            System.Action<float> tick = null;
            tick = AddTicker(dt =>
            {
                t += dt;
                if (t > 0.05f) box.RemoveFromClassList("notice-hidden");
                if (t > seconds) box.AddToClassList("notice-hidden");
                if (t > seconds + 0.6f) { box.RemoveFromHierarchy(); RemoveTicker(tick); }
            });
        }

        public void Letterbox(bool on)
        {
            _barTop.EnableInClassList("letterbox-hidden", !on);
            _barBottom.EnableInClassList("letterbox-hidden", !on);
        }

        /// <summary>Fades to ink (1) or back to the game (0).</summary>
        public void Fade(float target, float seconds = 0.5f)
        {
            _fade.style.transitionDuration = new List<TimeValue> { new TimeValue(seconds, TimeUnit.Second) };
            _fade.style.opacity = target;
        }

        public void FadeInstant(float value)
        {
            _fade.style.transitionDuration = new List<TimeValue> { new TimeValue(0f, TimeUnit.Second) };
            _fade.style.opacity = value;
        }

        // ---- kit ---------------------------------------------------------------------------

        public static void AddClasses(VisualElement e, string classes)
        {
            if (string.IsNullOrEmpty(classes)) return;
            foreach (var c in classes.Split(' ')) if (c.Length > 0) e.AddToClassList(c);
        }

        public static VisualElement Div(VisualElement parent, string classes = null)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            AddClasses(e, classes);
            parent?.Add(e);
            return e;
        }

        public static Label Text(VisualElement parent, string text, string classes = null)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            AddClasses(l, classes);
            parent?.Add(l);
            return l;
        }

        public static Button Btn(VisualElement parent, string text, Action onClick, string classes = null)
        {
            var b = new Button(() => { AudioDirector.Click(); onClick?.Invoke(); }) { text = text };
            b.AddToClassList("btn");
            AddClasses(b, classes);
            b.RegisterCallback<PointerEnterEvent>(_ => AudioDirector.Hover());
            parent?.Add(b);
            return b;
        }

        /// <summary>A row of key caps followed by what they do.</summary>
        public static VisualElement Keys(VisualElement parent, string keys, string what)
        {
            var row = Div(parent, "keys-row");
            foreach (var k in keys.Split(' '))
            {
                // The mouse is drawn, not spelled: a lit left button for a click.
                if (k == "CLICK") row.Add(new MouseIcon(MouseIcon.Lit.Left));
                else if (k == "RCLICK") row.Add(new MouseIcon(MouseIcon.Lit.Right));
                else if (k == "MOUSE") row.Add(new MouseIcon(MouseIcon.Lit.None));
                else Text(row, k, "key");
            }
            Text(row, what, "hint");
            return row;
        }

        /// <summary>
        /// The controls as they sit on the keyboard: W above A S D with what each does, the mouse to
        /// the right of them, the space bar underneath, and any other keys in a column below.
        /// </summary>
        public static VisualElement Controls(VisualElement parent, string w, string a, string s, string d, string mouseKey, string mouse, string space, params (string keys, string what)[] more)
        {
            var box = Div(parent, "controls");
            var top = Div(box, "controls-top");
            var pad = Div(top, "controls-pad");
            VisualElement Cap(VisualElement row, string key, string what)
            {
                var cell = Div(row, "controls-cell");
                Text(cell, key, "key key-square");
                Text(cell, what ?? "", "controls-what");
                return cell;
            }
            var r1 = Div(pad, "controls-keys");
            Cap(r1, "W", w);
            var r2 = Div(pad, "controls-keys");
            Cap(r2, "A", a); Cap(r2, "S", s); Cap(r2, "D", d);
            if (!string.IsNullOrEmpty(space))
            {
                var r3 = Div(pad, "controls-keys controls-space-row");
                Text(r3, "SPACE", "key key-space");
                Text(r3, space, "controls-what");
            }
            if (!string.IsNullOrEmpty(mouse))
            {
                var m = Div(top, "controls-mouse");
                m.Add(new MouseIcon(mouseKey == "CLICK" ? MouseIcon.Lit.Left : mouseKey == "RCLICK" ? MouseIcon.Lit.Right : MouseIcon.Lit.None));
                Text(m, mouse, "controls-what");
            }
            foreach (var (keys, what) in more) Keys(box, keys, what);
            return box;
        }

        public static string ColourTag(Color c, string text) => "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + text + "</color>";

        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
