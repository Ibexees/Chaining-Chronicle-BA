using System;
using System.Linq;
using CC.Core.Grid;
using CC.Core.Stage;
using NUnit.Framework;

namespace CC.Tests.Core.Stage
{
    public class StageGeneratorTests
    {
        private static readonly string[] EnemyTypes = { "mouse", "walrus", "eye" };

        private static StageGenerationParams ParamsFor(int stage) => StageRules.Create(stage, EnemyTypes);

        [Test]
        public void StagesAreValidForManySeeds()
        {
            foreach (int stage in new[] { 1, 12 })
            {
                for (int seed = 0; seed < 500; seed++)
                {
                    var layout = StageGenerator.Generate(seed, stage, ParamsFor(stage));
                    var problems = StageValidator.Validate(layout);
                    Assert.IsEmpty(problems, $"seed {seed}, stage {stage}: {string.Join("; ", problems)}");
                }
            }
        }

        [Test]
        public void StagesAreValidThroughTheWholeDifficultyRange()
        {
            for (int stage = 1; stage <= 80; stage++)
            {
                var layout = StageGenerator.Generate(1234, stage, ParamsFor(stage));
                Assert.IsEmpty(StageValidator.Validate(layout), $"stage {stage}");
            }
        }

        [Test]
        public void SameSeedAndStageGiveIdenticalLayout()
        {
            var a = StageGenerator.Generate(77, 5, ParamsFor(5));
            var b = StageGenerator.Generate(77, 5, ParamsFor(5));

            Assert.AreEqual(a.PlayerStart, b.PlayerStart);
            Assert.AreEqual(a.Stairs, b.Stairs);
            CollectionAssert.AreEqual(a.Enemies.Select(e => (e.Pos, e.TypeId)), b.Enemies.Select(e => (e.Pos, e.TypeId)));
            CollectionAssert.AreEqual(a.Pickups.Select(p => (p.Pos, p.Kind)), b.Pickups.Select(p => (p.Pos, p.Kind)));
            Assert.AreEqual(Dump(a), Dump(b));
        }

        [Test]
        public void DifferentSeedsGiveDifferentLayouts()
        {
            var layouts = Enumerable.Range(0, 20).Select(seed => Dump(StageGenerator.Generate(seed, 3, ParamsFor(3)))).Distinct();
            Assert.Greater(layouts.Count(), 15);
        }

        [Test]
        public void DifferentStagesOfTheSameRunDiffer()
        {
            Assert.AreNotEqual(
                Dump(StageGenerator.Generate(5, 1, ParamsFor(1))),
                Dump(StageGenerator.Generate(5, 2, ParamsFor(1))));
        }

        [Test]
        public void EnemyAndPickupCountsFollowTheRules()
        {
            for (int stage = 1; stage <= 40; stage++)
            {
                var p = ParamsFor(stage);
                for (int seed = 0; seed < 10; seed++)
                {
                    var layout = StageGenerator.Generate(seed, stage, p);
                    Assert.AreEqual(p.EnemyCount, layout.Enemies.Count, $"enemies, seed {seed}, stage {stage}");
                    Assert.AreEqual(p.PickupCount, layout.Pickups.Count, $"pickups, seed {seed}, stage {stage}");
                    Assert.IsTrue(layout.Enemies.All(e => e.Level == stage));
                    Assert.IsTrue(layout.Enemies.All(e => EnemyTypes.Contains(e.TypeId)));
                }
            }
        }

        [Test]
        public void EnemiesKeepDistanceFromPlayerStart()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var p = ParamsFor(6);
                var layout = StageGenerator.Generate(seed, 6, p);
                var map = new DistanceMap(layout.Grid, layout.PlayerStart);
                foreach (var enemy in layout.Enemies)
                    Assert.GreaterOrEqual(map[enemy.Pos], p.MinEnemyDistanceFromStart, $"seed {seed}");
            }
        }

        [Test]
        public void OuterBorderIsAlwaysWall()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var layout = StageGenerator.Generate(seed, 4, ParamsFor(4));
                var grid = layout.Grid;
                for (int x = 0; x < grid.Width; x++)
                {
                    Assert.AreEqual(TileType.Wall, grid.Get(new GridPos(x, 0)));
                    Assert.AreEqual(TileType.Wall, grid.Get(new GridPos(x, grid.Height - 1)));
                }
                for (int y = 0; y < grid.Height; y++)
                {
                    Assert.AreEqual(TileType.Wall, grid.Get(new GridPos(0, y)));
                    Assert.AreEqual(TileType.Wall, grid.Get(new GridPos(grid.Width - 1, y)));
                }
            }
        }

        [Test]
        public void StairsAreFarFromStart()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var layout = StageGenerator.Generate(seed, 2, ParamsFor(2));
                var map = new DistanceMap(layout.Grid, layout.PlayerStart);
                Assert.Greater(map[layout.Stairs], 0, $"seed {seed}");
                Assert.AreEqual(TileType.StairsDown, layout.Grid.Get(layout.Stairs));
            }
        }

        [Test]
        public void NoEnemyTypesMeansNoEnemies()
        {
            var layout = StageGenerator.Generate(1, 3, StageRules.Create(3, Array.Empty<string>()));
            Assert.AreEqual(0, layout.Enemies.Count);
            Assert.IsEmpty(StageValidator.Validate(layout));
        }

        [Test]
        public void GridSizeFollowsRulesAndIsCapped()
        {
            Assert.AreEqual(38, ParamsFor(1).Width);
            Assert.AreEqual(64, ParamsFor(500).Width);
            Assert.AreEqual(44, ParamsFor(500).Height);
            var layout = StageGenerator.Generate(1, 1, ParamsFor(1));
            Assert.AreEqual(38, layout.Grid.Width);
            Assert.AreEqual(27, layout.Grid.Height);
        }

        [Test]
        public void DifficultyNeverDecreasesAndIsCapped()
        {
            int previousEnemies = 0;
            for (int stage = 1; stage <= 200; stage++)
            {
                var p = ParamsFor(stage);
                Assert.GreaterOrEqual(p.EnemyCount, previousEnemies);
                Assert.LessOrEqual(p.EnemyCount, 14);
                Assert.LessOrEqual(p.PickupCount, 4);
                previousEnemies = p.EnemyCount;
            }
            Assert.Greater(ParamsFor(30).EnemyCount, ParamsFor(1).EnemyCount);
        }

        [Test]
        public void StageNumberBelowOneIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => StageRules.Create(0, EnemyTypes));
        }

        [Test]
        public void ValidatorDetectsUnreachableStairs()
        {
            var layout = TestMap.Parse(new[]
            {
                "#######",
                "#P.#.>#",
                "#######"
            });
            CollectionAssert.Contains(StageValidator.Validate(layout), "fewer than two rooms");
            Assert.IsTrue(StageValidator.Validate(layout).Any(p => p.Contains("not reachable")));
        }

        [Test]
        public void ValidatorDetectsPickupBlockingTheOnlyWay()
        {
            var layout = TestMap.Parse(new[]
            {
                "#######",
                "#PK..>#",
                "#######"
            });
            Assert.IsTrue(StageValidator.Validate(layout).Any(p => p.Contains("stairs not reachable")));
        }

        private static string Dump(StageLayout layout)
        {
            var sb = new System.Text.StringBuilder();
            for (int y = 0; y < layout.Grid.Height; y++)
                for (int x = 0; x < layout.Grid.Width; x++)
                    sb.Append((int)layout.Grid.Get(new GridPos(x, y)));
            foreach (var e in layout.Enemies) sb.Append(e.Pos).Append(e.TypeId);
            foreach (var p in layout.Pickups) sb.Append(p.Pos).Append(p.Kind);
            return sb.ToString();
        }
    }
}
