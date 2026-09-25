using System;
using System.Collections.Generic;

namespace Gnomes.Core
{
    /// <summary>Deterministic RNG (mulberry32) so a seed reproduces a whole night on every machine.</summary>
    public sealed class Rng
    {
        uint s;

        public Rng(int seed) { s = unchecked((uint)seed); if (s == 0) s = 0x9e3779b9; }

        public uint NextUInt()
        {
            unchecked
            {
                s += 0x6d2b79f5;
                uint t = s;
                t = (t ^ (t >> 15)) * (t | 1);
                t ^= t + (t ^ (t >> 7)) * (t | 61);
                return t ^ (t >> 14);
            }
        }

        /// <summary>[0, 1)</summary>
        public float Next() => NextUInt() / 4294967296f;
        public float Range(float a, float b) => a + (b - a) * Next();
        /// <summary>Inclusive range.</summary>
        public int Int(int a, int bInclusive) => a + (int)(Next() * (bInclusive - a + 1));
        public bool Chance(float p) => Next() < p;
        public T Pick<T>(IList<T> list) => list[(int)(Next() * list.Count) % list.Count];

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = (int)(Next() * (i + 1)) % (i + 1);
                var tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        public static int Hash(string text)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in text) { h ^= c; h *= 16777619; }
                return (int)h;
            }
        }
    }
}
