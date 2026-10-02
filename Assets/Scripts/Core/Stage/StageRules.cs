using System;
using System.Collections.Generic;

namespace CC.Core.Stage
{
    /// <summary>Difficulty scaling over the endless stage sequence (GDD 4.1).</summary>
    public static class StageRules
    {
        private static readonly PickupKind[] PickupPool =
        {
            PickupKind.Armor, PickupKind.Armor,
            PickupKind.ChainSkill, PickupKind.ChainSkill,
            PickupKind.Recruit
        };

        public static StageGenerationParams Create(int stageNumber, IReadOnlyList<string> enemyTypeIds)
        {
            if (stageNumber < 1) throw new ArgumentOutOfRangeException(nameof(stageNumber), "Stages start at 1");

            return new StageGenerationParams
            {
                Width = Math.Min(36 + stageNumber * 2, 64),
                Height = Math.Min(26 + stageNumber, 44),
                EnemyCount = Math.Min(2 + stageNumber / 2, 14),
                EnemyLevel = stageNumber,
                EnemyTypeIds = enemyTypeIds ?? Array.Empty<string>(),
                PickupCount = Math.Min(2 + stageNumber / 5, 4),
                PickupKinds = PickupPool
            };
        }
    }
}
