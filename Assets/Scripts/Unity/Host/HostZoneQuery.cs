using UnityEngine;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Demo 布局工区／灵地命中：先按地砖色带，再回落到地点圆心距离。
    /// </summary>
    public static class HostZoneQuery
    {
        public const float DefaultCenterRadius = 7f;

        public static string FindWorkLocation(SimulationWorld world, Vector3 worldPoint, float centerRadius = DefaultCenterRadius)
        {
            if (world?.LocalPlaces == null)
                return null;

            if (HostInteractSpots.TryFindNearest(worldPoint, HostInteractSpotKind.Work, out var spot, 3.5f, world))
                return spot.LocationId;

            var p = HostPresentationSpace.ToPresentation(worldPoint);
            return FindNearest(
                world,
                p,
                centerRadius,
                loc => HasWorkResource(loc));
        }

        /// <summary>优先命中表现层交互点；用于走到具体点再劳动。</summary>
        public static bool TryFindWorkSpot(Vector3 worldPoint, out HostInteractSpot spot, SimulationWorld world = null) =>
            HostInteractSpots.TryFindNearest(worldPoint, HostInteractSpotKind.Work, out spot, 3.5f, world);

        public static bool TryFindCultivateSpot(Vector3 worldPoint, out HostInteractSpot spot, SimulationWorld world = null) =>
            HostInteractSpots.TryFindNearest(worldPoint, HostInteractSpotKind.Cultivate, out spot, 3.5f, world);

        public static bool TryFindExploreSpot(Vector3 worldPoint, out HostInteractSpot spot, SimulationWorld world = null) =>
            HostInteractSpots.TryFindNearest(worldPoint, HostInteractSpotKind.Explore, out spot, 3.5f, world);

        public static bool TryFindLootSpot(Vector3 worldPoint, out HostInteractSpot spot, SimulationWorld world = null) =>
            HostInteractSpots.TryFindNearest(worldPoint, HostInteractSpotKind.Loot, out spot, 3.5f, world);

        /// <summary>
        /// 右键用：只认工区圆心附近，不用大色带（否则整片农田右键都会被吸去劳动中心，像粘住）。
        /// </summary>
        public static string FindWorkHotspot(
            SimulationWorld world,
            Vector3 worldPoint,
            float centerRadius = 2.75f)
        {
            if (world?.LocalPlaces == null)
                return null;
            var p = HostPresentationSpace.ToPresentation(worldPoint);
            return FindNearest(world, p, centerRadius, loc => HasWorkResource(loc));
        }

        public static string FindCultivateLocation(
            SimulationWorld world,
            Vector3 worldPoint,
            float centerRadius = DefaultCenterRadius)
        {
            if (world?.LocalPlaces == null)
                return null;

            if (HostInteractSpots.TryFindNearest(worldPoint, HostInteractSpotKind.Cultivate, out var spot, 3.5f, world))
                return spot.LocationId;

            var p = HostPresentationSpace.ToPresentation(worldPoint);
            return FindNearest(
                world,
                p,
                centerRadius,
                loc => loc.Kind == LocationKind.Opportunity);
        }

        /// <summary>右键用：灵地圆心热点，避免大色带误吸。</summary>
        public static string FindCultivateHotspot(
            SimulationWorld world,
            Vector3 worldPoint,
            float centerRadius = 2.75f)
        {
            if (world?.LocalPlaces == null)
                return null;
            var p = HostPresentationSpace.ToPresentation(worldPoint);
            return FindNearest(world, p, centerRadius, loc => loc.Kind == LocationKind.Opportunity);
        }

        public static bool TryGetLocationCenter(SimulationWorld world, string locationId, out Vector3 worldCenter)
        {
            worldCenter = default;
            if (!WorldLocationQuery.TryGet(world, locationId, out var loc))
                return false;
            worldCenter = HostPresentationSpace.FromPresentation(loc.PresentationX, loc.PresentationZ);
            return true;
        }

        public static bool LocationHasWork(SimulationWorld world, string locationId)
        {
            return WorldLocationQuery.TryGet(world, locationId, out var loc) &&
                   HasWorkResource(loc);
        }

        public static bool LocationIsCultivate(SimulationWorld world, string locationId)
        {
            return WorldLocationQuery.TryGet(world, locationId, out var loc) &&
                   loc.Kind == LocationKind.Opportunity;
        }

        static bool HasWorkResource(WorldLocationState loc) =>
            loc != null &&
            !string.IsNullOrEmpty(loc.ResourceOnExploreId) &&
            loc.ResourceOnExploreAmount > 0;

        static string FindNearest(
            SimulationWorld world,
            Vector2 p,
            float radius,
            System.Func<WorldLocationState, bool> pred)
        {
            string best = null;
            var bestDist = radius;
            foreach (var kv in world.ContinuousOutdoorMaterialization.PlacesByLocationId)
            {
                var loc = kv.Value;
                if (!pred(loc))
                    continue;
                var dx = loc.PresentationX - p.x;
                var dy = loc.PresentationZ - p.y;
                var d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = loc.Id;
                }
            }
            foreach (var kv in world.LocalPlaces.Locations)
            {
                var loc = kv.Value;
                if (world.ContinuousOutdoorMaterialization.PlacesByLocationId.ContainsKey(kv.Key))
                    continue;
                if (!pred(loc))
                    continue;
                var dx = loc.PresentationX - p.x;
                var dy = loc.PresentationZ - p.y;
                var d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = loc.Id;
                }
            }

            return best;
        }
    }
}
