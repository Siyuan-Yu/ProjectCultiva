using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Actions;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Labor;
using XianXia.Core.Npc;
using XianXia.Core.Orders;
using XianXia.Core.Schedule;
using XianXia.Core.Simulation;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;
using System.IO;

namespace XianXia.Tests
{
    public sealed class NpcSimulationFoundationTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        static void RegisterSampleWorkAreas(SimulationWorld world)
        {
            world.WorldRegion.Register(new WorldLocationState
            {
                Id = "loc_field",
                Name = "药田",
                Kind = LocationKind.Wild
            });
            world.WorldRegion.Register(new WorldLocationState
            {
                Id = "loc_home",
                Name = "房屋",
                Kind = LocationKind.Village
            });
            world.WorldRegion.Register(new WorldLocationState
            {
                Id = "loc_hub",
                Name = "枢纽",
                Kind = LocationKind.Village
            });
            world.WorldRegion.Register(new WorldLocationState
            {
                Id = "loc_mine",
                Name = "矿洞",
                Kind = LocationKind.Wild
            });

            world.RegisterWorkArea(new WorkAreaDefinition
            {
                Id = "wa_field",
                LocationId = "loc_field",
                OffsetX = 1f,
                OffsetZ = -2f,
                AllowedActivities = { "Labor", "Patrol" }
            });
            world.RegisterWorkArea(new WorkAreaDefinition
            {
                Id = "wa_home",
                LocationId = "loc_home",
                AllowedActivities = { "Rest", "Eat" }
            });
            world.RegisterWorkArea(new WorkAreaDefinition
            {
                Id = "wa_hub",
                LocationId = "loc_hub",
                AllowedActivities = { "Patrol", "Inspect", "Explore" }
            });
            world.RegisterWorkArea(new WorkAreaDefinition
            {
                Id = "wa_mine",
                LocationId = "loc_mine",
                AllowedActivities = { "Labor" }
            });
        }

        static Entity CreateScheduledNpc(SimulationWorld world, string atLocation, string scheduleId)
        {
            var entity = world.Entities.CreateNpc(new DefinitionId("base", "npc_test"), "测试").Value;
            Assert.IsTrue(entity.AddComponent(new JobComponent()).IsSuccess);
            Assert.IsTrue(entity.AddComponent(new ScheduleComponent(scheduleId)).IsSuccess);
            Assert.IsTrue(entity.AddComponent(new EntityLocationComponent { LocationId = atLocation }).IsSuccess);
            Assert.IsTrue(entity.AddComponent(new ActivityTendencyComponent()).IsSuccess);
            var tendency = entity.Get<ActivityTendencyComponent>();
            tendency.SetCapability(ScheduleActivity.Labor, true);
            tendency.SetCapability(ScheduleActivity.Patrol, true);
            tendency.SetCapability(ScheduleActivity.Rest, true);
            tendency.SetPriority(ScheduleActivity.Labor, 8);
            tendency.SetPriority(ScheduleActivity.Patrol, 9);
            if (entity.TryGet<DailyTaskComponent>(out var daily))
                daily.RequiredAmount = 10;
            return entity;
        }

        [Test]
        public void ActivityResolver_NeedsMove_WhenAwayFromWorkArea()
        {
            var world = new SimulationWorld();
            RegisterSampleWorkAreas(world);
            var npc = CreateScheduledNpc(world, "loc_home", "sched");
            npc.Get<ActivityTendencyComponent>().PreferredWorkAreaIds.Add("wa_field");

            Assert.IsTrue(ActivityResolver.TryResolve(
                world, npc, ScheduleActivity.Labor, 6, out var resolved));
            Assert.IsTrue(resolved.NeedsMove);
            Assert.AreEqual("wa_field", resolved.WorkAreaId);
            Assert.AreEqual("loc_field", resolved.LocationId);
        }

        [Test]
        public void ActivityResolver_PrefersPreferredWorkArea_ThenFallsBack()
        {
            var world = new SimulationWorld();
            RegisterSampleWorkAreas(world);
            var npc = CreateScheduledNpc(world, "loc_mine", "sched");
            npc.Get<ActivityTendencyComponent>().PreferredWorkAreaIds.Add("wa_mine");

            Assert.IsTrue(ActivityResolver.TryResolve(
                world, npc, ScheduleActivity.Labor, 4, out var resolved));
            Assert.IsFalse(resolved.NeedsMove);
            Assert.AreEqual("wa_mine", resolved.WorkAreaId);
        }

        [Test]
        public void NpcActivityDriver_EmitsMoveThenWork()
        {
            var world = new SimulationWorld();
            RegisterSampleWorkAreas(world);
            world.RegisterSchedule(new ScheduleDefinition("sched")
                .AddBlock(0, 100, ScheduleActivity.Labor, 8));
            var loop = new SimulationLoop(world);
            var npc = CreateScheduledNpc(world, "loc_home", "sched");
            npc.Get<ActivityTendencyComponent>().PreferredWorkAreaIds.Add("wa_field");

            loop.TickOnce();
            Assert.IsInstanceOf<MoveAction>(FirstActive(world));
            var move = (MoveAction)FirstActive(world);
            Assert.AreEqual("wa_field", move.TargetWorkAreaId);

            Assert.IsTrue(npc.TryGet<MovementIntentComponent>(out var intent));
            intent.HostArrived = true;
            loop.TickOnce();

            Assert.AreEqual("loc_field", npc.Get<EntityLocationComponent>().LocationId);

            for (var i = 0; i < 3 && !(FirstActive(world) is WorkAction); i++)
                loop.TickOnce();

            Assert.IsInstanceOf<WorkAction>(FirstActive(world));
            var work = (WorkAction)FirstActive(world);
            Assert.AreEqual(ScheduleActivity.Labor, work.Activity);
            Assert.AreEqual("wa_field", work.TargetWorkAreaId);
        }

        [Test]
        public void PatrolRoute_AdvancesWorkAreaIndex()
        {
            var world = new SimulationWorld();
            RegisterSampleWorkAreas(world);
            world.RegisterSchedule(new ScheduleDefinition("sched")
                .AddBlock(0, 200, ScheduleActivity.Patrol, 2));
            var loop = new SimulationLoop(world);
            var npc = CreateScheduledNpc(world, "loc_hub", "sched");
            var tendency = npc.Get<ActivityTendencyComponent>();
            tendency.PreferredWorkAreaIds.Add("wa_hub");
            tendency.PreferredWorkAreaIds.Add("wa_field");

            loop.TickOnce();
            Assert.IsInstanceOf<WorkAction>(FirstActive(world));
            Assert.AreEqual("wa_hub", ((WorkAction)FirstActive(world)).TargetWorkAreaId);

            while (FirstActive(world) is WorkAction)
                loop.TickOnce();

            Assert.AreEqual(1, npc.Get<JobComponent>().RouteIndex);
        }

        [Test]
        public void WorkAreaOffset_IsDataNotCodeConstant()
        {
            var world = new SimulationWorld();
            RegisterSampleWorkAreas(world);
            Assert.IsTrue(world.TryGetWorkArea("wa_field", out var area));
            Assert.AreEqual(1f, area.OffsetX);
            Assert.AreEqual(-2f, area.OffsetZ);
        }

        [Test]
        public void BaseGame_LoadsWorkAreasAndPreferredPlacesWithoutProfessionJobs()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : "");
            Assert.IsTrue(loaded.Value.Registry.TryGetWorkArea(
                new DefinitionId("base", "workarea_herb_field"), out var herbWa));
            Assert.AreEqual("base:loc_ref_herb_field", herbWa.LocationId);
            Assert.IsTrue(loaded.Value.Registry.TryGetCharacter(
                new DefinitionId("base", "character_ch01_ref_woodcutter"), out var wood));
            Assert.Contains("base:workarea_forest_woodcut", wood.PreferredWorkAreaIds);
            Assert.AreEqual(0, loaded.Value.Registry.Jobs.Count);

            var started = new PlayableDayBootstrap().Start(
                BaseGamePath,
                new PlayableDayOptions { OpeningScenarioId = "base:scenario_ch01_reference" });
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");

            var world = started.Value.World;
            Assert.IsTrue(world.TryGetWorkArea("base:workarea_mine", out _));
            Assert.IsTrue(world.WorldRegion.TryGet("base:loc_ref_herb_field", out var herbLoc));
            Assert.Contains("herb", herbLoc.Tags);

            var withTendency = 0;
            foreach (var e in world.Entities.All)
            {
                if (e.TryGet<ActivityTendencyComponent>(out _))
                    withTendency++;
            }

            Assert.GreaterOrEqual(withTendency, 13);
            Assert.IsTrue(world.TryGetWorkArea("base:workarea_spring_cultivate", out _));
        }

        static IAction FirstActive(SimulationWorld world)
        {
            foreach (var kv in world.ActiveActions)
                return kv.Value;
            return null;
        }
    }
}
