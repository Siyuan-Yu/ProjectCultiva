using System;
using XianXia.Core.Simulation;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Ch01 剧情进度 Hook（Final Closure — 仅边界与占位，不实现完整谈判/派军）。
    /// </summary>
    public static class Ch01ScenarioProgressionHooks
    {
        public const string HuangcunNodeId = "base:node_huangcun";
        public const string FlagPlayerFactionPoliticallyActive = "ch01:player_faction_politically_active";
        public const string FlagVassalageNegotiationOffered = "ch01:vassalage_negotiation_offered";

        public static void Register(SimulationWorld world)
        {
            ScenarioProgressionHooks.OnAllCaptureObjectivesCompletedForNode = HandleAllCaptureObjectivesCompleted;
        }

        public static void Unregister()
        {
            if (ScenarioProgressionHooks.OnAllCaptureObjectivesCompletedForNode == (Action<SimulationWorld, string>)HandleAllCaptureObjectivesCompleted)
                ScenarioProgressionHooks.OnAllCaptureObjectivesCompletedForNode = null;
        }

        static void HandleAllCaptureObjectivesCompleted(SimulationWorld world, string nodeId)
        {
            if (world == null || string.IsNullOrEmpty(nodeId))
                return;

            // Future Ch01 story hook — NOT implemented in Final Closure:
            // Huangcun captured → player faction politically active → DeclareWar(Player, FormerOverlordSect)
            if (string.Equals(nodeId, HuangcunNodeId, System.StringComparison.Ordinal))
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
