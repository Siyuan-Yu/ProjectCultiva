using System;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Ch01 剧情进度 Hook（Final Closure — 仅边界与占位，不实现完整谈判/派军）。
    /// </summary>
    public static class Ch01ScenarioProgressionHooks
    {
        public const string HuangcunSiteId = "base:site_huangcun";
        public const string FlagPlayerFactionPoliticallyActive = "ch01:player_faction_politically_active";
        public const string FlagVassalageNegotiationOffered = "ch01:vassalage_negotiation_offered";
        public const string FlagRebellionStarted = "ch01:rebellion_started";

        /// <summary>
        /// 第一章只在边界层记录剧情：玩家对旧宗主第一次完成正式军事侵略，即为起事开始。
        /// 通用外交服务不感知章节内容。
        /// </summary>
        public static void OnStrategicMilitaryAggressionCommitted(
            SimulationWorld world,
            string attackerFactionId,
            string defenderFactionId)
        {
            if (world?.Strategic?.Ch01FormationScenarioCompat != true ||
                !string.Equals(attackerFactionId, StrategicFactionCatalog.PlayerFactionId, StringComparison.Ordinal) ||
                !string.Equals(defenderFactionId, StrategicFactionCatalog.HuangcunLaborId, StringComparison.Ordinal))
                return;
            world.Flags.Set(FlagRebellionStarted);
        }

        public static void Register(SimulationWorld world)
        {
            ScenarioProgressionHooks.OnWorldSiteCaptured = HandleWorldSiteCaptured;
            ScenarioProgressionHooks.OnAllCaptureObjectivesCompletedForSite = HandleLegacyCompletedSite;
        }

        public static void Unregister()
        {
            if (ScenarioProgressionHooks.OnWorldSiteCaptured ==
                (Action<SimulationWorld, string, string, string, string>)HandleWorldSiteCaptured)
                ScenarioProgressionHooks.OnWorldSiteCaptured = null;
            if (ScenarioProgressionHooks.OnAllCaptureObjectivesCompletedForSite ==
                (Action<SimulationWorld, string>)HandleLegacyCompletedSite)
                ScenarioProgressionHooks.OnAllCaptureObjectivesCompletedForSite = null;
        }

        static void HandleWorldSiteCaptured(
            SimulationWorld world, string siteId, string oldOwnerFactionId, string newOwnerFactionId, string workAreaId)
        {
            if (world == null || string.IsNullOrEmpty(siteId))
                return;

            // 起事负责先前的独立与宣战；Capture 完成只负责第一章政治成立进度。
            if (string.Equals(siteId, HuangcunSiteId, System.StringComparison.Ordinal) &&
                string.Equals(newOwnerFactionId, StrategicFactionCatalog.PlayerFactionId, StringComparison.Ordinal))
                world.Flags.Set(FlagPlayerFactionPoliticallyActive);
        }

        // 仅供旧测试／旧外部调用兼容；正式路径只响应 WorldSiteCaptured。
        static void HandleLegacyCompletedSite(SimulationWorld world, string siteId)
        {
            if (string.Equals(siteId, HuangcunSiteId, StringComparison.Ordinal))
                world?.Flags.Set(FlagPlayerFactionPoliticallyActive);
        }

        /// <summary>Future: FormerOverlordSect offers vassalage after war progression. DEFER — hook only.</summary>
        public static void OfferVassalageNegotiation(SimulationWorld world, string overlordFactionId, string vassalFactionId)
        {
            if (world == null ||
                string.IsNullOrEmpty(overlordFactionId) ||
                string.IsNullOrEmpty(vassalFactionId))
                return;

            world.Flags.Set(FlagVassalageNegotiationOffered);
            // Future: scenario UI → VassalageBoard.CreateVassalage + end war per formal diplomacy flow.
        }
    }
}
