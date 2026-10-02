using System;

namespace CC.Core.Grid
{
    public enum TileType
    {
        Wall,
        Floor,
        StairsDown
    }

    /// <summary>Plain 2D tile data of a stage. Cells outside the grid count as walls.</summary>
    public sealed class TileGrid
    {
        private readonly TileType[] _tiles;

        public int Width { get; }
        public int Height { get; }

        public TileGrid(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            Width = width;
            Height = height;
            _tiles = new TileType[width * height]; // default(TileType) == Wall
        }

        public bool InBounds(GridPos pos) => pos.X >= 0 && pos.Y >= 0 && pos.X < Width && pos.Y < Height;

        public TileType Get(GridPos pos) => InBounds(pos) ? _tiles[Index(pos)] : TileType.Wall;

        public void Set(GridPos pos, TileType type)
        {
            if (!InBounds(pos)) throw new ArgumentOutOfRangeException(nameof(pos), pos.ToString());
            _tiles[Index(pos)] = type;
        }

        /// <summary>Walkable terrain: floor or stairs. Says nothing about entities standing there.</summary>
        public bool IsWalkable(GridPos pos) => Get(pos) != TileType.Wall;

        public int Index(GridPos pos) => pos.Y * Width + pos.X;
    }
}
