using System;

namespace CC.Core.Run
{
    /// <summary>State of one run (one life). Party and inventory are added with the combat system.</summary>
    public sealed class RunState
    {
        public int Seed { get; }
        public string CharacterName { get; }
        public int StageNumber { get; private set; } = 1;

        public RunState(int seed, string characterName)
        {
            Seed = seed;
            CharacterName = string.IsNullOrWhiteSpace(characterName) ? "Hero" : characterName.Trim();
        }

        public void AdvanceStage() => StageNumber = checked(StageNumber + 1);
    }
}
