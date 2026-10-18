using System.Linq;
using CC.Core.Grid;
using CC.Core.Stage;
using CC.Core.Util;
using NUnit.Framework;

namespace CC.Tests.Core
{
    /// <summary>A36: which enemies of the stage join a fight.</summary>
    public class CombatParticipantsTests
    {
        private static StageSession SessionOf(params string[] rows) =>
            new StageSession(TestMap.Parse(rows), ScriptedMover.Staying(), new SeededRng(1));

        /// <summary>The enemy standing on a map cell (column, row counted from the top row).</summary>
        private static StageEnemy EnemyAt(StageSession session, string[] rows, int col, int row) =>
            session.EnemyAt(new GridPos(col, rows.Length - 1 - row));

        [Test]
        public void EngagedEnemyComesFirstFollowedByTheNearestOthers()
        {
            var rows = new[]
            {
                "#########",
                "#E.E.P.E#",
                "#########"
            };
            var session = SessionOf(rows);
            var far = EnemyAt(session, rows, 1, 1);     // 4 steps from the player
            var engaged = EnemyAt(session, rows, 3, 1); // 2 steps
            var near = EnemyAt(session, rows, 7, 1);    // 2 steps

            var participants = session.CombatParticipants(engaged.Id, joinRadius: 5, maxEnemies: 4);

            // Engaged first, then by distance: near (2) before far (4).
            CollectionAssert.AreEqual(new[] { engaged, near, far }, participants.ToArray());
        }

        [Test]
        public void EnemiesBeyondTheRadiusDoNotJoin()
        {
            var rows = new[]
            {
                "############",
                "#E.......PE#",
                "############"
            };
            var session = SessionOf(rows);
            var close = EnemyAt(session, rows, 10, 1);
            var far = EnemyAt(session, rows, 1, 1);     // 8 steps

            var participants = session.CombatParticipants(close.Id, joinRadius: 5, maxEnemies: 4);

            CollectionAssert.AreEqual(new[] { close }, participants.ToArray());
            CollectionAssert.DoesNotContain(participants, far);
        }

        [Test]
        public void DistanceFollowsTheCorridorsNotTheStraightLine()
        {
            // The right enemy is two cells from the player in a straight line, but behind a wall: the way is 4 steps.
            var rows = new[]
            {
                "#######",
                "#EP#E.#",
                "#.....#",
                "#######"
            };
            var session = SessionOf(rows);
            var engaged = EnemyAt(session, rows, 1, 1);
            var behindWall = EnemyAt(session, rows, 4, 1);

            Assert.AreEqual(1, session.CombatParticipants(engaged.Id, joinRadius: 3, maxEnemies: 4).Count);
            CollectionAssert.Contains(session.CombatParticipants(engaged.Id, joinRadius: 4, maxEnemies: 4), behindWall);
        }

        [Test]
        public void TheLimitCapsTheNumberOfEnemies()
        {
            var rows = new[]
            {
                "#########",
                "#EEEPEEE#",
                "#########"
            };
            var session = SessionOf(rows);
            var engaged = EnemyAt(session, rows, 3, 1);

            var participants = session.CombatParticipants(engaged.Id, joinRadius: 9, maxEnemies: 3);

            Assert.AreEqual(3, participants.Count);
            Assert.AreSame(engaged, participants[0]);
        }

        [Test]
        public void EquallyFarEnemiesJoinInIdOrder()
        {
            var rows = new[]
            {
                "#######",
                "#E.P.E#",
                "###E###"
            };
            var session = SessionOf(rows);
            var engaged = EnemyAt(session, rows, 3, 2); // directly below the player
            var left = EnemyAt(session, rows, 1, 1);    // 2 steps, lower id
            var right = EnemyAt(session, rows, 5, 1);   // 2 steps, higher id

            var participants = session.CombatParticipants(engaged.Id, joinRadius: 4, maxEnemies: 4);

            CollectionAssert.AreEqual(new[] { engaged, left, right }, participants.ToArray());
            Assert.Less(left.Id, right.Id);
        }

        [Test]
        public void NobodyFightsWithoutAnEngagedEnemy()
        {
            var rows = new[] { "#####", "#PE.#", "#####" };
            var session = SessionOf(rows);

            Assert.IsEmpty(session.CombatParticipants(999, 5, 4));
            Assert.IsEmpty(session.CombatParticipants(session.Enemies[0].Id, 5, 0));
        }

        [Test]
        public void ZeroRadiusKeepsTheFightToTheEngagedEnemy()
        {
            var rows = new[] { "#######", "#EEPE.#", "#######" };
            var session = SessionOf(rows);

            var participants = session.CombatParticipants(session.Enemies[0].Id, joinRadius: 0, maxEnemies: 4);

            Assert.AreEqual(1, participants.Count);
        }
    }
}
