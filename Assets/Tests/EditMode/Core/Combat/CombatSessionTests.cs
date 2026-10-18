using System.Collections.Generic;
using System.Linq;
using CC.Core.Combat;
using CC.Core.Run;
using CC.Core.Util;
using NUnit.Framework;
using static CC.Tests.Combat.CombatTestSupport;

namespace CC.Tests.Combat
{
    public class CombatSessionTests
    {
        private readonly List<int> _chainCounts = new List<int>();
        private readonly List<CombatOutcome> _outcomes = new List<CombatOutcome>();

        [SetUp]
        public void SetUp()
        {
            _chainCounts.Clear();
            _outcomes.Clear();
            GameEvents.ClearSubscribers();
            GameEvents.ChainCounterChanged += c => _chainCounts.Add(c);
            GameEvents.CombatEnded += o => _outcomes.Add(o);
        }

        [TearDown]
        public void TearDown() => GameEvents.ClearSubscribers();

        private static IEnumerable<CombatEvent> Of(IEnumerable<CombatEvent> events, CombatEventKind kind) =>
            events.Where(e => e.Kind == kind);

        // ---- starting and turn order

        [Test]
        public void FasterEnemyActsBeforeThePlayerHasAnyInput()
        {
            var hero = Hero("hero", speed: 5, hp: 20, skills: new[] { Attack("slash", 4) });
            var foe = Foe("foe", speed: 9, skills: new[] { Attack("bite", 3) });

            var session = Session(new[] { hero }, new[] { foe });
            var events = session.Start();

            var used = Of(events, CombatEventKind.SkillUsed).Single();
            Assert.AreSame(foe, used.Actor);
            Assert.AreEqual(17, hero.Hp);
            Assert.AreEqual(CombatPhase.AwaitingInput, session.Phase);
            Assert.AreSame(hero, session.Current);
        }

        [Test]
        public void PlayerStartsWhenFastest()
        {
            var hero = Hero("hero", speed: 9, skills: new[] { Attack("slash", 4) });
            var foe = Foe("foe", speed: 5, skills: new[] { Attack("bite", 3) });

            var session = Session(new[] { hero }, new[] { foe });
            var events = session.Start();

            Assert.IsEmpty(Of(events, CombatEventKind.SkillUsed));
            Assert.AreSame(hero, session.Current);
        }

        // ---- damage and healing

        [Test]
        public void DamageIsPowerPlusAttackMinusDefenseAtLeastOne()
        {
            var hero = Hero("hero", 9, attack: 5, skills: new[] { Attack("slash", 4), Attack("weak", 1) });
            var foe = Foe("foe", 1, hp: 50, defense: 6, skills: new[] { Attack("poke", 1) });
            var session = Session(new[] { hero }, new[] { foe });
            session.Start();

            var events = session.SubmitAction(hero.Skills[0], foe);

            Assert.AreEqual(3, Of(events, CombatEventKind.SkillUsed).First().Amount); // 4 + 5 - 6
            Assert.AreEqual(47, foe.Hp);
        }

        [Test]
        public void DamageIsAtLeastOne()
        {
            var hero = Hero("hero", 9, attack: 0, skills: new[] { Attack("weak", 1) });
            var foe = Foe("foe", 1, hp: 50, defense: 10, skills: new[] { Attack("poke", 1) });
            var session = Session(new[] { hero }, new[] { foe });
            session.Start();

            session.SubmitAction(hero.Skills[0], foe);

            Assert.AreEqual(49, foe.Hp);
        }

        [Test]
        public void HealRestoresUpToMaximum()
        {
            var healer = Hero("healer", 9, hp: 20, attack: 4, skills: new[] { Heal("mend", 6) });
            var foe = Foe("foe", 1, skills: new[] { Attack("poke", 5) });
            var session = Session(new[] { healer }, new[] { foe });
            session.Start();
            healer.TakeDamage(7);

            var events = session.SubmitAction(healer.Skills[0], healer);

            // 6 + 4/2 = 8, but only 7 missing.
            Assert.AreEqual(7, Of(events, CombatEventKind.SkillUsed).First().Amount);
        }

        // ---- chaining (GDD 1.1)

        private static (Combatant a, Combatant b, Combatant c, Combatant foe) ChainCast()
        {
            var a = Hero("A", 10, skills: new[] { Attack("slash", 4, applies: "bleed") });
            var b = Hero("B", 8, skills: new[] { Attack("flame", 3, applies: "burn", followUpOn: "bleed") });
            var c = Hero("C", 6, skills: new[] { Attack("fissure", 2, applies: "shaken", followUpOn: "burn") });
            var foe = Foe("foe", 1, hp: 100, skills: new[] { Attack("poke", 1) });
            return (a, b, c, foe);
        }

        [Test]
        public void PartyMembersJoinTheChainWhenTheStatusMatches()
        {
            var (a, b, c, foe) = ChainCast();
            var session = Session(new[] { a, b, c }, new[] { foe });
            session.Start();

            var events = session.SubmitAction(a.Skills[0], foe);

            var used = Of(events, CombatEventKind.SkillUsed).ToList();
            Assert.AreEqual(new[] { "A", "B", "C" }, used.Select(e => e.Actor.Name).ToArray());
            Assert.AreEqual(new[] { 1, 2, 3 }, used.Select(e => e.ChainIndex).ToArray());
            Assert.AreEqual(new[] { false, true, true }, used.Select(e => e.IsFollowUp).ToArray());
            Assert.AreEqual(3, Of(events, CombatEventKind.ChainEnded).Single().ChainIndex);
            Assert.AreEqual(100 - 4 - 3 - 2, foe.Hp);
            Assert.AreEqual(new[] { 1, 2, 3 }, _chainCounts.ToArray());
        }

        [Test]
        public void ChainsDoNotChangeTheTurnOrder()
        {
            // Decision on A14: follow-ups are bonus actions, the next actor is simply the next in order.
            var (a, b, c, foe) = ChainCast();
            var session = Session(new[] { a, b, c }, new[] { foe });
            session.Start();

            session.SubmitAction(a.Skills[0], foe);

            Assert.AreSame(b, session.Current);
        }

        [Test]
        public void NoChainWithoutMatchingStatus()
        {
            var (a, b, c, foe) = ChainCast();
            var session = Session(new[] { b, c }, new[] { foe });
            session.Start();

            var events = session.SubmitAction(b.Skills[0], foe); // applies burn, c follows up on burn

            Assert.AreEqual(2, Of(events, CombatEventKind.SkillUsed).Count());
            // A skill that starts a chain does not need its own condition: slash would not chain on its own.
            var lone = Hero("lone", 9, skills: new[] { Attack("slash", 4, applies: "bleed") });
            var foe2 = Foe("foe2", 1, skills: new[] { Attack("poke", 1) });
            var session2 = Session(new[] { lone }, new[] { foe2 });
            session2.Start();
            var events2 = session2.SubmitAction(lone.Skills[0], foe2);
            Assert.AreEqual(1, Of(events2, CombatEventKind.SkillUsed).Count(e => e.Actor == lone));
            Assert.AreEqual(1, Of(events2, CombatEventKind.ChainEnded).First().ChainIndex);
        }

        [Test]
        public void OwnCharacterMayChainWithItself()
        {
            // Decision on A3.
            var solo = Hero("solo", 9, skills: new[]
            {
                Attack("slash", 4, applies: "bleed"),
                Attack("flame", 3, applies: "burn", followUpOn: "bleed")
            });
            var foe = Foe("foe", 1, skills: new[] { Attack("poke", 1) });
            var session = Session(new[] { solo }, new[] { foe });
            session.Start();

            var events = session.SubmitAction(solo.Skills[0], foe);

            var used = Of(events, CombatEventKind.SkillUsed).Where(e => e.Actor == solo).ToList();
            Assert.AreEqual(2, used.Count);
            Assert.AreEqual("flame", used[1].Skill.Id);
        }

        private static (Combatant[] party, Combatant foe) CycleCast()
        {
            // bleed -> burn -> shaken -> bleed ... would run forever without the upper limit.
            var party = new[]
            {
                Hero("A", 10, skills: new[] { Attack("slash", 1, applies: "bleed") }),
                Hero("B", 9, skills: new[] { Attack("flame", 1, applies: "burn", followUpOn: "bleed") }),
                Hero("C", 8, skills: new[] { Attack("fissure", 1, applies: "shaken", followUpOn: "burn") }),
                Hero("D", 7, skills: new[] { Attack("rend", 1, applies: "bleed", followUpOn: "shaken") })
            };
            return (party, Foe("foe", 1, hp: 500, skills: new[] { Attack("poke", 1) }));
        }

        [Test]
        public void UpperLimitStopsACyclingChain()
        {
            var (party, foe) = CycleCast();
            var session = Session(party, new[] { foe }, new ChainConfig(max: 5));
            session.Start();

            var events = session.SubmitAction(party[0].Skills[0], foe);

            Assert.AreEqual(5, Of(events, CombatEventKind.SkillUsed).Count());
            Assert.AreEqual(5, Of(events, CombatEventKind.ChainEnded).Single().ChainIndex);
        }

        [Test]
        public void LimitIsPerChainNotPerCombat()
        {
            var (party, foe) = CycleCast();
            var session = Session(party, new[] { foe }, new ChainConfig(max: 5));
            session.Start();
            session.SubmitAction(party[0].Skills[0], foe);

            // Next member starts a fresh chain with a skill that can start it again.
            var events = session.SubmitAction(party[1].Skills[0], foe);

            Assert.GreaterOrEqual(Of(events, CombatEventKind.SkillUsed).Count(), 2);
            Assert.LessOrEqual(Of(events, CombatEventKind.ChainEnded).Single().ChainIndex, 5);
        }

        [Test]
        public void SameSkillRuleShortensTheCycle()
        {
            var (party, foe) = CycleCast();
            var session = Session(party, new[] { foe }, new ChainConfig(max: 5, sameSkillOncePerCharacter: true));
            session.Start();

            var events = session.SubmitAction(party[0].Skills[0], foe);

            // slash, flame, fissure, rend: flame was already used by B, nobody else joins.
            Assert.AreEqual(4, Of(events, CombatEventKind.SkillUsed).Count());
        }

        [Test]
        public void ChainStopsWhenTheTargetIsDefeated()
        {
            var a = Hero("A", 10, attack: 10, skills: new[] { Attack("slash", 10, applies: "bleed") });
            var b = Hero("B", 8, skills: new[] { Attack("flame", 3, applies: "burn", followUpOn: "bleed") });
            var foe = Foe("foe", 1, hp: 5, skills: new[] { Attack("poke", 1) });
            var session = Session(new[] { a, b }, new[] { foe });
            session.Start();

            var events = session.SubmitAction(a.Skills[0], foe);

            Assert.AreEqual(1, Of(events, CombatEventKind.SkillUsed).Count());
            Assert.AreEqual(1, Of(events, CombatEventKind.Defeated).Count());
            Assert.IsEmpty(Of(events, CombatEventKind.StatusApplied));
            Assert.AreEqual(CombatOutcome.Won, session.Outcome);
        }

        [Test]
        public void EnemiesChainJustLikeThePlayer()
        {
            // GDD 6.1: enemies work exactly like player characters.
            var hero = Hero("hero", 1, hp: 100, skills: new[] { Attack("slash", 1) });
            var foe = Foe("foe", 9, skills: new[]
            {
                Attack("gaze", 2, applies: "shaken"),
                Attack("burst", 3, applies: "burn", followUpOn: "shaken")
            });
            var session = Session(new[] { hero }, new[] { foe });

            var events = session.Start();

            var used = Of(events, CombatEventKind.SkillUsed).ToList();
            Assert.AreEqual(new[] { "gaze", "burst" }, used.Select(e => e.Skill.Id).ToArray());
            Assert.AreEqual(100 - 2 - 3, hero.Hp);
        }

        // ---- statuses

        [Test]
        public void StatusWearsOffAfterItsTurns()
        {
            var hero = Hero("hero", 9, skills: new[] { Attack("slash", 1, applies: "bleed", turns: 1) });
            var foe = Foe("foe", 1, hp: 50, skills: new[] { Attack("poke", 1) });
            var session = Session(new[] { hero }, new[] { foe });
            session.Start();

            var events = session.SubmitAction(hero.Skills[0], foe);

            Assert.IsFalse(foe.Has("bleed"));
            var expired = Of(events, CombatEventKind.StatusExpired).Single();
            Assert.AreEqual("bleed", expired.StatusId);
        }

        [Test]
        public void StatusIsRefreshedNotStacked()
        {
            var hero = Hero("hero", 9, skills: new[] { Attack("slash", 1, applies: "bleed", turns: 3) });
            var foe = Foe("foe", 1, hp: 80, skills: new[] { Attack("poke", 1) });
            var session = Session(new[] { hero }, new[] { foe });
            session.Start();

            session.SubmitAction(hero.Skills[0], foe);
            session.SubmitAction(hero.Skills[0], foe);

            Assert.AreEqual(1, foe.Statuses.Count(s => s.Id == "bleed"));
        }

        // ---- end of combat

        [Test]
        public void WinWhenAllEnemiesAreDefeatedAndHitPointsCarryOver()
        {
            var spec = new PartyMemberSpec("hero", new StatBlock(30, 5, 0, 10), new StatBlock(5, 1, 0, 0),
                new[] { Attack("slash", 40) });
            var member = new PartyMember(spec, "Ada");
            var hero = Combatant.ForMember(100, member);
            var foe = Foe("foe", 1, hp: 10, attack: 3, skills: new[] { Attack("poke", 4) });
            var session = Session(new[] { hero }, new[] { foe });
            session.Start();
            hero.TakeDamage(6);

            var events = session.SubmitAction(hero.Skills[0], foe);

            Assert.AreEqual(CombatOutcome.Won, session.Outcome);
            Assert.AreEqual(CombatPhase.Ended, session.Phase);
            Assert.AreEqual(CombatOutcome.Won, Of(events, CombatEventKind.Ended).Single().Outcome);
            Assert.AreEqual(24, member.Hp);
            Assert.AreEqual(new[] { CombatOutcome.Won }, _outcomes.ToArray());
        }

        [Test]
        public void LostWhenThePartyIsDefeated()
        {
            var hero = Hero("hero", 1, hp: 5, skills: new[] { Attack("slash", 1) });
            var foe = Foe("foe", 9, attack: 10, skills: new[] { Attack("smash", 10) });
            var session = Session(new[] { hero }, new[] { foe });

            var events = session.Start();

            Assert.AreEqual(CombatOutcome.Lost, session.Outcome);
            Assert.IsFalse(hero.IsAlive);
            Assert.AreEqual(CombatOutcome.Lost, events.Last().Outcome);
        }

        [Test]
        public void FleeEndsTheCombatWhenGuaranteed()
        {
            var hero = Hero("hero", 9, skills: new[] { Attack("slash", 1) });
            var foe = Foe("foe", 1, skills: new[] { Attack("poke", 1) });
            var session = Session(new[] { hero }, new[] { foe }, fleeChance: 1f);
            session.Start();

            var events = session.TryFlee();

            Assert.AreEqual(CombatOutcome.Fled, session.Outcome);
            Assert.AreEqual(1, Of(events, CombatEventKind.Fled).Count());
            Assert.AreEqual(new[] { CombatOutcome.Fled }, _outcomes.ToArray());
        }

        [Test]
        public void FailedFleeCostsTheTurn()
        {
            var hero = Hero("hero", 9, hp: 20, skills: new[] { Attack("slash", 1) });
            var foe = Foe("foe", 1, skills: new[] { Attack("poke", 3) });
            var session = Session(new[] { hero }, new[] { foe }, fleeChance: 0f);
            session.Start();

            var events = session.TryFlee();

            Assert.AreEqual(CombatOutcome.None, session.Outcome);
            Assert.AreEqual(1, Of(events, CombatEventKind.FleeFailed).Count());
            Assert.AreEqual(17, hero.Hp); // the enemy used its turn
            Assert.AreSame(hero, session.Current);
        }

        // ---- misuse

        [Test]
        public void ActionsAreRejectedWhenInvalid()
        {
            var hero = Hero("hero", 9, skills: new[] { Attack("slash", 1), Heal("mend", 1) });
            var foe = Foe("foe", 1, skills: new[] { Attack("poke", 1) });
            var session = Session(new[] { hero }, new[] { foe });

            Assert.Throws<System.InvalidOperationException>(() => session.SubmitAction(hero.Skills[0], foe), "before start");
            session.Start();

            Assert.Throws<System.ArgumentException>(() => session.SubmitAction(foe.Skills[0], foe), "not the actor's skill");
            Assert.Throws<System.ArgumentException>(() => session.SubmitAction(hero.Skills[0], hero), "attack on own side");
            Assert.Throws<System.ArgumentException>(() => session.SubmitAction(hero.Skills[1], foe), "heal on the enemy");
        }

        [Test]
        public void NoActionsAfterTheEnd()
        {
            var hero = Hero("hero", 9, attack: 50, skills: new[] { Attack("slash", 50) });
            var foe = Foe("foe", 1, hp: 5, skills: new[] { Attack("poke", 1) });
            var session = Session(new[] { hero }, new[] { foe });
            session.Start();
            session.SubmitAction(hero.Skills[0], foe);

            Assert.Throws<System.InvalidOperationException>(() => session.SubmitAction(hero.Skills[0], foe));
            Assert.Throws<System.InvalidOperationException>(() => session.TryFlee());
        }

        [Test]
        public void SameSeedGivesTheSameFight()
        {
            string Play(int seed)
            {
                var rng = new SeededRng(seed);
                var hero = Hero("hero", 5, hp: 60, skills: new[] { Attack("slash", 3, applies: "bleed") });
                var foe = Foe("foe", 8, hp: 60, brain: new RandomEnemyBrain(rng.Derive(1)),
                    skills: new[] { Attack("bite", 4, applies: "bleed"), Attack("claw", 6), Heal("lick", 5) });
                var session = new CombatSession(new[] { hero }, new[] { foe }, rng, new ChainResolver(new ChainConfig()));
                var log = new List<string>();
                log.AddRange(session.Start().Select(e => e.ToString()));
                for (int i = 0; i < 6 && session.Outcome == CombatOutcome.None; i++)
                    log.AddRange(session.SubmitAction(hero.Skills[0], foe).Select(e => e.ToString()));
                return string.Join("|", log);
            }

            Assert.AreEqual(Play(7), Play(7));
        }
    }
}
