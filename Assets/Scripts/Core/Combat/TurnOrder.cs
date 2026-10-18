using System;
using System.Collections.Generic;
using System.Linq;

namespace CC.Core.Combat
{
    /// <summary>
    /// One shared turn order for everybody in the fight, fastest first (decision on A4: no head start
    /// for the player). On equal speed the party goes first, then the order of the list (A13).
    /// Chains do not change the order (A14). The order is fixed for the combat, defeated
    /// combatants are skipped. Effects that change the order are an extension point (Konzept 10).
    /// </summary>
    public sealed class TurnOrder
    {
        private readonly List<Combatant> _order;
        private int _index;

        public int Round { get; private set; } = 1;

        public TurnOrder(IEnumerable<Combatant> combatants)
        {
            var list = combatants?.ToList() ?? throw new ArgumentNullException(nameof(combatants));
            if (list.Count == 0) throw new ArgumentException("A fight needs combatants", nameof(combatants));

            // OrderBy is stable, so list order breaks the remaining ties.
            _order = list
                .Select((c, i) => (c, i))
                .OrderByDescending(x => x.c.Stats.Speed)
                .ThenBy(x => x.c.Team)
                .ThenBy(x => x.i)
                .Select(x => x.c)
                .ToList();

            SkipDefeated();
        }

        /// <summary>The combatant whose turn it is. Null when everybody is defeated.</summary>
        public Combatant Current => _order.Count > 0 && _order[_index].IsAlive ? _order[_index] : null;

        /// <summary>Everybody still in the fight, in turn order (not rotated).</summary>
        public IEnumerable<Combatant> Alive => _order.Where(c => c.IsAlive);

        /// <summary>Living members of one team, fastest first.</summary>
        public IEnumerable<Combatant> AliveOf(Team team) => _order.Where(c => c.IsAlive && c.Team == team);

        public IReadOnlyList<Combatant> All => _order;

        /// <summary>Moves to the next living combatant. Starts a new round after the slowest.</summary>
        public void Advance()
        {
            if (!_order.Any(c => c.IsAlive)) return;

            do
            {
                _index++;
                if (_index >= _order.Count)
                {
                    _index = 0;
                    Round++;
                }
            } while (!_order[_index].IsAlive);
        }

        private void SkipDefeated()
        {
            while (_index < _order.Count - 1 && !_order[_index].IsAlive) _index++;
        }
    }
}
