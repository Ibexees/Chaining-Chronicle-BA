using CC.Core.Combat;
using UnityEngine;

namespace CC.Data
{
    /// <summary>Design data of one skill. A new skill is a new asset, not a new class (Konzept 7, rule 5).</summary>
    [CreateAssetMenu(menuName = "Chaining Chronicle/Skill", fileName = "Skill")]
    public sealed class SkillDef : ScriptableObject
    {
        public string id;
        public string displayName;
        public int power = 4;
        public SkillTarget target = SkillTarget.Enemy;
        public SkillKind kind = SkillKind.Damage;
        [Tooltip("Status put on the target (damage skills only).")]
        public StatusDef appliesStatus;
        [Min(1)] public int statusTurns = 3;
        [Tooltip("The owner joins a chain when the target was just afflicted with this status.")]
        public StatusDef followUpOn;

        private Skill _core;

        /// <summary>The Core copy. Created once so that all combatants share the same instance.</summary>
        public Skill Core => _core ??= new Skill(
            string.IsNullOrEmpty(id) ? name : id,
            string.IsNullOrEmpty(displayName) ? name : displayName,
            power,
            target,
            kind,
            appliesStatus != null ? appliesStatus.id : null,
            statusTurns,
            followUpOn != null ? followUpOn.id : null);

        private void OnValidate() => _core = null;
    }
}
