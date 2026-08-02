using System.IO;
using NUnit.Framework;
using XianXia.Core.Content;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Input;
using XianXia.Core.Schedule;
using XianXia.Core.Settlement;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;

namespace XianXia.Tests
{
    /// <summary>
    /// Chapter 01 Reference Level：模板关卡内容／AI／区域门禁／觉醒弧闭环。
    /// </summary>
    public sealed class Chapter01ReferenceLevelAcceptanceTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        [Test]
        public void ReferenceLevel_LoadsMapRolesSchedulesAndChapter()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : "");
            Assert.IsTrue(new ContentReferenceValidator().Validate(loaded.Value.Registry).IsValid);

            var started = new PlayableDayBootstrap().Start(
                BaseGamePath,
                new PlayableDayOptions { OpeningScenarioId = "base:scenario_ch01_reference" });
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");

            var world = started.Value.World;
            Assert.AreEqual(8, world.WorldRegion.Locations.Count);
            Assert.IsTrue(world.WorldRegion.TryGet("base:loc_ref_labor_yard", out _));
            Assert.IsTrue(world.WorldRegion.TryGet("base:loc_ref_cave", out _));
            Assert.IsTrue(world.WorldRegion.TryGet("base:loc_ref_road_hub", out _));

            Assert.AreEqual("base:chapter_ch01_reference", world.Chapters.ActiveChapterId);
            Assert.IsTrue(world.Flags.Has("story:ch01_ref_started"));

            Assert.IsTrue(world.TryGetSchedule("base:schedule_mortal_day", out _));
            Assert.IsTrue(world.TryGetSchedule("base:schedule_cultivator_day", out _));
            Assert.IsTrue(world.TryGetSchedule("base:schedule_supervisor_day", out _));

            var roles = 0;
            var supervisor = 0;
            var mortal = 0;
            var cultivator = 0;
            foreach (var e in world.Entities.All)
            {
                if (!e.TryGet<NpcAiRoleComponent>(out var ai))
                    continue;
                roles++;
                if (ai.Role == NpcAiRoleKind.Supervisor)
                    supervisor++;
                if (ai.Role == NpcAiRoleKind.Mortal)
                    mortal++;
                if (ai.Role == NpcAiRoleKind.Cultivator)
                    cultivator++;
            }

            Assert.GreaterOrEqual(roles, 6);
            Assert.AreEqual(1, supervisor);
            Assert.GreaterOrEqual(mortal, 2);
            Assert.GreaterOrEqual(cultivator, 2);

            Assert.AreEqual(3, started.Value.CharacterIds.Count);
            Assert.IsFalse(started.Value.RecruitableNpcId.IsNone);

            Assert.IsTrue(world.Quests.TryGetSpec("base:quest_ch01_ref_inspect_yard", out _));
            Assert.IsTrue(world.Quests.TryGetSpec("base:quest_ch01_ref_dispatch_party", out _));
            Assert.IsTrue(world.Quests.TryGetSpec("base:quest_ch01_ref_gather_wood", out _));
            Assert.IsTrue(world.Quests.TryGetSpec("base:quest_ch01_ref_gather_herb", out _));
            Assert.IsTrue(world.Quests.TryGetSpec("base:quest_ch01_ref_spirit_sense", out _));
            Assert.IsTrue(world.Quests.TryGetSpec("base:quest_ch01_ref_breakthrough", out _));
            Assert.IsTrue(world.Quests.TryGetSpec("base:quest_ch01_ref_hide", out _));
            Assert.IsTrue(world.Quests.TryGetSpec("base:quest_ch01_ref_epilogue", out _));
            Assert.IsTrue(world.ContentEvents.TryGet("base:event_ch01_ref_miner_grumble", out _));
            Assert.IsTrue(loaded.Value.Registry.TryGetCharacter(
                new DefinitionId("base", "character_ch01_ref_miner"), out _));
            Assert.IsTrue(world.Quests.TryGetSpec("base:quest_ch01_ref_night_cultivate", out _));
            Assert.IsTrue(world.ContentEvents.TryGet("base:event_ch01_ref_opening", out _));
            Assert.IsTrue(world.ContentEvents.TryGet("base:event_ch01_ref_spring_whisper", out _));
            Assert.IsTrue(world.ContentEvents.TryGet("base:event_ch01_ref_woodcutter", out _));
            Assert.IsTrue(world.ContentEvents.TryGet("base:event_ch01_ref_merchant_tip", out _));
            Assert.IsTrue(world.ContentEvents.TryGet("base:event_ch01_ref_breakthrough_rite", out _));
            Assert.IsTrue(loaded.Value.Registry.TryGetCultivation(
                new DefinitionId("base", "cultivation_qingyun_manual"), out _));
            Assert.IsTrue(loaded.Value.Registry.TryGetCharacter(
                new DefinitionId("base", "character_ch01_ref_woodcutter"), out _));
        }

        [Test]
        public void ReferenceLevel_FullAwakeningArc_ToEpilogue()
        {
            var started = new PlayableDayBootstrap().Start(
                BaseGamePath,
                new PlayableDayOptions { OpeningScenarioId = "base:scenario_ch01_reference" });
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");

            var world = started.Value.World;
            var port = started.Value.Port;
            var loop = started.Value.Loop;
            var subject = started.Value.CharacterIds[0];
            Assert.IsTrue(world.Entities.TryGet(subject, out var protagonist));

            ResolveIfActive(port, subject, world, "accept_yoke");

            // 开局在农田：勘察同时收粮，完成巡视
            Assert.IsTrue(Explore(port, subject));
            AssertQuest(world, "base:quest_ch01_ref_inspect_yard", QuestStatus.Completed);

            // 三人分派：粮（已有）＋树林木＋药田药
            Assert.IsTrue(Travel(port, subject, "base:loc_ref_road_hub"));
            ResolveIfActive(port, subject, world, null);
            Assert.IsTrue(Travel(port, subject, "base:loc_ref_forest"));
            Assert.IsTrue(Explore(port, subject));
            Assert.IsTrue(Travel(port, subject, "base:loc_ref_road_hub"));
            Assert.IsTrue(Travel(port, subject, "base:loc_ref_herb_field"));
            Assert.IsTrue(Explore(port, subject));
            AssertQuest(world, "base:quest_ch01_ref_dispatch_party", QuestStatus.Completed);
            AssertQuest(world, "base:quest_ch01_ref_gather_wood", QuestStatus.Completed);
            AssertQuest(world, "base:quest_ch01_ref_gather_herb", QuestStatus.Completed);

            Assert.IsTrue(Travel(port, subject, "base:loc_ref_spring"));
            Assert.IsTrue(Explore(port, subject));
            Assert.IsTrue(world.ContentEvents.HasActive);
            Assert.AreEqual("base:event_ch01_ref_spring_whisper", world.ContentEvents.ActiveEventId);
            Assert.IsTrue(Resolve(port, subject, "listen"));
            AssertQuest(world, "base:quest_ch01_ref_spirit_sense", QuestStatus.Completed);

            Assert.IsTrue(Travel(port, subject, "base:loc_ref_road_hub"));
            Assert.IsTrue(Travel(port, subject, "base:loc_ref_forest"));
            Assert.IsTrue(world.ContentEvents.HasActive);
            Assert.AreEqual("base:event_ch01_ref_woodcutter", world.ContentEvents.ActiveEventId);
            Assert.IsTrue(Resolve(port, subject, "help_listen"));
            AssertQuest(world, "base:quest_ch01_ref_meet_elder", QuestStatus.Completed);

            Assert.IsTrue(Travel(port, subject, "base:loc_ref_road_hub"));
            Assert.IsTrue(Travel(port, subject, "base:loc_ref_cave"));
            Assert.IsTrue(Explore(port, subject));
            AssertQuest(world, "base:quest_ch01_ref_visit_cave", QuestStatus.Completed);

            Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                subject, PlayerCommandKind.Cultivate, 4)).IsSuccess);
            Assert.IsTrue(protagonist.Get<CultivationComponent>().HasLearnedManual);

            // Cultivate 学诀后需再 Explore／Travel 才会 Evaluate 任务链
            Assert.IsTrue(Explore(port, subject));
            ResolveIfActive(port, subject, world, "begin_dark");
            AssertQuest(world, "base:quest_ch01_ref_first_manual", QuestStatus.Completed);
            AssertQuest(world, "base:quest_ch01_ref_night_cultivate", QuestStatus.Completed);

            Assert.IsTrue(Travel(port, subject, "base:loc_ref_spring"));
            ResolveIfActive(port, subject, world, null);
            Assert.IsTrue(Travel(port, subject, "base:loc_ref_cave"));
            Assert.IsTrue(world.ContentEvents.HasActive);
            Assert.AreEqual("base:event_ch01_ref_breakthrough_rite", world.ContentEvents.ActiveEventId);
            Assert.IsTrue(Resolve(port, subject, "attempt"));

            for (var i = 0; i < 12 && protagonist.Get<CultivationComponent>().Realm == RealmStage.Mortal; i++)
                Assert.IsTrue(loop.TickOnce().IsSuccess);

            if (protagonist.Get<CultivationComponent>().Realm == RealmStage.Mortal)
            {
                Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                    subject, PlayerCommandKind.Cultivate, 4)).IsSuccess);
                for (var i = 0; i < 12; i++)
                    Assert.IsTrue(loop.TickOnce().IsSuccess);
            }

            Assert.AreEqual(RealmStage.QiRefining, protagonist.Get<CultivationComponent>().Realm);
            Assert.IsTrue(Explore(port, subject));
            ResolveIfActive(port, subject, world, null);
            AssertQuest(world, "base:quest_ch01_ref_breakthrough", QuestStatus.Completed);

            Assert.IsTrue(Travel(port, subject, "base:loc_ref_road_hub"));
            Assert.IsTrue(Travel(port, subject, "base:loc_ref_houses"));
            Assert.IsTrue(world.ContentEvents.HasActive);
            Assert.AreEqual("base:event_ch01_ref_hide_choice", world.ContentEvents.ActiveEventId);
            Assert.IsTrue(Resolve(port, subject, "swear_hide"));
            AssertQuest(world, "base:quest_ch01_ref_hide", QuestStatus.Completed);

            Assert.IsTrue(Travel(port, subject, "base:loc_ref_road_hub"));
            Assert.IsTrue(world.ContentEvents.HasActive);
            Assert.AreEqual("base:event_ch01_ref_epilogue_hub", world.ContentEvents.ActiveEventId);
            Assert.IsTrue(Resolve(port, subject, "remember"));
            AssertQuest(world, "base:quest_ch01_ref_epilogue", QuestStatus.Completed);
            Assert.IsTrue(world.Flags.Has("story:ch01_ref_arc_complete"));
        }

        [Test]
        public void ReferenceLevel_ScheduleActivityMapsToOrders()
        {
            Assert.AreEqual(
                XianXia.Core.Orders.OrderType.Rest,
                ScheduleActivityMapping.ToOrderType(ScheduleActivity.Eat));
            Assert.AreEqual(
                XianXia.Core.Orders.OrderType.Cultivate,
                ScheduleActivityMapping.ToOrderType(ScheduleActivity.Cultivate));
            Assert.AreEqual(
                XianXia.Core.Orders.OrderType.Observe,
                ScheduleActivityMapping.ToOrderType(ScheduleActivity.Patrol));
        }

        static void AssertQuest(SimulationWorld world, string questId, QuestStatus status)
        {
            Assert.IsTrue(world.Quests.TryGet(questId, out var rt), questId);
            Assert.AreEqual(status, rt.Status, questId);
        }

        static bool Travel(IPlayerInputPort port, EntityId subject, string locationId) =>
            port.Submit(new PlayerCommandRequest(
                subject, PlayerCommandKind.Travel, 1, EntityId.None, WorkRoleKind.None, locationId)).IsSuccess;

        static bool Explore(IPlayerInputPort port, EntityId subject) =>
            port.Submit(new PlayerCommandRequest(subject, PlayerCommandKind.Explore, 1)).IsSuccess;

        static bool Resolve(IPlayerInputPort port, EntityId subject, string choiceId) =>
            port.Submit(new PlayerCommandRequest(
                subject, PlayerCommandKind.ResolveContentChoice, 1, EntityId.None, WorkRoleKind.None,
                null, choiceId, null)).IsSuccess;

        static void ResolveIfActive(
            IPlayerInputPort port,
            EntityId subject,
            SimulationWorld world,
            string preferredChoiceId)
        {
            if (!world.ContentEvents.HasActive)
                return;
            if (!world.ContentEvents.TryGet(world.ContentEvents.ActiveEventId, out var spec) ||
                spec.Choices.Count == 0)
                return;
            var choice = preferredChoiceId;
            if (string.IsNullOrEmpty(choice))
                choice = spec.Choices[0].Id;
            Assert.IsTrue(Resolve(port, subject, choice), world.ContentEvents.ActiveEventId);
        }
    }
}
