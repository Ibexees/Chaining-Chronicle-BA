using System.Linq;
using CC.Core.Combat;
using UnityEngine;

namespace CC.Data
{
    /// <summary>Design data of one enemy type: how it looks, how strong it is and what it can do.</summary>
    [CreateAssetMenu(menuName = "Chaining Chronicle/Enemy", fileName = "Enemy")]
    public sealed class EnemyDef : ScriptableObject
    {
        /// <summary>Stable id used by the Core layer (EnemySpawn.TypeId).</summary>
        public string id;
        public string displayName;
        public RuntimeAnimatorController animator;
        /// <summary>First stage on which this enemy can appear.</summary>
        public int firstStage = 1;

        [Header("Level 1")]
        public int maxHp = 14;
        public int attack = 4;
        public int defense;
        public int speed = 8;

        [Header("Added per level above 1")]
        public int hpPerLevel = 4;
        public int attackPerLevel = 1;
        public int defensePerLevel;
        public int speedPerLevel;

        public SkillDef[] skills;
        [Tooltip("Fsm: aggressive, heals below 30 % hit points. Random: the fallback named in the GDD.")]
        public EnemyAi ai = EnemyAi.Fsm;
        [Tooltip("Experience of a level 1 enemy. Grows linearly with the level.")]
        public int baseXp = 10;

        public EnemySpec ToSpec() => new EnemySpec(
            string.IsNullOrEmpty(id) ? name : id,
            string.IsNullOrEmpty(displayName) ? name : displayName,
            new StatBlock(maxHp, attack, defense, speed),
            new StatBlock(hpPerLevel, attackPerLevel, defensePerLevel, speedPerLevel),
            (skills ?? new SkillDef[0]).Where(s => s != null).Select(s => s.Core),
            ai,
            baseXp);
    }
}
