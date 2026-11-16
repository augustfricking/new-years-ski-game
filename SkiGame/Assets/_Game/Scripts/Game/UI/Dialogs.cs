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
            // Other people playing right now: on the slopes (as ghosts) and in the tavern.
            ToggleRow(card, "Live players", SaveData.Live, v => SaveData.Live = v);
            {
                var grow = Ui.Div(card, "dialog-row");
                var glabel = Ui.Text(grow, "", "dialog-label");
                void Show() => glabel.text = "Skiers shown: " + SaveData.MaxGhosts;
                Show();
                var gs = new SliderInt(1, 15) { value = SaveData.MaxGhosts };
                gs.AddToClassList("slider");
                gs.RegisterValueChangedCallback(e => { SaveData.MaxGhosts = e.newValue; Show(); });
                grow.Add(gs);
            }
            // The name this device goes by on the boards.
            var row = Ui.Div(card, "dialog-row");
            Ui.Text(row, "Your name", "dialog-label");
            var name = new TextField { value = SaveData.PlayerName, maxLength = 16 };
            name.AddToClassList("name-field");
            name.RegisterValueChangedCallback(e => SaveData.PlayerName = e.newValue.Trim());
            name.RegisterCallback<FocusOutEvent>(_ => { SaveData.Save(); OnlineBoards.NameChanged(); LiveRooms.NameChanged(); });
            row.Add(name);
        }

        public static void Settings(Action onClose = null)
        {
            var card = Begin("SETTINGS");
            AddSettings(card);
            var row = Ui.Div(card, "row");
            row.style.justifyContent = Justify.Center;
            Ui.Btn(row, "DONE", () => { Close(); onClose?.Invoke(); });
        }

        /// <summary>First launch: what should we call you? The name goes on the boards and above your ghost.</summary>
        public static void AskName(Action onDone = null)
        {
            var card = Begin("YOUR NAME?");
            Ui.Text(card, "Shown on the boards and above your ghost.", "small");
            var field = new TextField { value = SaveData.PlayerName, maxLength = 16 };
            field.AddToClassList("name-field");
            field.AddToClassList("name-field-big");
            card.Add(field);
            void Done(bool keep)
            {
                if (keep) SaveData.PlayerName = field.value.Trim();
                SaveData.NameAsked = true;
                Close();                       // saves
                if (SaveData.PlayerName.Length > 0) OnlineBoards.NameChanged();
                onDone?.Invoke();
            }
            field.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) Done(true); }, TrickleDown.TrickleDown);
            var row = Ui.Div(card, "row");
            row.style.justifyContent = Justify.Center;
            Ui.Btn(row, "OK", () => Done(true));
            Ui.Btn(row, "LATER", () => Done(false), "btn-plain");
            field.schedule.Execute(() => field.Focus()).StartingIn(100);
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
