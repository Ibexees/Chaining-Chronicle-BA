using System;
using System.Collections.Generic;
using CC.Core.Grid;
using CC.Core.Stage;
using CC.Core.Util;
using NUnit.Framework;

namespace CC.Tests.Core.Stage
{
    public class StageSessionTests
    {
        [SetUp]
        public void SetUp() => GameEvents.ClearSubscribers();

        [TearDown]
        public void TearDown() => GameEvents.ClearSubscribers();

        private static StageSession Session(string[] rows, IStageEnemyMover mover = null) =>
            new StageSession(TestMap.Parse(rows), mover ?? ScriptedMover.Staying(), new SeededRng(1));

        private static readonly string[] Room =
        {
            "#######",
            "#P....#",
            "#.E...#",
            "#K...>#",
            "#######"
        };

        // Moves an enemy one cell towards the player along the larger axis, ignoring obstacles.
        private static GridPos Approach(StageEnemy enemy, IStageWorld world)
        {
            int dx = world.PlayerPos.X - enemy.Pos.X;
            int dy = world.PlayerPos.Y - enemy.Pos.Y;
            if (Math.Abs(dx) >= Math.Abs(dy) && dx != 0) return new GridPos(enemy.Pos.X + Math.Sign(dx), enemy.Pos.Y);
            if (dy != 0) return new GridPos(enemy.Pos.X, enemy.Pos.Y + Math.Sign(dy));
            return enemy.Pos;
        }

        [Test]
        public void StartsAtPlayerStartWithEnemiesAndPickups()
        {
            var session = Session(Room);
            Assert.AreEqual(TestMap.At(Room, 'P'), session.PlayerPos);
            Assert.AreEqual(1, session.Enemies.Count);
            Assert.AreEqual(1, session.Pickups.Count);
            Assert.AreEqual(-1, session.EngagedEnemyId);
        }

        [Test]
        public void WalkingIntoWallOnlyTurnsAndCostsNoTurn()
        {
            var mover = ScriptedMover.Staying();
            var session = Session(Room, mover);
            var start = session.PlayerPos;

            var result = session.TryMove(Direction.Up);

            Assert.AreEqual(TurnKind.Turned, result.Kind);
            Assert.AreEqual(start, session.PlayerPos);
            Assert.AreEqual(Direction.Up, session.Facing);
            Assert.IsFalse(result.TimePassed);
            Assert.AreEqual(0, mover.Calls);
        }

        [Test]
        public void WalkingOneCellMovesPlayerAndLetsEveryEnemyAct()
        {
            var mover = ScriptedMover.Staying();
            var session = Session(Room, mover);
            var start = session.PlayerPos;

            var result = session.TryMove(Direction.Right);

            Assert.AreEqual(TurnKind.Moved, result.Kind);
            Assert.AreEqual(start.Step(Direction.Right), session.PlayerPos);
            Assert.AreEqual(start, result.PlayerFrom);
            Assert.AreEqual(session.PlayerPos, result.PlayerTo);
            Assert.AreEqual(1, mover.Calls);
            Assert.IsTrue(result.TimePassed);
        }

        [Test]
        public void EnemiesNeverMoveWithoutPlayerAction()
        {
            var mover = ScriptedMover.Staying();
            var session = Session(Room, mover);

            session.TryMove(Direction.Up);     // wall
            session.TryMove(Direction.Left);   // wall
            session.Interact();                // nothing in front

            Assert.AreEqual(0, mover.Calls);
        }

        [Test]
        public void EnemyMovesAreReportedForAnimation()
        {
            var session = Session(Room, new ScriptedMover(Approach));
            var enemy = session.Enemies[0];
            var before = enemy.Pos;

            var result = session.TryMove(Direction.Right);

            Assert.AreEqual(1, result.EnemyMoves.Count);
            Assert.AreEqual(enemy.Id, result.EnemyMoves[0].EnemyId);
            Assert.AreEqual(before, result.EnemyMoves[0].From);
            Assert.AreEqual(enemy.Pos, result.EnemyMoves[0].To);
            Assert.AreNotEqual(before, enemy.Pos);
        }

        [Test]
        public void WalkingOntoEnemyStartsCombatWithoutEnemyTurn()
        {
            var rows = new[] { "#####", "#PE.#", "#####" };
            var mover = ScriptedMover.Staying();
            var session = Session(rows, mover);
            int engagedId = -2;
            GameEvents.EnemyEngaged += id => engagedId = id;

            var result = session.TryMove(Direction.Right);

            Assert.IsTrue(result.EnemyEngaged);
            Assert.AreEqual(session.Enemies[0].Id, result.EngagedEnemyId);
            Assert.AreEqual(session.Enemies[0].Pos, session.PlayerPos);
            Assert.AreEqual(session.Enemies[0].Id, session.EngagedEnemyId);
            Assert.AreEqual(session.Enemies[0].Id, engagedId);
            Assert.AreEqual(0, mover.Calls);
            Assert.AreEqual(0, result.EnemyMoves.Count);
        }

        [Test]
        public void EnemyWalkingOntoPlayerStartsCombat()
        {
            // After the player steps to x=2 the enemy at x=3 is adjacent and walks onto the player.
            var rows = new[] { "######", "#P.E.#", "######" };
            var session = Session(rows, new ScriptedMover(Approach));

            var result = session.TryMove(Direction.Right);

            Assert.AreEqual(TurnKind.Moved, result.Kind);
            Assert.IsTrue(result.EnemyEngaged);
            Assert.AreEqual(session.PlayerPos, session.Enemies[0].Pos);
            Assert.AreEqual(session.Enemies[0].Id, session.EngagedEnemyId);
        }

        [Test]
        public void InputIsIgnoredWhileCombatIsPendingAndResumesAfterRemoval()
        {
            var rows = new[] { "######", "#PE..#", "######" };
            var session = Session(rows);
            session.TryMove(Direction.Right);
            int enemyId = session.EngagedEnemyId;

            Assert.AreEqual(TurnKind.Ignored, session.TryMove(Direction.Right).Kind);
            Assert.AreEqual(TurnKind.Ignored, session.Interact().Kind);

            session.RemoveEnemy(enemyId);

            Assert.AreEqual(-1, session.EngagedEnemyId);
            Assert.AreEqual(0, session.Enemies.Count);
            Assert.AreEqual(TurnKind.Moved, session.TryMove(Direction.Right).Kind);
        }

        [Test]
        public void RemovingUnknownEnemyThrows()
        {
            Assert.Throws<ArgumentException>(() => Session(Room).RemoveEnemy(999));
        }

        [Test]
        public void OtherEnemiesWaitOnceOneEnemyEngages()
        {
            // Enemies act in spawn order. The left one (id 0) reaches the player's new cell first,
            // so the right one (id 1) must not move this turn.
            var rows = new[] { "#######", "#.E.PE#", "#######" };
            var session = Session(rows, new ScriptedMover(Approach));

            var result = session.TryMove(Direction.Left);

            Assert.AreEqual(TurnKind.Moved, result.Kind);
            Assert.IsTrue(result.EnemyEngaged);
            Assert.AreEqual(session.Enemies[0].Id, result.EngagedEnemyId);
            Assert.AreEqual(1, result.EnemyMoves.Count);
            Assert.AreEqual(new GridPos(5, 1), session.Enemies[1].Pos);
        }

        [Test]
        public void PickupBlocksMovementButPlayerTurnsToFaceIt()
        {
            var rows = new[] { "####", "#PK#", "####" };
            var session = Session(rows);

            var result = session.TryMove(Direction.Right);

            Assert.AreEqual(TurnKind.Turned, result.Kind);
            Assert.AreEqual(Direction.Right, session.Facing);
            Assert.AreEqual(new GridPos(1, 1), session.PlayerPos);
        }

        [Test]
        public void InteractCollectsFacedPickupAndCostsATurn()
        {
            var rows = new[] { "#####", "#PK.#", "#####" };
            var mover = ScriptedMover.Staying();
            var session = Session(rows, mover);
            PickupKind? collected = null;
            GameEvents.PowerUpCollected += kind => collected = kind;
            session.TryMove(Direction.Right); // turns, pickup blocks

            var result = session.Interact();

            Assert.AreEqual(TurnKind.PickedUp, result.Kind);
            Assert.AreEqual(PickupKind.Armor, result.Pickup);
            Assert.AreEqual(PickupKind.Armor, collected);
            Assert.AreEqual(0, session.Pickups.Count);
            Assert.IsTrue(result.TimePassed);
            Assert.AreEqual(TurnKind.Moved, session.TryMove(Direction.Right).Kind, "cell is free after collecting");
        }

        [Test]
        public void CollectingLetsEnemiesAct()
        {
            var rows = new[] { "######", "#PK.E#", "######" };
            var mover = ScriptedMover.Staying();
            var session = Session(rows, mover);
            session.TryMove(Direction.Right); // turn towards pickup
            Assert.AreEqual(0, mover.Calls);

            session.Interact();

            Assert.AreEqual(1, mover.Calls);
        }

        [Test]
        public void InteractNeedsToFaceThePickup()
        {
            var rows = new[] { "#####", "#PK.#", "#####" };
            var session = Session(rows); // facing Down at the start

            var result = session.Interact();

            Assert.AreEqual(TurnKind.NothingToInteract, result.Kind);
            Assert.AreEqual(1, session.Pickups.Count);
        }

        [Test]
        public void InteractOnStairsDescendsAndFinishesTheSession()
        {
            var rows = new[] { "####", "#P>#", "####" };
            var session = Session(rows);
            session.TryMove(Direction.Right);
            Assert.IsTrue(session.IsOnStairs);

            var result = session.Interact();

            Assert.AreEqual(TurnKind.Descended, result.Kind);
            Assert.AreEqual(TurnKind.Ignored, session.TryMove(Direction.Left).Kind);
            Assert.AreEqual(TurnKind.Ignored, session.Interact().Kind);
        }

        [Test]
        public void WalkingOntoStairsDoesNotDescendOnItsOwn()
        {
            var rows = new[] { "####", "#P>#", "####" };
            var session = Session(rows);

            var result = session.TryMove(Direction.Right);

            Assert.AreEqual(TurnKind.Moved, result.Kind);
            Assert.IsTrue(session.IsOnStairs);
        }

        [Test]
        public void IllegalEnemyStepsAreIgnored()
        {
            // Enemy 0 at (3,1) asks for a teleport to (1,1), enemy 1 at (5,1) asks to walk into the wall at (6,1).
            var rows = new[] { "#######", "#P.E.E#", "#######" };
            var mover = new ScriptedMover((enemy, world) =>
                enemy.Id == 0 ? new GridPos(1, 1) : new GridPos(6, 1));
            var session = new StageSession(TestMap.Parse(rows), mover, new SeededRng(1));

            session.TryMove(Direction.Right);

            Assert.AreEqual(new GridPos(3, 1), session.Enemies[0].Pos);
            Assert.AreEqual(new GridPos(5, 1), session.Enemies[1].Pos);
            Assert.AreEqual(-1, session.EngagedEnemyId);
        }

        [Test]
        public void EnemyCannotStepOntoOtherEnemyOrPickup()
        {
            var rows = new[] { "########", "#P..EEK#", "########" };
            // Enemy 0 at (4,1) tries to walk right onto enemy 1 (5,1); enemy 1 tries to walk onto the pickup (6,1).
            var mover = new ScriptedMover((enemy, world) => new GridPos(enemy.Pos.X + 1, enemy.Pos.Y));
            var session = new StageSession(TestMap.Parse(rows), mover, new SeededRng(1));

            session.TryMove(Direction.Right);

            Assert.AreEqual(new GridPos(4, 1), session.Enemies[0].Pos);
            Assert.AreEqual(new GridPos(5, 1), session.Enemies[1].Pos);
        }

        [Test]
        public void PlayerMovedAndStageStartedEventsAreRaised()
        {
            var cells = new List<GridPos>();
            var stages = new List<int>();
            GameEvents.PlayerMoved += cells.Add;
            GameEvents.StageStarted += stages.Add;

            var session = new StageSession(TestMap.Parse(new[] { "#####", "#P..#", "#####" }, stageNumber: 7),
                ScriptedMover.Staying(), new SeededRng(1));
            session.TryMove(Direction.Right);
            session.TryMove(Direction.Up); // wall: no event

            CollectionAssert.AreEqual(new[] { 7 }, stages);
            CollectionAssert.AreEqual(new[] { new GridPos(2, 1) }, cells);
        }

        [Test]
        public void SessionIsDeterministicWithTheSameSeed()
        {
            var layout = StageGenerator.Generate(5, 3, StageRules.Create(3, new[] { "a", "b" }));
            var a = new StageSession(layout, new ChaseOrWanderMover(), new SeededRng(9));
            var b = new StageSession(layout, new ChaseOrWanderMover(), new SeededRng(9));

            foreach (var direction in new[] { Direction.Right, Direction.Down, Direction.Left, Direction.Up, Direction.Right, Direction.Right })
            {
                a.TryMove(direction);
                b.TryMove(direction);
                if (a.EngagedEnemyId >= 0) break;
            }

            Assert.AreEqual(a.PlayerPos, b.PlayerPos);
            for (int i = 0; i < a.Enemies.Count; i++) Assert.AreEqual(a.Enemies[i].Pos, b.Enemies[i].Pos);
        }
    }
}
