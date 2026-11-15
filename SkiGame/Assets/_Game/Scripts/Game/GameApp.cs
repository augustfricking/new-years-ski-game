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
            AudioDirector.Create(transform);
            Ui.Create(transform);
            SaveData.Load();
            Character = SaveData.LastCharacter;
            if (DebugSlope.HasValue) StartRun(DebugSlope.Value, Character);
            else ShowMenu();
        }

        void CloseAll()
        {
            Dialogs.Close();
            if (Run != null) { Run.Close(); Run = null; }
            if (Lift != null) { Lift.Close(); Lift = null; }
            if (Map != null) { Map.Close(); Map = null; }
            if (Tavern != null) { Tavern.Close(); Tavern = null; }
            Look.Indoors = false;
            Cursor.lockState = CursorLockMode.None;
        }

        // ---- screens ---------------------------------------------------------------------

        public void ShowMenu()
        {
            CloseAll();
            Screen = GameScreen.Menu;
            Look.Storm = 0f; Look.Night = 0f;
            Lift = LiftScene.Create(transform, LiftScene.Mode.Menu);
            Lift.OnSelect = who =>
            {
                Character = who;
                SaveData.LastCharacter = who; SaveData.Save();
                Screen = GameScreen.Cutscene;
                Lift.PlayCutscene(who, () => { ShowMap(); Ui.Instance.Fade(0f, 0.8f); });
            };
            Ui.Instance.Fade(0f, 0.6f);
        }

        public void ShowMap()
        {
            CloseAll();
            Screen = GameScreen.Map;
            Map = MapScene.Create(transform, Character);
            Map.OnPick = slope => Transition(() => StartRun(slope, Character), 0.45f);
            Map.OnChangeSkier = () => Transition(ShowMenu);
        }

        public void StartRun(SlopeId slope, CharacterId who)
        {
            CloseAll();
            Character = who;
            Screen = GameScreen.Run;
            Look.Storm = 0.06f; Look.Night = 0f;
            if (AudioDirector.I != null) { AudioDirector.I.Music("music_ski", 0.6f); AudioDirector.I.Ambience(null); }
            Run = RunController.Create(transform, slope, who);
            Run.OnLiftReached = () => Transition(ShowRide);
            Run.OnTavernReached = () => Transition(ShowTavern);
        }

        /// <summary>The short ride back up after a run; cuts to the map.</summary>
        public void ShowRide()
        {
            CloseAll();
            Screen = GameScreen.Ride;
            Look.Storm = 0.1f;
            Lift = LiftScene.Create(transform, LiftScene.Mode.Ride, Character);
            _rideTimer = 3.8f;
        }

        public void ShowTavern()
        {
            CloseAll();
            Screen = GameScreen.Tavern;
            Tavern = TavernScene.Create(transform, Character);
            Tavern.OnLeave = () => Transition(ShowRide);
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

        void Update()
        {
            var kb = Keyboard.current; var mouse = Mouse.current;
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
                if (kb != null && kb.escapeKey.wasPressedThisFrame && !_busy)
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
            else if (Screen == GameScreen.Tavern && Tavern != null && Tavern.Game == null)
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
