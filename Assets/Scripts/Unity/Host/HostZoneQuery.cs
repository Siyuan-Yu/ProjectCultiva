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
            if (world?.WorldRegion == null)
                return null;

            if (HostInteractSpots.TryFindNearest(worldPoint, HostInteractSpotKind.Work, out var spot, 3.5f, world))
                return spot.LocationId;

            var p = HostPresentationSpace.ToPresentation(worldPoint);
            // 色带命中硬编码荒村 loc：仅当前地图确为荒村参考关时才启用
            var band = IsCh01ReferenceMap(world) ? ResolveWorkBand(p.x, p.y) : null;
            if (!string.IsNullOrEmpty(band) &&
                world.WorldRegion.TryGet(band, out var bandLoc) &&
                HasWorkResource(bandLoc))
                return band;

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
            if (world?.WorldRegion == null)
                return null;
            var p = HostPresentationSpace.ToPresentation(worldPoint);
            return FindNearest(world, p, centerRadius, loc => HasWorkResource(loc));
        }

        public static string FindCultivateLocation(
            SimulationWorld world,
            Vector3 worldPoint,
            float centerRadius = DefaultCenterRadius)
        {
            if (world?.WorldRegion == null)
                return null;

            if (HostInteractSpots.TryFindNearest(worldPoint, HostInteractSpotKind.Cultivate, out var spot, 3.5f, world))
                return spot.LocationId;

            var p = HostPresentationSpace.ToPresentation(worldPoint);
            var band = IsCh01ReferenceMap(world) ? ResolveSpiritBand(p.x, p.y) : null;
            if (!string.IsNullOrEmpty(band) &&
                world.WorldRegion.TryGet(band, out var bandLoc) &&
                bandLoc.Kind == LocationKind.Opportunity)
                return band;

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
            if (world?.WorldRegion == null)
                return null;
            var p = HostPresentationSpace.ToPresentation(worldPoint);
            return FindNearest(world, p, centerRadius, loc => loc.Kind == LocationKind.Opportunity);
        }

        public static bool TryGetLocationCenter(SimulationWorld world, string locationId, out Vector3 worldCenter)
        {
            worldCenter = default;
            if (world?.WorldRegion == null || string.IsNullOrEmpty(locationId))
                return false;
            if (!world.WorldRegion.TryGet(locationId, out var loc))
                return false;
            worldCenter = HostPresentationSpace.FromPresentation(loc.PresentationX, loc.PresentationZ);
            return true;
        }

        public static bool LocationHasWork(SimulationWorld world, string locationId)
        {
            return world?.WorldRegion != null &&
                   world.WorldRegion.TryGet(locationId, out var loc) &&
                   HasWorkResource(loc);
        }

        public static bool LocationIsCultivate(SimulationWorld world, string locationId)
        {
            return world?.WorldRegion != null &&
                   world.WorldRegion.TryGet(locationId, out var loc) &&
                   loc.Kind == LocationKind.Opportunity;
        }

        static bool HasWorkResource(WorldLocationState loc) =>
            loc != null &&
            !string.IsNullOrEmpty(loc.ResourceOnExploreId) &&
            loc.ResourceOnExploreAmount > 0;

        static bool IsCh01ReferenceMap(SimulationWorld world)
        {
            if (world?.LocalMap == null || world.WorldRegion == null)
                return false;
            var active = world.LocalMap.ActiveMapLayoutId ?? string.Empty;
            if (string.Equals(active, "base:map_ch01_reference", System.StringComparison.Ordinal))
                return true;
            // 开局尚未写入 ActiveMap：仅当地点表已是荒村时允许色带（EditMode／旧路径）
            return string.IsNullOrEmpty(active) &&
                   world.WorldRegion.TryGet("base:loc_ref_labor_yard", out _);
        }

        /// <summary>
        /// 旧大片农田／药田色带已删：田区只认 map 上的 grainField／herbField，
        /// 勿再把绿草矩形当成工区。仅保留林／矿色带（无格点物件时的回落）。
        /// </summary>
        static string ResolveWorkBand(float x, float y)
        {
            if (x <= -28f)
                return y >= 5f ? "base:loc_ref_mine" : "base:loc_ref_forest";
            return null;
        }

        static string ResolveSpiritBand(float x, float y)
        {
            if (x >= 24f && y <= -10f)
            {
                // 洞府略靠右下，灵泉略靠左；按点击再细分。
                if (x >= 26f && y <= -13f)
                    return "base:loc_ref_cave";
                return "base:loc_ref_spring";
            }

            return null;
        }

        static string FindNearest(
            SimulationWorld world,
            Vector2 p,
            float radius,
            System.Func<WorldLocationState, bool> pred)
        {
            string best = null;
            var bestDist = radius;
            foreach (var kv in world.WorldRegion.Locations)
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

            return best;
        }
    }
}
