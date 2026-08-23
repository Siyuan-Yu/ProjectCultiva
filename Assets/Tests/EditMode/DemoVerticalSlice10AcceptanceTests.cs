using System.IO;
using NUnit.Framework;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Domain.Time;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Input;
using XianXia.Core.Opportunity;
using XianXia.Core.Persistence;
using XianXia.Core.Settlement;
using XianXia.Core.Social;
using XianXia.Data.Bootstrap;
using XianXia.Data.Serialization;

namespace XianXia.Tests
{
    /// <summary>
    /// VS1.0: automated vertical-slice path matching the Demo 0.1 player loop.
    /// </summary>
    public sealed class DemoVerticalSlice10AcceptanceTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        [Test]
        public void Demo01_FullGrowthLoop_Labor_Explore_Cultivate_Breakthrough_Settlement()
        {
            var started = new PlayableDayBootstrap().Start(BaseGamePath);
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");

            var world = started.Value.World;
            var port = started.Value.Port;
            var loop = started.Value.Loop;
            var ids = started.Value.CharacterIds;

            // 三人开局 + 可招 NPC + 初始据点 + 区域
            Assert.AreEqual(3, ids.Count);
            Assert.IsFalse(started.Value.RecruitableNpcId.IsNone);
            Assert.IsTrue(world.Settlements.TryGetPrimary(out var settlement));
            Assert.AreEqual("青石洞府", settlement.Name);
            Assert.AreEqual(4, world.WorldRegion.Locations.Count);

            // 人物关系
            Assert.GreaterOrEqual(
                world.Relationships.Score(ids[0], ids[1]),
                SocialAlphaConstants.OpeningCompanionFavor);

            // 初始势力隶属
            Assert.IsTrue(world.Entities.TryGet(ids[0], out var protagonist));
            Assert.IsTrue(protagonist.Get<FactionMembershipComponent>().IsAffiliated);

            // 凡人杂役分工已存在
            Assert.IsTrue(protagonist.TryGet<WorkAssignmentComponent>(out var work));
            Assert.IsTrue(work.IsAssigned);

            // 发现修仙机会：旅行至洞口并探索
            Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                ids[0], PlayerCommandKind.Travel, 1, EntityId.None, WorkRoleKind.None,
                "base:loc_cave_mouth")).IsSuccess);
            Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                ids[0], PlayerCommandKind.Explore, 1)).IsSuccess);
            Assert.IsTrue(protagonist.Get<KnownSitesComponent>().Knows(
                new DefinitionId("base", "site_abandoned_cave")));

            // 学功法须显式 Learn（不再随 Cultivate 从机缘点保底）
            Assert.IsTrue(world.TryGetManual(
                new DefinitionId("base", "cultivation_qingyun_manual"), out var manual));
            Assert.IsTrue(new CultivationService().LearnManual(world, ids[0], manual).IsSuccess);
            Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                ids[0], PlayerCommandKind.Cultivate, 4)).IsSuccess);
            Assert.IsTrue(protagonist.Get<CultivationComponent>().HasLearnedManual);

            // 推进至瓶颈后手动突破感应前→中→后→炼气
            for (var i = 0; i < 8 && protagonist.Get<CultivationComponent>().Realm == RealmStage.Mortal; i++)
                Assert.IsTrue(loop.TickOnce().IsSuccess);

            if (protagonist.Get<CultivationComponent>().Realm == RealmStage.Mortal)
            {
                Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                    ids[0], PlayerCommandKind.Cultivate, 4)).IsSuccess);
                for (var i = 0; i < 8; i++)
                    Assert.IsTrue(loop.TickOnce().IsSuccess);
            }

            PushBreakthroughsToQiRefining(world, protagonist);
            Assert.AreEqual(RealmStage.QiRefining, protagonist.Get<CultivationComponent>().Realm);

            // 日终生产循环（据点成长）
            var woodBefore = settlement.GetStock("base:resource_rough_wood");
            world.Tick = new WorldTick(((world.Tick.Value / (ulong)WorldTick.TicksPerDay) + 1) *
                                      (ulong)WorldTick.TicksPerDay - 1);
            world.Events.Drain();
            Assert.IsTrue(loop.TickOnce().IsSuccess);
            Assert.Greater(settlement.GetStock("base:resource_rough_wood"), woodBefore);

            // 关系互动仍可用
            Assert.IsTrue(port.Submit(new PlayerCommandRequest(
                ids[0], PlayerCommandKind.Help, 1, ids[1])).IsSuccess);

            Assert.AreEqual(3, WorldSnapshot.CurrentSchemaVersion);
            var snap = new SnapshotService(new JsonSnapshotSerializer()).CaptureJson(world, loop);
            Assert.IsTrue(snap.IsSuccess, snap.IsFailure ? snap.Error.ToString() : "");
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
