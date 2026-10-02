using System.Collections.Generic;
using CC.Core.Grid;

namespace CC.Core.Stage
{
    /// <summary>
    /// Checks the guarantees of a generated stage: everything is reachable from the start,
    /// pickups (which block movement) can be interacted with, nothing overlaps.
    /// </summary>
    public static class StageValidator
    {
        /// <summary>Returns a description of every violated guarantee. Empty means the layout is valid.</summary>
        public static List<string> Validate(StageLayout layout)
        {
            var problems = new List<string>();
            var grid = layout.Grid;

            if (!grid.IsWalkable(layout.PlayerStart)) problems.Add("player start is not walkable");
            if (grid.Get(layout.Stairs) != TileType.StairsDown) problems.Add("stairs cell is not a stairs tile");
            if (layout.Stairs == layout.PlayerStart) problems.Add("stairs on player start");

            var used = new HashSet<GridPos> { layout.PlayerStart, layout.Stairs };
            var pickupCells = new HashSet<GridPos>();

            foreach (var enemy in layout.Enemies)
            {
                if (!grid.IsWalkable(enemy.Pos)) problems.Add($"enemy at {enemy.Pos} not walkable");
                if (!used.Add(enemy.Pos)) problems.Add($"enemy at {enemy.Pos} overlaps another object");
            }
            foreach (var pickup in layout.Pickups)
            {
                if (!grid.IsWalkable(pickup.Pos)) problems.Add($"pickup at {pickup.Pos} not walkable");
                if (!used.Add(pickup.Pos)) problems.Add($"pickup at {pickup.Pos} overlaps another object");
                pickupCells.Add(pickup.Pos);
            }

            if (problems.Count > 0) return problems;

            // Pickups are obstacles, so reachability is checked with them blocked.
            var map = new DistanceMap(grid, layout.PlayerStart, pos => pickupCells.Contains(pos));
            if (!map.IsReachable(layout.Stairs)) problems.Add("stairs not reachable from start");
            foreach (var enemy in layout.Enemies)
                if (!map.IsReachable(enemy.Pos)) problems.Add($"enemy at {enemy.Pos} not reachable");
            foreach (var pickup in layout.Pickups)
                if (!HasReachableNeighbour(grid, map, pickup.Pos)) problems.Add($"pickup at {pickup.Pos} cannot be interacted with");

            if (layout.Rooms.Count < 2) problems.Add("fewer than two rooms");
            return problems;
        }

        /// <summary>True if some cell next to <paramref name="pos"/> is walkable and reachable.</summary>
        public static bool HasReachableNeighbour(TileGrid grid, DistanceMap map, GridPos pos)
        {
            foreach (var direction in DirectionExtensions.All)
            {
                var neighbour = pos.Step(direction);
                if (grid.IsWalkable(neighbour) && map.IsReachable(neighbour)) return true;
            }
            return false;
        }
    }
}
