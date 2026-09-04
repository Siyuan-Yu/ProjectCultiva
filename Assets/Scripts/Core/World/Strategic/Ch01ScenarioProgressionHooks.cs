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

        public static void Register(SimulationWorld world)
        {
            ScenarioProgressionHooks.OnAllCaptureObjectivesCompletedForSite = HandleAllCaptureObjectivesCompleted;
        }

        public static void Unregister()
        {
            if (ScenarioProgressionHooks.OnAllCaptureObjectivesCompletedForSite ==
                (Action<SimulationWorld, string>)HandleAllCaptureObjectivesCompleted)
                ScenarioProgressionHooks.OnAllCaptureObjectivesCompletedForSite = null;
        }

        static void HandleAllCaptureObjectivesCompleted(SimulationWorld world, string siteId)
        {
            if (world == null || string.IsNullOrEmpty(siteId))
                return;

            // 起事负责先前的独立与宣战；Capture 完成只负责第一章政治成立进度。
            if (string.Equals(siteId, HuangcunSiteId, System.StringComparison.Ordinal))
                world.Flags.Set(FlagPlayerFactionPoliticallyActive);
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
