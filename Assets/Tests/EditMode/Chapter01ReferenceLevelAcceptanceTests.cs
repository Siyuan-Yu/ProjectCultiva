using System.IO;
using NUnit.Framework;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Schedule;
using XianXia.Core.Social;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;

namespace XianXia.Tests
{
    /// <summary>
    /// Chapter 01 Reference Level：模板关卡内容／AI／区域门禁。
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
            Assert.IsTrue(world.ContentEvents.TryGet("base:event_ch01_ref_spring_whisper", out _));
            Assert.IsTrue(loaded.Value.Registry.TryGetCultivation(
                new DefinitionId("base", "cultivation_qingyun_manual"), out _));
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
    }
}
