using System.Collections.Generic;
using System.Linq;
using CC.Core.Combat;
using CC.Core.Util;
using NUnit.Framework;
using static CC.Tests.Combat.CombatTestSupport;

namespace CC.Tests.Combat
{
    /// <summary>A36: several enemies in one fight.</summary>
    public class MultiEnemyCombatTests
    {
        [SetUp]
        public void SetUp() => GameEvents.ClearSubscribers();

        [TearDown]
        public void TearDown() => GameEvents.ClearSubscribers();

        private static Combatant WeakFoe(string name, int speed, int hp = 10) =>
            Foe(name, speed, hp: hp, skills: new[] { Attack("poke", 1) });

        private static IEnumerable<CombatEvent> Of(IEnumerable<CombatEvent> events, CombatEventKind kind) =>
            events.Where(e => e.Kind == kind);

        [Test]
        public void TheFightIsOnlyWonWhenEveryEnemyIsDefeated()
        {
            var hero = Hero("hero", 9, hp: 100, attack: 20, skills: new[] { Attack("slash", 20) });
            var a = WeakFoe("A", 1);
            var b = WeakFoe("B", 1);
            var session = Session(new[] { hero }, new[] { a, b });
            session.Start();

            session.SubmitAction(hero.Skills[0], a);
            Assert.AreEqual(CombatOutcome.None, session.Outcome, "one enemy is still standing");
            Assert.IsFalse(a.IsAlive);
            Assert.IsTrue(b.IsAlive);

            session.SubmitAction(hero.Skills[0], b);
            Assert.AreEqual(CombatOutcome.Won, session.Outcome);
        }

        [Test]
        public void ValidTargetsAreTheLivingEnemies()
        {
            var hero = Hero("hero", 9, hp: 100, attack: 20, skills: new[] { Attack("slash", 20) });
            var a = WeakFoe("A", 1);
            var b = WeakFoe("B", 1);
            var session = Session(new[] { hero }, new[] { a, b });
            session.Start();

            CollectionAssert.AreEquivalent(new[] { a, b }, session.ValidTargets(hero, hero.Skills[0]));

            session.SubmitAction(hero.Skills[0], a);

            CollectionAssert.AreEqual(new[] { b }, session.ValidTargets(hero, hero.Skills[0]));
            Assert.Throws<System.ArgumentException>(() => session.SubmitAction(hero.Skills[0], a), "a defeated enemy is no target");
        }

        [Test]
        public void EveryEnemyTakesItsTurnInOneSharedOrder()
        {
            var hero = Hero("hero", 5, hp: 100, skills: new[] { Attack("slash", 1) });
            var fast = WeakFoe("fast", 12);
            var mid = WeakFoe("mid", 7);
            var slow = WeakFoe("slow", 2);
            var session = Session(new[] { hero }, new[] { slow, fast, mid });

            var events = session.Start();
            Assert.AreEqual(new[] { "fast", "mid" },
                Of(events, CombatEventKind.SkillUsed).Select(e => e.Actor.Name).ToArray(), "before the hero");

            // Rest of the round (hero, slow), then the next round starts with fast and mid before the hero is asked again.
            events = session.SubmitAction(hero.Skills[0], fast);
            Assert.AreEqual(new[] { "hero", "slow", "fast", "mid" },
                Of(events, CombatEventKind.SkillUsed).Select(e => e.Actor.Name).ToArray());
        }

        [Test]
        public void AChainOnlyHitsTheEnemyItStartedOn()
        {
            var a = Hero("A", 10, skills: new[] { Attack("slash", 4, applies: "bleed") });
            var b = Hero("B", 8, skills: new[] { Attack("flame", 3, applies: "burn", followUpOn: "bleed") });
            var first = WeakFoe("first", 1, hp: 100);
            var second = WeakFoe("second", 1, hp: 100);
            var session = Session(new[] { a, b }, new[] { first, second });
            session.Start();

            var events = session.SubmitAction(a.Skills[0], second);

            var used = Of(events, CombatEventKind.SkillUsed).ToList();
            Assert.AreEqual(2, used.Count);
            Assert.IsTrue(used.All(e => e.Target == second));
            Assert.AreEqual(100, first.Hp, "the other enemy is untouched");
            Assert.AreEqual(100 - 4 - 3, second.Hp);
        }

        [Test]
        public void ADefeatedEnemyDoesNotActAnymore()
        {
            var hero = Hero("hero", 9, hp: 100, attack: 20, skills: new[] { Attack("slash", 20) });
            var a = WeakFoe("A", 5);
            var b = WeakFoe("B", 3);
            var session = Session(new[] { hero }, new[] { a, b });
            session.Start();

            var events = session.SubmitAction(hero.Skills[0], a);

            var actors = Of(events, CombatEventKind.SkillUsed).Select(e => e.Actor.Name).ToList();
            CollectionAssert.DoesNotContain(actors, "A");
            CollectionAssert.Contains(actors, "B");
        }

        [Test]
        public void SeveralEnemiesCanDefeatTheParty()
        {
            var hero = Hero("hero", 1, hp: 6, skills: new[] { Attack("slash", 1) });
            var foes = new[]
            {
                Foe("A", 9, attack: 3, skills: new[] { Attack("hit", 3) }),
                Foe("B", 8, attack: 3, skills: new[] { Attack("hit", 3) })
            };
            var session = Session(new[] { hero }, foes);

            session.Start();

            Assert.AreEqual(CombatOutcome.Lost, session.Outcome);
        }

        [Test]
        public void FleeingLeavesTheWholeFight()
        {
            var hero = Hero("hero", 9, skills: new[] { Attack("slash", 1) });
            var session = Session(new[] { hero }, new[] { WeakFoe("A", 1), WeakFoe("B", 1) });
            session.Start();

            session.TryFlee();

            Assert.AreEqual(CombatOutcome.Fled, session.Outcome);
        }

        [Test]
        public void EnemyTypesAreToldApartByLabels()
        {
            var spec = new EnemySpec("mouse", "Mouse", new StatBlock(10, 3, 0, 8), new StatBlock(4, 1, 0, 0),
                new[] { Attack("bite", 2) });
            var rng = new SeededRng(1);

            var a = spec.CreateCombatant(100, 1, rng, "A");
            var b = spec.CreateCombatant(101, 1, rng, "B");
            var plain = spec.CreateCombatant(102, 1, rng);

            Assert.AreEqual("Mouse A", a.Name);
            Assert.AreEqual("Mouse B", b.Name);
            Assert.AreEqual("Mouse", plain.Name);
        }
    }
}
