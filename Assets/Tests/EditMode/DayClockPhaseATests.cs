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
            AssertClock(11, day: 0, tickInDay: 11, hour: 0);
            AssertClock(12, day: 0, tickInDay: 12, hour: 1);
            AssertClock(287, day: 0, tickInDay: 287, hour: 23);
            AssertClock(288, day: 1, tickInDay: 0, hour: 0);
            AssertClock(289, day: 1, tickInDay: 1, hour: 0);
            AssertClock(300, day: 1, tickInDay: 12, hour: 1);
            AssertClock(576, day: 2, tickInDay: 0, hour: 0);
            AssertClock(575, day: 1, tickInDay: 287, hour: 23);
        }

        [Test]
        public void LastTickOfDay_To_Next_EmitsDayEndedThenDayStarted()
        {
            var endOfDay = (ulong)(WorldTick.TicksPerDay - 1);
            var world = new SimulationWorld { Tick = new WorldTick(endOfDay) };
            var loop = new SimulationLoop(world);
            world.Events.Drain();

            Assert.IsTrue(loop.TickOnce().IsSuccess);
            Assert.AreEqual((ulong)WorldTick.TicksPerDay, world.Tick.Value);

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
            var endOfDay = (ulong)(WorldTick.TicksPerDay - 1);
            var world = new SimulationWorld { Tick = new WorldTick(endOfDay) };
            var loop = new SimulationLoop(world);
            Assert.IsTrue(loop.TickOnce().IsSuccess);
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
            Assert.AreEqual((ulong)WorldTick.TicksPerDay, world2.Tick.Value);

            world2.Events.Drain();
            Assert.IsTrue(loop2.TickOnce().IsSuccess);
            var events = world2.Events.Drain();
            Assert.IsFalse(events.Exists(e =>
                e.Type == EventType.DayEnded || e.Type == EventType.DayStarted));

            var nextBoundary = (ulong)(WorldTick.TicksPerDay * 2 - 1);
            world2.Tick = new WorldTick(nextBoundary);
            world2.Events.Drain();
            Assert.IsTrue(loop2.TickOnce().IsSuccess);
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
            var world = new SimulationWorld { Tick = new WorldTick((ulong)(WorldTick.TicksPerDay - 1)) };
            var loop = new SimulationLoop(world, dayBoundaryHandlers: new[] { handler });

            Assert.IsTrue(loop.TickOnce().IsSuccess);
            Assert.AreEqual(2, calls.Count);
            Assert.AreEqual("ended:0", calls[0]);
            Assert.AreEqual("started:1", calls[1]);
        }

        [Test]
        public void MinuteOfHour_StepsByFiveAtOneXTick()
        {
            Assert.AreEqual(0, DayClock.FromWorldTick(new WorldTick(0)).MinuteOfHour);
            Assert.AreEqual(5, DayClock.FromWorldTick(new WorldTick(1)).MinuteOfHour);
            Assert.AreEqual(10, DayClock.FromWorldTick(new WorldTick(2)).MinuteOfHour);
            Assert.AreEqual(55, DayClock.FromWorldTick(new WorldTick(11)).MinuteOfHour);
            Assert.AreEqual(0, DayClock.FromWorldTick(new WorldTick(12)).MinuteOfHour);
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
