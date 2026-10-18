using System.Collections.Generic;
using CC.Core.Combat;

namespace CC.Tests.Combat
{
    /// <summary>Small builders so the combat tests read like the rules they check.</summary>
    internal static class CombatTestSupport
    {
        private static int _nextId = 1;

        public static Skill Attack(string id, int power, string applies = null, string followUpOn = null, int turns = 3) =>
            new Skill(id, id, power, SkillTarget.Enemy, SkillKind.Damage, applies, turns, followUpOn);

        public static Skill Heal(string id, int power) =>
            new Skill(id, id, power, SkillTarget.Ally, SkillKind.Heal);

        /// <summary>Party character without defense and attack unless given, so damage equals skill power.</summary>
        public static Combatant Hero(string name, int speed, int hp = 40, int attack = 0, int defense = 0, params Skill[] skills) =>
            new Combatant(_nextId++, name, Team.Party, 1, new StatBlock(hp, attack, defense, speed), skills, "hero");

        public static Combatant Foe(string name, int speed, int hp = 100, int attack = 0, int defense = 0,
            IEnemyBrain brain = null, params Skill[] skills) =>
            new Combatant(_nextId++, name, Team.Enemy, 1, new StatBlock(hp, attack, defense, speed), skills, "foe",
                brain ?? new FixedBrain());

        public static CombatSession Session(IEnumerable<Combatant> party, IEnumerable<Combatant> enemies,
            ChainConfig chain = null, float fleeChance = 1f, int seed = 1) =>
            new CombatSession(party, enemies, new CC.Core.Util.SeededRng(seed),
                new ChainResolver(chain ?? new ChainConfig()), fleeChance);
    }

    /// <summary>Always uses the first skill on the first valid target. Deterministic stand-in for an AI.</summary>
    internal sealed class FixedBrain : IEnemyBrain
    {
        public CombatAction ChooseAction(CombatSession session, Combatant actor)
        {
            foreach (var skill in actor.Skills)
            {
                var targets = session.ValidTargets(actor, skill);
                if (targets.Count > 0) return new CombatAction(skill, targets[0]);
            }
            throw new System.InvalidOperationException("no usable skill");
        }
    }
}
