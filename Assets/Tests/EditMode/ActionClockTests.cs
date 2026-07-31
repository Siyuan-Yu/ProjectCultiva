using NUnit.Framework;
using XianXia.Core.Domain.Time;

namespace XianXia.Tests
{
    public sealed class ActionClockTests
    {
        [Test]
        public void Consume_ReducesRemaining()
        {
            var clock = ActionClock.Start(10);
            var next = clock.Consume(3);

            Assert.AreEqual(10UL, next.TotalDurationTicks);
            Assert.AreEqual(7UL, next.RemainingTicks);
            Assert.AreEqual(3UL, next.ElapsedTicks);
            Assert.IsFalse(next.IsComplete);
        }

        [Test]
        public void Consume_NeverGoesBelowZero()
        {
            var clock = ActionClock.Start(5);
            var done = clock.Consume(100);

            Assert.AreEqual(0UL, done.RemainingTicks);
            Assert.IsTrue(done.IsComplete);
            Assert.AreEqual(5UL, done.TotalDurationTicks);
        }

        [Test]
        public void IsComplete_WhenRemainingIsZero()
        {
            var clock = new ActionClock(8, 0);
            Assert.IsTrue(clock.IsComplete);
            Assert.IsFalse(ActionClock.Start(8).IsComplete);
        }

        [Test]
        public void Consume_DoesNotMutateWorldTick()
        {
            var worldBefore = new WorldTick(42);
            var world = worldBefore;
            var clock = ActionClock.Start(4);

            clock = clock.Consume(1);
            clock = clock.Consume(1);

            Assert.AreEqual(worldBefore, world);
            Assert.AreEqual(42UL, world.Value);
            Assert.AreEqual(2UL, clock.RemainingTicks);
        }
    }
}
