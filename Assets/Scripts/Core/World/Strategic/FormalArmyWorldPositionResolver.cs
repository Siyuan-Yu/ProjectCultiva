using XianXia.Core.Simulation;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// FormalArmy 战略位置 → 大地图世界坐标的唯一纯函数真源。
    /// 只读 FormalArmy.StrategicPosition + WorldGraph；无副作用、无缓存。
    /// </summary>
    public static class FormalArmyWorldPositionResolver
    {
        public enum RenderSourceType
        {
            None = 0,
            AtNode,
            Garrisoned,
            OnRouteInterpolation,
            RouteAnchored,
        }

        public readonly struct WorldPositionInfo
        {
            public readonly float WorldX;
            public readonly float WorldY;
            public readonly RenderSourceType SourceType;
            public readonly string SourceId;
            public readonly string RenderReason;
            public readonly bool Resolved;

            public WorldPositionInfo(
                float worldX,
                float worldY,
                RenderSourceType sourceType,
                string sourceId,
                string renderReason,
                bool resolved)
            {
                WorldX = worldX;
                WorldY = worldY;
                SourceType = sourceType;
                SourceId = sourceId ?? string.Empty;
                RenderReason = renderReason ?? string.Empty;
                Resolved = resolved;
            }

            public static WorldPositionInfo Unresolved => default;
        }

        public static bool TryResolve(
            SimulationWorld world,
            FormalArmy army,
            out float worldX,
            out float worldY) =>
            TryResolve(world, army, out worldX, out worldY, out _);

        public static bool TryResolve(
            SimulationWorld world,
            FormalArmy army,
            out float worldX,
            out float worldY,
            out WorldPositionInfo info)
        {
            worldX = 0f;
            worldY = 0f;
            info = WorldPositionInfo.Unresolved;
            if (world == null || army == null)
                return false;

            if (world.HexWorld.HasGrid &&
                army.UsesHexStrategicPosition &&
                FormalArmyHexWorldPositionResolver.TryResolve(world, army, out worldX, out worldY))
            {
                var hexReason = army.State == FormalArmyState.Moving ? "HexMoving" : "HexStationary";
                info = new WorldPositionInfo(
                    worldX,
                    worldY,
                    army.State == FormalArmyState.Moving
                        ? RenderSourceType.OnRouteInterpolation
                        : RenderSourceType.AtNode,
                    army.CurrentHex.ToString(),
                    hexReason,
                    true);
                return true;
            }

            if (TryResolveRouteInterpolation(world, army, out worldX, out worldY, out var routeId, out var reason, out var sourceType))
            {
                info = new WorldPositionInfo(worldX, worldY, sourceType, routeId, reason, true);
                return true;
            }

            if (string.IsNullOrEmpty(army.NodeId) ||
                !world.WorldGraph.TryGetNode(army.NodeId, out var node) ||
                node == null)
                return false;

            worldX = node.WorldX;
            worldY = node.WorldY;
            if (army.State == FormalArmyState.Garrisoned)
            {
                info = new WorldPositionInfo(worldX, worldY, RenderSourceType.Garrisoned, army.NodeId, "Garrisoned", true);
                return true;
            }

            info = new WorldPositionInfo(worldX, worldY, RenderSourceType.AtNode, army.NodeId, "AtNode", true);
            return true;
        }

        static bool TryResolveRouteInterpolation(
            SimulationWorld world,
            FormalArmy army,
            out float worldX,
            out float worldY,
            out string routeId,
            out string renderReason,
            out RenderSourceType sourceType)
        {
            worldX = worldY = 0f;
            routeId = string.Empty;
            renderReason = string.Empty;
            sourceType = RenderSourceType.None;

            if (army == null || string.IsNullOrEmpty(army.RouteId))
                return false;
            if (!world.WorldGraph.TryGetRoute(army.RouteId, out var route) || route == null)
                return false;
            if (!world.WorldGraph.TryGetNode(route.FromNodeId, out var fromNode) || fromNode == null)
                return false;
            if (!world.WorldGraph.TryGetNode(route.ToNodeId, out var toNode) || toNode == null)
                return false;

            var useRoute = army.State == FormalArmyState.OnRoute || army.IsRouteAnchored;
            if (!useRoute)
                return false;

            var t = army.GetRouteDisplayProgress();
            worldX = fromNode.WorldX + (toNode.WorldX - fromNode.WorldX) * t;
            worldY = fromNode.WorldY + (toNode.WorldY - fromNode.WorldY) * t;
            routeId = army.RouteId;
            if (army.State == FormalArmyState.OnRoute)
            {
                sourceType = RenderSourceType.OnRouteInterpolation;
                renderReason = "OnRouteInterpolation";
            }
            else
            {
                sourceType = RenderSourceType.RouteAnchored;
                renderReason = "RouteAnchored";
            }

            return true;
        }
    }
}
