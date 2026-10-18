using System;
using System.Collections.Generic;
using System.Linq;
using CC.Core.Combat;
using CC.Core.Stage;
using CC.Core.Util;

namespace CC.Core.Run
{
    /// <summary>What the run can hand out when the player collects power-ups. Implemented by the game content.</summary>
    public interface IPartyCatalog
    {
        /// <summary>Types a recruit can be (A7: "other party members are found on the stage").</summary>
        IReadOnlyList<PartyMemberSpec> RecruitSpecs { get; }
        /// <summary>Skills that a ChainSkill power-up can teach.</summary>
        IReadOnlyList<Skill> ChainSkills { get; }
        /// <summary>Predefined pool of names for found party members (GDD 3.3).</summary>
        IReadOnlyList<string> Names { get; }
    }

    public enum PickupOutcome
    {
        RecruitJoined,
        PartyFull,
        ArmorEquipped,
        SkillLearned,
        NoFreeSlot,
        NothingAvailable
    }

    public sealed class PickupResult
    {
        public PickupKind Kind { get; }
        public PickupOutcome Outcome { get; }
        public PartyMember Member { get; }
        public Skill Skill { get; }

        public PickupResult(PickupKind kind, PickupOutcome outcome, PartyMember member = null, Skill skill = null)
        {
            Kind = kind;
            Outcome = outcome;
            Member = member;
            Skill = skill;
        }
    }

    /// <summary>
    /// The effect of the power-ups from the stage (GDD 2.2.6): armor, chain skills and recruits are the only
    /// resources. Choices are automatic, there is no inventory screen in the MVP (A30).
    /// </summary>
    public static class PowerUpEffects
    {
        /// <summary>Defense every member gets per armor power-up.</summary>
        public const int ArmorDefense = 1;

        public static PickupResult Apply(PickupKind kind, Party party, IPartyCatalog catalog, IRng rng)
        {
            if (party == null) throw new ArgumentNullException(nameof(party));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            switch (kind)
            {
                case PickupKind.Armor:
                    party.AddArmor(ArmorDefense);
                    return new PickupResult(kind, PickupOutcome.ArmorEquipped);

                case PickupKind.ChainSkill:
                    return LearnSkill(party, catalog, rng);

                case PickupKind.Recruit:
                    return Recruit(party, catalog, rng);

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static PickupResult LearnSkill(Party party, IPartyCatalog catalog, IRng rng)
        {
            var pool = catalog.ChainSkills;
            if (pool.Count == 0) return new PickupResult(PickupKind.ChainSkill, PickupOutcome.NothingAvailable);

            // Prefer a skill that somebody can still learn, so a power-up is not wasted while another one would fit.
            var learnable = pool
                .Where(s => party.Members.Any(m => m.HasFreeSlot && !m.Knows(s)))
                .ToList();
            if (learnable.Count == 0) return new PickupResult(PickupKind.ChainSkill, PickupOutcome.NoFreeSlot);

            var skill = learnable[rng.Next(0, learnable.Count)];
            var learner = party.Members.First(m => m.HasFreeSlot && !m.Knows(skill));
            learner.TryLearn(skill);
            return new PickupResult(PickupKind.ChainSkill, PickupOutcome.SkillLearned, learner, skill);
        }

        private static PickupResult Recruit(Party party, IPartyCatalog catalog, IRng rng)
        {
            if (party.IsFull) return new PickupResult(PickupKind.Recruit, PickupOutcome.PartyFull);
            if (catalog.RecruitSpecs.Count == 0) return new PickupResult(PickupKind.Recruit, PickupOutcome.NothingAvailable);

            var spec = catalog.RecruitSpecs[rng.Next(0, catalog.RecruitSpecs.Count)];
            var member = new PartyMember(spec, PickName(party, catalog, rng), party.AverageLevel, party.ArmorBonus);
            party.Add(member);
            return new PickupResult(PickupKind.Recruit, PickupOutcome.RecruitJoined, member);
        }

        private static string PickName(Party party, IPartyCatalog catalog, IRng rng)
        {
            var free = catalog.Names.Where(n => party.Members.All(m => m.Name != n)).ToList();
            if (free.Count > 0) return free[rng.Next(0, free.Count)];
            return "Recruit " + party.Count;
        }
    }
}
