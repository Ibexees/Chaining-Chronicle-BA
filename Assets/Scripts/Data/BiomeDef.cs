using System.Collections.Generic;
using UnityEngine;

namespace CC.Data
{
    /// <summary>
    /// Look and enemy pool of a group of stages. Stages differ only by swapping these sprites
    /// (palette swap, GDD 3.2.1).
    /// </summary>
    [CreateAssetMenu(menuName = "Chaining Chronicle/Biome", fileName = "Biome")]
    public sealed class BiomeDef : ScriptableObject
    {
        public string id;
        /// <summary>First entry is the plain floor tile, further entries are decoration shown now and then.</summary>
        public Sprite[] floorSprites;
        public Sprite[] wallSprites;
        public Sprite stairsSprite;
        public EnemyDef[] enemies;

        /// <summary>Ids of the enemies of this biome that may appear on the given stage.</summary>
        public IReadOnlyList<string> EnemyIdsFor(int stageNumber)
        {
            var ids = new List<string>();
            if (enemies == null) return ids;
            foreach (var enemy in enemies)
                if (enemy != null && enemy.firstStage <= stageNumber) ids.Add(enemy.id);
            return ids;
        }

        public EnemyDef FindEnemy(string enemyId)
        {
            if (enemies == null) return null;
            foreach (var enemy in enemies)
                if (enemy != null && enemy.id == enemyId) return enemy;
            return null;
        }
    }
}
