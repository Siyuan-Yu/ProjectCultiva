using System.IO;
using NUnit.Framework;
using XianXia.Core.Actions;
using XianXia.Core.Concealment;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Events;
using XianXia.Core.Input;
using XianXia.Core.Labor;
using XianXia.Core.Opportunity;
using XianXia.Core.Persistence;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;
using XianXia.Data.Content;
using XianXia.Data.Cultivation;
using XianXia.Data.Opportunity;
using XianXia.Data.Serialization;

namespace XianXia.Tests
{
    /// <summary>
    /// VS0.3 B–D command-sequence integration (not a chapter script).
    /// </summary>
    public sealed class Vs03PhaseBcdIntegrationTests
    {
        static readonly DefinitionId SiteId = new DefinitionId("base", "site_abandoned_cave");
        static readonly DefinitionId QingyunId = new DefinitionId("base", "cultivation_qingyun_manual");

        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        [Test]
        public void Observe_DiscoversSite_ForcedSuccess()
        {
            var (world, loop, entity, port) = CreateWorldWithSite();
            Assert.IsTrue(port.Submit(new PlayerCommandRequest(entity.Id, PlayerCommandKind.Observe, 2)).IsSuccess);
            loop.TickOnce();
            loop.TickOnce();

            Assert.IsTrue(entity.Get<KnownSitesComponent>().Knows(SiteId));
            var events = world.Events.Drain();
            Assert.IsTrue(events.Exists(e => e.Type == EventType.OpportunitySiteDiscovered));
            Assert.IsTrue(events.Exists(e => e.Type == EventType.ObservationResolved && e.Payload.Contains("discovered")));
        }

        [Test]
        public void Cultivate_WithoutKnownSite_IsRejected()
        {
            var (world, loop, entity, port) = CreateWorldWithSite();
            var result = port.Submit(new PlayerCommandRequest(entity.Id, PlayerCommandKind.Cultivate, 2));
            Assert.IsTrue(result.IsFailure);
            Assert.IsFalse(entity.Get<CultivationComponent>().HasLearnedManual);
        }

        [Test]
        public void Discover_ThenCultivate_LearnsQingyun_AndRaisesRisk()
        {
            var (world, loop, entity, port) = CreateWorldWithSite();
            DiscoverSite(port, loop, entity);

            Assert.IsFalse(entity.Get<CultivationComponent>().HasLearnedManual);
            Assert.IsTrue(port.Submit(new PlayerCommandRequest(entity.Id, PlayerCommandKind.Cultivate, 3)).IsSuccess);
            Assert.IsTrue(entity.Get<CultivationComponent>().HasLearnedManual);
            Assert.AreEqual(QingyunId, entity.Get<CultivationComponent>().LearnedManualId.Value);

            var progressBefore = entity.Get<CultivationComponent>().Progress;
            for (var i = 0; i < 3; i++)
                loop.TickOnce();

            Assert.Greater(entity.Get<CultivationComponent>().Progress, progressBefore);
            Assert.AreEqual(3, entity.Get<PersonalConcealmentRiskComponent>().Value);
        }

        [Test]
        public void DayEnded_AppliesQuotaConsequence_AndResetsCounters()
        {
            var world = new SimulationWorld { Tick = new WorldTick((ulong)(WorldTick.TicksPerDay - 1)) };
            var loop = new SimulationLoop(world);
            var entity = world.Entities.CreateCharacter(new DefinitionId("base", "p"), "甲").Value;
            var daily = entity.Get<DailyTaskComponent>();
            daily.RequiredAmount = 10;
            daily.CompletedAmount = 2;
            daily.Deviation = 5;
            world.Events.Drain();

            Assert.IsTrue(loop.TickOnce().IsSuccess);
            Assert.AreEqual((ulong)WorldTick.TicksPerDay, world.Tick.Value);

            Assert.IsTrue(daily.PendingReprimand);
            Assert.AreEqual(5, daily.LastSettledDeviation);
            Assert.AreEqual(0, daily.CompletedAmount);
            Assert.AreEqual(0, daily.Deviation);

            var events = world.Events.Drain();
            Assert.IsTrue(events.Exists(e => e.Type == EventType.DayEnded));
            Assert.IsTrue(events.Exists(e => e.Type == EventType.QuotaConsequenceApplied));
            Assert.IsTrue(events.Exists(e => e.Type == EventType.DayStarted));
        }

        [Test]
        public void FullDayLoop_Observe_Cultivate_Quota_Snapshot()
        {
            var (world, loop, entity, port) = CreateWorldWithSite();
            entity.Get<DailyTaskComponent>().RequiredAmount = 8;

            // Schedule labor so Override can create Deviation when observing.
            var schedule = new ScheduleDefinition("test:labor_day").AddBlock(0, WorldTick.TicksPerDay, ScheduleActivity.Labor, 8);
            world.RegisterSchedule(schedule);
            Assert.IsTrue(entity.AddComponent(new ScheduleComponent(schedule.Id)).IsSuccess);

            loop.TickOnce();
            Assert.IsInstanceOf<LaborAction>(FirstActive(world));

            DiscoverSite(port, loop, entity);
            Assert.Greater(entity.Get<DailyTaskComponent>().Deviation, 0);

            Assert.IsTrue(port.Submit(new PlayerCommandRequest(entity.Id, PlayerCommandKind.Cultivate, 2)).IsSuccess);
            loop.TickOnce();
            loop.TickOnce();
            Assert.Greater(entity.Get<CultivationComponent>().Progress, 0);
            Assert.Greater(entity.Get<PersonalConcealmentRiskComponent>().Value, 0);

            // Jump to day boundary
            world.Tick = new WorldTick((ulong)(WorldTick.TicksPerDay - 1));
            world.Events.Drain();
            var deviationBefore = entity.Get<DailyTaskComponent>().Deviation;
            Assert.Greater(deviationBefore, 0);
            Assert.IsTrue(loop.TickOnce().IsSuccess);

            Assert.IsTrue(entity.Get<DailyTaskComponent>().PendingReprimand);
            Assert.AreEqual(0, entity.Get<DailyTaskComponent>().Deviation);
            Assert.IsTrue(world.Events.Drain().Exists(e => e.Type == EventType.QuotaConsequenceApplied));

            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, loop);
            Assert.IsTrue(json.IsSuccess, json.IsFailure ? json.Error.ToString() : "");

            var restored = service.RestoreJson(json.Value, expectedPackageVersion: world.EnabledPackageVersion);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : "");
            var (world2, _) = restored.Value;

            Entity e2 = null;
            foreach (var e in world2.Entities.All)
                e2 = e;
            Assert.IsNotNull(e2);
            Assert.IsTrue(e2.Get<KnownSitesComponent>().Knows(SiteId));
            Assert.IsTrue(e2.Get<CultivationComponent>().HasLearnedManual);
            Assert.IsTrue(e2.Get<DailyTaskComponent>().PendingReprimand);
            Assert.Greater(e2.Get<PersonalConcealmentRiskComponent>().Value, 0);
            Assert.IsTrue(world2.TryGetOpportunitySite(SiteId, out _));
            Assert.IsTrue(world2.TryGetManual(QingyunId, out _));
        }

        [Test]
        public void Content_SitesJson_LoadsAndMaps()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : "");
            Assert.IsTrue(loaded.Value.Registry.TryGetOpportunitySite(SiteId, out var def));
            Assert.IsTrue(def.AllowsCultivation);
            Assert.AreEqual("site.abandoned_cave", def.NameKey);

            var runtime = OpportunitySiteMapper.ToRuntime(def);
            Assert.IsTrue(runtime.IsSuccess);
            Assert.AreEqual(QingyunId, runtime.Value.OfferedManualId.Value);

            Assert.IsTrue(loaded.Value.Registry.TryGetCultivation(QingyunId, out var cult));
            var manual = CultivationManualMapper.ToManualSpec(cult);
            Assert.IsTrue(manual.IsSuccess);
        }

        static (SimulationWorld world, SimulationLoop loop, Entity entity, PlayerInputPort port) CreateWorldWithSite()
        {
            var world = new SimulationWorld { ObservationDiscoverChancePercent = 100 };
            var loop = new SimulationLoop(world);
            var entity = world.Entities.CreateCharacter(new DefinitionId("base", "character_protagonist"), "主角").Value;

            world.RegisterOpportunitySite(new OpportunitySite(
                SiteId,
                allowsCultivation: true,
                offeredManualId: QingyunId,
                nameKey: "site.abandoned_cave",
                description: "废弃洞口"));

            world.RegisterManual(new CultivationManualSpec
            {
                Id = QingyunId,
                RequiredRealm = "Mortal",
                CultivationSpeed = 25,
                BreakthroughProgress = 100
            });

            return (world, loop, entity, new PlayerInputPort(loop));
        }

        static void DiscoverSite(PlayerInputPort port, SimulationLoop loop, Entity entity)
        {
            Assert.IsTrue(port.Submit(new PlayerCommandRequest(entity.Id, PlayerCommandKind.Observe, 1)).IsSuccess);
            loop.TickOnce();
            Assert.IsTrue(entity.Get<KnownSitesComponent>().Knows(SiteId));
        }

        static IAction FirstActive(SimulationWorld world)
        {
            foreach (var a in world.ActiveActions.Values)
                return a;
            return null;
        }
    }
}
