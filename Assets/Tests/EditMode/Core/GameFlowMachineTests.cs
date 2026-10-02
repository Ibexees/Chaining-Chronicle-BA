using System;
using System.Collections.Generic;
using CC.Core.Flow;
using CC.Core.Util;
using NUnit.Framework;

namespace CC.Tests.Core
{
    public class GameFlowMachineTests
    {
        [SetUp]
        public void SetUp() => GameEvents.ClearSubscribers();

        [TearDown]
        public void TearDown() => GameEvents.ClearSubscribers();

        [Test]
        public void StartsInBoot()
        {
            Assert.AreEqual(GameFlowState.Boot, new GameFlowMachine().Current);
        }

        [Test]
        public void FullRunCycleIsAllowed()
        {
            var machine = new GameFlowMachine();
            machine.TransitionTo(GameFlowState.Hub);
            machine.TransitionTo(GameFlowState.Exploring);
            machine.TransitionTo(GameFlowState.Combat);
            machine.TransitionTo(GameFlowState.Exploring); // win or flee
            machine.TransitionTo(GameFlowState.Combat);
            machine.TransitionTo(GameFlowState.GameOver);  // defeat
            machine.TransitionTo(GameFlowState.Hub);
            Assert.AreEqual(GameFlowState.Hub, machine.Current);
        }

        [TestCase(GameFlowState.Boot, GameFlowState.Exploring)]
        [TestCase(GameFlowState.Boot, GameFlowState.Combat)]
        [TestCase(GameFlowState.Hub, GameFlowState.Combat)]
        [TestCase(GameFlowState.Hub, GameFlowState.GameOver)]
        public void IllegalTransitionsThrow(GameFlowState from, GameFlowState to)
        {
            var machine = MachineIn(from);
            Assert.IsFalse(machine.CanTransitionTo(to));
            Assert.Throws<InvalidOperationException>(() => machine.TransitionTo(to));
            Assert.AreEqual(from, machine.Current);
        }

        [Test]
        public void CombatCannotReturnToHubDirectly()
        {
            Assert.IsFalse(MachineIn(GameFlowState.Combat).CanTransitionTo(GameFlowState.Hub));
        }

        [Test]
        public void TransitionRaisesEvent()
        {
            var received = new List<(GameFlowState, GameFlowState)>();
            GameEvents.FlowStateChanged += (a, b) => received.Add((a, b));

            new GameFlowMachine().TransitionTo(GameFlowState.Hub);

            Assert.AreEqual(1, received.Count);
            Assert.AreEqual((GameFlowState.Boot, GameFlowState.Hub), received[0]);
        }

        [Test]
        public void IllegalTransitionRaisesNoEvent()
        {
            int calls = 0;
            GameEvents.FlowStateChanged += (_, __) => calls++;

            Assert.Throws<InvalidOperationException>(() => new GameFlowMachine().TransitionTo(GameFlowState.Combat));

            Assert.AreEqual(0, calls);
        }

        private static GameFlowMachine MachineIn(GameFlowState target)
        {
            var machine = new GameFlowMachine();
            var path = new Dictionary<GameFlowState, GameFlowState[]>
            {
                [GameFlowState.Boot] = new GameFlowState[0],
                [GameFlowState.Hub] = new[] { GameFlowState.Hub },
                [GameFlowState.Exploring] = new[] { GameFlowState.Hub, GameFlowState.Exploring },
                [GameFlowState.Combat] = new[] { GameFlowState.Hub, GameFlowState.Exploring, GameFlowState.Combat },
                [GameFlowState.GameOver] = new[] { GameFlowState.Hub, GameFlowState.Exploring, GameFlowState.GameOver },
            };
            foreach (var step in path[target]) machine.TransitionTo(step);
            GameEvents.ClearSubscribers();
            return machine;
        }
    }
}
