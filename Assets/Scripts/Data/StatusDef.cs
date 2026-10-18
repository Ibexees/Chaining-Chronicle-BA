using UnityEngine;

namespace CC.Data
{
    /// <summary>
    /// A status that skills can apply and that other skills can follow up on (GDD 1.1). Only a name and a
    /// colour for the views: how long it lasts is set per skill (SkillDef.statusTurns).
    /// </summary>
    [CreateAssetMenu(menuName = "Chaining Chronicle/Status", fileName = "Status")]
    public sealed class StatusDef : ScriptableObject
    {
        /// <summary>Stable id used by the Core layer (Skill.AppliesStatus, Skill.FollowUpOn).</summary>
        public string id;
        public string displayName;
        public Color color = Color.white;
    }
}
