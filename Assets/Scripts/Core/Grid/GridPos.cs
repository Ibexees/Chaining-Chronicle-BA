using System;

namespace CC.Core.Grid
{
    /// <summary>Cardinal directions on the grid. Up is +y (same orientation as Unity world space).</summary>
    public enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }

    public static class DirectionExtensions
    {
        public static readonly Direction[] All = { Direction.Up, Direction.Down, Direction.Left, Direction.Right };
    }

    /// <summary>Integer cell position on a <see cref="TileGrid"/>.</summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public int X { get; }
        public int Y { get; }

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public GridPos Step(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return new GridPos(X, Y + 1);
                case Direction.Down: return new GridPos(X, Y - 1);
                case Direction.Left: return new GridPos(X - 1, Y);
                default: return new GridPos(X + 1, Y);
            }
        }

        public static int Manhattan(GridPos a, GridPos b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

        public bool Equals(GridPos other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPos other && Equals(other);
        public override int GetHashCode() => unchecked(X * 397 ^ Y);
        public override string ToString() => $"({X},{Y})";

        public static bool operator ==(GridPos a, GridPos b) => a.Equals(b);
        public static bool operator !=(GridPos a, GridPos b) => !a.Equals(b);
    }
}
