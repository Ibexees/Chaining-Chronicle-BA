using CC.Core.Flow;
using CC.Core.Run;
using CC.Core.Util;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CC.Game.Boot
{
    /// <summary>
    /// Unity side of the game flow: owns the pure GameFlowMachine and the current run, and loads the
    /// scene that belongs to a state. Combat and GameOver stay in the Stage scene.
    /// </summary>
    public sealed class GameFlow : MonoBehaviour
    {
        public const string HubScene = "Hub";
        public const string StageScene = "Stage";

        private readonly GameFlowMachine _machine = new GameFlowMachine();

        public GameFlowState Current => _machine.Current;

        /// <summary>The run in progress, or null while in Boot or Hub.</summary>
        public RunState Run { get; private set; }

        /// <summary>Leaves Boot and loads the Hub.</summary>
        public void Begin() => GoTo(GameFlowState.Hub);

        /// <summary>Starts a new run on stage 1. Only possible from the Hub.</summary>
        public void StartRun(int seed, string characterName = null)
        {
            if (!CanGoTo(GameFlowState.Exploring))
                throw new System.InvalidOperationException($"Cannot start a run from {Current}");

            Run = new RunState(seed, characterName);
            GameEvents.RaiseRunStarted(seed);
            GoTo(GameFlowState.Exploring);
        }

        /// <summary>Leaves the run (from GameOver or the pause menu) and returns to the Hub.</summary>
        public void ReturnToHub()
        {
            if (Run != null) GameEvents.RaiseRunEnded(Run.StageNumber);
            Run = null;
            GoTo(GameFlowState.Hub);
        }

        public bool CanGoTo(GameFlowState next) => _machine.CanTransitionTo(next);

        public void GoTo(GameFlowState next)
        {
            _machine.TransitionTo(next);

            string scene = SceneFor(next);
            if (scene != null && SceneManager.GetActiveScene().name != scene)
                SceneManager.LoadScene(scene);
        }

        /// <summary>Returns the scene to load for a state, or null if the state keeps the current scene.</summary>
        public static string SceneFor(GameFlowState state) => state switch
        {
            GameFlowState.Hub => HubScene,
            GameFlowState.Exploring => StageScene,
            _ => null
        };
    }
}
