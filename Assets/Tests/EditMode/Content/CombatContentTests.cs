using System.Collections.Generic;
using System.Linq;
using CC.Core.Combat;
using CC.Core.Run;
using CC.Core.Util;
using CC.Data;
using NUnit.Framework;
using UnityEditor;

namespace CC.Tests.Content
{
    /// <summary>Guards the combat content that Chaining Chronicle > Build Content produces.</summary>
    public class CombatContentTests
    {
        private GameContent _content;

        [SetUp]
        public void SetUp()
        {
            _content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Content/GameContent.asset");
            Assert.IsNotNull(_content, "GameContent missing, run Chaining Chronicle > Build Content");
        }

        private IEnumerable<EnemyDef> AllEnemies() =>
            _content.biomes.SelectMany(b => b.enemies).Where(e => e != null).Distinct();

        [Test]
        public void HeroCanFightAndHeal()
        {
            var hero = _content.CreateHero("Ada");

            Assert.AreEqual("Ada", hero.Name);
            Assert.AreEqual(1, hero.Level);
            Assert.IsTrue(hero.Skills.Any(s => s.Kind == SkillKind.Damage));
            Assert.IsTrue(hero.Skills.Any(s => s.Kind == SkillKind.Heal));
            Assert.AreEqual(hero.Stats.MaxHp, hero.Hp);
        }

        [Test]
        public void EveryEnemyHasStatsAndSkills()
        {
            foreach (var enemy in AllEnemies())
            {
                var spec = enemy.ToSpec();
                Assert.Greater(spec.BaseStats.MaxHp, 0, enemy.id);
                Assert.Greater(spec.BaseStats.Speed, 0, enemy.id);
                Assert.IsTrue(spec.Skills.Any(s => s.Kind == SkillKind.Damage), enemy.id + " has no attack");
                Assert.Greater(spec.XpReward(3), spec.XpReward(1), enemy.id + " reward grows with level");
            }
        }

        [Test]
        public void EnemyIdsOfTheBiomesCanBeLookedUp()
        {
            foreach (var enemy in AllEnemies())
                Assert.AreEqual(enemy.id, _content.EnemySpecFor(enemy.id).Id);
        }

        [Test]
        public void ChainSkillsAreFollowUpsOnExistingStatuses()
        {
            Assert.GreaterOrEqual(_content.ChainSkills.Count, 2);
            var applied = new HashSet<string>(
                _content.chainSkills.Concat(_content.hero.startSkills)
                    .Where(s => s.appliesStatus != null).Select(s => s.appliesStatus.id));

            foreach (var skill in _content.ChainSkills)
            {
                Assert.IsTrue(skill.IsFollowUpSkill, skill.Name + " is not a follow-up");
                Assert.Contains(skill.FollowUpOn, applied.ToList(), skill.Name + " follows a status nobody applies");
            }
        }

        [Test]
        public void TheHeroStartsAChainThatTheChainSkillsContinue()
        {
            // The first skill of the hero has to apply a status that a chain skill follows up on.
            var starter = _content.hero.startSkills.First(s => s.kind == SkillKind.Damage);

            Assert.IsNotNull(starter.appliesStatus);
            Assert.IsTrue(_content.chainSkills.Any(s => s.followUpOn == starter.appliesStatus));
        }

        [Test]
        public void RecruitsAndNamesAreAvailable()
        {
            Assert.GreaterOrEqual(_content.RecruitSpecs.Count, 2);
            Assert.GreaterOrEqual(_content.Names.Count, Party.MaxSize);
            foreach (var recruit in _content.recruits)
            {
                Assert.IsNotNull(recruit.animator, recruit.id);
                Assert.IsNotEmpty(recruit.startSkills, recruit.id);
            }
        }

        [Test]
        public void OtherEnemiesCanJoinAFight()
        {
            // A36: the radius is larger than zero (otherwise nobody joins) and there is room for several enemies.
            Assert.Greater(_content.joinRadius, 0);
            Assert.GreaterOrEqual(_content.maxEnemiesInCombat, 2);
        }

        [Test]
        public void ChainLimitMatchesTheDecision()
        {
            var config = _content.CreateChainConfig();

            Assert.AreEqual(5, config.Max); // A12, start value
            Assert.AreEqual(1f, _content.fleeChance); // A11
        }

        [Test]
        public void ARealFightAgainstTheFirstEnemyCanBeWonByTheHero()
        {
            var rng = new SeededRng(11);
            var hero = _content.CreateHero("Ada");
            var spec = _content.EnemySpecFor(_content.biomes[0].enemies[0].id);
            var party = new[] { CC.Core.Combat.Combatant.ForMember(1, hero) };
            var enemy = spec.CreateCombatant(100, 1, rng.Derive(1));
            var session = new CombatSession(party, new[] { enemy }, rng,
                new ChainResolver(_content.CreateChainConfig()), _content.fleeChance);
            session.Start();

            int guard = 0;
            while (session.Outcome == CombatOutcome.None && guard++ < 50)
            {
                var actor = session.Current;
                var skill = actor.Skills.First(s => s.Kind == SkillKind.Damage);
                session.SubmitAction(skill, enemy);
            }

            Assert.AreEqual(CombatOutcome.Won, session.Outcome);
            Assert.Greater(hero.Hp, 0);
        }
    }
}
