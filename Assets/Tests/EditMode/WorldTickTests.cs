using System;
using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Domain.Time;

namespace XianXia.Tests
{
    public sealed class WorldTickTests
    {
        [Test]
        public void Advance_IncrementsMonotonically()
        {
            var t0 = WorldTick.Zero;
            var t1 = t0.Add(1);
            var t2 = t1.Add(95);

            Assert.AreEqual(0UL, t0.Value);
            Assert.AreEqual(1UL, t1.Value);
            Assert.AreEqual(96UL, t2.Value);
            Assert.AreEqual(WorldTick.TicksPerDay, (int)t2.Value);
            Assert.AreEqual(15, WorldTick.GameMinutesPerTick);
        }

        [Test]
        public void Comparison_Operators_AreCorrect()
        {
            var early = new WorldTick(10);
            var late = new WorldTick(20);

            Assert.IsTrue(early < late);
            Assert.IsTrue(late > early);
            Assert.IsTrue(early <= early);
            Assert.IsTrue(late >= early);
            Assert.AreNotEqual(early, late);
            Assert.AreEqual(-1, early.CompareTo(late));
            Assert.AreEqual(1, late.CompareTo(early));
            Assert.AreEqual(0, early.CompareTo(early));
        }

        [Test]
        public void Add_Overflow_Throws()
        {
            var nearMax = new WorldTick(ulong.MaxValue);
            Assert.Throws<OverflowException>(() => nearMax.Add(1));
            Assert.Throws<OverflowException>(() => _ = nearMax + 1UL);
        }

        [Test]
        public void Subtract_Underflow_Throws_TrySubtract_Fails()
        {
            var tick = new WorldTick(3);
            Assert.Throws<OverflowException>(() => tick.Subtract(4));
            Assert.IsFalse(tick.TrySubtract(4, out _));

            Assert.IsTrue(tick.TrySubtract(2, out var result));
            Assert.AreEqual(1UL, result.Value);
            Assert.AreEqual(new WorldTick(1), tick - 2UL);
        }

        [Test]
        public void WorksAsDictionaryKey()
        {
            var map = new Dictionary<WorldTick, string> { [new WorldTick(5)] = "dawn" };
            Assert.AreEqual("dawn", map[new WorldTick(5)]);
        }
    }
}
