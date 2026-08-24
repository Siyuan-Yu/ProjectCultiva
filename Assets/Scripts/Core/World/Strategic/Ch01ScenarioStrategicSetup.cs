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
            if (world?.Strategic == null)
                return;

            world.Strategic.Ch01FormationScenarioCompat = true;
            ApplyPlayerFactionAndVassalage(world);
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

        static void SeedPrototypeBanditArmies(SimulationWorld world)
        {
            world.Strategic.Armies.Clear();
            ArmyStackAdapter.EnsureBanditPatrolArmy(
                world,
                Ch01HexPrototypeMapBuilder.SiteHuangcun,
                string.Empty,
                Ch01HexPrototypeMapBuilder.SiteQingyunLu,
                -1f);
            ArmyStackAdapter.EnsureBanditWeakPatrolArmy(
                world,
                Ch01HexPrototypeMapBuilder.SiteHuangcun,
                string.Empty,
                Ch01HexPrototypeMapBuilder.SiteQingyunLu,
                -1f);
            PositionPrototypeTestBanditArmies(world);
        }

        /// <summary>
        /// Hex 模式：两波 Prototype 测试山匪静止放置。
        /// strong=荒村南侧路廊；weak=荒村东侧横路（手操红框位）。
        /// </summary>
        public static void PositionPrototypeTestBanditArmies(SimulationWorld world)
        {
            if (!ArmyHexCommandService.IsHexStrategicActive(world))
                return;

            Ch01HexPrototypeMapBuilder.ResolvePrototypeTestBanditHexesBelowHuangcun(
                world,
                out var strongHex,
                out var weakHex);
            PositionPrototypeBanditArmyAtHex(world, ArmyStackAdapter.BanditPatrolFormalArmyId, strongHex);
            PositionPrototypeBanditArmyAtHex(world, ArmyStackAdapter.BanditWeakPatrolFormalArmyId, weakHex);
        }

        /// <summary>Hex 模式下将 Prototype 山匪放到荒村外 7～8 格（迁移/重建后也可复用）。</summary>
        public static void PositionPrototypeBanditPatrolArmy(SimulationWorld world) =>
            PositionPrototypeTestBanditArmies(world);

        static void PositionPrototypeBanditArmyAtHex(
            SimulationWorld world,
            string formalArmyId,
            HexCoord hex)
        {
            if (!world.Strategic.FormalArmies.TryGet(formalArmyId, out var bandit) || bandit == null)
                return;

            Ch01HexPrototypeMapBuilder.EnsurePrototypeTestBanditHexPassable(world, hex);
            ArmyHexTravelService.InitializeArmyAtHex(world, bandit, hex);
            var stackId = string.Equals(formalArmyId, ArmyStackAdapter.BanditWeakPatrolFormalArmyId, StringComparison.Ordinal)
                ? ArmyStackAdapter.BanditWeakPatrolStackId
                : ArmyStackAdapter.BanditPatrolStackId;
            if (world.Strategic.Armies.TryGet(stackId, out var stack) && stack != null)
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
