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
using XianXia.Core.Social;
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
        public void ActivityResolver_SkipsFullWorkArea_FallsBackToIdle()
        {
            var world = new SimulationWorld();
            RegisterSampleWorkAreas(world);
            world.TryGetWorkArea("wa_mine", out var mine);
            mine.Capacity = 1;
            world.TryGetWorkArea("wa_field", out var field);
            field.Capacity = 1;

            var miner = CreateScheduledNpc(world, "loc_mine", "sched");
            Assert.IsTrue(world.WorkAreaOccupancy.TryReserve("wa_mine", miner.Id, 1, out _));
            var farmer = CreateScheduledNpc(world, "loc_field", "sched");
            Assert.IsTrue(world.WorkAreaOccupancy.TryReserve("wa_field", farmer.Id, 1, out _));

            var third = CreateScheduledNpc(world, "loc_mine", "sched");
            Assert.IsFalse(ActivityResolver.TryResolve(
                world, third, ScheduleActivity.Labor, 4, out _));
            Assert.IsTrue(ActivityResolver.TryResolve(
                world, third, ScheduleActivity.Idle, 4, out var idle));
            Assert.AreEqual(ScheduleActivity.Idle, idle.Activity);
            Assert.IsTrue(string.IsNullOrEmpty(idle.WorkAreaId));
        }

        [Test]
        public void ActivityResolver_AssignsDistinctSlots_SameWorkArea()
        {
            var world = new SimulationWorld();
            RegisterSampleWorkAreas(world);
            world.TryGetWorkArea("wa_field", out var field);
            field.Capacity = 3;

            var a = CreateScheduledNpc(world, "loc_field", "sched");
            a.Get<ActivityTendencyComponent>().PreferredWorkAreaIds.Add("wa_field");
            var b = CreateScheduledNpc(world, "loc_field", "sched");
            b.Get<ActivityTendencyComponent>().PreferredWorkAreaIds.Add("wa_field");

            Assert.IsTrue(ActivityResolver.TryResolve(world, a, ScheduleActivity.Labor, 4, out var ra));
            Assert.IsTrue(world.WorkAreaOccupancy.TryReserve("wa_field", a.Id, 3, out var sa));
            Assert.IsTrue(ActivityResolver.TryResolve(world, b, ScheduleActivity.Labor, 4, out var rb));
            Assert.IsTrue(world.WorkAreaOccupancy.TryReserve("wa_field", b.Id, 3, out var sb));
            Assert.AreEqual("wa_field", ra.WorkAreaId);
            Assert.AreEqual("wa_field", rb.WorkAreaId);
            Assert.AreNotEqual(sa, sb);
        }

        [Test]
        public void HousingAssignment_AssignRequiresManageAndPlayerCamp()
        {
            var world = new SimulationWorld();
            world.RegisterWorkArea(new WorkAreaDefinition
            {
                Id = "wa_home",
                LocationId = "loc_home",
                AllowedActivities = { "Rest", "Eat" },
                ResidentTags = { "mortal" },
                Tags = { "home" }
            });
            world.WorldRegion.Register(new WorldLocationState
            {
                Id = "loc_home",
                Name = "房屋",
                Kind = LocationKind.Village
            });

            var party = world.Entities.CreateCharacter(new DefinitionId("base", "pc"), "玩家").Value;
            party.Get<FactionMembershipComponent>().Assign("base:sect_player", FactionRoleKind.Member);
            party.AddComponent(new ActivityTendencyComponent());

            var outsider = world.Entities.CreateNpc(new DefinitionId("base", "npc_x"), "外人").Value;
            outsider.Get<FactionMembershipComponent>().Assign("base:sect_other", FactionRoleKind.Member);
            outsider.AddComponent(new ActivityTendencyComponent());

            var partyIds = new List<EntityId> { party.Id };
            Assert.IsFalse(HousingAssignmentService.CanManageHousing(world));
            Assert.IsTrue(HousingAssignmentService.TryAssignOwner(
                world, "wa_home", party.Id, partyIds).IsFailure);

            world.Flags.Set("settlement_player_controlled");
            Assert.IsTrue(HousingAssignmentService.CanManageHousing(world));
            Assert.IsTrue(HousingAssignmentService.TryAssignOwner(
                world, "wa_home", outsider.Id, partyIds).IsFailure);
            Assert.IsTrue(HousingAssignmentService.TryAssignOwner(
                world, "wa_home", party.Id, partyIds).IsSuccess);
            Assert.AreEqual("wa_home", party.Get<ActivityTendencyComponent>().HomeWorkAreaId);
            Assert.IsTrue(world.HousingAssignments.TryGetOwner("wa_home", out var owner));
            Assert.AreEqual(party.Id, owner);
        }

        [Test]
        public void ActivityResolver_HomeWorkArea_PreferredForRest()
        {
            var world = new SimulationWorld();
            RegisterSampleWorkAreas(world);
            world.RegisterWorkArea(new WorkAreaDefinition
            {
                Id = "wa_guard_home",
                LocationId = "loc_home",
                Capacity = 2,
                AllowedActivities = { "Rest", "Eat" },
                ResidentTags = { "guard" }
            });
            world.TryGetWorkArea("wa_home", out var mortalHome);
            mortalHome.ResidentTags.Add("mortal");

            var guard = CreateScheduledNpc(world, "loc_field", "sched");
            guard.Get<PersonalityProfileComponent>().SetTags(new[] { "guard", "npc" });
            guard.Get<ActivityTendencyComponent>().HomeWorkAreaId = "wa_guard_home";
            guard.Get<ActivityTendencyComponent>().SetCapability(ScheduleActivity.Rest, true);

            Assert.IsTrue(ActivityResolver.TryResolve(
                world, guard, ScheduleActivity.Rest, 8, out var resolved));
            Assert.AreEqual("wa_guard_home", resolved.WorkAreaId);
        }

        [Test]
        public void ActivityResolver_SupervisorRestsAtQuarters_NotMansion()
        {
            var world = new SimulationWorld();
            RegisterSampleWorkAreas(world);
            world.RegisterWorkArea(new WorkAreaDefinition
            {
                Id = "wa_sup_home",
                LocationId = "loc_home",
                Capacity = 2,
                AllowedActivities = { "Rest", "Eat" },
                ResidentTags = { "supervisor" }
            });
            world.RegisterWorkArea(new WorkAreaDefinition
            {
                Id = "wa_mansion",
                LocationId = "loc_hub",
                IsControlCore = true,
                MaxDurability = 100,
                AllowedActivities = { "Inspect", "Patrol" }
            });
            world.TryGetWorkArea("wa_home", out var mortalHome);
            mortalHome.ResidentTags.Add("mortal");

            var boss = CreateScheduledNpc(world, "loc_field", "sched");
            boss.Get<PersonalityProfileComponent>().SetTags(new[] { "supervisor", "npc" });
            boss.Get<ActivityTendencyComponent>().HomeWorkAreaId = "wa_sup_home";
            boss.Get<ActivityTendencyComponent>().SetCapability(ScheduleActivity.Rest, true);

            Assert.IsTrue(ActivityResolver.TryResolve(
                world, boss, ScheduleActivity.Rest, 8, out var resolved));
            Assert.AreEqual("wa_sup_home", resolved.WorkAreaId);
            Assert.AreNotEqual("wa_mansion", resolved.WorkAreaId);
        }

        [Test]
        public void ControlCore_DamageThenCapture()
        {
            var world = new SimulationWorld();
            world.RegisterWorkArea(new WorkAreaDefinition
            {
                Id = "wa_mansion",
                Name = "主管府",
                LocationId = "loc_hub",
                IsControlCore = true,
                MaxDurability = 50,
                Defense = 5,
                AllowedActivities = { "Inspect", "Patrol" }
            });
            Assert.IsTrue(world.ControlCores.TryGet("wa_mansion", out var core));
            Assert.AreEqual(50, core.CurrentDurability);
            Assert.AreEqual(5, core.Defense);

            // 25 raw → after defense 20
            Assert.IsTrue(ControlCoreService.ApplyStrike(world, "wa_mansion", 25).IsSuccess);
            Assert.AreEqual(30, core.CurrentDurability);
            Assert.IsFalse(core.CaptureAvailable);

            Assert.IsTrue(ControlCoreService.ApplyStrike(world, "wa_mansion", 40).IsSuccess);
            Assert.AreEqual(0, core.CurrentDurability);
            Assert.IsTrue(core.CaptureAvailable);

            Assert.IsFalse(ControlCoreService.TryCapture(world, "wa_mansion").IsSuccess);

            world.ControlCores.AddOccupyProgress("wa_mansion", core.OccupyHoldSeconds, out _);
            Assert.IsTrue(ControlCoreService.TryCapture(world, "wa_mansion").IsSuccess);
            Assert.IsTrue(core.PlayerControlled);
            Assert.IsTrue(world.Flags.Has("settlement_player_controlled"));
            Assert.IsTrue(world.SettlementAuthority.CanManageHousing);
            Assert.IsTrue(world.SettlementAuthority.CanManageSchedules);
        }

        [Test]
        public void ControlCore_TickOccupy_AutoCapturesAndGrantsPrivileges()
        {
            var world = new SimulationWorld();
            world.RegisterWorkArea(new WorkAreaDefinition
            {
                Id = "wa_mansion",
                Name = "主管府",
                LocationId = "loc_hub",
                IsControlCore = true,
                MaxDurability = 100,
                OccupyHoldSeconds = 10f,
                AllowedActivities = { "Inspect" }
            });
            world.TryGetWorkArea("wa_mansion", out var area);
            area.GrantsPrivileges.Add("manageHousing");
            area.GrantsPrivileges.Add("manageSchedules");
            world.ControlCores.RegisterOrRefresh(area);

            Assert.IsTrue(ControlCoreService.ApplyStrike(world, "wa_mansion", 100).IsSuccess);
            Assert.IsTrue(world.ControlCores.TryGet("wa_mansion", out var core));
            Assert.IsTrue(core.CaptureAvailable);

            ControlCoreService.TickOccupy(world, "wa_mansion", 9.5f, true);
            Assert.IsFalse(core.PlayerControlled);
            ControlCoreService.TickOccupy(world, "wa_mansion", 0.6f, true);
            Assert.IsTrue(core.PlayerControlled);
            Assert.IsTrue(HousingAssignmentService.CanManageHousing(world));
            Assert.IsTrue(HousingAssignmentService.CanManageSchedules(world));
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
