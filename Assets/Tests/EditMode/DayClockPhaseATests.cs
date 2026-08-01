using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Domain.Time;
using XianXia.Core.Events;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Data.Serialization;

namespace XianXia.Tests
{
    public sealed class DayClockPhaseATests
    {
        [Test]
        public void DayClock_DerivesFromArbitraryWorldTick()
        {
            AssertClock(0, day: 0, tickInDay: 0, hour: 0);
            AssertClock(1, day: 0, tickInDay: 1, hour: 0);
            AssertClock(3, day: 0, tickInDay: 3, hour: 0);
            AssertClock(4, day: 0, tickInDay: 4, hour: 1);
            AssertClock(95, day: 0, tickInDay: 95, hour: 23);
            AssertClock(96, day: 1, tickInDay: 0, hour: 0);
            AssertClock(97, day: 1, tickInDay: 1, hour: 0);
            AssertClock(100, day: 1, tickInDay: 4, hour: 1);
            AssertClock(192, day: 2, tickInDay: 0, hour: 0);
            AssertClock(191, day: 1, tickInDay: 95, hour: 23);
        }

        [Test]
        public void Tick95_To_96_EmitsDayEndedThenDayStarted()
        {
            var world = new SimulationWorld { Tick = new WorldTick(95) };
            var loop = new SimulationLoop(world);
            world.Events.Drain();

            Assert.IsTrue(loop.TickOnce().IsSuccess);
            Assert.AreEqual(96UL, world.Tick.Value);

            var events = world.Events.Drain();
            var dayEvents = events.FindAll(e =>
                e.Type == EventType.DayEnded || e.Type == EventType.DayStarted);
            Assert.AreEqual(2, dayEvents.Count);
            Assert.AreEqual(EventType.DayEnded, dayEvents[0].Type);
            Assert.AreEqual("dayIndex=0", dayEvents[0].Payload);
            Assert.AreEqual(EventType.DayStarted, dayEvents[1].Type);
            Assert.AreEqual("dayIndex=1", dayEvents[1].Payload);

            var clock = DayClock.FromWorldTick(world.Tick);
            Assert.AreEqual(1UL, clock.DayIndex);
            Assert.AreEqual(0, clock.TickInDay);
        }

        [Test]
        public void SameDay_DoesNotRepeatDayBoundaryEvents()
        {
            var world = new SimulationWorld { Tick = new WorldTick(10) };
            var loop = new SimulationLoop(world);
            world.Events.Drain();

            for (var i = 0; i < 20; i++)
                Assert.IsTrue(loop.TickOnce().IsSuccess);

            Assert.AreEqual(30UL, world.Tick.Value);
            var events = world.Events.Drain();
            Assert.IsFalse(events.Exists(e =>
                e.Type == EventType.DayEnded || e.Type == EventType.DayStarted));
        }

        [Test]
        public void Snapshot_RestoresDayClock_AndDoesNotRefireBoundary()
        {
            var world = new SimulationWorld { Tick = new WorldTick(95) };
            var loop = new SimulationLoop(world);
            Assert.IsTrue(loop.TickOnce().IsSuccess); // → 96, day boundary fired
            world.Events.Drain();

            var before = DayClock.FromWorldTick(world.Tick);
            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, loop);
            Assert.IsTrue(json.IsSuccess, json.IsFailure ? json.Error.ToString() : "");

            var restored = service.RestoreJson(json.Value, expectedPackageVersion: world.EnabledPackageVersion);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : "");
            var (world2, loop2) = restored.Value;

            var afterRestore = DayClock.FromWorldTick(world2.Tick);
            Assert.AreEqual(before.DayIndex, afterRestore.DayIndex);
            Assert.AreEqual(before.TickInDay, afterRestore.TickInDay);
            Assert.AreEqual(before.HourOfDay, afterRestore.HourOfDay);
            Assert.AreEqual(96UL, world2.Tick.Value);

            world2.Events.Drain();
            Assert.IsTrue(loop2.TickOnce().IsSuccess); // → 97, same day
            var events = world2.Events.Drain();
            Assert.IsFalse(events.Exists(e =>
                e.Type == EventType.DayEnded || e.Type == EventType.DayStarted));

            // Advance to next boundary still works once
            world2.Tick = new WorldTick(191);
            world2.Events.Drain();
            Assert.IsTrue(loop2.TickOnce().IsSuccess); // → 192
            var boundary = world2.Events.Drain().FindAll(e =>
                e.Type == EventType.DayEnded || e.Type == EventType.DayStarted);
            Assert.AreEqual(2, boundary.Count);
            Assert.AreEqual(EventType.DayEnded, boundary[0].Type);
            Assert.AreEqual("dayIndex=1", boundary[0].Payload);
            Assert.AreEqual(EventType.DayStarted, boundary[1].Type);
            Assert.AreEqual("dayIndex=2", boundary[1].Payload);
        }

        [Test]
        public void DayBoundaryHandler_IsInvokedInOrder_EmptyByDefault()
        {
            var calls = new List<string>();
            var handler = new RecordingHandler(calls);
            var world = new SimulationWorld { Tick = new WorldTick(95) };
            var loop = new SimulationLoop(world, dayBoundaryHandlers: new[] { handler });

            Assert.IsTrue(loop.TickOnce().IsSuccess);
            Assert.AreEqual(2, calls.Count);
            Assert.AreEqual("ended:0", calls[0]);
            Assert.AreEqual("started:1", calls[1]);
        }

        static void AssertClock(ulong tick, ulong day, int tickInDay, int hour)
        {
            var clock = DayClock.FromWorldTick(new WorldTick(tick));
            Assert.AreEqual(day, clock.DayIndex, "day @ tick " + tick);
            Assert.AreEqual(tickInDay, clock.TickInDay, "tickInDay @ tick " + tick);
            Assert.AreEqual(hour, clock.HourOfDay, "hour @ tick " + tick);
        }

        sealed class RecordingHandler : IDayBoundaryHandler
        {
            readonly List<string> _calls;

            public RecordingHandler(List<string> calls) => _calls = calls;

            public void OnDayEnded(SimulationWorld world, ulong endedDayIndex) =>
                _calls.Add("ended:" + endedDayIndex);

            public void OnDayStarted(SimulationWorld world, ulong startedDayIndex) =>
                _calls.Add("started:" + startedDayIndex);
        }
    }
}
