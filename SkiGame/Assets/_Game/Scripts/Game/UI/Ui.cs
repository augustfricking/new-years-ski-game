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

        public static string ColourTag(Color c, string text) => "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + text + "</color>";

        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
