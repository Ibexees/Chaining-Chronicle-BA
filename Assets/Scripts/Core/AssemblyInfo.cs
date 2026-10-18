using System.Runtime.CompilerServices;

// The EditMode tests set up combat states (e.g. a defeated combatant) through the internal members
// that only CombatSession is meant to call.
[assembly: InternalsVisibleTo("CC.Tests.EditMode")]
