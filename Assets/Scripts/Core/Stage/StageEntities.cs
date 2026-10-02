using CC.Core.Grid;
using CC.Core.Util;

namespace CC.Core.Stage
{
    /// <summary>An enemy walking around the stage. Created and moved only by <see cref="StageSession"/>.</summary>
    public sealed class StageEnemy
    {
        public int Id { get; }
        public string TypeId { get; }
        public int Level { get; }
        public GridPos Pos { get; internal set; }

        internal StageEnemy(int id, string typeId, int level, GridPos pos)
        {
            Id = id;
            TypeId = typeId;
            Level = level;
            Pos = pos;
        }
    }

    /// <summary>A collectable object on the stage. Blocks movement, collected via the interact key.</summary>
    public sealed class StagePickup
    {
        public int Id { get; }
        public PickupKind Kind { get; }
        public GridPos Pos { get; }

        internal StagePickup(int id, PickupKind kind, GridPos pos)
        {
            Id = id;
            Kind = kind;
            Pos = pos;
        }
    }

    /// <summary>What an enemy mover may look at. Implemented by <see cref="StageSession"/>.</summary>
    public interface IStageWorld
    {
        TileGrid Grid { get; }
        GridPos PlayerPos { get; }
        /// <summary>True if another enemy or a pickup stands on the cell. The player does not count.</summary>
        bool IsOccupied(GridPos pos);
    }

    /// <summary>Decides where an enemy goes during its turn on the stage (GDD 6.1, second AI task).</summary>
    public interface IStageEnemyMover
    {
        /// <summary>Returns the cell to move to (adjacent) or the enemy's own position to stay.</summary>
        GridPos ChooseStep(StageEnemy enemy, IStageWorld world, IRng rng);
    }
}
