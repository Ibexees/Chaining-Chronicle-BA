using System;
using System.Collections.Generic;

namespace CC.Core.Grid
{
    /// <summary>
    /// Breadth-first step distances from one start cell over walkable terrain (4-neighbourhood).
    /// The blocked predicate is tested for every cell except the start cell.
    /// Unreachable cells have distance -1.
    /// </summary>
    public sealed class DistanceMap
    {
        private readonly TileGrid _grid;
        private readonly int[] _distance;

        public GridPos Start { get; }

        public DistanceMap(TileGrid grid, GridPos start, Func<GridPos, bool> isBlocked = null, int maxSteps = int.MaxValue)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Start = start;
            _distance = new int[grid.Width * grid.Height];
            for (int i = 0; i < _distance.Length; i++) _distance[i] = -1;

            if (!grid.IsWalkable(start)) return;

            var queue = new Queue<GridPos>();
            _distance[grid.Index(start)] = 0;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                int next = _distance[grid.Index(current)] + 1;
                if (next > maxSteps) continue;

                foreach (var direction in DirectionExtensions.All)
                {
                    var neighbour = current.Step(direction);
                    if (!grid.IsWalkable(neighbour)) continue;
                    if (_distance[grid.Index(neighbour)] != -1) continue;
                    if (isBlocked != null && isBlocked(neighbour)) continue;

                    _distance[grid.Index(neighbour)] = next;
                    queue.Enqueue(neighbour);
                }
            }
        }

        /// <summary>Steps from the start cell, or -1 if the cell cannot be reached.</summary>
        public int this[GridPos pos] => _grid.InBounds(pos) ? _distance[_grid.Index(pos)] : -1;

        public bool IsReachable(GridPos pos) => this[pos] >= 0;
    }
}
