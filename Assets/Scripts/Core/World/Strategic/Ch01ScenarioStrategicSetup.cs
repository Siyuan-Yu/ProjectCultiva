using System;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Ch01 Opening Scenario 战略初始化（Final Closure）。
    /// Generic StrategicBootstrap 不拥有剧情外交决定权；本类负责 Ch01 兼容与 Scenario Hook 注册。
    /// </summary>
    public static class Ch01ScenarioStrategicSetup
    {
        public static void Apply(SimulationWorld world)
        {
            if (world?.Strategic == null || !world.WorldGraph.HasGraph)
                return;

            world.Strategic.Ch01FormationScenarioCompat = true;
            ApplyPlayerFactionAndVassalage(world);
            ApplyCh01TerritoryOwners(world);
            SeedPrototypeBanditArmies(world);
            ApplyPrototypeRegressionDiplomacy(world);
            Ch01ScenarioProgressionHooks.Register(world);
        }

        /// <summary>主角团（PlayerFaction）为压迫宗门附庸；战略 UI 只认 PlayerFaction 成员。</summary>
        static void ApplyPlayerFactionAndVassalage(SimulationWorld world)
        {
            world.Strategic.PlayerFactionId = StrategicFactionCatalog.PlayerFactionId;
            world.Strategic.Vassalages.TryBindVassalage(
                StrategicFactionCatalog.PlayerFactionId,
                StrategicFactionCatalog.HuangcunLaborId);
        }

        /// <summary>
        /// Ch01 Prototype 领土：压迫宗门 3 节点 + 五方区域势力各 2～3 节点；其余保持无归属。
        /// 山匪无领土（Landless），仅游荡军队。
        /// </summary>
        static void ApplyCh01TerritoryOwners(SimulationWorld world)
        {
            var overlord = StrategicFactionCatalog.HuangcunLaborId;
            SetNodeOwner(world, "base:node_huangcun", overlord);
            SetNodeOwner(world, "base:node_qingyun_lu", overlord);
            SetNodeOwner(world, "base:node_lingdi", overlord);

            SetNodeOwner(world, "base:node_cunzhuang_nan", StrategicFactionCatalog.NanYanLeagueId);
            SetNodeOwner(world, "base:node_zhuangyuan", StrategicFactionCatalog.NanYanLeagueId);

            SetNodeOwner(world, "base:node_haijiao", StrategicFactionCatalog.FisherVillageId);
            SetNodeOwner(world, "base:node_shuizhai", StrategicFactionCatalog.FisherVillageId);
            SetNodeOwner(world, "base:node_yucun", StrategicFactionCatalog.FisherVillageId);

            SetNodeOwner(world, "base:node_cunzhuang_bei", StrategicFactionCatalog.ShuoFengFortId);
            SetNodeOwner(world, "base:node_shankou", StrategicFactionCatalog.ShuoFengFortId);

            SetNodeOwner(world, "base:node_shulin_dong", StrategicFactionCatalog.DongLinGuildId);
            SetNodeOwner(world, "base:node_miao", StrategicFactionCatalog.DongLinGuildId);
            SetNodeOwner(world, "base:node_gudao", StrategicFactionCatalog.DongLinGuildId);

            SetNodeOwner(world, "base:node_dukou_xi", StrategicFactionCatalog.XiJinGuildId);
            SetNodeOwner(world, "base:node_yaotian", StrategicFactionCatalog.XiJinGuildId);
        }

        static void SetNodeOwner(SimulationWorld world, string nodeId, string ownerFactionId)
        {
            if (!world.WorldGraph.TryGetNode(nodeId, out var node) || node == null)
                return;
            node.OwnerId = ownerFactionId ?? string.Empty;
        }

        static void SeedPrototypeBanditArmies(SimulationWorld world)
        {
            world.Strategic.Armies.Clear();
            if (ArmyHexCommandService.IsHexStrategicActive(world))
            {
                ArmyStackAdapter.EnsureBanditPatrolArmy(
                    world,
                    "base:node_huangcun",
                    string.Empty,
                    "base:node_qingyun_lu",
                    -1f);
                PositionPrototypeBanditPatrolArmy(world);
                return;
            }

            if (!world.WorldGraph.TryFindRoute("base:node_huangcun", "base:node_guanai", out var route) &&
                !world.WorldGraph.TryFindRoute("base:node_guanai", "base:node_huangcun", out route))
            {
                if (!world.WorldGraph.TryFindRoute("base:node_huangcun", "base:node_linjian", out route) &&
                    !world.WorldGraph.TryFindRoute("base:node_linjian", "base:node_huangcun", out route))
                    return;
            }

            var fromId = route.FromNodeId;
            var toId = route.ToNodeId;
            if (!string.Equals(fromId, "base:node_huangcun", StringComparison.Ordinal))
            {
                fromId = route.ToNodeId;
                toId = route.FromNodeId;
            }

            ArmyStackAdapter.EnsureBanditPatrolArmy(world, fromId, route.Id, toId, 0.42f);
        }

        /// <summary>Hex 模式下将 Prototype 山匪放到荒村外 7～8 格（迁移/重建后也可复用）。</summary>
        public static void PositionPrototypeBanditPatrolArmy(SimulationWorld world)
        {
            if (!ArmyHexCommandService.IsHexStrategicActive(world))
                return;
            if (!world.Strategic.FormalArmies.TryGet(ArmyStackAdapter.BanditPatrolFormalArmyId, out var bandit) ||
                bandit == null)
                return;

            var patrolHex = Ch01HexPrototypeMapBuilder.ResolvePrototypeBanditPatrolHex(world);
            ArmyHexTravelService.InitializeArmyAtHex(bandit, patrolHex);
            if (world.Strategic.Armies.TryGet(ArmyStackAdapter.BanditPatrolStackId, out var stack) && stack != null)
                ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, stack);
        }

        /// <summary>
        /// Prototype 回归用 Bandit 敌对 — 非 Ch01 正式剧情战争。
        /// 正式剧情 War（Player vs FormerOverlordSect）须在荒村 Capture 后由 Scenario Progression 触发。
        /// </summary>
        static void ApplyPrototypeRegressionDiplomacy(SimulationWorld world)
        {
            WarGateService.DeclareWar(world, StrategicFactionCatalog.HuangcunLaborId, StrategicFactionCatalog.BanditId);
            WarGateService.DeclareWar(world, StrategicFactionCatalog.PlayerFactionId, StrategicFactionCatalog.BanditId);
        }
    }
}
