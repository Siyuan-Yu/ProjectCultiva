using NUnit.Framework;
using XianXia.Core.Random;

namespace XianXia.Tests
{
    public sealed class RandomTests
    {
        [Test]
        public void SameSeed_SameSequence()
        {
            var a = new DeterministicRandom(12345);
            var b = new DeterministicRandom(12345);
            for (var i = 0; i < 32; i++)
            {
                Assert.AreEqual(a.NextInt(0, 1000), b.NextInt(0, 1000));
                Assert.AreEqual(a.NextDouble(), b.NextDouble(), 0.0);
            }
        }

        [Test]
        public void CaptureRestore_ContinuesIdenticalSequence()
        {
            var live = new DeterministicRandom(99);
            for (var i = 0; i < 5; i++)
                live.NextInt(0, 100);

            var state = live.CaptureState();
            var expectedNext = live.NextInt(0, 100);
            var expectedDouble = live.NextDouble();

            var restored = new DeterministicRandom(1);
            restored.RestoreState(state);
            Assert.AreEqual(expectedNext, restored.NextInt(0, 100));
            Assert.AreEqual(expectedDouble, restored.NextDouble(), 0.0);
        }

        [Test]
        public void DifferentSeeds_Diverge()
        {
            var a = new DeterministicRandom(1);
            var b = new DeterministicRandom(2);
            Assert.AreNotEqual(a.NextInt(0, int.MaxValue), b.NextInt(0, int.MaxValue));
        }
    }
}
