using System.IO;
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

namespace XianXia.Tests
{
    public sealed class NpcSimulationFoundationTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        static void RegisterSampleJobWorld(SimulationWorld world)
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
                OffsetZ = -2f
            });
            world.RegisterWorkArea(new WorkAreaDefinition
            {
                Id = "wa_home",
                LocationId = "loc_home"
            });
            world.RegisterWorkArea(new WorkAreaDefinition
            {
                Id = "wa_hub",
                LocationId = "loc_hub"
            });
            world.RegisterWorkArea(new WorkAreaDefinition
            {
                Id = "wa_mine",
                LocationId = "loc_mine"
            });

            var herb = new JobDefinition { Id = "job_herb", Name = "药农", PrimaryWorkAreaId = "wa_field" };
            herb.ActivityBindings.Add(new JobActivityBinding
            {
                Activity = ScheduleActivity.Labor,
                WorkAreaIds = { "wa_field" }
            });
            herb.ActivityBindings.Add(new JobActivityBinding
            {
                Activity = ScheduleActivity.Rest,
                WorkAreaIds = { "wa_home" }
            });
            world.RegisterJob(herb);

            var guard = new JobDefinition { Id = "job_guard", Name = "巡卫", PrimaryWorkAreaId = "wa_hub" };
            var patrol = new JobActivityBinding { Activity = ScheduleActivity.Patrol, Route = true };
            patrol.WorkAreaIds.Add("wa_hub");
            patrol.WorkAreaIds.Add("wa_field");
            guard.ActivityBindings.Add(patrol);
            world.RegisterJob(guard);

            var miner = new JobDefinition { Id = "job_miner", Name = "矿工", PrimaryWorkAreaId = "wa_mine" };
            miner.ActivityBindings.Add(new JobActivityBinding
            {
                Activity = ScheduleActivity.Labor,
                WorkAreaIds = { "wa_mine" }
            });
            world.RegisterJob(miner);
        }

        static Entity CreateJobNpc(SimulationWorld world, string jobId, string atLocation, string scheduleId)
        {
            var entity = world.Entities.CreateNpc(new DefinitionId("base", "npc_test"), "测试").Value;
            Assert.IsTrue(entity.AddComponent(new JobComponent()).IsSuccess);
            entity.Get<JobComponent>().Assign(jobId);
            Assert.IsTrue(entity.AddComponent(new ScheduleComponent(scheduleId)).IsSuccess);
            Assert.IsTrue(entity.AddComponent(new EntityLocationComponent { LocationId = atLocation }).IsSuccess);
            if (entity.TryGet<DailyTaskComponent>(out var daily))
                daily.RequiredAmount = 10;
            return entity;
        }

        [Test]
        public void ActivityResolver_NeedsMove_WhenAwayFromWorkArea()
        {
            var world = new SimulationWorld();
            RegisterSampleJobWorld(world);
            var npc = CreateJobNpc(world, "job_herb", "loc_home", "sched");

            Assert.IsTrue(ActivityResolver.TryResolve(
                world, npc, ScheduleActivity.Labor, 6, out var resolved));
            Assert.IsTrue(resolved.NeedsMove);
            Assert.AreEqual("wa_field", resolved.WorkAreaId);
            Assert.AreEqual("loc_field", resolved.LocationId);
        }

        [Test]
        public void NpcActivityDriver_EmitsMoveThenWork()
        {
            var world = new SimulationWorld();
            RegisterSampleJobWorld(world);
            world.RegisterSchedule(new ScheduleDefinition("sched")
                .AddBlock(0, 100, ScheduleActivity.Labor, 8));
            var loop = new SimulationLoop(world);
            var npc = CreateJobNpc(world, "job_herb", "loc_home", "sched");

            loop.TickOnce();
            Assert.IsInstanceOf<MoveAction>(FirstActive(world));
            var move = (MoveAction)FirstActive(world);
            Assert.AreEqual("wa_field", move.TargetWorkAreaId);

            // Host arrival ack
            Assert.IsTrue(npc.TryGet<MovementIntentComponent>(out var intent));
            intent.HostArrived = true;
            loop.TickOnce();

            Assert.AreEqual("loc_field", npc.Get<EntityLocationComponent>().LocationId);

            // After move completes, driver injects Work
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
            RegisterSampleJobWorld(world);
            world.RegisterSchedule(new ScheduleDefinition("sched")
                .AddBlock(0, 200, ScheduleActivity.Patrol, 2));
            var loop = new SimulationLoop(world);
            var npc = CreateJobNpc(world, "job_guard", "loc_hub", "sched");

            loop.TickOnce();
            Assert.IsInstanceOf<WorkAction>(FirstActive(world));
            Assert.AreEqual("wa_hub", ((WorkAction)FirstActive(world)).TargetWorkAreaId);

            while (FirstActive(world) is WorkAction)
                loop.TickOnce();

            Assert.AreEqual(1, npc.Get<JobComponent>().RouteIndex);

            for (var i = 0; i < 4 && FirstActive(world) == null; i++)
                loop.TickOnce();

            // Next target is wa_field → may Move first
            var active = FirstActive(world);
            Assert.IsNotNull(active);
            if (active is MoveAction move)
                Assert.AreEqual("wa_field", move.TargetWorkAreaId);
            else if (active is WorkAction work)
                Assert.AreEqual("wa_field", work.TargetWorkAreaId);
            else
                Assert.Fail("Expected Move or Work after route advance.");
        }

        [Test]
        public void MinerJob_ResolvesMineWorkArea()
        {
            var world = new SimulationWorld();
            RegisterSampleJobWorld(world);
            var npc = CreateJobNpc(world, "job_miner", "loc_mine", "sched");
            Assert.IsTrue(ActivityResolver.TryResolve(
                world, npc, ScheduleActivity.Labor, 4, out var resolved));
            Assert.IsFalse(resolved.NeedsMove);
            Assert.AreEqual("wa_mine", resolved.WorkAreaId);
        }

        [Test]
        public void WorkAreaOffset_IsDataNotCodeConstant()
        {
            var world = new SimulationWorld();
            RegisterSampleJobWorld(world);
            Assert.IsTrue(world.TryGetWorkArea("wa_field", out var area));
            Assert.AreEqual(1f, area.OffsetX);
            Assert.AreEqual(-2f, area.OffsetZ);
        }

        [Test]
        public void BaseGame_LoadsJobsWorkAreasAndBindsSampleNpcs()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : "");
            Assert.IsTrue(loaded.Value.Registry.TryGetJob(
                new DefinitionId("base", "job_herb_farmer"), out _));
            Assert.IsTrue(loaded.Value.Registry.TryGetJob(
                new DefinitionId("base", "job_miner"), out _));
            Assert.IsTrue(loaded.Value.Registry.TryGetJob(
                new DefinitionId("base", "job_patrol_guard"), out _));
            Assert.IsTrue(loaded.Value.Registry.TryGetJob(
                new DefinitionId("base", "job_supervisor"), out _));
            Assert.IsTrue(loaded.Value.Registry.TryGetWorkArea(
                new DefinitionId("base", "workarea_herb_field"), out var herbWa));
            Assert.AreEqual("base:loc_ref_herb_field", herbWa.LocationId);

            var started = new PlayableDayBootstrap().Start(
                BaseGamePath,
                new PlayableDayOptions { OpeningScenarioId = "base:scenario_ch01_reference" });
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");

            var world = started.Value.World;
            Assert.IsTrue(world.TryGetJob("base:job_herb_farmer", out _));
            Assert.IsTrue(world.TryGetWorkArea("base:workarea_mine", out _));
            Assert.IsTrue(world.WorldRegion.TryGet("base:loc_ref_herb_field", out var herbLoc));
            Assert.Contains("herb", herbLoc.Tags);

            var jobbed = 0;
            foreach (var e in world.Entities.All)
            {
                if (e.TryGet<JobComponent>(out var job) && job.HasJob)
                    jobbed++;
            }

            Assert.GreaterOrEqual(jobbed, 7);
        }

        static IAction FirstActive(SimulationWorld world)
        {
            foreach (var kv in world.ActiveActions)
                return kv.Value;
            return null;
        }
    }
}
