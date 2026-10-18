using System;

namespace CC.Core.Run
{
    /// <summary>State of one run (one life): seed, stage and the party that persists through the stages.</summary>
    public sealed class RunState
    {
        public int Seed { get; }
        public string CharacterName { get; }
        public int StageNumber { get; private set; } = 1;
        /// <summary>Empty until the game adds the hero (needs the content, see StageController).</summary>
        public Party Party { get; } = new Party();

        public RunState(int seed, string characterName)
        {
            Seed = seed;
            CharacterName = string.IsNullOrWhiteSpace(characterName) ? "Hero" : characterName.Trim();
        }

        public void AdvanceStage() => StageNumber = checked(StageNumber + 1);
    }
}
