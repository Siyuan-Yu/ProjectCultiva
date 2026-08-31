using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    /// <summary>
    /// Phase 5S：Prototype Bandit 已迁移 Content JSON（FormalArmyContentBootstrap）。
    /// 旧 EditMode 测试夹具依赖的 EnsureBandit* 代码生成已退役；
    /// 本类提供等价 authored-army 测试 helper：真实 NPC → CreateAuthoredArmy → EnsureLinkedStackView。
    /// 仅测试用；Production 一律走 Content。
    /// </summary>
    public static class TestArmyFixtures
    {
        public static Result<FormalArmy> EnsureBanditPatrolArmy(SimulationWorld world, string siteId) =>
            EnsureArmy(world, siteId, ArmyStackAdapter.BanditPatrolFormalArmyId,
                ArmyStackAdapter.BanditPatrolStackId, "荒村山匪", 4);

        public static Result<FormalArmy> EnsureBanditWeakPatrolArmy(SimulationWorld world, string siteId) =>
            EnsureArmy(world, siteId, ArmyStackAdapter.BanditWeakPatrolFormalArmyId,
                ArmyStackAdapter.BanditWeakPatrolStackId, "试炼弱匪（自动必胜）", 1);

        public static Result<FormalArmy> EnsureBanditScoutArmy(SimulationWorld world, string siteId) =>
            EnsureArmy(world, siteId, ArmyStackAdapter.BanditScoutFormalArmyId,
                ArmyStackAdapter.BanditScoutStackId, "山匪斥候", 4);

        public static Result<FormalArmy> EnsureBanditCasualtyTestArmy(SimulationWorld world, string siteId) =>
            EnsureArmy(world, siteId, ArmyStackAdapter.BanditCasualtyTestFormalArmyId,
                ArmyStackAdapter.BanditCasualtyTestStackId, "试炼强匪（自动伤亡）", 3);

        static Result<FormalArmy> EnsureArmy(
            SimulationWorld world,
            string siteId,
            string armyId,
            string stackId,
            string displayName,
            int memberCount)
        {
            if (world?.Strategic == null)
                return Result.Fail<FormalArmy>(ErrorCode.InvalidArgument, "world null");
            if (world.Strategic.FormalArmies.TryGet(armyId, out var existing) && existing != null)
                return Result.Ok(existing);
            EnsureSiteRegistered(world, siteId);

            var members = new List<EntityId>(memberCount);
            for (var i = 0; i < memberCount; i++)
            {
                var created = world.Entities.CreateNpc(
                    new DefinitionId("test", armyId + "_" + i),
                    displayName + i);
                if (created.IsFailure)
                    return Result.Fail<FormalArmy>(created.Error);
                created.Value.Get<FactionMembershipComponent>()
                    .Assign(StrategicFactionCatalog.BanditId, FactionRoleKind.Member);
                world.WorldPresence.SetAtSite(created.Value.Id, siteId);
                members.Add(created.Value.Id);
            }

            var made = ArmyService.CreateAuthoredArmy(
                world,
                armyId,
                StrategicFactionCatalog.BanditId,
                siteId,
                members,
                members[0]);
            if (made.IsFailure)
                return made;
            ArmyStackAdapter.EnsureLinkedStackView(world, made.Value, stackId, displayName);
            return made;
        }

        static void EnsureSiteRegistered(SimulationWorld world, string siteId)
        {
            if (string.IsNullOrEmpty(siteId) || world.Strategic.Sites.TryGet(siteId, out _))
                return;
            var index = 2 + (world.Strategic.Sites.Sites.Count % 8);
            world.Strategic.Sites.Register(new WorldSite
            {
                SiteId = siteId,
                DisplayName = siteId,
                SiteType = "Test",
                AnchorHex = new HexCoord(index, index),
            });
        }
    }
}
