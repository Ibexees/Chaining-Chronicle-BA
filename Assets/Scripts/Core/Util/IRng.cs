namespace CC.Core.Util
{
    /// <summary>Source of randomness for all game logic, so runs and tests are reproducible.</summary>
    public interface IRng
    {
        /// <summary>Random integer in [minInclusive, maxExclusive).</summary>
        int Next(int minInclusive, int maxExclusive);

        /// <summary>Random float in [0, 1).</summary>
        float NextFloat();

        /// <summary>True with the given probability (0..1).</summary>
        bool Chance(float probability);

        /// <summary>Creates an independent generator derived from this seed and a salt (e.g. stage number).</summary>
        IRng Derive(int salt);
    }
}
