using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    public static class FormalArmyWorldLocationQuery
    {
        public static bool TryResolve(
            SimulationWorld world,
            FormalArmy army,
            out FormalArmyLocationKind kind,
            out string siteId,
            out WorldVec2 worldPos,
            out HexCoord derivedHex)
        {
            kind = FormalArmyLocationKind.Unknown;
            siteId = string.Empty;
            worldPos = default;
            derivedHex = default;
            if (world == null || army == null)
                return false;

            var motion = army.WorldMotion;
            if (!motion.HasPosition)
                return false;

            kind = motion.LocationKind;
            siteId = motion.SiteId ?? string.Empty;
            worldPos = motion.WorldPosition;
            derivedHex = motion.CurrentHex;
            return true;
        }

        public static bool IsAtFriendlyWorldSite(SimulationWorld world, FormalArmy army)
        {
            if (!TryResolve(world, army, out var kind, out var siteId, out _, out _))
                return false;
            if (kind != FormalArmyLocationKind.AtWorldSite || string.IsNullOrEmpty(siteId))
                return false;
            if (!world.Strategic.Sites.TryGet(siteId, out var site) || site == null)
                return false;
            return ArmyFormationSitePolicy.IsFriendlySiteForFaction(site, army.FactionId);
        }
    }
}
