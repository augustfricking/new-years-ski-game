using System;
using System.Collections;
using Ski.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Ski.Game
{
    public enum GameScreen { None, Menu, Cutscene, Map, Run, Ride, Tavern }

    /// <summary>
    /// Entry point and flow. The Main scene is empty; this object builds everything at run time:
    /// lift menu -> storm cutscene -> ski map -> a run -> village (lift back up, or the tavern).
    /// </summary>
    public sealed class GameApp : MonoBehaviour
    {
        public static GameApp Instance { get; private set; }
        public WorldLook Look { get; private set; }
        public GameScreen Screen { get; private set; }
        public CharacterId Character { get; private set; } = CharacterId.Erik;

        public RunController Run { get; private set; }
        public LiftScene Lift { get; private set; }
        public MapScene Map { get; private set; }
        public TavernScene Tavern { get; private set; }

        /// <summary>Set before Play to jump straight into a run (used while developing and by tests).</summary>
        public static SlopeId? DebugSlope;
        bool _busy;
        float _rideTimer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; DebugSlope = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (SceneManager.GetActiveScene().name != "Main" || Instance != null) return;
            new GameObject("GameApp").AddComponent<GameApp>();
        }

        void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = -1;
            Look = WorldLook.Create(transform);
            GraphicsQuality.Apply();
            // The inked contour: a touch heavier than the default, as in a comic.
            var ink = Resources.Load<Material>("Materials/InkEdges");
            if (ink != null) { ink.SetFloat("_Thickness", 2.1f); ink.SetFloat("_InnerThickness", 0.9f); ink.SetFloat("_InnerEnd", 28f); }
            AudioDirector.Create(transform);
            Ui.Create(transform);
            SaveData.Load();
            GraphicsQuality.Apply();
            SaveData.Unlocked += UnlockPopup.Show;
            OnlineBoards.Connect();
            Songs.LoadList(this, () => AudioDirector.I?.RefreshMusic());
            VoiceChat.Create(transform);   // anonymous device profile for the world boards; local play does not wait for it
            Character = SaveData.LastCharacter;
            if (DebugSlope.HasValue) StartRun(DebugSlope.Value, Character);
            else ShowMenu();
        }

        /// <summary>The shop, while it is open.</summary>
        public ShopPanel Shop { get; private set; }

        void CloseAll()
        {
            Shop?.Close();
            Shop = null;
            Dialogs.Close();
            if (Run != null) { Run.Close(); Run = null; }
            if (Lift != null) { Lift.Close(); Lift = null; }
            if (Map != null) { Map.Close(); Map = null; }
            if (Tavern != null) { Tavern.Close(); Tavern = null; }
            Look.Indoors = false;
            Cursor.lockState = CursorLockMode.None;
        }

        // ---- screens ---------------------------------------------------------------------

        static bool _welcomed;

        public void ShowMenu()
        {
            CloseAll();
            Screen = GameScreen.Menu;
            Look.Storm = 0f; Look.Night = 0f;
            LiveRooms.Enter("lift"); VoiceChat.Place = "lift";
            Lift = LiftScene.Create(transform, LiftScene.Mode.Menu);
            Lift.OnSelect = who =>
            {
                Character = who;
                SaveData.LastCharacter = who; SaveData.Save();
                // The cutscene plays once a visit; changing skier afterwards goes straight to the map.
                if (BrowserSession.Has("cutscene")) { Transition(ShowMap); return; }
                BrowserSession.Set("cutscene");
                Screen = GameScreen.Cutscene;
                Lift.PlayCutscene(who, () => { ShowMap(); Ui.Instance.Fade(0f, 0.8f); });
            };
            Ui.Instance.Fade(0f, 0.6f);
            // First thing after loading: an account or a guest name (in the Editor only until answered once).
            if (!_welcomed && (!Application.isEditor || !SaveData.NameAsked)) Dialogs.Welcome();
            _welcomed = true;
        }

        public void ShowMap()
        {
            CloseAll();
            Screen = GameScreen.Map;
            LiveRooms.Enter("lift"); VoiceChat.Place = "lift";
            Map = MapScene.Create(transform, Character);
            Map.OnPick = slope => Transition(() => StartRun(slope, Character), 0.45f);
            Map.OnWatch = (slope, entry) => Transition(() => WatchFromMap(slope, entry), 0.45f);
            Map.OnChangeSkier = () => Transition(ShowMenu);
        }

        SlopeId _lastSlope = SlopeId.Green;

        public void StartRun(SlopeId slope, CharacterId who)
        {
            CloseAll();
            Character = who;
            Screen = GameScreen.Run;
            Look.Storm = 0.06f; Look.Night = 0f;
            if (AudioDirector.I != null) { AudioDirector.I.Music("music_ski", 0.6f); AudioDirector.I.Ambience(null); }
            _lastSlope = slope;
            Run = RunController.Create(transform, slope, who);
            Cursor.lockState = CursorLockMode.Locked;   // mouse look from the first moment (browsers engage it on the first key or click)
            Run.OnLiftReached = () => Transition(ShowRide);
            Run.OnTavernReached = () => Transition(ShowTavern);
            Run.OnShopReached = () =>
            {
                if (Shop != null && Shop.IsOpen) return;
                Run.Paused = true;
                Cursor.lockState = CursorLockMode.None;
                Shop = ShopPanel.Open(transform, Character, () =>
                {
                    Shop = null;
                    if (Run == null) return;
                    Run.Rig.RebuildBody();
                    Run.Paused = false;
                    Cursor.lockState = CursorLockMode.Locked;
                });
            };
        }

        /// <summary>Someone's run from the board, on its slope; back to the map when it ends.</summary>
        void WatchFromMap(SlopeId slope, OnlineBoards.Entry entry)
        {
            StartRun(slope, Character);
            Run.InputEnabled = false;
            Cursor.lockState = CursorLockMode.None;
            if (!Run.Watch(entry, () => Transition(ShowMap))) ShowMap();
        }

        /// <summary>The short ride back up after a run; cuts to the map.</summary>
        public void ShowRide()
        {
            CloseAll();
            Screen = GameScreen.Ride;
            Look.Storm = 0.1f;
            LiveRooms.Enter("lift"); VoiceChat.Place = "lift";
            Lift = LiftScene.Create(transform, LiftScene.Mode.Ride, Character);
            _rideTimer = 3.8f;
        }

        public void ShowTavern()
        {
            CloseAll();
            Screen = GameScreen.Tavern;
            Tavern = TavernScene.Create(transform, Character);
            Tavern.OnLeave = () => Transition(() => { StartRun(_lastSlope, Character); Run.StartInVillage(); });
        }

        /// <summary>Fades to ink, swaps the screen, fades back.</summary>
        public void Transition(Action swap, float seconds = 0.35f)
        {
            if (_busy) return;
            StartCoroutine(DoTransition(swap, seconds));
        }

        IEnumerator DoTransition(Action swap, float seconds)
        {
            _busy = true;
            Ui.Instance.Fade(1f, seconds);
            yield return new WaitForSecondsRealtime(seconds + 0.05f);
            swap();
            yield return null;
            Ui.Instance.Fade(0f, seconds + 0.15f);
            _busy = false;
        }

        // ---- frame -----------------------------------------------------------------------

        UnityEngine.UIElements.Label _queue;

        /// <summary>While the world is full: our place in line (the game plays on alone meanwhile).</summary>
        void ShowQueue()
        {
            if (Ui.Instance == null) return;
            bool show = LiveRooms.Queued && LiveRooms.QueuePosition > 0;
            if (_queue == null)
            {
                if (!show) return;
                _queue = Ui.Text(Ui.Instance.TopLayer, "", "queue-badge");
                _queue.pickingMode = UnityEngine.UIElements.PickingMode.Ignore;
            }
            _queue.style.display = show ? UnityEngine.UIElements.DisplayStyle.Flex : UnityEngine.UIElements.DisplayStyle.None;
            if (show)
            {
                int n = LiveRooms.QueuePosition, of = Mathf.Max(n, LiveRooms.QueueLength);
                _queue.text = "WORLD FULL  ·  YOU ARE #" + n + " IN LINE" + (of > 1 ? "  (OF " + of + ")" : "") + "  ·  PLAYING ALONE MEANWHILE";
            }
        }

        void Update()
        {
            var kb = Keyboard.current; var mouse = Mouse.current;
            LiveRooms.Tick();
            CloudSync.Tick();
            ShowQueue();
            // The walkman: M for the next song, N on and off (not while typing in a dialog).
            if (kb != null && SaveData.HasWalkman && !Dialogs.Open && Screen != GameScreen.Tavern)
            {
                if (kb.mKey.wasPressedThisFrame) Walkman.Next();
                else if (kb.nKey.wasPressedThisFrame) Walkman.Toggle();
            }
            if (Screen == GameScreen.Ride && !_busy)
            {
                _rideTimer -= Time.deltaTime;
                bool skip = kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame);
                if (_rideTimer <= 0f || skip) Transition(ShowMap);
            }
            if (Screen == GameScreen.Run && Run != null)
            {
                // Clicking the game captures the mouse for looking around.
                if (!Dialogs.Open && mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                    Cursor.lockState = CursorLockMode.Locked;
                if (kb != null && kb.escapeKey.wasPressedThisFrame && !_busy && Shop != null && Shop.IsOpen) Shop.Close();
                else if (kb != null && kb.escapeKey.wasPressedThisFrame && !_busy && Run.Watching) Run.StopWatching();
                else if (kb != null && kb.escapeKey.wasPressedThisFrame && !_busy)
                {
                    if (Dialogs.Open) { Dialogs.Close(); Resume(); }
                    else
                    {
                        Run.Paused = true;
                        Cursor.lockState = CursorLockMode.None;
                        var slope = Run.Session.Slope;
                        Dialogs.Pause(Resume, () => Transition(() => StartRun(slope, Character)), () => Transition(ShowMap), () => Transition(ShowMenu));
                    }
                }
            }
            else if (Screen == GameScreen.Tavern && Tavern != null && !Tavern.Busy && Tavern.GameEndFrame < Time.frameCount - 1 && Tavern.BusyEndFrame < Time.frameCount - 1)
            {
                if (!Dialogs.Open && mouse != null && mouse.leftButton.wasPressedThisFrame) Cursor.lockState = CursorLockMode.Locked;
                if (kb != null && kb.escapeKey.wasPressedThisFrame && !_busy)
                {
                    if (Dialogs.Open) { Dialogs.Close(); Cursor.lockState = CursorLockMode.Locked; }
                    else
                    {
                        Cursor.lockState = CursorLockMode.None;
                        Dialogs.Pause(() => Cursor.lockState = CursorLockMode.Locked, null, () => Transition(ShowRide), () => Transition(ShowMenu));
                    }
                }
            }
            else if ((Screen == GameScreen.Map || Screen == GameScreen.Menu) && kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (Dialogs.Open) Dialogs.Close(); else Dialogs.Settings();
            }
        }

        void Resume()
        {
            if (Run != null) Run.Paused = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
}
