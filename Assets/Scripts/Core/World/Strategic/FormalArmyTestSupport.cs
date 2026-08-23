using System.Collections.Generic;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>EditMode 测试专用：设置 FormalArmy 战略位置。勿在 Runtime 产品路径调用。</summary>
    public static class FormalArmyTestSupport
    {
        public static void AnchorOnRoute(
            FormalArmy army,
            string routeId,
            string destNodeId,
            float progress,
            string nodeId = null)
        {
            if (army == null)
                return;

            army.ClearTravel();
            army.State = FormalArmyState.AtNode;
            army.RouteId = routeId ?? string.Empty;
            army.DestNodeId = destNodeId ?? string.Empty;
            if (!string.IsNullOrEmpty(nodeId))
                army.NodeId = nodeId;
            army.RouteAnchorProgress = progress;
        }

        public static void SetRouteTravel(
            FormalArmy army,
            string routeId,
            string nodeId,
            string destNodeId,
            float segmentOrigin,
            float segmentEnd,
            int travelTotalTicks,
            int remainingTravelTicks)
        {
            if (army == null)
                return;

            army.State = FormalArmyState.OnRoute;
            army.RouteId = routeId ?? string.Empty;
            army.NodeId = nodeId ?? string.Empty;
            army.DestNodeId = destNodeId ?? string.Empty;
            army.RouteSegmentOriginProgress = segmentOrigin;
            army.RouteSegmentEndProgress = segmentEnd;
            army.TravelTotalTicks = travelTotalTicks;
            army.RemainingTravelTicks = remainingTravelTicks;
            army.RouteAnchorProgress = -1f;
        }

        public static void SetDestNodeId(FormalArmy army, string destNodeId)
        {
            if (army == null)
                return;

            army.DestNodeId = destNodeId ?? string.Empty;
        }

        public static void ScaleTravelTicks(FormalArmy army, int divisor)
        {
            if (army == null || divisor < 1)
                return;

            var scaled = System.Math.Max(8, army.TravelTotalTicks / divisor);
            army.TravelTotalTicks = scaled;
            army.RemainingTravelTicks = scaled;
        }

        public static void AnchorOnHex(FormalArmy army, HexCoord hex, bool garrisoned = false)
        {
            if (army == null)
                return;
            ArmyHexTravelService.InitializeArmyAtHex(army, hex, garrisoned);
        }

        public static void SetHexMidTravel(
            SimulationWorld world,
            FormalArmy army,
            HexCoord from,
            HexCoord to,
            float stepProgress)
        {
            if (army == null)
                return;

            army.UsesHexStrategicPosition = true;
            army.CurrentHex = from;
            var path = new List<HexCoord>(8);
            if (world?.HexWorld != null && world.HexWorld.HasGrid &&
                HexPathfinder.TryFindPath(world.HexWorld, from, to, path) &&
                path.Count >= 2)
            {
                army.SetHexPath(path, to);
            }
            else
            {
                path.Add(from);
                path.Add(to);
                army.SetHexPath(path, to);
            }

            var t = System.Math.Max(0f, System.Math.Min(0.99f, stepProgress));
            army.StepProgress = t;
            army.StepRemainingTicks = System.Math.Max(1, (int)((1f - t) * army.StepTotalTicks));
        }

        public static void ScaleHexStepTicks(FormalArmy army, int divisor)
        {
            if (army == null || divisor < 1)
                return;

            var scaled = System.Math.Max(2, army.StepTotalTicks / divisor);
            army.StepTotalTicks = scaled;
            army.StepRemainingTicks = System.Math.Min(army.StepRemainingTicks, scaled);
        }
    }
}
