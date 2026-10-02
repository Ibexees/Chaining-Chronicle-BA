using System;
using System.Collections.Generic;
using CC.Core.Grid;
using CC.Core.Util;

namespace CC.Core.Stage
{
    /// <summary>
    /// Seed-based stage generator: random non-overlapping rooms, joined by L-shaped corridors
    /// (A9). Deterministic for (masterSeed, stageNumber, params).
    /// </summary>
    public static class StageGenerator
    {
        private const int MaxAttempts = 20;

        public static StageLayout Generate(int masterSeed, int stageNumber, StageGenerationParams p)
        {
            var stageRng = new SeededRng(masterSeed).Derive(stageNumber);

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var layout = TryGenerate(masterSeed, stageNumber, p, stageRng.Derive(attempt));
                if (layout != null && StageValidator.Validate(layout).Count == 0) return layout;
            }

            throw new InvalidOperationException(
                $"No valid stage found for seed {masterSeed}, stage {stageNumber} after {MaxAttempts} attempts");
        }

        private static StageLayout TryGenerate(int masterSeed, int stageNumber, StageGenerationParams p, IRng rng)
        {
            var grid = new TileGrid(p.Width, p.Height);
            var rooms = PlaceRooms(grid, p, rng);
            if (rooms.Count < 2) return null;

            ConnectRooms(grid, rooms, rng);

            var start = rooms[0].Center;
            var distanceFromStart = new DistanceMap(grid, start);
            var stairs = ChooseStairs(rooms, distanceFromStart);
            grid.Set(stairs, TileType.StairsDown);

            var roomCells = CollectRoomCells(rooms);

            var enemies = PlaceEnemies(roomCells, distanceFromStart, stairs, p, rng);
            var pickups = PlacePickups(grid, roomCells, start, stairs, enemies, p, rng);

            return new StageLayout(masterSeed, stageNumber, grid, rooms, start, stairs, enemies, pickups);
        }

        private static List<Room> PlaceRooms(TileGrid grid, StageGenerationParams p, IRng rng)
        {
            var rooms = new List<Room>();
            for (int i = 0; i < p.RoomAttempts; i++)
            {
                int width = rng.Next(p.MinRoomSize, p.MaxRoomSize + 1);
                int height = rng.Next(p.MinRoomSize, p.MaxRoomSize + 1);
                // Keep a one cell wall border around the whole stage.
                if (width + 2 > p.Width || height + 2 > p.Height) continue;

                var room = new Room(rng.Next(1, p.Width - width), rng.Next(1, p.Height - height), width, height);
                if (rooms.Exists(existing => existing.Intersects(room, 1))) continue;

                rooms.Add(room);
                for (int x = room.X; x < room.X + room.Width; x++)
                    for (int y = room.Y; y < room.Y + room.Height; y++)
                        grid.Set(new GridPos(x, y), TileType.Floor);
            }
            return rooms;
        }

        private static void ConnectRooms(TileGrid grid, List<Room> rooms, IRng rng)
        {
            // Tree: every room is joined to the closest earlier room, so all rooms are connected.
            for (int i = 1; i < rooms.Count; i++)
            {
                int closest = 0;
                int closestDistance = int.MaxValue;
                for (int j = 0; j < i; j++)
                {
                    int distance = GridPos.Manhattan(rooms[i].Center, rooms[j].Center);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closest = j;
                    }
                }
                CarveCorridor(grid, rooms[i].Center, rooms[closest].Center, rng);
            }

            // A few extra corridors create loops, so enemies cannot always be cornered in a dead end.
            int extra = rooms.Count / 3;
            for (int k = 0; k < extra; k++)
            {
                int a = rng.Next(0, rooms.Count);
                int b = rng.Next(0, rooms.Count);
                if (a != b) CarveCorridor(grid, rooms[a].Center, rooms[b].Center, rng);
            }
        }

        private static void CarveCorridor(TileGrid grid, GridPos from, GridPos to, IRng rng)
        {
            bool horizontalFirst = rng.Chance(0.5f);
            var corner = horizontalFirst ? new GridPos(to.X, from.Y) : new GridPos(from.X, to.Y);
            CarveLine(grid, from, corner);
            CarveLine(grid, corner, to);
        }

        private static void CarveLine(TileGrid grid, GridPos from, GridPos to)
        {
            int dx = Math.Sign(to.X - from.X);
            int dy = Math.Sign(to.Y - from.Y);
            var pos = from;
            while (true)
            {
                if (grid.Get(pos) == TileType.Wall) grid.Set(pos, TileType.Floor);
                if (pos == to) break;
                pos = new GridPos(pos.X + dx, pos.Y + dy);
            }
        }

        /// <summary>Stairs go into the centre of the room that is walking-wise furthest from the start.</summary>
        private static GridPos ChooseStairs(List<Room> rooms, DistanceMap distanceFromStart)
        {
            var best = rooms[1].Center;
            int bestDistance = -1;
            for (int i = 1; i < rooms.Count; i++)
            {
                int distance = distanceFromStart[rooms[i].Center];
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    best = rooms[i].Center;
                }
            }
            return best;
        }

        private static List<GridPos> CollectRoomCells(List<Room> rooms)
        {
            var cells = new List<GridPos>();
            foreach (var room in rooms)
                for (int x = room.X; x < room.X + room.Width; x++)
                    for (int y = room.Y; y < room.Y + room.Height; y++)
                        cells.Add(new GridPos(x, y));
            return cells;
        }

        private static List<EnemySpawn> PlaceEnemies(
            List<GridPos> roomCells, DistanceMap distanceFromStart, GridPos stairs, StageGenerationParams p, IRng rng)
        {
            var enemies = new List<EnemySpawn>();
            if (p.EnemyTypeIds.Count == 0) return enemies;

            var candidates = roomCells.FindAll(cell =>
                cell != stairs && distanceFromStart[cell] >= p.MinEnemyDistanceFromStart);
            Shuffle(candidates, rng);

            int count = Math.Min(p.EnemyCount, candidates.Count);
            for (int i = 0; i < count; i++)
            {
                string typeId = p.EnemyTypeIds[rng.Next(0, p.EnemyTypeIds.Count)];
                enemies.Add(new EnemySpawn(candidates[i], typeId, p.EnemyLevel));
            }
            return enemies;
        }

        private static List<PickupSpawn> PlacePickups(
            TileGrid grid, List<GridPos> roomCells, GridPos start, GridPos stairs,
            List<EnemySpawn> enemies, StageGenerationParams p, IRng rng)
        {
            var pickups = new List<PickupSpawn>();
            if (p.PickupKinds.Count == 0) return pickups;

            var taken = new HashSet<GridPos> { start, stairs };
            foreach (var enemy in enemies) taken.Add(enemy.Pos);

            var candidates = roomCells.FindAll(cell => !taken.Contains(cell));
            Shuffle(candidates, rng);

            var placed = new List<GridPos>();
            foreach (var cell in candidates)
            {
                if (pickups.Count >= p.PickupCount) break;

                // Pickups block movement, so only keep a cell if everything stays reachable.
                placed.Add(cell);
                if (StillConnected(grid, start, stairs, enemies, placed))
                    pickups.Add(new PickupSpawn(cell, p.PickupKinds[rng.Next(0, p.PickupKinds.Count)]));
                else
                    placed.RemoveAt(placed.Count - 1);
            }
            return pickups;
        }

        private static bool StillConnected(
            TileGrid grid, GridPos start, GridPos stairs, List<EnemySpawn> enemies, List<GridPos> pickupCells)
        {
            var blocked = new HashSet<GridPos>(pickupCells);
            var map = new DistanceMap(grid, start, pos => blocked.Contains(pos));

            if (!map.IsReachable(stairs)) return false;
            foreach (var enemy in enemies)
                if (!map.IsReachable(enemy.Pos)) return false;
            foreach (var pickup in pickupCells)
                if (!StageValidator.HasReachableNeighbour(grid, map, pickup)) return false;
            return true;
        }

        private static void Shuffle<T>(List<T> list, IRng rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
