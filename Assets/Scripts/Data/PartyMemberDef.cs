using System.Linq;
using CC.Core.Combat;
using CC.Core.Run;
using UnityEngine;

namespace CC.Data
{
    /// <summary>Design data of a party member type: the hero and the characters that can be recruited (A7).</summary>
    [CreateAssetMenu(menuName = "Chaining Chronicle/Party Member", fileName = "PartyMember")]
    public sealed class PartyMemberDef : ScriptableObject
    {
        public string id;
        public string displayName;
        public RuntimeAnimatorController animator;
        [Tooltip("Colour multiplied onto the sprite so recruits can share the hero animator and still differ.")]
        public Color tint = Color.white;

        [Header("Level 1")]
        public int maxHp = 30;
        public int attack = 6;
        public int defense = 2;
        public int speed = 10;

        [Header("Added per level")]
        public int hpPerLevel = 6;
        public int attackPerLevel = 1;
        public int defensePerLevel = 1;
        public int speedPerLevel = 1;

        public SkillDef[] startSkills;

        public PartyMemberSpec ToSpec() => new PartyMemberSpec(
            string.IsNullOrEmpty(id) ? name : id,
            new StatBlock(maxHp, attack, defense, speed),
            new StatBlock(hpPerLevel, attackPerLevel, defensePerLevel, speedPerLevel),
            (startSkills ?? new SkillDef[0]).Where(s => s != null).Select(s => s.Core));
    }
}
