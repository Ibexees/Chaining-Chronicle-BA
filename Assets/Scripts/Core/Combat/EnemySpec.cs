using System;
using System.Collections.Generic;
using System.Linq;
using CC.Core.Util;

namespace CC.Core.Combat
{
    /// <summary>
    /// Core copy of an enemy type (EnemyDef asset). Turns a stage enemy of a given level into a combatant.
    /// </summary>
    public sealed class EnemySpec
    {
        private readonly List<Skill> _skills;

        public string Id { get; }
        public string Name { get; }
        public StatBlock BaseStats { get; }
        /// <summary>Added once per level above 1.</summary>
        public StatBlock Growth { get; }
        public EnemyAi Ai { get; }
        /// <summary>Experience for a level 1 enemy. The reward grows linearly with the level.</summary>
        public int BaseXp { get; }
        public IReadOnlyList<Skill> Skills => _skills;

        public EnemySpec(
            string id,
            string name,
            StatBlock baseStats,
            StatBlock growth,
            IEnumerable<Skill> skills,
            EnemyAi ai = EnemyAi.Fsm,
            int baseXp = 10)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Enemy needs an id", nameof(id));
            Id = id;
            Name = string.IsNullOrWhiteSpace(name) ? id : name;
            BaseStats = baseStats;
            Growth = growth;
            Ai = ai;
            BaseXp = baseXp;
            _skills = (skills ?? Enumerable.Empty<Skill>()).ToList();
            if (_skills.Count == 0) throw new ArgumentException($"Enemy {id} needs at least one skill", nameof(skills));
        }

        public StatBlock StatsAt(int level) => BaseStats.AtLevel(Growth, level);

        public int XpReward(int level) => BaseXp * Math.Max(level, 1);

        public Combatant CreateCombatant(int combatantId, int level, IRng rng)
        {
            IEnemyBrain brain = Ai == EnemyAi.Random
                ? new RandomEnemyBrain(rng)
                : (IEnemyBrain)new FsmEnemyBrain();
            return new Combatant(combatantId, Name, Team.Enemy, level, StatsAt(level), _skills, Id, brain);
        }
    }
}
