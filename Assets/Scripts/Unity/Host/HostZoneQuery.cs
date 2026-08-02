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

            var p = HostPresentationSpace.ToPresentation(worldPoint);
            var band = ResolveWorkBand(p.x, p.y);
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

            var p = HostPresentationSpace.ToPresentation(worldPoint);
            var band = ResolveSpiritBand(p.x, p.y);
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

        /// <summary>对齐 HostDemoTileMap.ChooseGroundPrefab 色带。</summary>
        static string ResolveWorkBand(float x, float y)
        {
            if (x <= -28f)
                return y >= 5f ? "base:loc_ref_mine" : "base:loc_ref_forest";
            if (x >= -10f && x <= 3f && y >= -20f && y <= -11f)
                return "base:loc_ref_herb_field";
            if (x >= 8f && x <= 32f && y >= -20f && y <= -4f)
                return "base:loc_ref_labor_yard";
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
