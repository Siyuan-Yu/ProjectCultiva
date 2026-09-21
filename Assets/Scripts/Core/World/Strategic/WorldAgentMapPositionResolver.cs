using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.World.Strategic
{
    /// <summary>WorldAgentPresence �?大地图世界坝标（Hex-only）�?/summary>
    public static class WorldAgentMapPositionResolver
    {
        public static bool TryResolve(
            SimulationWorld world,
            EntityId entityId,
            WorldAgentPresence presence,
            out float worldX,
            out float worldY)
        {
            worldX = worldY = 0f;
            if (world == null || presence == null)
                return false;

            // MAP-03 normal authority: a precise continuous position is sufficient.  Do this
            // before consulting HexWorld so NPC/residual presentation survives without a grid.
            if (presence.HasContinuousWorldPosition)
            {
                worldX = presence.WorldPosX;
                worldY = presence.WorldPosY;
                return true;
            }

            if (world.HexWorld == null || !world.HexWorld.HasGrid)
                return false;

            if (presence.UsesHexPresence)
            {
                HexMath.ToWorldPosition(presence.ResidualHex, world.HexWorld.HexSize, out worldX, out worldY);
                return true;
            }

            if (presence.Mode == PartyWorldPresenceMode.AtSite &&
                !string.IsNullOrEmpty(presence.SiteId) &&
                world.Strategic.Sites.TryGet(presence.SiteId, out var site) &&
                site != null)
            {
                site.EnsurePresenceHexValid();
                HexMath.ToWorldPosition(site.PresenceHex, world.HexWorld.HexSize, out worldX, out worldY);
                return true;
            }

            if (!string.IsNullOrEmpty(presence.SiteId) &&
                world.Strategic.Sites.TryResolveSitePresenceHex(presence.SiteId, out var siteHex))
            {
                HexMath.ToWorldPosition(siteHex, world.HexWorld.HexSize, out worldX, out worldY);
                return true;
            }
            if (world.Strategic.Squads.TryGetForCharacter(entityId, out var squad) &&
                world.Strategic.SquadWorldMotions.TryGet(squad.SquadId, out var motion) && motion.HasPosition)
            { worldX = motion.WorldPosition.X; worldY = motion.WorldPosition.Y; return true; }

            return false;
        }
    }
}
