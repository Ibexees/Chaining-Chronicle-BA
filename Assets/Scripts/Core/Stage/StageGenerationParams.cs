using System;
using System.Collections.Generic;

namespace CC.Core.Stage
{
    /// <summary>All knobs of the stage generator for one stage. Created by <see cref="StageRules"/>.</summary>
    public sealed class StageGenerationParams
    {
        public int Width { get; set; } = 40;
        public int Height { get; set; } = 28;
        public int RoomAttempts { get; set; } = 30;
        public int MinRoomSize { get; set; } = 4;
        public int MaxRoomSize { get; set; } = 9;

        public int EnemyCount { get; set; } = 2;
        public int EnemyLevel { get; set; } = 1;
        /// <summary>Enemy type ids that may spawn. Empty means no enemies.</summary>
        public IReadOnlyList<string> EnemyTypeIds { get; set; } = Array.Empty<string>();
        /// <summary>Minimum walking distance between player start and any enemy spawn.</summary>
        public int MinEnemyDistanceFromStart { get; set; } = 6;

        public int PickupCount { get; set; } = 2;
        /// <summary>Kinds that may spawn. Repeat a kind to make it more likely.</summary>
        public IReadOnlyList<PickupKind> PickupKinds { get; set; } = new[] { PickupKind.Armor };
    }
}
