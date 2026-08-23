using System;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>组军／解散／驻扎：须在己方 WorldSite 足迹内（Hex 战略真源）。</summary>
    public static class ArmyFormationSitePolicy
    {
        public static bool TryGetSiteForLegacyNode(SimulationWorld world, string nodeId, out WorldSite site)
        {
            site = null;
            if (world?.Strategic?.Sites == null || string.IsNullOrEmpty(nodeId))
                return false;

            foreach (var kv in world.Strategic.Sites.Sites)
            {
                var candidate = kv.Value;
                if (candidate == null || string.IsNullOrEmpty(candidate.LegacyNodeId))
                    continue;
                if (!string.Equals(candidate.LegacyNodeId, nodeId, StringComparison.Ordinal))
                    continue;
                site = candidate;
                return true;
            }

            return false;
        }

        public static bool IsFriendlySiteForFaction(WorldSite site, string factionId)
        {
            if (site == null || string.IsNullOrEmpty(factionId))
                return false;
            if (string.IsNullOrEmpty(site.OwnerFactionId))
                return false;
            return string.Equals(site.OwnerFactionId, factionId, StringComparison.Ordinal);
        }

        public static bool IsFriendlyHexForFaction(SimulationWorld world, HexCoord hex, string factionId)
        {
            if (world?.Strategic?.Sites == null || string.IsNullOrEmpty(factionId))
                return false;
            if (!world.Strategic.Sites.TryGetAtHex(hex, out var site) || site == null)
                return false;
            return IsFriendlySiteForFaction(site, factionId);
        }

        public static bool HasFactionMemberAtSite(SimulationWorld world, WorldSite site, string factionId)
        {
            if (world?.WorldPresence == null || site == null || string.IsNullOrEmpty(factionId))
                return false;

            foreach (var kv in world.WorldPresence.All)
            {
                var presence = kv.Value;
                if (presence == null || presence.EntityId.IsNone)
                    continue;
                if (presence.Mode != PartyWorldPresenceMode.AtNode)
                    continue;
                if (!TryGetSiteForLegacyNode(world, presence.NodeId, out var residentSite) ||
                    residentSite == null ||
                    !string.Equals(residentSite.SiteId, site.SiteId, StringComparison.Ordinal))
                    continue;
                var charFaction = ArmyService.ResolveCharacterFactionId(world, presence.EntityId);
                if (string.Equals(charFaction, factionId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public static bool TryValidateFriendlySite(
            SimulationWorld world,
            string factionId,
            WorldSite site,
            out GameError error)
        {
            error = default;
            if (site == null)
            {
                error = new GameError(ErrorCode.NotFound, "WorldSite not found.");
                return false;
            }

            if (IsFriendlySiteForFaction(site, factionId))
                return true;

            if (!string.IsNullOrEmpty(site.OwnerFactionId))
            {
                error = new GameError(
                    ErrorCode.InvalidOperation,
                    "Army operations require friendly WorldSite owner.",
                    site.SiteId + ";owner=" + site.OwnerFactionId + ";faction=" + factionId);
            }
            else
            {
                error = new GameError(
                    ErrorCode.InvalidOperation,
                    "Army operations require friendly WorldSite (owner or faction presence).",
                    site.SiteId + ";faction=" + factionId);
            }

            return false;
        }

        public static bool TryValidateFriendlySiteForNode(
            SimulationWorld world,
            string factionId,
            string nodeId,
            out GameError error)
        {
            error = default;
            if (!TryGetSiteForLegacyNode(world, nodeId, out var site) || site == null)
            {
                error = new GameError(ErrorCode.NotFound, "WorldSite not found for node.", nodeId);
                return false;
            }

            return TryValidateFriendlySite(world, factionId, site, out error);
        }

        public static bool TryValidateFormationAtHex(
            SimulationWorld world,
            string factionId,
            HexCoord hex,
            out GameError error)
        {
            error = default;
            if (!world.Strategic.Sites.TryGetAtHex(hex, out var site) || site == null)
            {
                error = new GameError(
                    ErrorCode.InvalidOperation,
                    "Army operations require a WorldSite footprint.",
                    hex.ToString());
                return false;
            }

            return TryValidateFriendlySite(world, factionId, site, out error);
        }
    }

}
