using System;
using System.Collections.Generic;

namespace CC.Core.Grid
{
    /// <summary>
    /// Shortest paths on the tile grid. Movement cost is uniform, so breadth-first search
    /// already yields optimal paths (see Architekturkonzept: deviation from "A*", documented).
    /// </summary>
    public static class Pathfinder
    {
        /// <summary>
        /// Finds a shortest path from start to goal. The path excludes the start cell and includes the goal.
        /// The blocked predicate is not applied to start and goal, both only need walkable terrain.
        /// </summary>
        /// <param name="maxSteps">Longest path length that is still accepted.</param>
        public static bool TryFindPath(
            TileGrid grid,
            GridPos start,
            GridPos goal,
            Func<GridPos, bool> isBlocked,
            out List<GridPos> path,
            int maxSteps = int.MaxValue)
        {
            path = null;
            if (!grid.IsWalkable(start) || !grid.IsWalkable(goal)) return false;
            if (start == goal)
            {
                path = new List<GridPos>();
                return true;
            }

            // Search outward from the goal so the path can be read off by walking downhill from the start.
            Func<GridPos, bool> blocked = pos => pos != start && isBlocked != null && isBlocked(pos);
            var map = new DistanceMap(grid, goal, blocked, maxSteps);
            int distance = map[start];
            if (distance < 0) return false;

            path = new List<GridPos>(distance);
            var current = start;
            while (current != goal)
            {
                int currentDistance = map[current];
                GridPos? best = null;
                foreach (var direction in DirectionExtensions.All)
                {
                    var neighbour = current.Step(direction);
                    if (map[neighbour] == currentDistance - 1)
                    {
                        best = neighbour;
                        break;
                    }
                }
                current = best.Value; // always exists: distance decreases by one along a shortest path
                path.Add(current);
            }
            return true;
        }
    }
}
