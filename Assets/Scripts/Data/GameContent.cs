using CC.Core.Stage;
using UnityEngine;

namespace CC.Data
{
    /// <summary>
    /// Root of all design data. Picks the biome for a stage and turns data into Core parameters.
    /// Rebuilt by the menu item "Chaining Chronicle/Build Content".
    /// </summary>
    [CreateAssetMenu(menuName = "Chaining Chronicle/Game Content", fileName = "GameContent")]
    public sealed class GameContent : ScriptableObject
    {
        public BiomeDef[] biomes;
        /// <summary>The biome changes every this many stages and then cycles through the list.</summary>
        public int stagesPerBiome = 3;
        public RuntimeAnimatorController playerAnimator;
        public RuntimeAnimatorController pickupAnimator;

        public BiomeDef BiomeFor(int stageNumber)
        {
            if (biomes == null || biomes.Length == 0)
                throw new System.InvalidOperationException("GameContent has no biomes");
            int block = (Mathf.Max(stageNumber, 1) - 1) / Mathf.Max(stagesPerBiome, 1);
            return biomes[block % biomes.Length];
        }

        public StageGenerationParams ParamsFor(int stageNumber) =>
            StageRules.Create(stageNumber, BiomeFor(stageNumber).EnemyIdsFor(stageNumber));
    }
}
