using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ski.Game
{
    /// <summary>Modal dialogs: settings and the pause menu.</summary>
    public static class Dialogs
    {
        static VisualElement _current;
        public static bool Open => _current != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _current = null; }

        public static void Close()
        {
            if (_current == null) return;
            _current.RemoveFromHierarchy();
            _current = null;
            SaveData.Save();
        }

        static VisualElement Begin(string title)
        {
            Close();
            var ui = Ui.Instance;
            var dim = Ui.Div(ui.DialogLayer, "dim");
            dim.pickingMode = PickingMode.Position;
            var card = Ui.Div(dim, "card shadowed dialog");
            Ui.Text(card, title, "h1");
            _current = dim;
            return card;
        }

        static void SliderRow(VisualElement parent, string label, float value, float min, float max, Action<float> set)
        {
            var row = Ui.Div(parent, "dialog-row");
            Ui.Text(row, label, "dialog-label");
            var s = new Slider(min, max) { value = value };
            s.AddToClassList("slider");
            s.RegisterValueChangedCallback(e => { set(e.newValue); SaveData.Apply(); });
            row.Add(s);
        }

        static void ToggleRow(VisualElement parent, string label, bool value, Action<bool> set)
        {
            var row = Ui.Div(parent, "dialog-row");
            Ui.Text(row, label, "dialog-label");
            Button b = null;
            b = Ui.Btn(row, value ? "ON" : "OFF", () => { value = !value; set(value); b.text = value ? "ON" : "OFF"; }, "btn-plain btn-small");
        }

        static void AddSettings(VisualElement card)
        {
            SliderRow(card, "Overall volume", SaveData.MasterVolume, 0f, 1f, v => SaveData.MasterVolume = v);
            SliderRow(card, "Music", SaveData.MusicVolume, 0f, 1f, v => SaveData.MusicVolume = v);
            SliderRow(card, "Wind and ambience", SaveData.AmbienceVolume, 0f, 1f, v => SaveData.AmbienceVolume = v);
            SliderRow(card, "Effects (skis, poles, crashes)", SaveData.EffectsVolume, 0f, 1f, v => SaveData.EffectsVolume = v);
            SliderRow(card, "Voices", SaveData.VoiceVolume, 0f, 1f, v => SaveData.VoiceVolume = v);
            SliderRow(card, "Mouse sensitivity", SaveData.MouseSensitivity, 0.03f, 0.35f, v => SaveData.MouseSensitivity = v);
            ToggleRow(card, "Invert look up/down", SaveData.InvertLook, v => SaveData.InvertLook = v);
            ToggleRow(card, "Subtitles", SaveData.Subtitles, v => SaveData.Subtitles = v);
        }

        public static void Settings(Action onClose = null)
        {
            var card = Begin("SETTINGS");
            AddSettings(card);
            var row = Ui.Div(card, "row");
            row.style.justifyContent = Justify.Center;
            Ui.Btn(row, "DONE", () => { Close(); onClose?.Invoke(); });
        }

        /// <summary>Pause menu. Null actions hide their buttons.</summary>
        public static void Pause(Action resume, Action restart, Action toMap, Action toMenu)
        {
            var card = Begin("PAUSED");
            AddSettings(card);
            var row = Ui.Div(card, "row");
            row.style.justifyContent = Justify.Center;
            row.style.flexWrap = Wrap.Wrap;
            Ui.Btn(row, "RESUME", () => { Close(); resume?.Invoke(); });
            if (restart != null) Ui.Btn(row, "RESTART RUN", () => { Close(); restart(); }, "btn-plain");
            if (toMap != null) Ui.Btn(row, "SKI MAP", () => { Close(); toMap(); }, "btn-plain");
            if (toMenu != null) Ui.Btn(row, "MAIN MENU", () => { Close(); toMenu(); }, "btn-plain");
        }
    }
}
