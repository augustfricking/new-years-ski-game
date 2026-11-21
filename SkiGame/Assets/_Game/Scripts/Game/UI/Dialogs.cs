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
            var s = new Slider(min, max) { value = value, fill = true };
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

        static int _section;
        static readonly string[] Sections = { "AUDIO", "GRAPHICS", "CONTROLS", "ONLINE" };

        /// <summary>The settings, in sections chosen with tabs along the top.</summary>
        static void AddSettings(VisualElement card)
        {
            var tabs = Ui.Div(card, "row settings-tabs");
            // The body scrolls when a section is taller than the screen allows (audio with a walkman, the keys).
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("settings-scroll");
            card.Add(scroll);
            var body = scroll.contentContainer;
            body.AddToClassList("settings-body");
            var buttons = new Button[Sections.Length];
            void Show(int i)
            {
                _section = i;
                for (int k = 0; k < buttons.Length; k++) buttons[k].EnableInClassList("btn-selected", k == i);
                body.Clear();
                switch (i)
                {
                    case 0: AudioSection(body); break;
                    case 1: GraphicsSection(body); break;
                    case 2: ControlsSection(body); break;
                    default: OnlineSection(body); break;
                }
            }
            for (int i = 0; i < Sections.Length; i++)
            {
                int k = i;
                buttons[i] = Ui.Btn(tabs, Sections[i], () => Show(k), "btn-plain btn-small");
            }
            Show(_section);
        }

        static void AudioSection(VisualElement card)
        {
            SliderRow(card, "Overall volume", SaveData.MasterVolume, 0f, 1f, v => SaveData.MasterVolume = v);
            SliderRow(card, "Voice chat", SaveData.VoiceChatVolume, 0f, 1f, v => SaveData.VoiceChatVolume = v);
            SliderRow(card, "Effects", SaveData.EffectsVolume, 0f, 1f, v => SaveData.EffectsVolume = v);
            SliderRow(card, "Voices", SaveData.VoiceVolume, 0f, 1f, v => SaveData.VoiceVolume = v);
            SliderRow(card, "Songs", SaveData.SongVolume, 0f, 1f, v => SaveData.SongVolume = v);
            SliderRow(card, "Music", SaveData.MusicVolume, 0f, 1f, v => SaveData.MusicVolume = v);
            SliderRow(card, "Wind and ambience", SaveData.AmbienceVolume, 0f, 1f, v => SaveData.AmbienceVolume = v);
            ToggleRow(card, "Subtitles", SaveData.Subtitles, v => SaveData.Subtitles = v);
            // The walkman: on or off, which song, again and again or the next one.
            if (SaveData.HasWalkman && Songs.All.Count > 0)
            {
                ToggleRow(card, "Walkman (N)", SaveData.WalkmanOn, v => { SaveData.WalkmanOn = v; SaveData.Save(); AudioDirector.I?.RefreshMusic(true); });
                var wrow = Ui.Div(card, "dialog-row");
                Ui.Text(wrow, "Song (M)", "dialog-label");
                Button songBtn = null;
                songBtn = Ui.Btn(wrow, Songs.Get(Walkman.Current)?.title ?? "-", () => { Walkman.Next(); songBtn.text = Songs.Get(Walkman.Current)?.title ?? "-"; }, "btn-plain btn-small");
                ToggleRow(card, "Repeat song", SaveData.WalkmanRepeat, v => SaveData.WalkmanRepeat = v);
            }
            // The songs' model asks to be named (CC BY-NC 4.0 with its grant to individual creators).
            Ui.Text(card, "Songs made with YuE2 (m-a-p/YuE2-3B)", "small");
        }

        static void GraphicsSection(VisualElement card)
        {
            var row = Ui.Div(card, "dialog-row");
            Ui.Text(row, "Quality", "dialog-label");
            var pick = Ui.Div(row, "row");
            var qs = new Button[GraphicsQuality.Names.Length];
            for (int i = 0; i < qs.Length; i++)
            {
                int k = i;
                qs[i] = Ui.Btn(pick, GraphicsQuality.Names[i], () =>
                {
                    SaveData.Quality = k;
                    for (int j = 0; j < qs.Length; j++) qs[j].EnableInClassList("btn-selected", j == k);
                    GraphicsQuality.Apply();
                }, "btn-plain btn-small");
                qs[i].EnableInClassList("btn-selected", i == SaveData.Quality);
            }
            SliderRow(card, "Resolution", SaveData.RenderScale, 0.5f, 1f, v => { SaveData.RenderScale = v; GraphicsQuality.Apply(); });
            Ui.Text(card, "Lower both if the game stutters.", "small");
        }

        static void ControlsSection(VisualElement card)
        {
            SliderRow(card, "Mouse sensitivity", SaveData.MouseSensitivity, 0.03f, 0.35f, v => SaveData.MouseSensitivity = v);
            ToggleRow(card, "Invert look up/down", SaveData.InvertLook, v => SaveData.InvertLook = v);
            var cols = Ui.Div(card, "row keys-columns");
            var ski = Ui.Div(cols, "keys-column");
            Ui.Text(ski, "SKIING", "keys-heading");
            Ui.Keys(ski, "A D", "turn");
            Ui.Keys(ski, "W", "tuck");
            Ui.Keys(ski, "S", "brake");
            Ui.Keys(ski, "SPACE", "jump");
            Ui.Keys(ski, "CLICK", "pole push");
            Ui.Keys(ski, "W S", "flip in the air");
            Ui.Keys(ski, "1 2 3 4 5", "grabs, cork");
            Ui.Keys(ski, "Q E", "grab variants");
            Ui.Keys(ski, "R", "back on the line");
            Ui.Keys(ski, "C", "camera");
            var other = Ui.Div(cols, "keys-column");
            Ui.Text(other, "TAVERN", "keys-heading");
            Ui.Keys(other, "W A S D", "walk");
            Ui.Keys(other, "SPACE", "jump");
            Ui.Keys(other, "E", "use, sit, request");
            Ui.Keys(other, "CLICK", "punch");
            Ui.Keys(other, "ESC", "leave the bar game");
            Ui.Text(other, "ANYWHERE", "keys-heading");
            Ui.Keys(other, "V", "hold to talk");
            Ui.Keys(other, "M", "next song");
            Ui.Keys(other, "N", "walkman on / off");
            Ui.Keys(other, "ESC", "pause");
        }

        static void OnlineSection(VisualElement card)
        {
            // The name this device goes by on the boards.
            var row = Ui.Div(card, "dialog-row");
            Ui.Text(row, "Your name", "dialog-label");
            var name = new TextField { value = SaveData.PlayerName, maxLength = 16 };
            name.AddToClassList("name-field");
            name.RegisterValueChangedCallback(e => SaveData.PlayerName = e.newValue.Trim());
            name.RegisterCallback<FocusOutEvent>(_ => { SaveData.Save(); OnlineBoards.NameChanged(); LiveRooms.NameChanged(); });
            row.Add(name);
            // Other people playing right now: on the slopes (as ghosts) and in the tavern.
            ToggleRow(card, "Live players", SaveData.Live, v => SaveData.Live = v);
            ToggleRow(card, "Voice chat (hold V)", SaveData.Voice, v => SaveData.Voice = v);
            var grow = Ui.Div(card, "dialog-row");
            var glabel = Ui.Text(grow, "", "dialog-label");
            void Show() => glabel.text = "Skiers shown: " + SaveData.MaxGhosts;
            Show();
            var gs = new SliderInt(1, 15) { value = SaveData.MaxGhosts };
            gs.AddToClassList("slider");
            gs.RegisterValueChangedCallback(e => { SaveData.MaxGhosts = e.newValue; Show(); });
            grow.Add(gs);
            // The account: the same progress on any device.
            var head = Ui.Div(card, "row");
            Ui.Text(head, "SAVE ONLINE", "keys-heading");
            InfoButton(head, CloudSync.Why);
            AccountRows(card);
        }

        /// <summary>A small round "i": hover it and a note opens beside it.</summary>
        static void InfoButton(VisualElement parent, string text)
        {
            var b = Ui.Div(parent, "info-button");
            b.pickingMode = PickingMode.Position;
            Ui.Text(b, "i", "info-button-i").pickingMode = PickingMode.Ignore;
            // The note lives on the top layer, so nothing in the dialog covers it.
            var top = Ui.Instance.TopLayer;
            var tip = Ui.Text(top, text, "info-tip");
            tip.pickingMode = PickingMode.Ignore;
            tip.style.display = DisplayStyle.None;
            b.RegisterCallback<PointerEnterEvent>(_ =>
            {
                var r = b.worldBound;
                var at = top.WorldToLocal(new Vector2(r.xMax + 8f, r.yMin - 4f));
                tip.style.left = at.x; tip.style.top = at.y;
                tip.style.display = DisplayStyle.Flex;
                tip.BringToFront();
            });
            b.RegisterCallback<PointerLeaveEvent>(_ => tip.style.display = DisplayStyle.None);
            b.RegisterCallback<DetachFromPanelEvent>(_ => tip.RemoveFromHierarchy());
        }

        static string CloudStatus() => CloudSync.Status == CloudSync.State.Saved ? "Saved online"
            : CloudSync.Status == CloudSync.State.Syncing ? "Saving..." : CloudSync.Status == CloudSync.State.Failed ? "Saved here only" : "Saved here";

        static void AccountRows(VisualElement card)
        {
            var box = Ui.Div(card, "account");
            void Build()
            {
                box.Clear();
                if (CloudSync.Username != null)
                {
                    var r = Ui.Div(box, "dialog-row");
                    Ui.Text(r, "Signed in: " + CloudSync.Username, "dialog-label");
                    Ui.Btn(r, "SIGN OUT", async () => { await CloudSync.SignOut(); Build(); }, "btn-plain btn-small");
                    Ui.Text(box, CloudStatus(), "small");
                    return;
                }
                var user = new TextField { maxLength = 20 };
                user.AddToClassList("name-field");
                var pass = new TextField { maxLength = 30, isPasswordField = true };
                pass.AddToClassList("name-field");
                var r1 = Ui.Div(box, "dialog-row"); Ui.Text(r1, "Username", "dialog-label"); r1.Add(user);
                var r2 = Ui.Div(box, "dialog-row"); Ui.Text(r2, "Password", "dialog-label"); r2.Add(pass);
                ToggleRow(box, "Remember me", CloudSync.Remember, v => CloudSync.Remember = v);
                var r3 = Ui.Div(box, "row");
                var msg = Ui.Text(box, CloudStatus(), "small");
                async void Go(bool create)
                {
                    msg.text = "...";
                    string err = await CloudSync.Account(user.value.Trim(), pass.value, create);
                    if (err == null) Build(); else msg.text = err;
                }
                Ui.Btn(r3, "CREATE", () => Go(true), "btn-plain btn-small");
                Ui.Btn(r3, "SIGN IN", () => Go(false), "btn-plain btn-small");
            }
            Build();
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
        /// <summary>
        /// The first thing after the game loads: sign in to an account (or make one), or play as a
        /// guest under a name of your choosing. Someone already signed in to an account on this
        /// browser goes straight through.
        /// </summary>
        public static void Welcome(Action onDone = null)
        {
            var card = Begin("WELCOME");
            card.AddToClassList("welcome");
            var cols = Ui.Div(card, "row welcome-cols");

            // An account: the same progress on any device.
            var acc = Ui.Div(cols, "welcome-col");
            var accHead = Ui.Div(acc, "row");
            Ui.Text(accHead, "ACCOUNT", "keys-heading");
            InfoButton(accHead, CloudSync.Why);
            var user = new TextField { maxLength = 20 };
            user.AddToClassList("name-field");
            var pass = new TextField { maxLength = 30, isPasswordField = true };
            pass.AddToClassList("name-field");
            var r1 = Ui.Div(acc, "dialog-row"); Ui.Text(r1, "Username", "dialog-label"); r1.Add(user);
            var r2 = Ui.Div(acc, "dialog-row"); Ui.Text(r2, "Password", "dialog-label"); r2.Add(pass);
            ToggleRow(acc, "Remember me", CloudSync.Remember, v => CloudSync.Remember = v);
            var r3 = Ui.Div(acc, "row");
            var accMsg = Ui.Text(acc, "", "small");

            // Or a guest, with a name.
            var guest = Ui.Div(cols, "welcome-col welcome-guest");
            Ui.Text(guest, "GUEST", "keys-heading");
            var name = new TextField { value = SaveData.PlayerName, maxLength = 16 };
            name.AddToClassList("name-field");
            var r4 = Ui.Div(guest, "dialog-row"); Ui.Text(r4, "Your name", "dialog-label"); r4.Add(name);
            var r5 = Ui.Div(guest, "row");
            var guestMsg = Ui.Text(guest, "", "small");

            bool done = false;
            void Finish()
            {
                if (done) return;
                done = true;
                SaveData.NameAsked = true;
                Close();                       // saves
                if (SaveData.PlayerName.Length > 0) { OnlineBoards.NameChanged(); LiveRooms.NameChanged(); }
                onDone?.Invoke();
            }
            async void Account(bool create)
            {
                string u = user.value.Trim();
                if (u.Length < 3) { accMsg.text = "Name too short"; return; }
                if (pass.value.Length < 8) { accMsg.text = "Password too short"; return; }
                accMsg.text = "...";
                string err = await CloudSync.Account(u, pass.value, create);
                if (err != null) { accMsg.text = err; return; }
                if (SaveData.PlayerName.Length == 0) SaveData.PlayerName = u;
                Finish();
            }
            void Guest()
            {
                string n = name.value.Trim();
                if (n.Length < 2) { guestMsg.text = "Pick a name"; name.Focus(); return; }
                SaveData.PlayerName = n;
                Finish();
            }
            Ui.Btn(r3, "SIGN IN", () => Account(false));
            Ui.Btn(r3, "CREATE", () => Account(true), "btn-plain");
            Ui.Btn(r5, "PLAY AS GUEST", Guest);
            name.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) Guest(); }, TrickleDown.TrickleDown);
            pass.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) Account(false); }, TrickleDown.TrickleDown);

            // Already signed in to an account here (the browser remembers it): straight in.
            var ui = Ui.Instance;
            Action<float> tick = null;
            tick = ui.AddTicker(dt =>
            {
                if (done || !Open) { ui.RemoveTicker(tick); return; }
                if (CloudSync.Username != null)
                {
                    ui.RemoveTicker(tick);
                    if (SaveData.PlayerName.Length == 0) SaveData.PlayerName = CloudSync.Username;
                    Finish();
                    Ui.Instance.Notice("WELCOME BACK", CloudSync.Username, 2.5f);
                }
            });
            name.schedule.Execute(() => name.Focus()).StartingIn(100);
        }

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
