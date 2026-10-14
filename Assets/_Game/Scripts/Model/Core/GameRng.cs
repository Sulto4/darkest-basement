using System;
using System.Collections.Generic;

namespace DarkestBasement.Model.Core
{
    public sealed class GameRng
    {
        private uint state;
        public int Seed { get; }
        public GameRng(int seed) { Seed = seed; state = unchecked((uint)seed) ^ 0xA3C59AC3u; if (state == 0) state = 1; }
        private uint Next()
        { uint x = state; x ^= x << 13; x ^= x >> 17; x ^= x << 5; state = x; return x; }
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            uint span = (uint)((long)maxExclusive - minInclusive);
            // Respingem coada pentru a evita favorizarea unor resturi modulo.
            uint limit = unchecked(0u - span) % span;
            uint value; do { value = Next(); } while (value < limit);
            return (int)(minInclusive + (long)(value % span));
        }
        public bool Chance(double probability)
        { if (double.IsNaN(probability) || probability < 0 || probability > 1) throw new ArgumentOutOfRangeException(nameof(probability)); return Next() / 4294967296.0 < probability; }
        public T Pick<T>(IReadOnlyList<T> values) { if (values.Count == 0) throw new ArgumentException("Empty pool."); return values[Range(0, values.Count)]; }
        public void Shuffle<T>(IList<T> values)
        { for (int i = values.Count - 1; i > 0; i--) { int j = Range(0, i + 1); T temp = values[i]; values[i] = values[j]; values[j] = temp; } }
        public GameRng Fork(string streamName)
        {
            if (streamName == null) throw new ArgumentNullException(nameof(streamName));
            // Hash stabil; string.GetHashCode diferă între runtime-uri.
            uint hash = 2166136261u;
            foreach (char c in streamName) { hash ^= c; hash = unchecked(hash * 16777619u); }
            return new GameRng(unchecked((int)(hash ^ (uint)Seed)));
        }
    }
}
