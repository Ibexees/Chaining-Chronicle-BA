using System;
using CC.Core.Util;

namespace CC.Core.Flow
{
    /// <summary>
    /// Pure state machine for the game flow. Knows which transitions are legal,
    /// but nothing about scenes. Scene handling lives in CC.Game.GameFlow.
    /// </summary>
    public sealed class GameFlowMachine
    {
        public GameFlowState Current { get; private set; } = GameFlowState.Boot;

        public bool CanTransitionTo(GameFlowState next)
        {
            switch (Current)
            {
                case GameFlowState.Boot:
                    return next == GameFlowState.Hub;
                case GameFlowState.Hub:
                    return next == GameFlowState.Exploring;
                case GameFlowState.Exploring:
                    return next == GameFlowState.Combat
                        || next == GameFlowState.GameOver
                        || next == GameFlowState.Hub;
                case GameFlowState.Combat:
                    // Win or flee -> Exploring, defeat -> GameOver (permadeath)
                    return next == GameFlowState.Exploring
                        || next == GameFlowState.GameOver;
                case GameFlowState.GameOver:
                    return next == GameFlowState.Hub;
                default:
                    return false;
            }
        }

        public void TransitionTo(GameFlowState next)
        {
            if (!CanTransitionTo(next))
                throw new InvalidOperationException($"Illegal game flow transition {Current} -> {next}");

            var previous = Current;
            Current = next;
            GameEvents.RaiseFlowStateChanged(previous, next);
        }
    }
}
