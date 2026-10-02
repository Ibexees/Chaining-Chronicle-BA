using System;
using CC.Core.Grid;
using NUnit.Framework;

namespace CC.Tests.Core.Grid
{
    public class TileGridTests
    {
        [Test]
        public void NewGridIsAllWalls()
        {
            var grid = new TileGrid(3, 2);
            for (int x = 0; x < 3; x++)
                for (int y = 0; y < 2; y++)
                    Assert.AreEqual(TileType.Wall, grid.Get(new GridPos(x, y)));
        }

        [Test]
        public void OutsideCellsAreWalls()
        {
            var grid = new TileGrid(2, 2);
            grid.Set(new GridPos(0, 0), TileType.Floor);
            Assert.IsFalse(grid.IsWalkable(new GridPos(-1, 0)));
            Assert.IsFalse(grid.IsWalkable(new GridPos(2, 0)));
            Assert.IsFalse(grid.IsWalkable(new GridPos(0, 2)));
            Assert.IsFalse(grid.InBounds(new GridPos(0, -1)));
        }

        [Test]
        public void FloorAndStairsAreWalkable()
        {
            var grid = new TileGrid(2, 1);
            grid.Set(new GridPos(0, 0), TileType.Floor);
            grid.Set(new GridPos(1, 0), TileType.StairsDown);
            Assert.IsTrue(grid.IsWalkable(new GridPos(0, 0)));
            Assert.IsTrue(grid.IsWalkable(new GridPos(1, 0)));
        }

        [Test]
        public void SetOutsideThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TileGrid(2, 2).Set(new GridPos(2, 2), TileType.Floor));
        }

        [Test]
        public void StepMovesInTheExpectedDirection()
        {
            var origin = new GridPos(5, 5);
            Assert.AreEqual(new GridPos(5, 6), origin.Step(Direction.Up));
            Assert.AreEqual(new GridPos(5, 4), origin.Step(Direction.Down));
            Assert.AreEqual(new GridPos(4, 5), origin.Step(Direction.Left));
            Assert.AreEqual(new GridPos(6, 5), origin.Step(Direction.Right));
        }

        [Test]
        public void GridPosEqualityAndHashing()
        {
            Assert.AreEqual(new GridPos(1, 2), new GridPos(1, 2));
            Assert.IsTrue(new GridPos(1, 2) == new GridPos(1, 2));
            Assert.IsTrue(new GridPos(1, 2) != new GridPos(2, 1));
            Assert.AreEqual(new GridPos(3, 4).GetHashCode(), new GridPos(3, 4).GetHashCode());
        }
    }
}
