using System.IO;
using NUnit.Framework;
using XianXia.Core.Concealment;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Labor;
using XianXia.Core.Opportunity;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;
using XianXia.Data.Bootstrap;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class PlayableDayBootstrapPhaseATests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        [Test]
        public void PlayableDayBootstrap_LoadsBaseGame_CreatesThreeCharacters()
        {
            var result = new PlayableDayBootstrap().Start(BaseGamePath);
            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : "");

            Assert.AreEqual(3, result.Value.CharacterIds.Count);
            Assert.AreEqual(4, result.Value.World.Entities.Count); // +1 recruitable Npc (VS0.5-D)
            Assert.IsFalse(result.Value.RecruitableNpcId.IsNone);
            Assert.IsNotNull(result.Value.Loop);
            Assert.IsNotNull(result.Value.Port);
            Assert.IsNotNull(result.Value.Registry);
            Assert.Greater(result.Value.World.OpportunitySites.Count, 0);
            Assert.Greater(result.Value.World.Manuals.Count, 0);
            Assert.IsTrue(result.Value.World.TryGetSchedule(PlayableDayBootstrap.DefaultScheduleId, out _));
        }

        [Test]
        public void PlayableDayBootstrap_AssemblesScheduleDailyTaskRisk_AndQuotaHandler()
        {
            var result = new PlayableDayBootstrap().Start(BaseGamePath);
            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : "");

            foreach (var id in result.Value.CharacterIds)
            {
                Assert.IsTrue(result.Value.World.Entities.TryGet(id, out var entity));
                Assert.IsTrue(entity.TryGet<ScheduleComponent>(out var schedule));
                Assert.AreEqual(PlayableDayBootstrap.DefaultScheduleId, schedule.DefinitionId);
                Assert.IsTrue(entity.TryGet<DailyTaskComponent>(out var daily));
                Assert.AreEqual(10, daily.RequiredAmount);
                Assert.IsTrue(entity.TryGet<KnownSitesComponent>(out _));
                Assert.IsTrue(entity.TryGet<PersonalConcealmentRiskComponent>(out _));
            }

            // Day boundary with QuotaConsequenceHandler (default on SimulationLoop).
            var world = result.Value.World;
            var loop = result.Value.Loop;
            var first = world.Entities.TryGet(result.Value.CharacterIds[0], out var e0);
            Assert.IsTrue(first);
            e0.Get<DailyTaskComponent>().Deviation = 3;
            e0.Get<DailyTaskComponent>().CompletedAmount = 0;
            world.Tick = new WorldTick((ulong)(WorldTick.TicksPerDay - 1));
            world.Events.Drain();
            Assert.IsTrue(loop.TickOnce().IsSuccess);
            Assert.IsTrue(world.Events.Drain().Exists(ev => ev.Type == EventType.QuotaConsequenceApplied));
            Assert.IsTrue(e0.Get<DailyTaskComponent>().PendingReprimand);
        }

        [Test]
        public void PlayableDayBootstrap_LoopAdvancesWorldTick()
        {
            var result = new PlayableDayBootstrap().Start(BaseGamePath);
            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : "");
            Assert.AreEqual(0UL, result.Value.World.Tick.Value);
            Assert.IsTrue(result.Value.Loop.TickOnce().IsSuccess);
            Assert.AreEqual(1UL, result.Value.World.Tick.Value);
        }

        [Test]
        public void PlayableDayBootstrap_MissingPath_FailsClearly()
        {
            var result = new PlayableDayBootstrap().Start("D:\\definitely\\missing\\BaseGame");
            Assert.IsTrue(result.IsFailure);
        }

        [Test]
        public void PlayableDayOptions_CanRaiseDiscoverChance_WithoutChangingUnsetDefaultPath()
        {
            var withOverride = new PlayableDayBootstrap().Start(
                BaseGamePath,
                new PlayableDayOptions { ObservationDiscoverChancePercent = 100 });
            Assert.IsTrue(withOverride.IsSuccess);
            Assert.AreEqual(100, withOverride.Value.World.ObservationDiscoverChancePercent);

            var plain = new PlayableDayBootstrap().Start(BaseGamePath);
            Assert.IsTrue(plain.IsSuccess);
            // World default remains whatever SimulationWorld constructs (not a Core rule edit).
            Assert.AreEqual(new SimulationWorld().ObservationDiscoverChancePercent,
                plain.Value.World.ObservationDiscoverChancePercent);
        }

        [Test]
        public void PlayableHostSession_InitializeAndTick()
        {
            var session = new PlayableHostSession();
            var init = session.Initialize(BaseGamePath);
            Assert.IsTrue(init.IsSuccess, init.IsFailure ? init.Error.ToString() : "");
            Assert.IsTrue(session.IsInitialized);
            Assert.AreEqual(3, session.CharacterIds.Count);

            Assert.IsTrue(session.TickOnce().IsSuccess);
            Assert.AreEqual(1UL, session.World.Tick.Value);

            session.Clear();
            Assert.IsFalse(session.IsInitialized);
        }

        [Test]
        public void PlayableHostBootstrap_ResolvesEditorBaseGamePath()
        {
            Assert.IsTrue(PlayableHostBootstrap.TryResolveEditorBaseGamePath(out var path, out var error), error);
            Assert.IsTrue(Directory.Exists(path));
            Assert.IsTrue(File.Exists(Path.Combine(path, "manifest.json")));
            Assert.AreEqual(Path.GetFullPath(BaseGamePath), Path.GetFullPath(path));
        }
    }
}
