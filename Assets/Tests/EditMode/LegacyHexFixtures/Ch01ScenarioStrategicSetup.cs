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
        /// <summary>
        /// 旧 Core fixture／EditMode 兼容入口。Data 正式启动路径不得调用：
        /// 正式 Content 应分别调用 ApplyRuntimeHooks 与 StrategicOpeningContentBootstrap。
        /// </summary>
        [Obsolete("仅 Core fixture/旧测试兼容；Data playable path 使用 StrategicOpeningContentBootstrap。")]
        public static void Apply(SimulationWorld world)
        {
            ApplyRuntimeHooks(world);
            ApplyLegacyFixtureStrategicDefaults(world);
        }

        public static void ApplyRuntimeHooks(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return;

            Ch01ScenarioProgressionHooks.Register(world);
        }

        public static void ApplyLegacyFixtureStrategicDefaults(SimulationWorld world)
        {
            if (world?.Strategic == null) return;
            ApplyPlayerFactionAndVassalage(world);
            ApplyPrototypeRegressionDiplomacy(world);
        }

        /// <summary>
        /// Explicit legacy Core fixture only. Normal Data/playable bootstrap must never call this;
        /// LevelTester no longer requires or recreates a PlayerCamp WorldSite.
        /// </summary>
        public static void EnsureLevelTesterFixtures(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return;
            if (world.Strategic.Sites.TryGet(Ch01HexPrototypeMapBuilder.SitePlayerCamp, out _))
                return;
            Ch01HexPrototypeMapBuilder.EnsureLevelTesterPlayerCampSite(world);
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
        /// Hex 模式：三波 Prototype 测试山匪静止放置。
        /// strong=荒村南侧路廊；weak=荒村东侧横路；casualtyTest=荒村西北（自动战伤亡夹具）。
        /// </summary>
        public static void PositionPrototypeTestBanditArmies(SimulationWorld world)
        {
            // Retired Army runtime has no Hex fixture deployment.
        }

        /// <summary>Hex 模式下将 Prototype 山匪放到荒村外 7～8 格（迁移/重建后也可复用）。</summary>
        public static void PositionPrototypeBanditPatrolArmy(SimulationWorld world) =>
            PositionPrototypeTestBanditArmies(world);

        /// <summary>
        /// Prototype 回归用 Bandit 敌对 — 非 Ch01 正式剧情战争。
        /// 正式剧情 War（Player vs FormerOverlordSect）由玩家在荒村主动起事时触发，
        /// Capture 后仅推进政治成立标记。
        /// </summary>
        static void ApplyPrototypeRegressionDiplomacy(SimulationWorld world)
        {
            WarGateService.DeclareWar(world, StrategicFactionCatalog.HuangcunLaborId, StrategicFactionCatalog.BanditId);
            WarGateService.DeclareWar(world, StrategicFactionCatalog.PlayerFactionId, StrategicFactionCatalog.BanditId);
        }
    }
}
