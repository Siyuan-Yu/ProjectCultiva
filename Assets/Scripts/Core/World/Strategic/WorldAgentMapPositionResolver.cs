using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.World.Strategic
{
    /// <summary>WorldAgentPresence → 大地图世界坐标（Graph + Hex Residual）。</summary>
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

            // Residual Hex：唯一位置真源 = AtHex / HexCoord（禁止读 Node / Route）
            if (presence.UsesHexPresence)
            {
                if (world.HexWorld != null && world.HexWorld.HasGrid)
                {
                    HexMath.ToWorldPosition(presence.ResidualHex, world.HexWorld.HexSize, out worldX, out worldY);
                    return true;
                }

                HexMath.ToWorldPosition(presence.ResidualHex, HexWorldScale.DefaultHexOuterRadius, out worldX, out worldY);
                return true;
            }

            if (ArmyHexCommandService.IsHexStrategicActive(world) &&
                world.HexWorld != null &&
                world.HexWorld.HasGrid)
            {
                if (ArmyHexBattleAnchorService.TryResolveHexForNode(world, presence.NodeId, out var nodeHex))
                {
                    HexMath.ToWorldPosition(nodeHex, world.HexWorld.HexSize, out worldX, out worldY);
                    return true;
                }
            }

            if (!WorldTravelService.TryResolveTravelWorldPoints(
                    world,
                    presence,
                    out var fromX,
                    out var fromY,
                    out var toX,
                    out var toY))
                return false;

            worldX = fromX;
            worldY = fromY;
            if (presence.HasRoutePresentation)
            {
                var t = presence.Mode == PartyWorldPresenceMode.RouteAnchored
                    ? Clamp01(presence.RouteAnchorProgress)
                    : Clamp01(presence.TravelProgress);
                worldX = fromX + (toX - fromX) * t;
                worldY = fromY + (toY - fromY) * t;
            }

            return true;
        }

        static float Clamp01(float v)
        {
            if (v < 0f)
                return 0f;
            if (v > 1f)
                return 1f;
            return v;
        }
    }
}
