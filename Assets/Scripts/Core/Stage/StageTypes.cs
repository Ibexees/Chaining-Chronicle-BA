using System;
using System.Collections.Generic;
using CC.Core.Grid;

namespace CC.Core.Stage
{
    /// <summary>What a pickup on the stage gives the player (GDD 2.2.6, A7).</summary>
    public enum PickupKind
    {
        Armor,
        ChainSkill,
        Recruit
    }

    /// <summary>Rectangular room of the generated stage, in cell coordinates.</summary>
    public readonly struct Room
    {
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }

        public Room(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public GridPos Center => new GridPos(X + Width / 2, Y + Height / 2);

        public bool Contains(GridPos pos) =>
            pos.X >= X && pos.X < X + Width && pos.Y >= Y && pos.Y < Y + Height;

        /// <summary>True if the rooms overlap or are closer than <paramref name="margin"/> empty cells.</summary>
        public bool Intersects(Room other, int margin) =>
            X - margin < other.X + other.Width && other.X - margin < X + Width &&
            Y - margin < other.Y + other.Height && other.Y - margin < Y + Height;
    }

    public readonly struct EnemySpawn
    {
        public GridPos Pos { get; }
        public string TypeId { get; }
        public int Level { get; }

        public EnemySpawn(GridPos pos, string typeId, int level)
        {
            Pos = pos;
            TypeId = typeId ?? throw new ArgumentNullException(nameof(typeId));
            Level = level;
        }
    }

    public readonly struct PickupSpawn
    {
        public GridPos Pos { get; }
        public PickupKind Kind { get; }

        public PickupSpawn(GridPos pos, PickupKind kind)
        {
            Pos = pos;
            Kind = kind;
        }
    }

    /// <summary>Result of the stage generator: terrain plus where everything starts.</summary>
    public sealed class StageLayout
    {
        public int Seed { get; }
        public int StageNumber { get; }
        public TileGrid Grid { get; }
        public IReadOnlyList<Room> Rooms { get; }
        public GridPos PlayerStart { get; }
        public GridPos Stairs { get; }
        public IReadOnlyList<EnemySpawn> Enemies { get; }
        public IReadOnlyList<PickupSpawn> Pickups { get; }

        public StageLayout(
            int seed,
            int stageNumber,
            TileGrid grid,
            IReadOnlyList<Room> rooms,
            GridPos playerStart,
            GridPos stairs,
            IReadOnlyList<EnemySpawn> enemies,
            IReadOnlyList<PickupSpawn> pickups)
        {
            Seed = seed;
            StageNumber = stageNumber;
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
            PlayerStart = playerStart;
            Stairs = stairs;
            Enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
            Pickups = pickups ?? throw new ArgumentNullException(nameof(pickups));
        }
    }
}
