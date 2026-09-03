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
            if (world == null || presence == null || !world.HexWorld.HasGrid)
                return false;

            if (presence.UsesHexPresence)
            {
                if (presence.HasContinuousWorldPosition)
                {
                    // precise world position（Local Combat 倒下时保存）：LocalMap 与 WorldMap
                    // 用同一 physical truth，而非 Hex 中心。
                    worldX = presence.WorldPosX;
                    worldY = presence.WorldPosY;
                    return true;
                }

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

            if (ArmyHexBattleAnchorService.TryResolveHexForSite(world, presence.SiteId, out var siteHex) ||
                ArmyHexBattleAnchorService.TryResolveHexForSite(world, presence.SiteId, out siteHex))
            {
                HexMath.ToWorldPosition(siteHex, world.HexWorld.HexSize, out worldX, out worldY);
                return true;
            }

            if (ArmyService.TryGetArmyForCharacter(world, entityId, out var army) &&
                army != null &&
                army.UsesHexStrategicPosition &&
                FormalArmyHexWorldPositionResolver.TryResolve(world, army, out worldX, out worldY))
                return true;

            return false;
        }
    }
}
