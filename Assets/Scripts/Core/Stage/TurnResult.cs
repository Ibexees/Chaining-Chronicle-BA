using System.Collections.Generic;
using CC.Core.Grid;

namespace CC.Core.Stage
{
    public enum TurnKind
    {
        /// <summary>Input was ignored (combat pending or stage left).</summary>
        Ignored,
        /// <summary>Player only turned to face a direction (blocked move). No time passes.</summary>
        Turned,
        /// <summary>Player walked one cell. Enemies took their turn.</summary>
        Moved,
        /// <summary>Player collected the pickup in front of them. Enemies took their turn.</summary>
        PickedUp,
        /// <summary>Player used the stairs. The stage is finished.</summary>
        Descended,
        /// <summary>Interact key pressed with nothing to interact with. No time passes.</summary>
        NothingToInteract
    }

    public readonly struct EnemyMove
    {
        public int EnemyId { get; }
        public GridPos From { get; }
        public GridPos To { get; }

        public EnemyMove(int enemyId, GridPos from, GridPos to)
        {
            EnemyId = enemyId;
            From = from;
            To = to;
        }
    }

    /// <summary>Everything a view needs to animate one player action.</summary>
    public sealed class TurnResult
    {
        public static readonly IReadOnlyList<EnemyMove> NoMoves = new EnemyMove[0];

        public TurnKind Kind { get; }
        public GridPos PlayerFrom { get; }
        public GridPos PlayerTo { get; }
        public Direction Facing { get; }
        public PickupKind? Pickup { get; }
        public int PickupId { get; }
        public IReadOnlyList<EnemyMove> EnemyMoves { get; }
        /// <summary>Id of the enemy that now shares the player's cell, or -1.</summary>
        public int EngagedEnemyId { get; }

        public bool TimePassed => Kind == TurnKind.Moved || Kind == TurnKind.PickedUp;
        public bool EnemyEngaged => EngagedEnemyId >= 0;

        public TurnResult(
            TurnKind kind,
            GridPos playerFrom,
            GridPos playerTo,
            Direction facing,
            PickupKind? pickup = null,
            int pickupId = -1,
            IReadOnlyList<EnemyMove> enemyMoves = null,
            int engagedEnemyId = -1)
        {
            Kind = kind;
            PlayerFrom = playerFrom;
            PlayerTo = playerTo;
            Facing = facing;
            Pickup = pickup;
            PickupId = pickupId;
            EnemyMoves = enemyMoves ?? NoMoves;
            EngagedEnemyId = engagedEnemyId;
        }
    }
}
