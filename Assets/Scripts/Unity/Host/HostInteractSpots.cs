using System.Collections.Generic;
using UnityEngine;
using XianXia.Core.Simulation;

namespace XianXia.Unity.Host
{
    public enum HostInteractSpotKind
    {
        Work = 0,
        Cultivate = 1,
        /// <summary>洞口等：走到后探索／发现，不自动打坐。</summary>
        Explore = 2,
        /// <summary>地表／洞内可拾取物。</summary>
        Loot = 3
    }

    /// <summary>表现层可交互点（多点／同地点）；不进 Core Freeze。</summary>
    public readonly struct HostInteractSpot
    {
        public HostInteractSpot(
            string locationId,
            HostInteractSpotKind kind,
            float presentationX,
            float presentationZ,
            string label,
            string lootSpotId = null,
            string lootItemId = null)
        {
            LocationId = locationId;
            Kind = kind;
            PresentationX = presentationX;
            PresentationZ = presentationZ;
            Label = label ?? string.Empty;
            LootSpotId = lootSpotId ?? string.Empty;
            LootItemId = lootItemId ?? string.Empty;
        }

        public string LocationId { get; }
        public HostInteractSpotKind Kind { get; }
        public float PresentationX { get; }
        public float PresentationZ { get; }
        public string Label { get; }
        public string LootSpotId { get; }
        public string LootItemId { get; }

        public Vector3 WorldPosition =>
            HostPresentationSpace.FromPresentation(PresentationX, PresentationZ);
    }

    public static class HostInteractSpots
    {
        static readonly List<HostInteractSpot> Dynamic = new List<HostInteractSpot>(256);
        static readonly HostInteractSpot[] Empty = System.Array.Empty<HostInteractSpot>();
        static bool _layoutRebuilt;

        // 农田／药田已改成 map 上的 grainField／herbField 格，勿再放旧大片绿区上的麦垄／药畦热点。
        static readonly HostInteractSpot[] LegacyFallback =
        {
            new HostInteractSpot("base:loc_ref_forest", HostInteractSpotKind.Work, -34f, 0f, "古树"),
            new HostInteractSpot("base:loc_ref_forest", HostInteractSpotKind.Work, -32f, -3f, "柴堆"),
            new HostInteractSpot("base:loc_ref_forest", HostInteractSpotKind.Work, -36f, 2f, "林缘"),
            new HostInteractSpot("base:loc_ref_mine", HostInteractSpotKind.Work, -30f, 8f, "洞口"),
            new HostInteractSpot("base:loc_ref_mine", HostInteractSpotKind.Work, -28f, 6f, "矿堆"),
            new HostInteractSpot("base:loc_ref_spring", HostInteractSpotKind.Cultivate, 27f, -11f, "泉眼"),
            new HostInteractSpot("base:loc_ref_spring", HostInteractSpotKind.Cultivate, 29f, -13f, "泉畔石"),
            new HostInteractSpot("base:loc_ref_cave", HostInteractSpotKind.Explore, 24f, -14f, "洞口"),
            new HostInteractSpot("base:loc_ref_cave", HostInteractSpotKind.Explore, 26f, -15f, "洞口石径"),
        };

        public static bool HasDynamicPlots => Dynamic.Count > 0;

        /// <summary>
        /// 有动态地块用动态；地图已 Rebuild 但无软热点则空（勿回落 Legacy 麦垄，否则田区周围又变可交互）。
        /// 仅未 Rebuild 的旧路径才用 Legacy。
        /// </summary>
        public static IReadOnlyList<HostInteractSpot> GetSpots(SimulationWorld world)
        {
            if (Dynamic.Count > 0)
                return Dynamic;
            if (_layoutRebuilt)
                return Empty;
            if (world?.LocalMap == null || world.WorldRegion == null)
                return Empty;
            if (!world.WorldRegion.TryGet("base:loc_ref_labor_yard", out _))
                return Empty;
            var active = world.LocalMap.ActiveMapLayoutId ?? string.Empty;
            if (string.Equals(active, "base:map_ch01_reference", System.StringComparison.Ordinal))
                return LegacyFallback;
            if (string.IsNullOrEmpty(active))
                return LegacyFallback;
            return Empty;
        }

        public static void BeginLayoutRebuild()
        {
            Dynamic.Clear();
            _layoutRebuilt = true;
        }

        public static void RegisterPlot(HostInteractSpot spot) => Dynamic.Add(spot);

        public static bool TryFindNearest(
            Vector3 worldPoint,
            HostInteractSpotKind kind,
            out HostInteractSpot spot,
            float maxDist = 3.5f,
            SimulationWorld world = null)
        {
            spot = default;
            var p = HostPresentationSpace.ToPresentation(worldPoint);
            var best = maxDist * maxDist;
            var found = false;
            HostInteractSpot bestSpot = default;
            var list = GetSpots(world);
            for (var i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s.Kind != kind)
                    continue;
                var dx = s.PresentationX - p.x;
                var dy = s.PresentationZ - p.y;
                var d2 = dx * dx + dy * dy;
                if (d2 > best)
                    continue;
                best = d2;
                bestSpot = s;
                found = true;
            }

            if (!found)
                return false;
            spot = bestSpot;
            return true;
        }

        public static bool TryGetSlotSpot(
            string locationId,
            HostInteractSpotKind kind,
            int slotIndex,
            out HostInteractSpot spot,
            SimulationWorld world = null)
        {
            spot = default;
            if (string.IsNullOrEmpty(locationId))
                return false;
            var matches = new List<HostInteractSpot>(8);
            var list = GetSpots(world);
            for (var i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s.Kind != kind)
                    continue;
                if (!string.Equals(s.LocationId, locationId, System.StringComparison.Ordinal))
                    continue;
                matches.Add(s);
            }

            if (matches.Count == 0)
                return false;
            var idx = slotIndex % matches.Count;
            if (idx < 0)
                idx += matches.Count;
            spot = matches[idx];
            return true;
        }

        public static Vector3 RingOffset(int slotIndex)
        {
            var a = slotIndex * 2.399963f;
            var r = 0.55f + (slotIndex % 3) * 0.35f;
            return new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
        }
    }
}
