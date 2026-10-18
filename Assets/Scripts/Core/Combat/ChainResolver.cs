using System;
using System.Collections.Generic;
using System.Linq;

namespace CC.Core.Combat
{
    /// <summary>An extra condition that decides whether a skill may join a chain (Konzept 5.4, 10).</summary>
    public interface IChainRule
    {
        bool Allows(ChainState chain, Combatant actor, Skill skill);
    }

    /// <summary>GDD TODO: a character may use the same skill only once in one chain.</summary>
    public sealed class SameSkillOncePerCharacterRule : IChainRule
    {
        public bool Allows(ChainState chain, Combatant actor, Skill skill) => !chain.HasUsed(actor, skill);
    }

    /// <summary>
    /// The core mechanic (GDD 1.1): after an action put a status on the target, the combatants of the
    /// acting side are checked, fastest first. The first one with a skill whose follow-up condition
    /// matches that status joins the chain. The mandatory upper limit ends every chain.
    /// </summary>
    public sealed class ChainResolver
    {
        private readonly List<IChainRule> _rules = new List<IChainRule>();

        public ChainConfig Config { get; }

        public ChainResolver(ChainConfig config, IEnumerable<IChainRule> extraRules = null)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            if (config.SameSkillOncePerCharacter) _rules.Add(new SameSkillOncePerCharacterRule());
            if (extraRules != null) _rules.AddRange(extraRules);
        }

        /// <summary>
        /// Looks for the next follow-up. <paramref name="candidatesBySpeed"/> are the living combatants of
        /// the acting team, fastest first. The acting combatant itself is a candidate.
        /// </summary>
        public bool TryFindFollowUp(
            ChainState chain,
            IEnumerable<Combatant> candidatesBySpeed,
            string statusId,
            out Combatant actor,
            out Skill skill)
        {
            actor = null;
            skill = null;
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (statusId == null || chain.Count >= Config.Max) return false;

            foreach (var candidate in candidatesBySpeed)
            {
                if (!candidate.IsAlive) continue;
                foreach (var s in candidate.Skills)
                {
                    if (s.FollowUpOn != statusId) continue;
                    if (!_rules.All(r => r.Allows(chain, candidate, s))) continue;

                    actor = candidate;
                    skill = s;
                    return true;
                }
            }
            return false;
        }
    }
}
