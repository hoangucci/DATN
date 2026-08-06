using System;

namespace MidnightChaos.Procedural
{
    /// <summary>
    /// Small PRNG with an explicit algorithm. Its sequence is stable across
    /// Unity/.NET versions, unlike relying on an implementation-defined RNG.
    /// </summary>
    public struct DeterministicRandom
    {
        private uint state;

        public DeterministicRandom(int seed)
        {
            state = Mix(unchecked((uint)seed));
            if (state == 0)
            {
                state = 0x6D2B79F5u;
            }
        }

        public uint NextUInt()
        {
            uint value = state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            state = value;
            return value;
        }

        public float NextFloat01()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        public float Range(float minimum, float maximum)
        {
            return minimum + (maximum - minimum) * NextFloat01();
        }

        public int Range(int minimumInclusive, int maximumExclusive)
        {
            if (maximumExclusive <= minimumInclusive)
            {
                return minimumInclusive;
            }

            uint range = unchecked((uint)(maximumExclusive - minimumInclusive));
            return minimumInclusive + unchecked((int)(NextUInt() % range));
        }

        public static int DeriveSeed(int worldSeed, uint stream)
        {
            uint mixed = Mix(unchecked((uint)worldSeed) ^ Mix(stream + 0x9E3779B9u));
            return unchecked((int)mixed);
        }

        public static int CreateNextSeed(int currentSeed, uint revision)
        {
            return DeriveSeed(currentSeed, revision ^ 0xA511E9B3u);
        }

        private static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }
    }
}
