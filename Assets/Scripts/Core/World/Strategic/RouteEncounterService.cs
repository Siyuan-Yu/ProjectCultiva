using System;
using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Route danger 检定 → 战略遭遇（113-E）。</summary>
    public static class RouteEncounterService
    {
        public const string DefaultEncounterLocalMapId = "base:map_world_node_stub";
        public const string DefaultEncounterId = "base:encounter_trail_ambush";

        /// <summary>Travel 每 tick 后调用；命中则写入 StrategicBoard.RouteEncounter 并返回 true。</summary>
        public static bool TryRollDuringTravel(SimulationWorld world, WorldAgentPresence traveler)
        {
            if (world?.Strategic == null || traveler == null)
                return false;
            if (traveler.Mode != PartyWorldPresenceMode.Traveling)
                return false;
            if (world.Strategic.HasBlockingInterrupt)
                return false;
            if (string.IsNullOrEmpty(traveler.RouteId) ||
                !world.WorldGraph.TryGetRoute(traveler.RouteId, out var route))
                return false;

            var danger = route.Danger;
            if (danger <= 0f)
                return false;

            // 期望：中等 danger≈0.15 时全程约 15% 至少一次；每 tick 独立检定。
            var chance = Math.Clamp(danger * 0.02f, 0.001f, 0.25f);
            var roll = world.Random.NextDouble();
            if (roll > chance)
                return false;

            var pending = world.Strategic.RouteEncounter;
            pending.Resolved = false;
            pending.RouteId = route.Id;
            pending.EncounterId = string.IsNullOrEmpty(route.EncounterPoolId)
                ? DefaultEncounterId
                : route.EncounterPoolId;
            pending.LocalMapId = ResolveEncounterMapId(pending.EncounterId);
            pending.Title = "路遇险情 · " + (route.Kind ?? "Trail");
            world.PartyWorld.EncounterId = pending.EncounterId;
            return true;
        }

        public static string ResolveEncounterMapId(string encounterId)
        {
            if (string.IsNullOrEmpty(encounterId))
                return DefaultEncounterLocalMapId;
            return DefaultEncounterLocalMapId;
        }

        public static void ResolveSuccess(SimulationWorld world)
        {
            if (world?.Strategic == null)
                return;
            world.Strategic.ClearRouteEncounter();
            world.PartyWorld.EncounterId = string.Empty;
        }
    }
}
