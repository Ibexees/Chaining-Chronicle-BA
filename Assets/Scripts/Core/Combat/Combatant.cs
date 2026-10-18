using System;
using System.Collections.Generic;
using System.Linq;
using CC.Core.Run;

namespace CC.Core.Combat
{
    /// <summary>
    /// One fighter in a combat: hit points, stats, skills and statuses. Party members and enemies
    /// are the same type (GDD 6.1: enemies work like player characters). Enemies get a brain,
    /// party members are driven by the player.
    /// </summary>
    public sealed class Combatant
    {
        private readonly List<Skill> _skills;
        private readonly List<StatusEffect> _statuses = new List<StatusEffect>();

        public int Id { get; }
        public string Name { get; }
        public Team Team { get; }
        public int Level { get; }
        public StatBlock Stats { get; }
        public int Hp { get; private set; }
        /// <summary>Content id of the enemy type or party member template. Used by views to pick the animator.</summary>
        public string TypeId { get; }
        /// <summary>The persistent party member behind this combatant, or null for enemies.</summary>
        public PartyMember Member { get; }
        /// <summary>Null for combatants that the player controls.</summary>
        public IEnemyBrain Brain { get; }

        public IReadOnlyList<Skill> Skills => _skills;
        public IReadOnlyList<StatusEffect> Statuses => _statuses;
        public bool IsAlive => Hp > 0;
        public bool IsPlayerControlled => Brain == null;
        public bool IsHurt => Hp < Stats.MaxHp;

        public Combatant(
            int id,
            string name,
            Team team,
            int level,
            StatBlock stats,
            IEnumerable<Skill> skills,
            string typeId,
            IEnemyBrain brain = null,
            PartyMember member = null,
            int? startHp = null)
        {
            if (stats.MaxHp < 1) throw new ArgumentOutOfRangeException(nameof(stats), "MaxHp must be positive");

            Id = id;
            Name = name;
            Team = team;
            Level = level;
            Stats = stats;
            TypeId = typeId;
            Brain = brain;
            Member = member;
            _skills = (skills ?? Enumerable.Empty<Skill>()).ToList();
            Hp = Math.Min(startHp ?? stats.MaxHp, stats.MaxHp);
        }

        /// <summary>Creates the combatant for a persistent party member. Hit points carry over from earlier fights.</summary>
        public static Combatant ForMember(int id, PartyMember member) =>
            new Combatant(id, member.Name, Team.Party, member.Level, member.Stats, member.Skills,
                member.TemplateId, brain: null, member: member, startHp: member.Hp);

        public bool Has(string statusId) => _statuses.Any(s => s.Id == statusId);

        /// <summary>Returns the damage actually taken, which never exceeds the remaining hit points.</summary>
        internal int TakeDamage(int amount)
        {
            int taken = Math.Min(Math.Max(amount, 0), Hp);
            Hp -= taken;
            return taken;
        }

        /// <summary>Returns the hit points actually restored.</summary>
        internal int Heal(int amount)
        {
            int healed = Math.Min(Math.Max(amount, 0), Stats.MaxHp - Hp);
            Hp += healed;
            return healed;
        }

        /// <summary>A status that is already present is refreshed, not stacked (MVP).</summary>
        internal void ApplyStatus(string id, int turns)
        {
            var existing = _statuses.Find(s => s.Id == id);
            if (existing != null) existing.TurnsLeft = Math.Max(existing.TurnsLeft, turns);
            else _statuses.Add(new StatusEffect(id, turns));
        }

        /// <summary>Called at the start of this combatant's turn. Returns the ids of statuses that wore off.</summary>
        internal List<string> TickStatuses()
        {
            var expired = new List<string>();
            foreach (var status in _statuses)
            {
                status.TurnsLeft--;
                if (status.TurnsLeft <= 0) expired.Add(status.Id);
            }
            _statuses.RemoveAll(s => s.TurnsLeft <= 0);
            return expired;
        }

        public override string ToString() => $"{Name} {Hp}/{Stats.MaxHp}";
    }
}
