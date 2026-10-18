using System;
using System.Collections.Generic;
using System.Linq;
using CC.Core.Util;

namespace CC.Core.Combat
{
    /// <summary>
    /// One fight between the party and the enemy that the player ran into (Konzept 5.3). Pure logic and
    /// synchronous: every call resolves what it has to and returns the list of events, which the view
    /// plays back. The session asks enemy brains for their actions, so only the player's turns return
    /// control to the caller (phase AwaitingInput).
    /// </summary>
    public sealed class CombatSession
    {
        private readonly List<Combatant> _party;
        private readonly List<Combatant> _enemies;
        private readonly TurnOrder _order;
        private readonly ChainResolver _resolver;
        private readonly IRng _rng;

        public IReadOnlyList<Combatant> Party => _party;
        public IReadOnlyList<Combatant> Enemies => _enemies;
        public CombatPhase Phase { get; private set; } = CombatPhase.NotStarted;
        public CombatOutcome Outcome { get; private set; } = CombatOutcome.None;
        /// <summary>Chance that a flight succeeds. 1 = always (decision on A11).</summary>
        public float FleeChance { get; }
        public int Round => _order.Round;
        public ChainConfig ChainConfig => _resolver.Config;

        /// <summary>Everybody still fighting, fastest first.</summary>
        public IEnumerable<Combatant> TurnOrder => _order.Alive;

        /// <summary>Whose turn it is while waiting for the player, otherwise null.</summary>
        public Combatant Current => Phase == CombatPhase.AwaitingInput ? _order.Current : null;

        public CombatSession(
            IEnumerable<Combatant> party,
            IEnumerable<Combatant> enemies,
            IRng rng,
            ChainResolver resolver = null,
            float fleeChance = 1f)
        {
            _party = party?.ToList() ?? throw new ArgumentNullException(nameof(party));
            _enemies = enemies?.ToList() ?? throw new ArgumentNullException(nameof(enemies));
            if (_party.Count == 0 || _enemies.Count == 0) throw new ArgumentException("Both sides need a combatant");
            if (_party.Any(c => c.Team != Team.Party) || _enemies.Any(c => c.Team != Team.Enemy))
                throw new ArgumentException("Combatants are on the wrong team");

            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _resolver = resolver ?? new ChainResolver(new ChainConfig());
            FleeChance = Math.Max(0f, Math.Min(1f, fleeChance));
            _order = new TurnOrder(_party.Concat(_enemies));
        }

        /// <summary>Begins the fight. Runs enemy turns until the player has to choose or the fight is over.</summary>
        public IReadOnlyList<CombatEvent> Start()
        {
            if (Phase != CombatPhase.NotStarted) throw new InvalidOperationException("Combat already started");
            var events = new List<CombatEvent>();
            RunUntilInput(events);
            return events;
        }

        /// <summary>Living combatants that <paramref name="skill"/> can be used on by <paramref name="actor"/>.</summary>
        public IReadOnlyList<Combatant> ValidTargets(Combatant actor, Skill skill)
        {
            var side = skill.Target == SkillTarget.Ally ? actor.Team : Opposite(actor.Team);
            return (side == Team.Party ? _party : _enemies).Where(c => c.IsAlive).ToList();
        }

        /// <summary>The player's action for the current combatant.</summary>
        public IReadOnlyList<CombatEvent> SubmitAction(Skill skill, Combatant target)
        {
            var actor = RequireInput();
            Validate(actor, skill, target);

            var events = new List<CombatEvent>();
            Phase = CombatPhase.Resolving;
            ResolveAction(actor, skill, target, events);
            _order.Advance();
            RunUntilInput(events);
            return events;
        }

        /// <summary>
        /// The current combatant tries to leave the fight. On success the fight ends as Fled, otherwise the
        /// turn is used up. The enemy leaves the stage in both the won and the fled case (decision on A6).
        /// </summary>
        public IReadOnlyList<CombatEvent> TryFlee()
        {
            var actor = RequireInput();
            var events = new List<CombatEvent>();

            if (FleeChance >= 1f || _rng.Chance(FleeChance))
            {
                events.Add(new CombatEvent(CombatEventKind.Fled) { Actor = actor });
                Finish(CombatOutcome.Fled, events);
                return events;
            }

            events.Add(new CombatEvent(CombatEventKind.FleeFailed) { Actor = actor });
            Phase = CombatPhase.Resolving;
            _order.Advance();
            RunUntilInput(events);
            return events;
        }

        private Combatant RequireInput()
        {
            if (Phase != CombatPhase.AwaitingInput)
                throw new InvalidOperationException($"Combat is not waiting for input (phase {Phase})");
            var actor = _order.Current;
            if (actor == null || !actor.IsPlayerControlled)
                throw new InvalidOperationException("It is not the player's turn");
            return actor;
        }

        private void Validate(Combatant actor, Skill skill, Combatant target)
        {
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (!actor.Skills.Contains(skill))
                throw new ArgumentException($"{actor.Name} does not know {skill.Name}");
            if (!ValidTargets(actor, skill).Contains(target))
                throw new ArgumentException($"{target.Name} is not a valid target for {skill.Name}");
        }

        private void RunUntilInput(List<CombatEvent> events)
        {
            while (true)
            {
                if (CheckEnd(events)) return;

                var actor = _order.Current;
                foreach (var expired in actor.TickStatuses())
                    events.Add(new CombatEvent(CombatEventKind.StatusExpired) { Target = actor, StatusId = expired });
                events.Add(new CombatEvent(CombatEventKind.TurnStarted) { Actor = actor });

                if (actor.IsPlayerControlled)
                {
                    Phase = CombatPhase.AwaitingInput;
                    return;
                }

                Phase = CombatPhase.Resolving;
                var action = actor.Brain.ChooseAction(this, actor);
                Validate(actor, action.Skill, action.Target);
                ResolveAction(actor, action.Skill, action.Target, events);
                _order.Advance();
            }
        }

        private bool CheckEnd(List<CombatEvent> events)
        {
            if (_enemies.All(e => !e.IsAlive)) { Finish(CombatOutcome.Won, events); return true; }
            if (_party.All(p => !p.IsAlive)) { Finish(CombatOutcome.Lost, events); return true; }
            return false;
        }

        private void Finish(CombatOutcome outcome, List<CombatEvent> events)
        {
            Phase = CombatPhase.Ended;
            Outcome = outcome;

            // Hit points carry over to the next fight (A27).
            foreach (var combatant in _party)
                if (combatant.Member != null) combatant.Member.Hp = combatant.Hp;

            events.Add(new CombatEvent(CombatEventKind.Ended) { Outcome = outcome });
            GameEvents.RaiseCombatEnded(outcome);
        }

        /// <summary>One action of the active combatant plus the whole chain it starts.</summary>
        private void ResolveAction(Combatant actor, Skill skill, Combatant target, List<CombatEvent> events)
        {
            var chain = new ChainState();
            string applied = Perform(actor, skill, target, chain, false, events);

            while (applied != null && target.IsAlive)
            {
                var candidates = _order.AliveOf(actor.Team).ToList();
                if (!_resolver.TryFindFollowUp(chain, candidates, applied, out var follower, out var followSkill)) break;
                applied = Perform(follower, followSkill, target, chain, true, events);
            }

            events.Add(new CombatEvent(CombatEventKind.ChainEnded) { ChainIndex = chain.Count });
        }

        /// <summary>Returns the id of the status the action put on a surviving target, or null.</summary>
        private static string Perform(
            Combatant actor, Skill skill, Combatant target, ChainState chain, bool isFollowUp, List<CombatEvent> events)
        {
            chain.Add(actor, skill);
            GameEvents.RaiseChainCounterChanged(chain.Count);

            int amount = skill.Kind == SkillKind.Heal
                ? target.Heal(DamageCalculator.Healing(actor, skill))
                : target.TakeDamage(DamageCalculator.Damage(actor, skill, target));

            events.Add(new CombatEvent(CombatEventKind.SkillUsed)
            {
                Actor = actor,
                Target = target,
                Skill = skill,
                Amount = amount,
                TargetHpAfter = target.Hp,
                ChainIndex = chain.Count,
                IsFollowUp = isFollowUp
            });

            string applied = null;
            if (skill.Kind == SkillKind.Damage && skill.AppliesStatus != null && target.IsAlive)
            {
                target.ApplyStatus(skill.AppliesStatus, skill.StatusTurns);
                applied = skill.AppliesStatus;
                events.Add(new CombatEvent(CombatEventKind.StatusApplied)
                {
                    Actor = actor, Target = target, Skill = skill, StatusId = applied
                });
            }

            if (!target.IsAlive)
                events.Add(new CombatEvent(CombatEventKind.Defeated) { Actor = actor, Target = target });

            return applied;
        }

        private static Team Opposite(Team team) => team == Team.Party ? Team.Enemy : Team.Party;
    }
}
