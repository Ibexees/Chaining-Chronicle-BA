using CC.Core.Grid;
using NUnit.Framework;

namespace CC.Tests.Core.Grid
{
    public class PathfinderTests
    {
        [Test]
        public void StraightCorridorGivesDirectPath()
        {
            var grid = TestMap.Grid("#####", "#...>", "#####");
            var start = new GridPos(1, 1);
            var goal = new GridPos(4, 1);

            Assert.IsTrue(Pathfinder.TryFindPath(grid, start, goal, null, out var path));

            CollectionAssert.AreEqual(new[] { new GridPos(2, 1), new GridPos(3, 1), goal }, path);
        }

        [Test]
        public void PathGoesAroundWall()
        {
            var grid = TestMap.Grid(
                "#####",
                "#...#",
                "#.#.#",
                "#...#",
                "#####");
            var start = new GridPos(1, 3);
            var goal = new GridPos(3, 3);

            Assert.IsTrue(Pathfinder.TryFindPath(grid, start, goal, null, out var path));

            Assert.AreEqual(2, path.Count); // straight along the top row
            Assert.AreEqual(goal, path[path.Count - 1]);
        }

        [Test]
        public void PathIsShortestAndMadeOfAdjacentSteps()
        {
            var grid = TestMap.Grid(
                "#######",
                "#.....#",
                "#.###.#",
                "#.....#",
                "#######");
            var start = new GridPos(1, 3);
            var goal = new GridPos(5, 1);

            Assert.IsTrue(Pathfinder.TryFindPath(grid, start, goal, null, out var path));

            Assert.AreEqual(GridPos.Manhattan(start, goal), path.Count);
            var previous = start;
            foreach (var step in path)
            {
                Assert.AreEqual(1, GridPos.Manhattan(previous, step));
                Assert.IsTrue(grid.IsWalkable(step));
                previous = step;
            }
        }

        [Test]
        public void NoPathWhenWallSeparatesCells()
        {
            var grid = TestMap.Grid("#####", "#.#.#", "#####");
            Assert.IsFalse(Pathfinder.TryFindPath(grid, new GridPos(1, 1), new GridPos(3, 1), null, out var path));
            Assert.IsNull(path);
        }

        [Test]
        public void StartEqualsGoalGivesEmptyPath()
        {
            var grid = TestMap.Grid("###", "#.#", "###");
            Assert.IsTrue(Pathfinder.TryFindPath(grid, new GridPos(1, 1), new GridPos(1, 1), null, out var path));
            Assert.AreEqual(0, path.Count);
        }

        [Test]
        public void WallGoalIsNotReachable()
        {
            var grid = TestMap.Grid("###", "#.#", "###");
            Assert.IsFalse(Pathfinder.TryFindPath(grid, new GridPos(1, 1), new GridPos(0, 0), null, out _));
        }

        [Test]
        public void BlockedCellsForceDetour()
        {
            var grid = TestMap.Grid(
                "#####",
                "#...#",
                "#...#",
                "#####");
            var blocked = new GridPos(2, 2);

            Assert.IsTrue(Pathfinder.TryFindPath(grid, new GridPos(1, 2), new GridPos(3, 2), p => p == blocked, out var path));

            CollectionAssert.DoesNotContain(path, blocked);
            Assert.AreEqual(4, path.Count);
        }

        [Test]
        public void BlockedPredicateIsNotAppliedToStartAndGoal()
        {
            var grid = TestMap.Grid("#####", "#...#", "#####");
            var start = new GridPos(1, 1);
            var goal = new GridPos(3, 1);

            Assert.IsTrue(Pathfinder.TryFindPath(grid, start, goal, p => p == start || p == goal, out var path));

            Assert.AreEqual(2, path.Count);
        }

        [Test]
        public void FullyBlockedCorridorHasNoPath()
        {
            var grid = TestMap.Grid("#####", "#...#", "#####");
            Assert.IsFalse(Pathfinder.TryFindPath(grid, new GridPos(1, 1), new GridPos(3, 1), p => p == new GridPos(2, 1), out _));
        }

        [Test]
        public void PathLongerThanMaxStepsIsRejected()
        {
            var grid = TestMap.Grid("#######", "#.....#", "#######");
            var start = new GridPos(1, 1);
            var goal = new GridPos(5, 1);

            Assert.IsFalse(Pathfinder.TryFindPath(grid, start, goal, null, out _, maxSteps: 3));
            Assert.IsTrue(Pathfinder.TryFindPath(grid, start, goal, null, out _, maxSteps: 4));
        }

        [Test]
        public void DistanceMapMarksUnreachableCells()
        {
            var grid = TestMap.Grid("#####", "#.#.#", "#####");
            var map = new DistanceMap(grid, new GridPos(1, 1));

            Assert.AreEqual(0, map[new GridPos(1, 1)]);
            Assert.AreEqual(-1, map[new GridPos(3, 1)]);
            Assert.AreEqual(-1, map[new GridPos(-5, 2)]);
            Assert.IsFalse(map.IsReachable(new GridPos(3, 1)));
        }
    }
}
