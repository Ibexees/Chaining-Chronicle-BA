using CC.Core.Grid;
using CC.Core.Stage;
using CC.Core.Util;
using NUnit.Framework;

namespace CC.Tests.Core.Stage
{
    public class ChaseOrWanderMoverTests
    {
        [SetUp]
        public void SetUp() => GameEvents.ClearSubscribers();

        [TearDown]
        public void TearDown() => GameEvents.ClearSubscribers();

        private static StageSession Session(params string[] rows) =>
            new StageSession(TestMap.Parse(rows), ScriptedMover.Staying(), new SeededRng(1));

        [Test]
        public void ChasesThePlayerAlongTheShortestPath()
        {
            var session = Session("#########", "#P...E..#", "#########");
            var mover = new ChaseOrWanderMover(sightRange: 7, wanderChance: 0f);
            var enemy = session.Enemies[0];

            var step = mover.ChooseStep(enemy, session, new SeededRng(1));

            Assert.AreEqual(new GridPos(4, 1), step);
        }

        [Test]
        public void StepsAroundObstaclesWhileChasing()
        {
            var session = Session(
                "#######",
                "#.....#",
                "#P#E..#",
                "#.....#",
                "#######");
            var mover = new ChaseOrWanderMover(sightRange: 7, wanderChance: 0f);

            var step = mover.ChooseStep(session.Enemies[0], session, new SeededRng(1));

            Assert.AreEqual(1, GridPos.Manhattan(session.Enemies[0].Pos, step));
            Assert.IsTrue(session.Grid.IsWalkable(step));
        }

        [Test]
        public void ChaseCanEndOnThePlayersCell()
        {
            var session = Session("#####", "#PE.#", "#####");
            var mover = new ChaseOrWanderMover(7, 0f);

            var step = mover.ChooseStep(session.Enemies[0], session, new SeededRng(1));

            Assert.AreEqual(session.PlayerPos, step);
        }

        [Test]
        public void DoesNotNoticeThePlayerBeyondSightRange()
        {
            var session = Session("###########", "#P......E.#", "###########");
            var mover = new ChaseOrWanderMover(sightRange: 3, wanderChance: 0f);

            var step = mover.ChooseStep(session.Enemies[0], session, new SeededRng(1));

            Assert.AreEqual(session.Enemies[0].Pos, step, "idle: out of sight and no wandering");
        }

        [Test]
        public void WandersWhenPlayerIsBehindAWall()
        {
            // Manhattan distance is 2, but the walking path is far longer than the sight range.
            var session = Session(
                "#########",
                "#.......#",
                "#P#E....#",
                "#########");
            var mover = new ChaseOrWanderMover(sightRange: 3, wanderChance: 1f);
            var enemy = session.Enemies[0];

            var step = mover.ChooseStep(enemy, session, new SeededRng(1));

            Assert.AreNotEqual(enemy.Pos, step);
            Assert.AreEqual(1, GridPos.Manhattan(enemy.Pos, step));
            Assert.IsTrue(session.Grid.IsWalkable(step));
        }

        [Test]
        public void WanderingNeverLeavesWalkableCellsOrEntersOccupiedOnes()
        {
            var session = Session(
                "#######",
                "#E.K..#",
                "#..E.P#",
                "#######");
            var mover = new ChaseOrWanderMover(sightRange: 0, wanderChance: 1f);
            var rng = new SeededRng(42);

            for (int i = 0; i < 500; i++)
            {
                foreach (var enemy in session.Enemies)
                {
                    var step = mover.ChooseStep(enemy, session, rng);
                    Assert.IsTrue(session.Grid.IsWalkable(step));
                    Assert.LessOrEqual(GridPos.Manhattan(enemy.Pos, step), 1);
                    if (step != enemy.Pos)
                    {
                        Assert.IsNull(session.PickupAt(step), "walked onto a pickup");
                        Assert.IsNull(session.EnemyAt(step), "walked onto an enemy");
                    }
                }
            }
        }

        [Test]
        public void EnemiesInAClosedCellStayPut()
        {
            var session = Session("#####", "#E#P#", "#####");
            var mover = new ChaseOrWanderMover(sightRange: 0, wanderChance: 1f);

            var step = mover.ChooseStep(session.Enemies[0], session, new SeededRng(1));

            Assert.AreEqual(session.Enemies[0].Pos, step);
        }

        [Test]
        public void ChasingEnemyDoesNotWalkThroughOtherEnemies()
        {
            var session = Session("#######", "#PEE..#", "#######");
            var mover = new ChaseOrWanderMover(7, 0f);

            // The rear enemy (id 1) is boxed in by the front enemy and the wall: no path, so it wanders (chance 0: stays).
            var step = mover.ChooseStep(session.Enemies[1], session, new SeededRng(1));

            Assert.AreEqual(session.Enemies[1].Pos, step);
        }
    }
}
