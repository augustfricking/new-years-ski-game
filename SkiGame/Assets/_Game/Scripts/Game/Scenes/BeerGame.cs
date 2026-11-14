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
        public const float BartenderRecord = 7.5f;

        readonly TavernScene _tavern;
        readonly Person _bartender;
        readonly Action _onClose;
        ChugGame _game;
        VisualElement _ui, _result;
        ZoneBar _bar;
        Label _status, _timer;
        Transform _stein, _beer, _foam;
        float _countdown = 1.6f, _raise, _flash;
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
                Ui.Keys(panel, "SPACE", "gulp when the marker is in the green");
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
            beer.Col = Palette.Lamp; beer.Cylinder(Vector3.zero, 0.062f, 0.066f, 0.2f, 12);
            _beer = beer.ToObject("Beer", Mats.Glow(Color.white, 1.3f), _stein, false, false).transform;
            var foam = new MeshBuilder();
            foam.Col = Palette.White; foam.Cylinder(Vector3.zero, 0.068f, 0.06f, 0.04f, 12);
            _foam = foam.ToObject("Foam", Mats.Props, _stein, false).transform;
            _beer.localPosition = new Vector3(0, 0.012f, 0);
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
            bool leave = kb != null && (kb.escapeKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame);

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
                        SetLevel(_game.Level);
                        if (_status != null) _status.text = _game.Gulps >= ChugGame.GulpsToFinish - 2 && !_game.Finished ? "ALMOST..." : "CHUG!";
                    }
                    else
                    {
                        AudioDirector.Sfx("gate_miss", 0.6f, 1.3f);
                        if (_status != null) _status.text = "SPILLED!  +" + ChugGame.SpillPenalty.ToString("0.0") + " s";
                    }
                }
                if (_game.Finished) Finish();
            }

            // Stein: raised to the mouth while drinking, tipped further as it empties.
            bool drinking = _game.Running;
            _raise = Mathf.MoveTowards(_raise, drinking ? 1f : 0f, dt * 3.5f);
            Tilt = Mathf.Lerp(Tilt, drinking ? 0.25f + (1f - _game.Level) * 0.75f : 0f, 1f - Mathf.Exp(-6f * dt));
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 4f);
            _stein.localPosition = Vector3.Lerp(new Vector3(0.26f, -0.3f, 0.55f), new Vector3(0.17f, -0.2f + _flash * 0.015f, 0.46f), _raise);
            _stein.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -18f - Tilt * 42f, _raise), -25f, Mathf.Lerp(0f, 12f, _raise));

            if (_bar != null)
            {
                _bar.Value = _game.Marker; _bar.ZoneMin = _game.ZoneMin; _bar.ZoneMax = _game.ZoneMax;
                _bar.Fill = 1f - _game.Level;
                _bar.MarkDirtyRepaint();
                _timer.text = _game.Time.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s";
            }
        }

        void Finish()
        {
            float t = _game.Time;
            bool best = SaveData.BestChug <= 0f || t < SaveData.BestChug;
            bool beatBar = t < BartenderRecord;
            if (best) { SaveData.BestChug = t; SaveData.Save(); }
            _tavern.Wobble = Mathf.Min(_tavern.Wobble + 1.6f, 6f);
            AudioDirector.Sfx("burp", 0.9f);
            AudioDirector.Sfx("cheer", beatBar ? 1f : 0.6f);
            _bartender.Target = beatBar ? Pose.Cheer : Pose.Stand.WithArms(40, 10, 70, 40, 10, 70);
            if (_status != null) _status.text = "";
            var ui = Ui.Instance;
            if (ui == null) return;
            _result = Ui.Div(_ui, "dim");
            _result.pickingMode = PickingMode.Ignore;
            var card = Ui.Div(_result, "card shadowed dialog tilt-left");
            Ui.Text(card, beatBar ? "SKÅL! NEW HOUSE RECORD!" : best ? "SKÅL! PERSONAL BEST!" : "SKÅL!", "h1");
            Ui.Text(card, t.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s", "results-big");
            Ui.Text(card, _game.Spills == 0 ? "Not a drop spilled." : _game.Spills + " spill" + (_game.Spills > 1 ? "s" : "") + " (+" + (_game.Spills * ChugGame.SpillPenalty).ToString("0.0") + " s)", "small");
            Ui.Text(card, "Your best: " + SaveData.BestChug.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s     Bartender's record: " + BartenderRecord.ToString("0.0") + " s", "small");
            var row = Ui.Div(card);
            Ui.Keys(row, "SPACE", "another one");
            Ui.Keys(row, "E", "that's enough");
        }

        public void Dispose()
        {
            _ui?.RemoveFromHierarchy();
            if (_stein != null) UnityEngine.Object.Destroy(_stein.gameObject);
            _bartender.Target = Pose.Stand.WithArms(40, 10, 70, 40, 10, 70);
        }
    }
}
