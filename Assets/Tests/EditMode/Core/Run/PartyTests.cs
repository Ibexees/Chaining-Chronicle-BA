using System.Collections.Generic;
using System.Linq;
using CC.Core.Combat;
using CC.Core.Run;
using CC.Core.Stage;
using CC.Core.Util;
using NUnit.Framework;

namespace CC.Tests.Run
{
    public class PartyTests
    {
        private static Skill Strike(string id) => new Skill(id, id, 3, SkillTarget.Enemy, SkillKind.Damage);

        private static PartyMemberSpec Spec(string id = "hero", params Skill[] skills) =>
            new PartyMemberSpec(id, new StatBlock(30, 5, 2, 10), new StatBlock(6, 1, 1, 1), skills);

        // ---- level and experience

        [Test]
        public void StatsGrowWithTheLevel()
        {
            var member = new PartyMember(Spec(), "Ada", level: 3);

            Assert.AreEqual(new StatBlock(42, 7, 4, 12).ToString(), member.Stats.ToString());
            Assert.AreEqual(42, member.Hp);
        }

        [Test]
        public void ExperienceBelowTheThresholdOnlyAccumulates()
        {
            var member = new PartyMember(Spec(), "Ada");

            var levels = member.GainXp(19);

            Assert.IsEmpty(levels);
            Assert.AreEqual(1, member.Level);
            Assert.AreEqual(19, member.Xp);
        }

        [Test]
        public void LevelUpRaisesStatsAndHealsOnlyTheGain()
        {
            var member = new PartyMember(Spec(), "Ada");
            member.Hp = 10;

            var levels = member.GainXp(25); // 20 needed for level 2, 5 carry over

            CollectionAssert.AreEqual(new[] { 2 }, levels.ToArray());
            Assert.AreEqual(2, member.Level);
            Assert.AreEqual(5, member.Xp);
            Assert.AreEqual(36, member.Stats.MaxHp);
            Assert.AreEqual(16, member.Hp); // 10 + 6 gained, no full heal
        }

        [Test]
        public void SeveralLevelsCanBeGainedAtOnce()
        {
            var member = new PartyMember(Spec(), "Ada");

            var levels = member.GainXp(20 + 40 + 7);

            CollectionAssert.AreEqual(new[] { 2, 3 }, levels.ToArray());
            Assert.AreEqual(7, member.Xp);
        }

        [Test]
        public void ThresholdGrowsWithTheLevel()
        {
            Assert.AreEqual(20, PartyMember.XpForNextLevel(1));
            Assert.AreEqual(60, PartyMember.XpForNextLevel(3));
        }

        // ---- skills and slots

        [Test]
        public void SkillSlotsAreLimitedAndSkillsAreUnique()
        {
            var member = new PartyMember(Spec("hero", Strike("a")), "Ada");

            Assert.IsFalse(member.TryLearn(Strike("a")), "already known");
            Assert.IsTrue(member.TryLearn(Strike("b")));
            Assert.IsTrue(member.TryLearn(Strike("c")));
            Assert.IsTrue(member.TryLearn(Strike("d")));
            Assert.IsFalse(member.TryLearn(Strike("e")), "no free slot");
            Assert.AreEqual(PartyMember.MaxSkillSlots, member.Skills.Count);
        }

        // ---- party

        [Test]
        public void PartyIsLimitedInSize()
        {
            var party = new Party();
            for (int i = 0; i < Party.MaxSize; i++) party.Add(new PartyMember(Spec(), "m" + i));

            Assert.IsTrue(party.IsFull);
            Assert.Throws<System.InvalidOperationException>(() => party.Add(new PartyMember(Spec(), "x")));
        }

        [Test]
        public void ArmorAppliesToEveryoneAndToLaterRecruits()
        {
            var party = new Party();
            var hero = new PartyMember(Spec(), "Ada");
            party.Add(hero);

            party.AddArmor(2);
            var late = new PartyMember(Spec(), "Bo", armorBonus: party.ArmorBonus);

            Assert.AreEqual(4, hero.Stats.Defense);
            Assert.AreEqual(4, late.Stats.Defense);
        }

        [Test]
        public void RunStateStartsWithAnEmptyParty()
        {
            var run = new RunState(1, "Ada");

            Assert.AreEqual(0, run.Party.Count);
            Assert.IsFalse(run.Party.IsDefeated);
        }
    }

    public class PowerUpEffectsTests
    {
        private sealed class Catalog : IPartyCatalog
        {
            public IReadOnlyList<PartyMemberSpec> RecruitSpecs { get; set; } = new List<PartyMemberSpec>();
            public IReadOnlyList<Skill> ChainSkills { get; set; } = new List<Skill>();
            public IReadOnlyList<string> Names { get; set; } = new List<string>();
        }

        private static Skill Strike(string id) => new Skill(id, id, 3, SkillTarget.Enemy, SkillKind.Damage);

        private static PartyMemberSpec Spec(string id) =>
            new PartyMemberSpec(id, new StatBlock(20, 4, 1, 8), new StatBlock(4, 1, 0, 0), new[] { Strike("basic") });

        private static Party PartyWithHero()
        {
            var party = new Party();
            party.Add(new PartyMember(Spec("hero"), "Ada"));
            return party;
        }

        [Test]
        public void ArmorRaisesDefenseOfTheWholeParty()
        {
            var party = PartyWithHero();
            int before = party.Members[0].Stats.Defense;

            var result = PowerUpEffects.Apply(PickupKind.Armor, party, new Catalog(), new SeededRng(1));

            Assert.AreEqual(PickupOutcome.ArmorEquipped, result.Outcome);
            Assert.AreEqual(before + PowerUpEffects.ArmorDefense, party.Members[0].Stats.Defense);
        }

        [Test]
        public void ChainSkillIsTaughtToAMemberWithAFreeSlot()
        {
            var party = PartyWithHero();
            var catalog = new Catalog { ChainSkills = new[] { Strike("flame") } };

            var result = PowerUpEffects.Apply(PickupKind.ChainSkill, party, catalog, new SeededRng(1));

            Assert.AreEqual(PickupOutcome.SkillLearned, result.Outcome);
            Assert.AreEqual("flame", result.Skill.Id);
            Assert.IsTrue(party.Members[0].Knows(result.Skill));
        }

        [Test]
        public void ChainSkillGoesToTheNextMemberWhenTheFirstKnowsIt()
        {
            var party = PartyWithHero();
            var second = new PartyMember(Spec("other"), "Bo");
            party.Add(second);
            var flame = Strike("flame");
            party.Members[0].TryLearn(flame);
            var catalog = new Catalog { ChainSkills = new[] { flame } };

            var result = PowerUpEffects.Apply(PickupKind.ChainSkill, party, catalog, new SeededRng(1));

            Assert.AreEqual(PickupOutcome.SkillLearned, result.Outcome);
            Assert.AreSame(second, result.Member);
        }

        [Test]
        public void ChainSkillIsWastedWhenNobodyCanLearnIt()
        {
            var party = PartyWithHero();
            var flame = Strike("flame");
            party.Members[0].TryLearn(flame);
            var catalog = new Catalog { ChainSkills = new[] { flame } };

            var result = PowerUpEffects.Apply(PickupKind.ChainSkill, party, catalog, new SeededRng(1));

            Assert.AreEqual(PickupOutcome.NoFreeSlot, result.Outcome);
        }

        [Test]
        public void RecruitJoinsAtTheAverageLevelWithAPoolName()
        {
            var party = PartyWithHero();
            party.Members[0].GainXp(20 + 40); // level 3
            party.AddArmor(1);
            var catalog = new Catalog
            {
                RecruitSpecs = new[] { Spec("wanderer") },
                Names = new[] { "Mira", "Tobin" }
            };

            var result = PowerUpEffects.Apply(PickupKind.Recruit, party, catalog, new SeededRng(3));

            Assert.AreEqual(PickupOutcome.RecruitJoined, result.Outcome);
            Assert.AreEqual(2, party.Count);
            Assert.AreEqual(3, result.Member.Level);
            CollectionAssert.Contains(new[] { "Mira", "Tobin" }, result.Member.Name);
            Assert.AreEqual(party.Members[0].Stats.Defense, result.Member.Stats.Defense);
        }

        [Test]
        public void RecruitNamesAreNotReused()
        {
            var party = PartyWithHero();
            var catalog = new Catalog { RecruitSpecs = new[] { Spec("w") }, Names = new[] { "Ada", "Mira" } };

            var result = PowerUpEffects.Apply(PickupKind.Recruit, party, catalog, new SeededRng(5));

            Assert.AreEqual("Mira", result.Member.Name);
        }

        [Test]
        public void RecruitIsWastedWhenThePartyIsFull()
        {
            var party = new Party();
            for (int i = 0; i < Party.MaxSize; i++) party.Add(new PartyMember(Spec("m"), "m" + i));
            var catalog = new Catalog { RecruitSpecs = new[] { Spec("w") }, Names = new[] { "Mira" } };

            var result = PowerUpEffects.Apply(PickupKind.Recruit, party, catalog, new SeededRng(1));

            Assert.AreEqual(PickupOutcome.PartyFull, result.Outcome);
            Assert.AreEqual(Party.MaxSize, party.Count);
        }

        [Test]
        public void SameSeedGivesTheSameRecruit()
        {
            string Recruit(int seed)
            {
                var party = PartyWithHero();
                var catalog = new Catalog
                {
                    RecruitSpecs = new[] { Spec("a"), Spec("b"), Spec("c") },
                    Names = new[] { "Mira", "Tobin", "Elsa", "Bram" }
                };
                var member = PowerUpEffects.Apply(PickupKind.Recruit, party, catalog, new SeededRng(seed)).Member;
                return member.Name + member.TemplateId;
            }

            Assert.AreEqual(Recruit(11), Recruit(11));
        }
    }
}
