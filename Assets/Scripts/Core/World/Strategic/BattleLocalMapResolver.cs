using System;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    public enum BattleLocalMapResolutionKind { WorldSite, Wilderness, ExplicitEncounterMap }

    public sealed class BattleLocalMapLocation
    {
        public BattleLocalMapResolutionKind Kind { get; set; }
        public string SiteId { get; set; } = string.Empty;
        public HexCoord BattleHex { get; set; }
        public string ExplicitLocalMapId { get; set; } = string.Empty;
    }

    public sealed class BattleLocalMapResolution
    {
        public bool Success { get; private set; }
        public BattleLocalMapResolutionKind Kind { get; private set; }
        public string LocalMapId { get; private set; } = string.Empty;
        public string SiteId { get; private set; } = string.Empty;
        public HexCoord BattleHex { get; private set; }
        public string FailureReason { get; private set; } = string.Empty;

        internal static BattleLocalMapResolution Resolved(BattleLocalMapLocation source, string mapId) =>
            new BattleLocalMapResolution { Success = true, Kind = source.Kind, LocalMapId = mapId, SiteId = source.SiteId ?? string.Empty, BattleHex = source.BattleHex };
        internal static BattleLocalMapResolution Failed(BattleLocalMapLocation source, string reason) =>
            new BattleLocalMapResolution { Kind = source?.Kind ?? default, SiteId = source?.SiteId ?? string.Empty, BattleHex = source?.BattleHex ?? default, FailureReason = reason ?? string.Empty };
    }

    /// <summary>纯 Core：Battle location → 真实或显式 LocalMap；不写 PartyWorld、不执行 materialize。</summary>
    public static class BattleLocalMapResolver
    {
        /// <summary>消费 Phase 4 已冻结的接战位置；不从展示锚点或 UI 反推地点。</summary>
        public static BattleLocalMapResolution ResolvePendingEngagement(SimulationWorld world)
        {
            var engagement = world?.Strategic?.PendingEngagement;
            if (engagement == null || !engagement.IsActive)
                return BattleLocalMapResolution.Failed(null, "没有活动中的接战。");

            if (!string.IsNullOrEmpty(engagement.DefenderFormalArmyId) &&
                world.Strategic.FormalArmies.TryGet(engagement.DefenderFormalArmyId, out var defender) &&
                defender?.WorldMotion != null &&
                defender.WorldMotion.LocationKind == FormalArmyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(defender.WorldMotion.SiteId))
            {
                return Resolve(world, new BattleLocalMapLocation
                {
                    Kind = BattleLocalMapResolutionKind.WorldSite,
                    SiteId = defender.WorldMotion.SiteId
                });
            }

            if (!engagement.HasBattleLocation)
                return BattleLocalMapResolution.Failed(null, "接战缺少冻结的战斗 Hex。");
            return Resolve(world, new BattleLocalMapLocation
            {
                Kind = BattleLocalMapResolutionKind.Wilderness,
                BattleHex = engagement.BattleLocation
            });
        }

        public static BattleLocalMapResolution Resolve(SimulationWorld world, BattleLocalMapLocation location)
        {
            if (location == null) return BattleLocalMapResolution.Failed(null, "Battle location is required.");
            switch (location.Kind)
            {
                case BattleLocalMapResolutionKind.WorldSite:
                    if (world?.Strategic?.Sites == null || string.IsNullOrWhiteSpace(location.SiteId) ||
                        !world.Strategic.Sites.TryGet(location.SiteId.Trim(), out var site) || site == null)
                        return BattleLocalMapResolution.Failed(location, "WorldSite not found.");
                    if (string.IsNullOrWhiteSpace(site.LocalMapId))
                        return BattleLocalMapResolution.Failed(location, "WorldSite LocalMapId is empty.");
                    return BattleLocalMapResolution.Resolved(location, site.LocalMapId.Trim());

                case BattleLocalMapResolutionKind.Wilderness:
                    if (!WildernessLocalMapFallback.TryResolve(world, location.BattleHex, out var wildernessMap) || string.IsNullOrWhiteSpace(wildernessMap))
                        return BattleLocalMapResolution.Failed(location, "Wilderness LocalMap resolution failed.");
                    return BattleLocalMapResolution.Resolved(location, wildernessMap);

                case BattleLocalMapResolutionKind.ExplicitEncounterMap:
                    if (string.IsNullOrWhiteSpace(location.ExplicitLocalMapId))
                        return BattleLocalMapResolution.Failed(location, "Explicit encounter LocalMapId is empty.");
                    return BattleLocalMapResolution.Resolved(location, location.ExplicitLocalMapId.Trim());

                default:
                    return BattleLocalMapResolution.Failed(location, "Unknown resolution kind.");
            }
        }
    }
}
