using UnityEngine;

namespace CC.Data
{
    /// <summary>Design data of one enemy type. Combat stats are added with the combat system.</summary>
    [CreateAssetMenu(menuName = "Chaining Chronicle/Enemy", fileName = "Enemy")]
    public sealed class EnemyDef : ScriptableObject
    {
        /// <summary>Stable id used by the Core layer (EnemySpawn.TypeId).</summary>
        public string id;
        public string displayName;
        public RuntimeAnimatorController animator;
        /// <summary>First stage on which this enemy can appear.</summary>
        public int firstStage = 1;
    }
}
