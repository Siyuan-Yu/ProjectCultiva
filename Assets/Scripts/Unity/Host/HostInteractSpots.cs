using System.Collections.Generic;
using UnityEngine;

namespace XianXia.Unity.Host
{
    public enum HostInteractSpotKind
    {
        Work = 0,
        Cultivate = 1
    }

    /// <summary>表现层可交互点（多点／同地点）；不进 Core Freeze。</summary>
    public readonly struct HostInteractSpot
    {
        public HostInteractSpot(string locationId, HostInteractSpotKind kind, float presentationX, float presentationZ, string label)
        {
            LocationId = locationId;
            Kind = kind;
            PresentationX = presentationX;
            PresentationZ = presentationZ;
            Label = label ?? string.Empty;
        }

        public string LocationId { get; }
        public HostInteractSpotKind Kind { get; }
        public float PresentationX { get; }
        public float PresentationZ { get; }
        public string Label { get; }

        public Vector3 WorldPosition =>
            HostPresentationSpace.FromPresentation(PresentationX, PresentationZ);
    }

    /// <summary>
    /// 交互点：优先用 mapLayout 刷出来的地块；否则回退第一章硬编码样例点。
    /// </summary>
    public static class HostInteractSpots
    {
        public const float DefaultHitRadius = 2.1f;
        public const float PlotHitRadius = 0.85f;

        static readonly List<HostInteractSpot> Dynamic = new List<HostInteractSpot>(256);

        static readonly HostInteractSpot[] LegacyFallback =
        {
            new HostInteractSpot("base:loc_ref_labor_yard", HostInteractSpotKind.Work, 18f, -12f, "麦垄甲"),
            new HostInteractSpot("base:loc_ref_labor_yard", HostInteractSpotKind.Work, 22f, -11f, "麦垄乙"),
            new HostInteractSpot("base:loc_ref_labor_yard", HostInteractSpotKind.Work, 20f, -15f, "田埂"),
            new HostInteractSpot("base:loc_ref_labor_yard", HostInteractSpotKind.Work, 25f, -10f, "场边堆"),
            new HostInteractSpot("base:loc_ref_forest", HostInteractSpotKind.Work, -34f, 0f, "古树"),
            new HostInteractSpot("base:loc_ref_forest", HostInteractSpotKind.Work, -32f, -3f, "柴堆"),
            new HostInteractSpot("base:loc_ref_forest", HostInteractSpotKind.Work, -36f, 2f, "林缘"),
            new HostInteractSpot("base:loc_ref_herb_field", HostInteractSpotKind.Work, -3f, -15f, "药畦甲"),
            new HostInteractSpot("base:loc_ref_herb_field", HostInteractSpotKind.Work, -5f, -13f, "药畦乙"),
            new HostInteractSpot("base:loc_ref_herb_field", HostInteractSpotKind.Work, -1f, -17f, "药畦丙"),
            new HostInteractSpot("base:loc_ref_mine", HostInteractSpotKind.Work, -30f, 8f, "洞口"),
            new HostInteractSpot("base:loc_ref_mine", HostInteractSpotKind.Work, -28f, 6f, "矿堆"),
            new HostInteractSpot("base:loc_ref_spring", HostInteractSpotKind.Cultivate, 27f, -11f, "泉眼"),
            new HostInteractSpot("base:loc_ref_spring", HostInteractSpotKind.Cultivate, 29f, -13f, "泉畔石"),
            new HostInteractSpot("base:loc_ref_cave", HostInteractSpotKind.Cultivate, 24f, -14f, "洞口"),
            new HostInteractSpot("base:loc_ref_cave", HostInteractSpotKind.Cultivate, 26f, -15f, "洞中蒲团"),
        };

        public static bool HasDynamicPlots => Dynamic.Count > 0;

        public static IReadOnlyList<HostInteractSpot> Spots =>
            Dynamic.Count > 0 ? Dynamic : LegacyFallback;

        public static void BeginLayoutRebuild() => Dynamic.Clear();

        public static void RegisterPlot(HostInteractSpot spot) => Dynamic.Add(spot);

        public static bool TryFindNearest(
            Vector3 worldPoint,
            HostInteractSpotKind kind,
            out HostInteractSpot spot,
            float hitRadius = -1f)
        {
            if (hitRadius < 0f)
                hitRadius = HasDynamicPlots ? PlotHitRadius : DefaultHitRadius;

            spot = default;
            var p = HostPresentationSpace.ToPresentation(worldPoint);
            var best = hitRadius;
            var found = false;
            HostInteractSpot bestSpot = default;
            var list = Spots;
            for (var i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s.Kind != kind)
                    continue;
                var dx = s.PresentationX - p.x;
                var dy = s.PresentationZ - p.y;
                var d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > best)
                    continue;
                best = d;
                bestSpot = s;
                found = true;
            }

            if (!found)
                return false;
            spot = bestSpot;
            return true;
        }

        /// <summary>
        /// Soft work slot → concrete interact spot at a location (wraps if more workers than spots).
        /// </summary>
        public static bool TryGetSlotSpot(
            string locationId,
            HostInteractSpotKind kind,
            int slotIndex,
            out HostInteractSpot spot)
        {
            spot = default;
            if (string.IsNullOrEmpty(locationId) || slotIndex < 0)
                return false;

            var matches = new List<HostInteractSpot>(8);
            var list = Spots;
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
            spot = matches[slotIndex % matches.Count];
            return true;
        }

        /// <summary>Ring offset when no interact spots exist for the location.</summary>
        public static Vector3 RingOffset(int slotIndex, float radius = 1.35f)
        {
            if (slotIndex < 0)
                return Vector3.zero;
            var ang = slotIndex * 2.399963f; // golden angle-ish
            return new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius, 0f);
        }
    }
}
