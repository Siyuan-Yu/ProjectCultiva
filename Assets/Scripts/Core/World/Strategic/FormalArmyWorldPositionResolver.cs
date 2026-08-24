using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// FormalArmy 战略位置 → 大地图世界坐标的唯一纯函数真源（Hex-only）。
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

            if (FormalArmyHexWorldPositionResolver.TryResolve(world, army, out worldX, out worldY))
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

            if (world.Strategic.Sites.TryGet(army.NodeId, out var site) && site != null)
            {
                HexMath.ToWorldPosition(site.AnchorHex, world.HexWorld.HexSize, out worldX, out worldY);
                info = new WorldPositionInfo(
                    worldX,
                    worldY,
                    army.State == FormalArmyState.Garrisoned
                        ? RenderSourceType.Garrisoned
                        : RenderSourceType.AtNode,
                    site.SiteId,
                    army.State == FormalArmyState.Garrisoned ? "Garrisoned" : "AtSite",
                    true);
                return true;
            }

            return false;
        }
    }
}
