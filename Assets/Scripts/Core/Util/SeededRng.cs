using System;

namespace CC.Core.Util
{
    /// <summary>
    /// SplitMix64 generator. Own implementation instead of System.Random so results
    /// are identical on every platform and Unity version.
    /// </summary>
    public sealed class SeededRng : IRng
    {
        private readonly int _seed;
        private ulong _state;

        public SeededRng(int seed)
        {
            _seed = seed;
            _state = (ulong)(uint)seed;
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be greater than minInclusive");

            ulong range = (ulong)((long)maxExclusive - minInclusive);
            return (int)(minInclusive + (long)(NextUInt64() % range));
        }

        public float NextFloat() => (NextUInt64() >> 40) / (float)(1UL << 24);

        public bool Chance(float probability) => NextFloat() < probability;

        public IRng Derive(int salt) => new SeededRng(unchecked(_seed * 397 ^ salt * 16777619));

        private ulong NextUInt64()
        {
            _state += 0x9E3779B97F4A7C15UL;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
