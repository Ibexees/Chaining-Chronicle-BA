using System.Linq;
using CC.Core.Combat;
using NUnit.Framework;
using static CC.Tests.Combat.CombatTestSupport;

namespace CC.Tests.Combat
{
    public class TurnOrderTests
    {
        [Test]
        public void FastestActsFirstAcrossBothTeams()
        {
            // Decision on A4: one shared order, no head start for the player.
            var slowHero = Hero("slowHero", speed: 5);
            var fastFoe = Foe("fastFoe", speed: 12);
            var midHero = Hero("midHero", speed: 8);

            var order = new TurnOrder(new[] { slowHero, fastFoe, midHero });

            Assert.AreEqual(new[] { fastFoe, midHero, slowHero }, order.All.ToArray());
            Assert.AreSame(fastFoe, order.Current);
        }

        [Test]
        public void PartyActsFirstOnEqualSpeed()
        {
            var foe = Foe("foe", speed: 7);
            var hero = Hero("hero", speed: 7);

            var order = new TurnOrder(new[] { foe, hero });

            Assert.AreSame(hero, order.Current);
        }

        [Test]
        public void AdvanceWrapsAroundAndCountsRounds()
        {
            var a = Hero("a", 10);
            var b = Foe("b", 5);
            var order = new TurnOrder(new[] { a, b });

            Assert.AreEqual(1, order.Round);
            order.Advance();
            Assert.AreSame(b, order.Current);
            order.Advance();
            Assert.AreSame(a, order.Current);
            Assert.AreEqual(2, order.Round);
        }

        [Test]
        public void DefeatedCombatantsAreSkipped()
        {
            var a = Hero("a", 10);
            var b = Hero("b", 8);
            var c = Foe("c", 5);
            var order = new TurnOrder(new[] { a, b, c });

            b.TakeDamage(999);
            order.Advance();

            Assert.AreSame(c, order.Current);
            Assert.AreEqual(new[] { a, c }, order.Alive.ToArray());
        }
    }

    public class ChainResolverTests
    {
        private static ChainResolver Resolver(int max = 5, bool once = false) =>
            new ChainResolver(new ChainConfig(max, once));

        [Test]
        public void FindsFollowUpWithMatchingStatus()
        {
            var starter = Hero("starter", 10, skills: new[] { Attack("slash", 3, applies: "bleed") });
            var partner = Hero("partner", 8, skills: new[] { Attack("flame", 3, applies: "burn", followUpOn: "bleed") });

            bool found = Resolver().TryFindFollowUp(new ChainState(), new[] { starter, partner }, "bleed", out var actor, out var skill);

            Assert.IsTrue(found);
            Assert.AreSame(partner, actor);
            Assert.AreEqual("flame", skill.Id);
        }

        [Test]
        public void NoFollowUpWhenStatusDoesNotMatch()
        {
            var partner = Hero("partner", 8, skills: new[] { Attack("flame", 3, followUpOn: "bleed") });

            Assert.IsFalse(Resolver().TryFindFollowUp(new ChainState(), new[] { partner }, "burn", out _, out _));
        }

        [Test]
        public void NoFollowUpWithoutStatus()
        {
            var partner = Hero("partner", 8, skills: new[] { Attack("flame", 3, followUpOn: "bleed") });

            Assert.IsFalse(Resolver().TryFindFollowUp(new ChainState(), new[] { partner }, null, out _, out _));
        }

        [Test]
        public void FastestMatchingCombatantJoinsFirst()
        {
            var fast = Hero("fast", 12, skills: new[] { Attack("a", 1, followUpOn: "bleed") });
            var slow = Hero("slow", 4, skills: new[] { Attack("b", 1, followUpOn: "bleed") });

            Resolver().TryFindFollowUp(new ChainState(), new[] { fast, slow }, "bleed", out var actor, out _);

            Assert.AreSame(fast, actor);
        }

        [Test]
        public void DefeatedCombatantsCannotJoin()
        {
            var dead = Hero("dead", 12, hp: 1, skills: new[] { Attack("a", 1, followUpOn: "bleed") });
            dead.TakeDamage(5);

            Assert.IsFalse(Resolver().TryFindFollowUp(new ChainState(), new[] { dead }, "bleed", out _, out _));
        }

        [Test]
        public void UpperLimitEndsTheChain()
        {
            var partner = Hero("partner", 8, skills: new[] { Attack("flame", 3, followUpOn: "bleed") });
            var chain = new ChainState();
            for (int i = 0; i < 3; i++) chain.Add(partner, partner.Skills[0]);

            Assert.IsTrue(Resolver(max: 4).TryFindFollowUp(chain, new[] { partner }, "bleed", out _, out _));
            chain.Add(partner, partner.Skills[0]);
            Assert.IsFalse(Resolver(max: 4).TryFindFollowUp(chain, new[] { partner }, "bleed", out _, out _));
        }

        [Test]
        public void SameSkillRuleBlocksRepeatsWhenEnabled()
        {
            var partner = Hero("partner", 8, skills: new[] { Attack("flame", 3, followUpOn: "bleed") });
            var chain = new ChainState();
            chain.Add(partner, partner.Skills[0]);

            Assert.IsTrue(Resolver(once: false).TryFindFollowUp(chain, new[] { partner }, "bleed", out _, out _));
            Assert.IsFalse(Resolver(once: true).TryFindFollowUp(chain, new[] { partner }, "bleed", out _, out _));
        }

        [Test]
        public void ChainConfigRejectsZeroLimit()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new ChainConfig(0));
        }

        [Test]
        public void SkillValidatesItsRules()
        {
            Assert.Throws<System.ArgumentException>(() => new Skill("h", "h", 1, SkillTarget.Enemy, SkillKind.Heal));
            Assert.Throws<System.ArgumentException>(() => new Skill("f", "f", 1, SkillTarget.Ally, SkillKind.Damage, followUpOn: "x"));
            Assert.Throws<System.ArgumentException>(() => new Skill("", "n", 1, SkillTarget.Enemy, SkillKind.Damage));
        }
    }
}
