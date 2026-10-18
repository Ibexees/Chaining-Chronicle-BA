using System.Linq;
using CC.Core.Combat;
using CC.Core.Util;
using NUnit.Framework;
using static CC.Tests.Combat.CombatTestSupport;

namespace CC.Tests.Combat
{
    public class EnemyBrainTests
    {
        private static (CombatSession session, Combatant foe) Fight(Combatant foe, params Combatant[] party)
        {
            var session = Session(party, new[] { foe });
            return (session, foe);
        }

        [Test]
        public void HealsBelowThirtyPercent()
        {
            var hero = Hero("hero", 1, skills: new[] { Attack("slash", 1) });
            var brain = new FsmEnemyBrain();
            var foe = Foe("foe", 5, hp: 20, brain: brain, skills: new[] { Attack("bite", 4), Heal("mend", 6) });
            foe.TakeDamage(15); // 5/20 = 25 %
            var (session, _) = Fight(foe, hero);

            var action = brain.ChooseAction(session, foe);

            Assert.AreEqual("mend", action.Skill.Id);
            Assert.AreSame(foe, action.Target);
            Assert.AreEqual(FsmState.Heal, brain.State);
        }

        [Test]
        public void ThirtyPercentExactlyIsStillAggressive()
        {
            var hero = Hero("hero", 1, skills: new[] { Attack("slash", 1) });
            var brain = new FsmEnemyBrain();
            var foe = Foe("foe", 5, hp: 20, brain: brain, skills: new[] { Attack("bite", 4), Heal("mend", 6) });
            foe.TakeDamage(14); // 6/20 = 30 %
            var (session, _) = Fight(foe, hero);

            var action = brain.ChooseAction(session, foe);

            Assert.AreEqual("bite", action.Skill.Id);
            Assert.AreEqual(FsmState.Aggressive, brain.State);
        }

        [Test]
        public void AttacksWhenLowOnHitPointsButCannotHeal()
        {
            var hero = Hero("hero", 1, skills: new[] { Attack("slash", 1) });
            var brain = new FsmEnemyBrain();
            var foe = Foe("foe", 5, hp: 20, brain: brain, skills: new[] { Attack("bite", 4) });
            foe.TakeDamage(18);
            var (session, _) = Fight(foe, hero);

            var action = brain.ChooseAction(session, foe);

            Assert.AreEqual("bite", action.Skill.Id);
        }

        [Test]
        public void PrefersATargetItCanKill()
        {
            var tank = Hero("tank", 1, hp: 50, skills: new[] { Attack("x", 1) });
            var weak = Hero("weak", 1, hp: 4, skills: new[] { Attack("x", 1) });
            var brain = new FsmEnemyBrain();
            var foe = Foe("foe", 5, brain: brain, skills: new[] { Attack("bite", 5) });
            var (session, _) = Fight(foe, tank, weak);

            var action = brain.ChooseAction(session, foe);

            Assert.AreSame(weak, action.Target);
        }

        [Test]
        public void WithoutAKillItTargetsTheWeakestAndHitsHardest()
        {
            var a = Hero("a", 1, hp: 40, skills: new[] { Attack("x", 1) });
            var b = Hero("b", 1, hp: 30, skills: new[] { Attack("x", 1) });
            var brain = new FsmEnemyBrain();
            var foe = Foe("foe", 5, brain: brain, skills: new[] { Attack("jab", 2), Attack("smash", 7) });
            var (session, _) = Fight(foe, a, b);

            var action = brain.ChooseAction(session, foe);

            Assert.AreSame(b, action.Target);
            Assert.AreEqual("smash", action.Skill.Id);
        }

        [Test]
        public void RandomBrainOnlyPicksLegalActions()
        {
            var hero = Hero("hero", 1, hp: 500, skills: new[] { Attack("x", 1) });
            var other = Hero("other", 1, hp: 500, skills: new[] { Attack("x", 1) });
            for (int seed = 0; seed < 40; seed++)
            {
                var brain = new RandomEnemyBrain(new SeededRng(seed));
                var foe = Foe("foe", 5, brain: brain,
                    skills: new[] { Attack("bite", 2), Heal("mend", 3) });
                var (session, _) = Fight(foe, hero, other);

                var action = brain.ChooseAction(session, foe);

                CollectionAssert.Contains(session.ValidTargets(foe, action.Skill), action.Target);
            }
        }

        [Test]
        public void ARunOfFsmEnemiesAlwaysFinishes()
        {
            // Smoke test: the loop must never get stuck, whatever the numbers are.
            for (int seed = 1; seed <= 25; seed++)
            {
                var rng = new SeededRng(seed);
                var hero = Hero("hero", rng.Next(1, 15), hp: 60, attack: 4, defense: 1, skills: new[] { Attack("slash", 5, applies: "bleed") });
                var foe = Foe("foe", rng.Next(1, 15), hp: 50, attack: 3, brain: new FsmEnemyBrain(),
                    skills: new[] { Attack("bite", 3), Heal("mend", 4) });
                var session = new CombatSession(new[] { hero }, new[] { foe }, rng, new ChainResolver(new ChainConfig()));
                session.Start();

                int guard = 0;
                while (session.Outcome == CombatOutcome.None)
                {
                    session.SubmitAction(hero.Skills[0], foe);
                    Assert.Less(++guard, 200, $"seed {seed} did not finish");
                }
                Assert.AreNotEqual(CombatOutcome.None, session.Outcome);
            }
        }
    }
}
