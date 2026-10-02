using System;
using System.Collections.Generic;
using CC.Core.Grid;
using CC.Core.Stage;
using CC.Core.Util;

namespace CC.Tests.Core
{
    /// <summary>
    /// Builds small stages from ASCII art, top row first.
    /// '#' wall, '.' floor, '>' stairs, 'P' player start, 'E' enemy, 'K' pickup.
    /// </summary>
    internal static class TestMap
    {
        public static StageLayout Parse(string[] rows, int stageNumber = 1)
        {
            int height = rows.Length;
            int width = rows[0].Length;
            var grid = new TileGrid(width, height);
            var enemies = new List<EnemySpawn>();
            var pickups = new List<PickupSpawn>();
            var start = new GridPos(-1, -1);
            var stairs = new GridPos(-1, -1);

            for (int row = 0; row < height; row++)
            {
                if (rows[row].Length != width) throw new ArgumentException($"Row {row} has a different width");
                for (int col = 0; col < width; col++)
                {
                    var pos = new GridPos(col, height - 1 - row);
                    switch (rows[row][col])
                    {
                        case '#': break;
                        case '.': grid.Set(pos, TileType.Floor); break;
                        case '>': grid.Set(pos, TileType.StairsDown); stairs = pos; break;
                        case 'P': grid.Set(pos, TileType.Floor); start = pos; break;
                        case 'E': grid.Set(pos, TileType.Floor); enemies.Add(new EnemySpawn(pos, "test", 1)); break;
                        case 'K': grid.Set(pos, TileType.Floor); pickups.Add(new PickupSpawn(pos, PickupKind.Armor)); break;
                        default: throw new ArgumentException($"Unknown map character '{rows[row][col]}'");
                    }
                }
            }
            return new StageLayout(1, stageNumber, grid, new List<Room>(), start, stairs, enemies, pickups);
        }

        public static TileGrid Grid(params string[] rows) => Parse(rows).Grid;

        public static GridPos At(string[] rows, char marker)
        {
            int height = rows.Length;
            for (int row = 0; row < height; row++)
            {
                int col = rows[row].IndexOf(marker);
                if (col >= 0) return new GridPos(col, height - 1 - row);
            }
            throw new ArgumentException($"Marker '{marker}' not found");
        }
    }

    /// <summary>Enemy mover for tests: every enemy follows the given function and the calls are counted.</summary>
    internal sealed class ScriptedMover : IStageEnemyMover
    {
        private readonly Func<StageEnemy, IStageWorld, GridPos> _choose;

        public int Calls { get; private set; }

        public ScriptedMover(Func<StageEnemy, IStageWorld, GridPos> choose) => _choose = choose;

        public static ScriptedMover Staying() => new ScriptedMover((enemy, world) => enemy.Pos);

        public GridPos ChooseStep(StageEnemy enemy, IStageWorld world, IRng rng)
        {
            Calls++;
            return _choose(enemy, world);
        }
    }
}
