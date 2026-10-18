using System;
using System.Collections.Generic;
using System.Linq;
using CC.Core.Util;

namespace CC.Core.Combat
{
    /// <summary>
    /// Decides what an enemy does in combat (GDD 6.1, first AI task). The session does not know the
    /// difference between a player and a brain: it just asks for an action.
    /// </summary>
    public interface IEnemyBrain
    {
        CombatAction ChooseAction(CombatSession session, Combatant actor);
    }

    public enum EnemyAi
    {
        /// <summary>Finite state machine: aggressive, heal below 30 % hit points.</summary>
        Fsm,
        /// <summary>Random skill and target. The fallback the GDD names if the FSM turns out too complex.</summary>
        Random
    }

    public enum FsmState
    {
        Aggressive,
        Heal
    }

    /// <summary>Damage and healing formulas. Modifiers (GDD TODO damage multipliers) would plug in here.</summary>
    public static class DamageCalculator
    {
        /// <summary>Power plus the attacker's attack minus the target's defense, at least 1.</summary>
        public static int Damage(Combatant attacker, Skill skill, Combatant target) =>
            Math.Max(1, skill.Power + attacker.Stats.Attack - target.Stats.Defense);

        public static int Healing(Combatant healer, Skill skill) =>
            skill.Power + healer.Stats.Attack / 2;
    }

    /// <summary>
    /// Two states. Heal when own hit points are below 30 % and a heal skill exists, otherwise Aggressive:
    /// prefer a target that one of the skills can kill, else the weakest target with the hardest hitting skill.
    /// </summary>
    public sealed class FsmEnemyBrain : IEnemyBrain
    {
        /// <summary>Percent of maximum hit points below which the brain heals.</summary>
        public const int HealThresholdPercent = 30;

        public FsmState State { get; private set; } = FsmState.Aggressive;

        public CombatAction ChooseAction(CombatSession session, Combatant actor)
        {
            State = actor.Hp * 100 < actor.Stats.MaxHp * HealThresholdPercent ? FsmState.Heal : FsmState.Aggressive;

            if (State == FsmState.Heal && TryHeal(session, actor, out var heal)) return heal;
            if (TryAttack(session, actor, out var attack)) return attack;
            if (TryHeal(session, actor, out heal)) return heal;
            throw new InvalidOperationException($"{actor.Name} has no usable skill");
        }

        private static bool TryHeal(CombatSession session, Combatant actor, out CombatAction action)
        {
            action = default;
            foreach (var skill in actor.Skills.Where(s => s.Kind == SkillKind.Heal))
            {
                var target = session.ValidTargets(actor, skill)
                    .Where(t => t.IsHurt)
                    .OrderBy(t => (float)t.Hp / t.Stats.MaxHp)
                    .FirstOrDefault();
                if (target == null) continue;

                action = new CombatAction(skill, target);
                return true;
            }
            return false;
        }

        private static bool TryAttack(CombatSession session, Combatant actor, out CombatAction action)
        {
            action = default;
            Skill bestSkill = null;
            Combatant bestTarget = null;
            int bestDamage = -1;
            bool bestKills = false;

            foreach (var skill in actor.Skills.Where(s => s.Kind == SkillKind.Damage))
            {
                foreach (var target in session.ValidTargets(actor, skill))
                {
                    int damage = DamageCalculator.Damage(actor, skill, target);
                    bool kills = damage >= target.Hp;
                    bool better = bestSkill == null
                        || (kills && !bestKills)
                        || (kills == bestKills && kills && damage > bestDamage)
                        // Nobody can be killed: weakest target first, then the hardest hit.
                        || (!kills && !bestKills && (target.Hp < bestTarget.Hp
                            || (target.Hp == bestTarget.Hp && damage > bestDamage)));
                    if (!better) continue;

                    bestSkill = skill;
                    bestTarget = target;
                    bestDamage = damage;
                    bestKills = kills;
                }
            }

            if (bestSkill == null) return false;
            action = new CombatAction(bestSkill, bestTarget);
            return true;
        }
    }

    /// <summary>Picks a random skill that has a target, and a random target.</summary>
    public sealed class RandomEnemyBrain : IEnemyBrain
    {
        private readonly IRng _rng;

        public RandomEnemyBrain(IRng rng) => _rng = rng ?? throw new ArgumentNullException(nameof(rng));

        public CombatAction ChooseAction(CombatSession session, Combatant actor)
        {
            var usable = new List<(Skill skill, IReadOnlyList<Combatant> targets)>();
            foreach (var skill in actor.Skills)
            {
                var targets = session.ValidTargets(actor, skill);
                if (targets.Count > 0) usable.Add((skill, targets));
            }
            if (usable.Count == 0) throw new InvalidOperationException($"{actor.Name} has no usable skill");

            var pick = usable[_rng.Next(0, usable.Count)];
            return new CombatAction(pick.skill, pick.targets[_rng.Next(0, pick.targets.Count)]);
        }
    }
}
