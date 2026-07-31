using System;

namespace XianXia.Core.Random
{
    /// <summary>
    /// XorShift128+ PRNG with full-state capture. Not cryptographic.
    /// </summary>
    public sealed class DeterministicRandom : IRandomSource
    {
        ulong _s0;
        ulong _s1;
        readonly RandomStreamId _streamId;

        public DeterministicRandom(ulong seed, RandomStreamId streamId = default)
        {
            _streamId = streamId.Value == 0 ? RandomStreamId.World : streamId;
            // SplitMix64 seeding so zero seed still yields a valid non-zero state.
            var z = seed + 0x9E3779B97F4A7C15UL;
            _s0 = SplitMix64(ref z);
            _s1 = SplitMix64(ref z);
            if (_s0 == 0 && _s1 == 0)
                _s1 = 1;
        }

        public RandomStreamId StreamId => _streamId;

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be greater than minInclusive.");

            var range = (uint)(maxExclusive - minInclusive);
            var value = (int)(NextUInt64() % range);
            return minInclusive + value;
        }

        public double NextDouble()
        {
            // 53 bits of mantissa
            return (NextUInt64() >> 11) * (1.0 / (1UL << 53));
        }

        public RandomState CaptureState() => new RandomState(_s0, _s1, _streamId);

        public void RestoreState(RandomState state)
        {
            if (!state.StreamId.Equals(_streamId))
                throw new ArgumentException("RandomState stream id does not match this source.", nameof(state));
            if (state.S0 == 0 && state.S1 == 0)
                throw new ArgumentException("RandomState must not be all zeroes.", nameof(state));

            _s0 = state.S0;
            _s1 = state.S1;
        }

        ulong NextUInt64()
        {
            var s1 = _s0;
            var s0 = _s1;
            _s0 = s0;
            s1 ^= s1 << 23;
            _s1 = s1 ^ s0 ^ (s1 >> 18) ^ (s0 >> 5);
            return _s1 + s0;
        }

        static ulong SplitMix64(ref ulong z)
        {
            z += 0x9E3779B97F4A7C15UL;
            var result = z;
            result = (result ^ (result >> 30)) * 0xBF58476D1CE4E5B9UL;
            result = (result ^ (result >> 27)) * 0x94D049BB133111EBUL;
            return result ^ (result >> 31);
        }
    }
}
