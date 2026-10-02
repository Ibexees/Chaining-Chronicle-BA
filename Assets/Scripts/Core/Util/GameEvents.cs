using System;
using CC.Core.Flow;
using CC.Core.Grid;
using CC.Core.Stage;

namespace CC.Core.Util
{
    /// <summary>
    /// Typed event bus. Core raises events, views (HUD, audio, ...) subscribe.
    /// New events are added together with the system that raises them.
    /// </summary>
    public static class GameEvents
    {
        public static event Action<GameFlowState, GameFlowState> FlowStateChanged;
        /// <summary>Argument: master seed of the run.</summary>
        public static event Action<int> RunStarted;
        /// <summary>Argument: highest stage number reached.</summary>
        public static event Action<int> RunEnded;
        /// <summary>Argument: stage number.</summary>
        public static event Action<int> StageStarted;
        /// <summary>Argument: new player cell.</summary>
        public static event Action<GridPos> PlayerMoved;
        /// <summary>Argument: id of the enemy that shares the player's cell.</summary>
        public static event Action<int> EnemyEngaged;
        public static event Action<PickupKind> PowerUpCollected;

        public static void RaiseFlowStateChanged(GameFlowState previous, GameFlowState next) =>
            FlowStateChanged?.Invoke(previous, next);

        public static void RaiseRunStarted(int seed) => RunStarted?.Invoke(seed);

        public static void RaiseRunEnded(int stageReached) => RunEnded?.Invoke(stageReached);

        public static void RaiseStageStarted(int stageNumber) => StageStarted?.Invoke(stageNumber);

        public static void RaisePlayerMoved(GridPos position) => PlayerMoved?.Invoke(position);

        public static void RaiseEnemyEngaged(int enemyId) => EnemyEngaged?.Invoke(enemyId);

        public static void RaisePowerUpCollected(PickupKind kind) => PowerUpCollected?.Invoke(kind);

        /// <summary>Removes all subscribers. Used for tests and when entering play mode without domain reload.</summary>
        public static void ClearSubscribers()
        {
            FlowStateChanged = null;
            RunStarted = null;
            RunEnded = null;
            StageStarted = null;
            PlayerMoved = null;
            EnemyEngaged = null;
            PowerUpCollected = null;
        }
    }
}
