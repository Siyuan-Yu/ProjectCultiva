using System.IO;
using NUnit.Framework;
using XianXia.Core.Content;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Input;
using XianXia.Core.Persistence;
using XianXia.Core.Settlement;
using XianXia.Core.Social;
using XianXia.Data.Bootstrap;

namespace XianXia.Tests
{
    /// <summary>
    /// Content Ready: systems can carry Chapter-1 authoring (quests／events／locations／growth loop).
    /// </summary>
    public sealed class ContentReadyMilestoneAcceptanceTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        [Test]
        public void ContentReady_CoreLoop_SelectAssignExploreEventCultivate()
        {
            var started = new PlayableDayBootstrap().Start(BaseGamePath);
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");

            var world = started.Value.World;
            var port = started.Value.Port;
            var loop = started.Value.Loop;
            var ids = started.Value.CharacterIds;
            var subject = ids[0];

            Assert.IsTrue(world.Entities.TryGet(subject, out var protagonist));
            Assert.IsTrue(protagonist.TryGet<PersonalityProfileComponent>(out var profile));
            Assert.IsTrue(profile.HasTag(TalentGrowthRules.TagMixedRoot));

            // 选角色后已有分工
            Assert.IsTrue(protagonist.TryGet<WorkAssignmentComponent>(out var work));
            Assert.IsTrue(work.IsAssigned);

            // 内容骨架已加载
            Assert.IsTrue(world.Quests.TryGetSpec("base:quest_scout_herb_slope", out _));
            Assert.IsTrue(world.ContentEvents.TryGet("base:event_herb_whisper", out _));

            // 未探村口时不可进入采药坡
            Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                subject, PlayerCommandKind.Travel, 1, EntityId.None, WorkRoleKind.None,
                "base:loc_village_edge")).IsSuccess);
            var blocked = port.Submit(new PlayerCommandRequest(
                subject, PlayerCommandKind.Travel, 1, EntityId.None, WorkRoleKind.None,
                "base:loc_herb_slope"));
            Assert.IsTrue(blocked.IsFailure);

            // 探索村口 → 进入条件满足 → 探索采药坡 → 任务完成 → 内容事件弹出
            Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                subject, PlayerCommandKind.Explore, 1)).IsSuccess);
            Assert.IsTrue(world.Flags.Has("explored:base:loc_village_edge"));

            Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                subject, PlayerCommandKind.Travel, 1, EntityId.None, WorkRoleKind.None,
                "base:loc_herb_slope")).IsSuccess);
            Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                subject, PlayerCommandKind.Explore, 1)).IsSuccess);

            Assert.IsTrue(world.Quests.TryGet("base:quest_scout_herb_slope", out var scout));
            Assert.AreEqual(QuestStatus.ReadyToClaim, scout.Status);
            Assert.IsTrue(world.ContentEvents.HasActive);
            Assert.AreEqual("base:event_herb_whisper", world.ContentEvents.ActiveEventId);
            Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                subject, PlayerCommandKind.ClaimQuestRewards, 1, EntityId.None, WorkRoleKind.None,
                null, null, "base:quest_scout_herb_slope")).IsSuccess);
            Assert.AreEqual(QuestStatus.Completed, scout.Status);

            // 选项结算 → 接取并完成后续任务
            Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                subject, PlayerCommandKind.ResolveContentChoice, 1, EntityId.None, WorkRoleKind.None,
                null, "gather", null)).IsSuccess);
            Assert.IsFalse(world.ContentEvents.HasActive);
            Assert.IsTrue(world.Flags.Has("event:herb_whisper_resolved"));
            Assert.IsTrue(world.Quests.TryGet("base:quest_listen_herb_whisper", out var listen));
            Assert.AreEqual(QuestStatus.ReadyToClaim, listen.Status);
            Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                subject, PlayerCommandKind.ClaimQuestRewards, 1, EntityId.None, WorkRoleKind.None,
                null, null, "base:quest_listen_herb_whisper")).IsSuccess);
            Assert.AreEqual(QuestStatus.Completed, listen.Status);

            // 发现修炼地点后进入成长（天赋杂灵根：突破 MaxHp＋修炼 Progress）
            Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                subject, PlayerCommandKind.Travel, 1, EntityId.None, WorkRoleKind.None,
                "base:loc_cave_mouth")).IsSuccess);
            Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                subject, PlayerCommandKind.Explore, 1)).IsSuccess);

            var cult = protagonist.Get<CultivationComponent>();
            var hpBefore = protagonist.Get<AttributesComponent>().GetBase(XianXia.Core.Attributes.AttributeId.MaxHp);
            Assert.IsTrue(world.TryGetManual(
                new DefinitionId("base", "cultivation_qingyun_manual"), out var manual));
            Assert.IsTrue(new CultivationService().LearnManual(world, subject, manual).IsSuccess);
            Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                subject, PlayerCommandKind.Cultivate, 4)).IsSuccess);
            Assert.IsTrue(cult.HasLearnedManual);

            for (var i = 0; i < 8 && cult.Realm == RealmStage.Mortal; i++)
                Assert.IsTrue(loop.TickOnce().IsSuccess);

            if (cult.Realm == RealmStage.Mortal)
            {
                Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                    subject, PlayerCommandKind.Cultivate, 4)).IsSuccess);
                for (var i = 0; i < 8; i++)
                    Assert.IsTrue(loop.TickOnce().IsSuccess);
            }

            Assert.IsTrue(cult.IsAtBottleneck || cult.Progress > 0);
            PushBreakthroughsToQiRefining(world, protagonist);

            Assert.AreEqual(RealmStage.QiRefining, cult.Realm);
            Assert.Greater(
                protagonist.Get<AttributesComponent>().GetBase(XianXia.Core.Attributes.AttributeId.MaxHp),
                hpBefore);

            Assert.AreEqual(5, WorldSnapshot.CurrentSchemaVersion);
        }

        static void PushBreakthroughsToQiRefining(
            XianXia.Core.Simulation.SimulationWorld world,
            XianXia.Core.Entities.Entity entity)
        {
            var svc = new CultivationService();
            var cult = entity.Get<CultivationComponent>();
            for (var i = 0; i < 8 && cult.Realm == RealmStage.Mortal; i++)
            {
                svc.SyncProgressRequired(world, cult);
                if (cult.BreakthroughProgressRequired <= 0)
                    cult.BreakthroughProgressRequired = 100;
                cult.Progress = cult.BreakthroughProgressRequired;
                var r = svc.TryBreakthrough(world, entity.Id);
                Assert.IsTrue(r.IsSuccess, r.IsFailure ? r.Error.ToString() : "");
            }
        }
    }
}
