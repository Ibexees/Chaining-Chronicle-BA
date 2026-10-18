using System;
using System.Collections.Generic;

namespace CC.Core.Combat
{
    public enum Team
    {
        Party,
        Enemy
    }

    /// <summary>Who a skill can be used on. Own character counts as an ally.</summary>
    public enum SkillTarget
    {
        Enemy,
        Ally
    }

    public enum SkillKind
    {
        Damage,
        Heal
    }

    public enum CombatPhase
    {
        NotStarted,
        AwaitingInput,
        Resolving,
        Ended
    }

    public enum CombatOutcome
    {
        None,
        Won,
        Fled,
        Lost
    }

    /// <summary>The four combat stats. Also used for per-level growth.</summary>
    public readonly struct StatBlock
    {
        public int MaxHp { get; }
        public int Attack { get; }
        public int Defense { get; }
        public int Speed { get; }

        public StatBlock(int maxHp, int attack, int defense, int speed)
        {
            MaxHp = maxHp;
            Attack = attack;
            Defense = defense;
            Speed = speed;
        }

        public static StatBlock operator +(StatBlock a, StatBlock b) =>
            new StatBlock(a.MaxHp + b.MaxHp, a.Attack + b.Attack, a.Defense + b.Defense, a.Speed + b.Speed);

        public static StatBlock operator *(StatBlock a, int factor) =>
            new StatBlock(a.MaxHp * factor, a.Attack * factor, a.Defense * factor, a.Speed * factor);

        /// <summary>Stats of a character that starts with these stats and gains <paramref name="growth"/> per level above 1.</summary>
        public StatBlock AtLevel(StatBlock growth, int level) => this + growth * Math.Max(level - 1, 0);

        public StatBlock WithDefenseBonus(int bonus) => new StatBlock(MaxHp, Attack, Defense + bonus, Speed);

        public override string ToString() => $"HP {MaxHp} ATK {Attack} DEF {Defense} SPD {Speed}";
    }

    /// <summary>
    /// One action a combatant can take (Architekturkonzept 5.3). Content is data: a new skill is a new
    /// asset (SkillDef), converted to this class.
    /// </summary>
    public sealed class Skill
    {
        public string Id { get; }
        public string Name { get; }
        public int Power { get; }
        public SkillTarget Target { get; }
        public SkillKind Kind { get; }
        /// <summary>Id of the status this skill puts on its target, or null.</summary>
        public string AppliesStatus { get; }
        public int StatusTurns { get; }
        /// <summary>Status id that lets the owner join a chain with this skill, or null (GDD 1.1).</summary>
        public string FollowUpOn { get; }

        public Skill(
            string id,
            string name,
            int power,
            SkillTarget target,
            SkillKind kind,
            string appliesStatus = null,
            int statusTurns = 3,
            string followUpOn = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Skill needs an id", nameof(id));
            if (power < 0) throw new ArgumentOutOfRangeException(nameof(power));
            if (kind == SkillKind.Heal && target != SkillTarget.Ally)
                throw new ArgumentException($"Heal skill {id} must target an ally");
            if (kind == SkillKind.Heal && (appliesStatus != null || followUpOn != null))
                throw new ArgumentException($"Heal skill {id} cannot apply a status or follow up");
            if (followUpOn != null && target != SkillTarget.Enemy)
                throw new ArgumentException($"Follow-up skill {id} must target an enemy");
            if (appliesStatus != null && statusTurns < 1)
                throw new ArgumentOutOfRangeException(nameof(statusTurns));

            Id = id;
            Name = string.IsNullOrWhiteSpace(name) ? id : name;
            Power = power;
            Target = target;
            Kind = kind;
            AppliesStatus = appliesStatus;
            StatusTurns = statusTurns;
            FollowUpOn = followUpOn;
        }

        public bool IsFollowUpSkill => FollowUpOn != null;

        public override string ToString() => Name;
    }

    /// <summary>A status on a combatant. For the MVP it is a marker for chaining that wears off after some turns.</summary>
    public sealed class StatusEffect
    {
        public string Id { get; }
        public int TurnsLeft { get; internal set; }

        internal StatusEffect(string id, int turns)
        {
            Id = id;
            TurnsLeft = turns;
        }
    }

    /// <summary>The action a player or an enemy brain picked.</summary>
    public readonly struct CombatAction
    {
        public Skill Skill { get; }
        public Combatant Target { get; }

        public CombatAction(Skill skill, Combatant target)
        {
            Skill = skill;
            Target = target;
        }
    }

    public enum CombatEventKind
    {
        TurnStarted,
        SkillUsed,
        StatusApplied,
        StatusExpired,
        Defeated,
        ChainEnded,
        FleeFailed,
        Fled,
        Ended
    }

    /// <summary>
    /// What happened during one step of combat. The session returns a list per call, the view plays it back.
    /// Only the fields that belong to the kind are set.
    /// </summary>
    public sealed class CombatEvent
    {
        public CombatEventKind Kind { get; }
        public Combatant Actor { get; internal set; }
        public Combatant Target { get; internal set; }
        public Skill Skill { get; internal set; }
        /// <summary>Damage dealt or hit points healed.</summary>
        public int Amount { get; internal set; }
        public int TargetHpAfter { get; internal set; }
        public string StatusId { get; internal set; }
        /// <summary>Position of this action in its chain, starting at 1. For ChainEnded: the length of the chain.</summary>
        public int ChainIndex { get; internal set; }
        public bool IsFollowUp { get; internal set; }
        public CombatOutcome Outcome { get; internal set; }

        internal CombatEvent(CombatEventKind kind) => Kind = kind;

        public override string ToString() => Kind switch
        {
            CombatEventKind.TurnStarted => $"{Actor.Name}'s turn",
            CombatEventKind.SkillUsed => $"{Actor.Name} uses {Skill.Name} on {Target.Name}: {Amount}" +
                                         (IsFollowUp ? $" (chain {ChainIndex})" : ""),
            CombatEventKind.StatusApplied => $"{Target.Name} is {StatusId}",
            CombatEventKind.StatusExpired => $"{Target.Name} is no longer {StatusId}",
            CombatEventKind.Defeated => $"{Target.Name} is defeated",
            CombatEventKind.ChainEnded => $"Chain of {ChainIndex}",
            CombatEventKind.FleeFailed => $"{Actor.Name} could not flee",
            CombatEventKind.Fled => $"{Actor.Name} fled",
            _ => $"Combat ended: {Outcome}"
        };
    }

    /// <summary>Chain parameters (Architekturkonzept 5.4). The upper limit is mandatory (decision on A3, A12).</summary>
    public sealed class ChainConfig
    {
        /// <summary>Most actions in one chain, the starting action included.</summary>
        public int Max { get; }
        /// <summary>GDD TODO "limiting use of same skill in one chain from one character". Off by default.</summary>
        public bool SameSkillOncePerCharacter { get; }

        public ChainConfig(int max = 5, bool sameSkillOncePerCharacter = false)
        {
            if (max < 1) throw new ArgumentOutOfRangeException(nameof(max));
            Max = max;
            SameSkillOncePerCharacter = sameSkillOncePerCharacter;
        }
    }

    /// <summary>One action that took part in a chain.</summary>
    public readonly struct ChainLink
    {
        public Combatant Actor { get; }
        public Skill Skill { get; }

        public ChainLink(Combatant actor, Skill skill)
        {
            Actor = actor;
            Skill = skill;
        }
    }

    /// <summary>The chain that is currently being resolved.</summary>
    public sealed class ChainState
    {
        private readonly List<ChainLink> _links = new List<ChainLink>();

        public int Count => _links.Count;
        public IReadOnlyList<ChainLink> Links => _links;

        internal void Add(Combatant actor, Skill skill) => _links.Add(new ChainLink(actor, skill));

        public bool HasUsed(Combatant actor, Skill skill)
        {
            foreach (var link in _links)
                if (link.Actor == actor && link.Skill == skill) return true;
            return false;
        }
    }
}
