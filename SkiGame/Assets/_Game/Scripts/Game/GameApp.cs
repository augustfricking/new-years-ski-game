using Ski.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ski.Game
{
    /// <summary>
    /// Entry point. The Main scene is empty; this object builds everything at run time and owns
    /// the flow between the lift menu, the cutscene, the ski map, a run and the tavern.
    /// </summary>
    public sealed class GameApp : MonoBehaviour
    {
        public static GameApp Instance { get; private set; }
        public WorldLook Look { get; private set; }
        public RunController Run { get; private set; }

        /// <summary>Set before Play to jump straight into a run (used while developing and by tests).</summary>
        public static SlopeId? DebugSlope;
        public static CharacterId DebugCharacter = CharacterId.Erik;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

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
            Look = WorldLook.Create(transform);
            StartRun(DebugSlope ?? SlopeId.Green, DebugCharacter);
        }

        public void StartRun(SlopeId slope, CharacterId who)
        {
            if (Run != null) Destroy(Run.gameObject);
            Run = RunController.Create(transform, slope, who);
        }
    }
}
