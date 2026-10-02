using System.Collections.Generic;
using CC.Core.Grid;
using CC.Core.Util;

namespace CC.Core.Stage
{
    /// <summary>
    /// Default enemy movement: walk the shortest path to the player when close,
    /// otherwise wander randomly. Never walks into walls, other enemies or pickups.
    /// </summary>
    public sealed class ChaseOrWanderMover : IStageEnemyMover
    {
        /// <summary>Enemies notice the player within this many walking steps.</summary>
        public int SightRange { get; }

        /// <summary>Chance per turn that a wandering enemy moves at all.</summary>
        public float WanderChance { get; }

        public ChaseOrWanderMover(int sightRange = 7, float wanderChance = 0.5f)
        {
            SightRange = sightRange;
            WanderChance = wanderChance;
        }

        public GridPos ChooseStep(StageEnemy enemy, IStageWorld world, IRng rng)
        {
            if (GridPos.Manhattan(enemy.Pos, world.PlayerPos) <= SightRange)
            {
                // Close in a straight line is not enough: the player may be behind a wall.
                // Without a short walkable path the enemy just keeps wandering.
                if (Pathfinder.TryFindPath(world.Grid, enemy.Pos, world.PlayerPos, world.IsOccupied, out var path, SightRange)
                    && path.Count > 0)
                    return path[0];
            }

            if (!rng.Chance(WanderChance)) return enemy.Pos;

            var options = new List<GridPos>(4);
            foreach (var direction in DirectionExtensions.All)
            {
                var next = enemy.Pos.Step(direction);
                if (world.Grid.IsWalkable(next) && !world.IsOccupied(next)) options.Add(next);
            }
            return options.Count == 0 ? enemy.Pos : options[rng.Next(0, options.Count)];
        }
    }
}
