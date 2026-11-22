using System;
using Ski.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Ski.Game
{
    /// <summary>
    /// The view of the chug game at the bar: the stein in your hand, the timing bar, the
    /// bartender's reactions and the result card.
    /// </summary>
    public sealed class BeerGame
    {

        readonly TavernScene _tavern;
        readonly Person _bartender;
        readonly Action _onClose;
        ChugGame _game;
        VisualElement _ui, _result;
        ZoneBar _bar;
        Label _status, _timer;
        Transform _stein, _beer, _foam;
        float _countdown = 1.6f, _raise, _flash, _shown = 1f, _slosh;
        readonly Transform[] _drops = new Transform[10];
        readonly Vector3[] _dropVel = new Vector3[10];
        readonly float[] _dropLife = new float[10];
        int _round;

        /// <summary>How far the head is tipped back, 0..1.</summary>
        public float Tilt { get; private set; }
        public ChugGame Logic => _game;

        public BeerGame(TavernScene tavern, Person bartender, Action onClose)
        {
            _tavern = tavern; _bartender = bartender; _onClose = onClose;
            BuildStein();
            var ui = Ui.Instance;
            if (ui != null)
            {
                _ui = Ui.Div(ui.ScreenLayer, "layer");
                var panel = Ui.Div(_ui, "mini-panel");
                _status = Ui.Text(panel, "GET READY...", "h1 outlined");
                _timer = Ui.Text(panel, "", "h2 outlined");
                _bar = new ZoneBar();
                panel.Add(_bar);
                var gulp = Ui.Div(panel, "beer-gulp");
                Ui.Keys(gulp, "SPACE", "gulp in green");
                var keys = Ui.Div(panel, "row beer-keys");
                Ui.Keys(keys, "R", "restart");
                Ui.Keys(keys, "ESC", "leave");
            }
            Begin();
        }

        void Begin()
        {
            _round++;
            _game = new ChugGame(_round * 17 + 3);
            _countdown = 1.6f;
            _result?.RemoveFromHierarchy(); _result = null;
            if (_status != null) _status.text = "GET READY...";
            _bartender.Target = Pose.Stand.WithArms(70, 8, 30, 40, 10, 70);
            AudioDirector.Sfx("clink", 0.7f);
            _shown = 1f;
            SetLevel(1f);
        }

        void BuildStein()
        {
            // The stein is held in front of the camera, bottom right, and raised to drink.
            _stein = new GameObject("Stein").transform;
            _stein.SetParent(_tavern.Cam.transform, false);
            var glass = new MeshBuilder(); var none = new MeshBuilder();
            glass.Col = Palette.Mix(Palette.Snow, Palette.SnowShadow, 0.3f);
            glass.Cylinder(Vector3.zero, 0.07f, 0.07f, 0.012f, 12);
            glass.Tube(new Vector3(0.07f, 0.04f, 0), new Vector3(0.125f, 0.1f, 0), 0.012f, 0.012f, 5, false);
            glass.Tube(new Vector3(0.125f, 0.1f, 0), new Vector3(0.07f, 0.17f, 0), 0.012f, 0.012f, 5, false);
            // Glove holding the handle.
            glass.Col = CharacterStyle.Get(SaveData.LastCharacter).Gloves;
            glass.Sphere(new Vector3(0.125f, 0.1f, 0f), new Vector3(0.05f, 0.065f, 0.05f), 8, 5);
            glass.ToObject("Glass", Mats.Props, _stein);
            var beer = new MeshBuilder();
            beer.Col = Palette.Mix(Palette.Lamp, Palette.SafetyOrange, 0.35f); beer.Cylinder(Vector3.zero, 0.062f, 0.066f, 0.2f, 12);
            _beer = beer.ToObject("Beer", Mats.Glow(Color.white, 0.95f), _stein, false, false).transform;
            var foam = new MeshBuilder();
            foam.Col = Palette.White; foam.Cylinder(Vector3.zero, 0.068f, 0.06f, 0.04f, 12);
            _foam = foam.ToObject("Foam", Mats.Props, _stein, false).transform;
            _beer.localPosition = new Vector3(0, 0.012f, 0);
            // Drops that fly when a gulp is mistimed.
            for (int i = 0; i < _drops.Length; i++)
            {
                var d = new MeshBuilder();
                d.Col = i % 3 == 0 ? Palette.White : Palette.Lamp;
                d.Sphere(Vector3.zero, new Vector3(0.012f, 0.018f, 0.012f), 5, 4);
                _drops[i] = d.ToObject("Drop", Mats.Glow(Color.white, 1.3f), _tavern.Cam.transform, false, false).transform;
                _drops[i].gameObject.SetActive(false);
            }
        }

        void Spill()
        {
            _slosh = 1f;
            var rng = new Rng(_game.Spills * 31 + _game.Gulps + 7);
            for (int i = 0; i < _drops.Length; i++)
            {
                _drops[i].gameObject.SetActive(true);
                _drops[i].localPosition = _stein.localPosition + _stein.localRotation * new Vector3(rng.Range(-0.05f, 0.05f), 0.012f + 0.2f * _shown, rng.Range(-0.05f, 0.05f));
                _dropVel[i] = new Vector3(rng.Range(-0.5f, 0.35f), rng.Range(0.5f, 1.3f), rng.Range(-0.15f, 0.25f));
                _dropLife[i] = rng.Range(0.45f, 0.8f);
            }
        }

        void SetLevel(float level)
        {
            _beer.localScale = new Vector3(1f, Mathf.Max(level, 0.001f), 1f);
            _foam.localPosition = new Vector3(0, 0.012f + 0.2f * level, 0);
            _foam.gameObject.SetActive(level > 0.02f);
        }

        public void Tick(float dt)
        {
            var kb = Keyboard.current;
            bool press = kb != null && kb.spaceKey.wasPressedThisFrame;
            // E is for using things, so it never leaves the game by accident: Esc (or Q) leaves, R starts over.
            bool leave = kb != null && (kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame);
            bool restart = kb != null && kb.rKey.wasPressedThisFrame;

            if (restart) { Begin(); return; }
            if (_result != null)
            {
                if (press) Begin();
                else if (leave) { Dispose(); _onClose?.Invoke(); return; }
            }
            else if (_countdown > 0f)
            {
                _countdown -= dt;
                if (_countdown <= 0f) { _game.Start(); if (_status != null) _status.text = "CHUG!"; }
                else if (leave) { Dispose(); _onClose?.Invoke(); return; }
            }
            else
            {
                _game.Step(dt);
                if (press)
                {
                    if (_game.Press())
                    {
                        AudioDirector.Sfx("gulp", 0.9f, 0.9f + _game.Gulps * 0.03f);
                        _flash = 1f;
                        if (_status != null) _status.text = _game.Gulps >= ChugGame.GulpsToFinish - 2 && !_game.Finished ? "ALMOST..." : "CHUG!";
                    }
                    else
                    {
                        AudioDirector.Sfx("spill", 0.8f);
                        Spill();
                        if (_status != null) _status.text = "SPILLED!  +" + ChugGame.SpillPenalty.ToString("0.0") + " s";
                    }
                }
                if (_game.Finished) Finish();
                else if (_game.TimedOut) TooSlow();
                else if (leave) { Dispose(); _onClose?.Invoke(); return; }
            }

            // Stein: raised to the mouth while drinking, tipped further as it empties.
            bool drinking = _game.Running;
            _raise = Mathf.MoveTowards(_raise, drinking ? 1f : 0f, dt * 3.5f);
            Tilt = Mathf.Lerp(Tilt, drinking ? 0.25f + (1f - _game.Level) * 0.75f : 0f, 1f - Mathf.Exp(-6f * dt));
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 4f);
            _slosh = Mathf.MoveTowards(_slosh, 0f, dt * 2.6f);
            float jolt = Mathf.Sin(_slosh * 22f) * _slosh;
            _stein.localPosition = Vector3.Lerp(new Vector3(0.26f, -0.3f, 0.55f), new Vector3(0.17f, -0.2f + _flash * 0.015f, 0.46f), _raise) + new Vector3(jolt * 0.02f, _slosh * 0.02f, 0f);
            _stein.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -18f - Tilt * 42f, _raise), -25f, Mathf.Lerp(0f, 12f, _raise) + jolt * 14f);
            // The beer goes down a notch with every gulp, and a splash is lost with every spill.
            float want = Mathf.Clamp01(_game.Level - Mathf.Min(_game.Spills, 6) * 0.012f);
            _shown = Mathf.MoveTowards(_shown, want, dt * 0.9f);
            SetLevel(_shown);
            for (int i = 0; i < _drops.Length; i++)
            {
                if (_dropLife[i] <= 0f) continue;
                _dropLife[i] -= dt;
                _dropVel[i] += Vector3.down * 5.5f * dt;
                _drops[i].localPosition += _dropVel[i] * dt;
                if (_dropLife[i] <= 0f) _drops[i].gameObject.SetActive(false);
            }

            if (_bar != null)
            {
                _bar.Value = _game.Marker; _bar.ZoneMin = _game.ZoneMin; _bar.ZoneMax = _game.ZoneMax;
                _bar.Fill = 1f - _game.Level;
                _bar.MarkDirtyRepaint();
                _timer.text = _game.Time.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s";
            }
        }

        /// <summary>A minute went by: the bartender takes the stein back.</summary>
        void TooSlow()
        {
            AudioDirector.Sfx("spill", 0.6f, 0.8f);
            _bartender.Target = Pose.Stand.WithArms(70, 8, 30, 70, 8, 30);
            if (_status != null) _status.text = "";
            var ui = Ui.Instance;
            if (ui == null) return;
            _result = Ui.Div(_ui, "dim");
            _result.pickingMode = PickingMode.Ignore;
            var card = Ui.Div(_result, "card shadowed dialog tilt-left");
            Ui.Text(card, "TOO SLOW!", "h1");
            Ui.Text(card, "A minute is all you get.", "small");
            var row = Ui.Div(card);
            Ui.Keys(row, "SPACE", "again");
            Ui.Keys(row, "ESC", "done");
        }

        void Finish()
        {
            float t = _game.Time;
            var who = SaveData.LastCharacter;
            var res = SaveData.Chug.Submit(who, t);
            bool best = res.PersonalBest;
            bool beatBar = res.PersonalBest && !float.IsNaN(res.PreviousBest);
            if (best) { SaveData.BestChug = t; SaveData.Save(); }
            SaveData.Record(who, p => { if (p.bestChug <= 0f || t < p.bestChug) p.bestChug = t; });
            _tavern.Wobble = Mathf.Min(_tavern.Wobble + 1.6f, 6f);
            // The whole hall cheers an empty stein.
            _tavern.Cheer(beatBar ? 6f : 3.6f);
            AudioDirector.Sfx("burp", 0.9f);
            AudioDirector.Sfx("cheer", 1f);
            if (beatBar) AudioDirector.Sfx("cheer", 0.8f, 1.12f);
            _bartender.Target = beatBar ? Pose.Cheer : Pose.Stand.WithArms(40, 10, 70, 40, 10, 70);
            if (_status != null) _status.text = "";
            var ui = Ui.Instance;
            if (ui == null) return;
            _result = Ui.Div(_ui, "dim");
            _result.pickingMode = PickingMode.Ignore;
            var card = Ui.Div(_result, "card shadowed dialog tilt-left");
            var title = Ui.Text(card, beatBar ? "SKÅL! PERSONAL BEST!" : "SKÅL!", "h1");
            Ui.Text(card, t.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s", "results-big");
            Ui.Text(card, _game.Spills == 0 ? "Not a drop spilled." : _game.Spills + " spill" + (_game.Spills > 1 ? "s" : "") + " (+" + (_game.Spills * ChugGame.SpillPenalty).ToString("0.0") + " s)", "small");
            // The board: the world's fastest (one time each, the best); this tavern's own times only when the world cannot be reached.
            var list = SaveData.Chug.Entries;
            var place = Ui.Text(card, "", "h2");
            var boardTitle = Ui.Text(card, "WORLD'S FASTEST", "tiny");
            var board = Ui.Div(card, "board");
            Ui.Text(board, "Loading...", "small board-wait");
            bool shownWorld = false;
            void Local()
            {
                if (shownWorld || board.parent == null) return;
                shownWorld = true;
                boardTitle.text = "YOUR TIMES  (world board offline)";
                board.Clear();
                int shown = 0;
                for (int i = 0; i < list.Count && shown < 8; i++)
                {
                    bool mine = i == res.Rank - 1;
                    if (!mine && i >= 6 && res.Rank - 1 > i) continue;   // keep room for the player's own row
                    var line = Ui.Div(board, mine ? "board-row board-row-me" : "board-row");
                    Ui.Text(line, (i + 1) + ".", "board-rank");
                    Ui.Text(line, SaveData.BoardName(list[i]), "board-name");
                    Ui.Text(line, list[i].Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s", "board-value");
                    shown++;
                }
            }
            OnlineBoards.Report(OnlineBoards.ChugBoard, t, "", (int)who, false, true, w =>
            {
                if (board.parent == null) return;
                if (w == null || w.Rows == null) { Local(); return; }
                shownWorld = true;
                board.Clear();
                if (w.RunRank > 0) place.text = "#" + w.RunRank + " IN THE WORLD" + (!w.NewBest && w.BestRank > 0 ? "  -  YOUR BEST #" + w.BestRank : "");
                int n = 0;
                foreach (var e in w.Rows)
                {
                    if (n >= 8 && !e.Me) continue;
                    var line = Ui.Div(board, e.Me ? "board-row board-row-me" : "board-row");
                    Ui.Text(line, (e.Rank + 1) + ".", "board-rank");
                    BoardRow.Name(line, e);
                    Ui.Text(line, e.Score.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s", "board-value");
                    n++;
                }
                if (w.Rows.Count == 0) Ui.Text(board, "Nobody yet", "small board-wait");
                if (w.RunRank == 1 && w.NewBest && w.Rows.Count > 1) { title.text = "SKÅL! HOUSE RECORD!"; _tavern.Cheer(6f); }
            });
            float waited = 0f;
            System.Action<float> tick = null;
            tick = ui.AddTicker(dt => { waited += dt; if (shownWorld || board.parent == null) { ui.RemoveTicker(tick); return; } if (waited > 8f) { ui.RemoveTicker(tick); Local(); } });
            var row = Ui.Div(card);
            Ui.Keys(row, "SPACE", "again");
            Ui.Keys(row, "ESC", "done");
        }

        public void Dispose()
        {
            _ui?.RemoveFromHierarchy();
            if (_stein != null) UnityEngine.Object.Destroy(_stein.gameObject);
            _bartender.Target = Pose.Stand.WithArms(40, 10, 70, 40, 10, 70);
        }
    }
}
