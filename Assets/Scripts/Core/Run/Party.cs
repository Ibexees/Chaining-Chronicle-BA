using System;
using System.Collections.Generic;
using System.Linq;
using CC.Core.Combat;

namespace CC.Core.Run
{
    /// <summary>Blueprint of a party member type (hero or recruit). Core copy of a PartyMemberDef asset.</summary>
    public sealed class PartyMemberSpec
    {
        private readonly List<Skill> _startSkills;

        public string TemplateId { get; }
        public StatBlock BaseStats { get; }
        public StatBlock Growth { get; }
        public IReadOnlyList<Skill> StartSkills => _startSkills;

        public PartyMemberSpec(string templateId, StatBlock baseStats, StatBlock growth, IEnumerable<Skill> startSkills)
        {
            if (string.IsNullOrWhiteSpace(templateId)) throw new ArgumentException("Template needs an id", nameof(templateId));
            TemplateId = templateId;
            BaseStats = baseStats;
            Growth = growth;
            _startSkills = (startSkills ?? Enumerable.Empty<Skill>()).Take(PartyMember.MaxSkillSlots).ToList();
        }
    }

    /// <summary>
    /// A character of the party. Persists through the whole run: level, experience, hit points and
    /// skills carry over from stage to stage (Konzept 5.5).
    /// </summary>
    public sealed class PartyMember
    {
        /// <summary>Skill slots per character (GDD 2.2.6: power-ups are allotted to skill slots).</summary>
        public const int MaxSkillSlots = 4;

        private readonly StatBlock _baseStats;
        private readonly StatBlock _growth;
        private readonly List<Skill> _skills;

        public string Name { get; }
        public string TemplateId { get; }
        public int Level { get; private set; }
        public int Xp { get; private set; }
        public int ArmorBonus { get; private set; }
        public int Hp { get; internal set; }

        public IReadOnlyList<Skill> Skills => _skills;

        /// <summary>Current stats: base plus growth for the level plus armor.</summary>
        public StatBlock Stats => _baseStats.AtLevel(_growth, Level).WithDefenseBonus(ArmorBonus);

        public int XpToNextLevel => XpForNextLevel(Level);

        public bool HasFreeSlot => _skills.Count < MaxSkillSlots;

        public PartyMember(PartyMemberSpec spec, string name, int level = 1, int armorBonus = 0)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            _baseStats = spec.BaseStats;
            _growth = spec.Growth;
            _skills = spec.StartSkills.ToList();
            TemplateId = spec.TemplateId;
            Name = string.IsNullOrWhiteSpace(name) ? "Hero" : name.Trim();
            Level = Math.Max(level, 1);
            ArmorBonus = Math.Max(armorBonus, 0);
            Hp = Stats.MaxHp;
        }

        /// <summary>Experience needed to reach the next level. Placeholder curve, balancing comes later.</summary>
        public static int XpForNextLevel(int level) => 20 * Math.Max(level, 1);

        public bool Knows(Skill skill) => _skills.Any(s => s.Id == skill.Id);

        /// <summary>Adds a skill if there is a free slot and it is not known yet.</summary>
        public bool TryLearn(Skill skill)
        {
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            if (!HasFreeSlot || Knows(skill)) return false;
            _skills.Add(skill);
            return true;
        }

        public void AddArmor(int defense) => ArmorBonus += Math.Max(defense, 0);

        /// <summary>
        /// Adds experience. Returns the levels reached, empty if none. A level up raises the stats and
        /// heals by the maximum hit points gained (A27), it is no full heal.
        /// </summary>
        public IReadOnlyList<int> GainXp(int amount)
        {
            var reached = new List<int>();
            if (amount <= 0) return reached;

            Xp += amount;
            while (Xp >= XpToNextLevel)
            {
                Xp -= XpToNextLevel;
                int oldMax = Stats.MaxHp;
                Level++;
                Hp = Math.Min(Hp + (Stats.MaxHp - oldMax), Stats.MaxHp);
                reached.Add(Level);
            }
            return reached;
        }
    }

    /// <summary>The characters of the run, hero first. Limited in size (A32).</summary>
    public sealed class Party
    {
        public const int MaxSize = 4;

        private readonly List<PartyMember> _members = new List<PartyMember>();

        public IReadOnlyList<PartyMember> Members => _members;
        public int Count => _members.Count;
        public bool IsFull => _members.Count >= MaxSize;
        public bool IsDefeated => _members.Count > 0 && _members.All(m => m.Hp <= 0);

        /// <summary>Defense that every member has from armor found so far. Recruits join with it.</summary>
        public int ArmorBonus { get; private set; }

        /// <summary>Average level, rounded down, at least 1. New recruits join at this level (A33).</summary>
        public int AverageLevel => _members.Count == 0 ? 1 : Math.Max(1, (int)_members.Average(m => m.Level));

        public void Add(PartyMember member)
        {
            if (member == null) throw new ArgumentNullException(nameof(member));
            if (IsFull) throw new InvalidOperationException("The party is full");
            _members.Add(member);
        }

        public void AddArmor(int defense)
        {
            ArmorBonus += Math.Max(defense, 0);
            foreach (var member in _members) member.AddArmor(defense);
        }
    }
}
