using System.Collections.Generic;
using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// 耕种田区：同一 boundLocationId 下的药田／农田格集合（RimWorld 式整片区）。
    /// </summary>
    public static class HostFarmFieldRegistry
    {
        static readonly Dictionary<string, List<HostMapPlotCell>> ByLocation =
            new Dictionary<string, List<HostMapPlotCell>>(System.StringComparer.Ordinal);

        public static void BeginRebuild() => ByLocation.Clear();

        public static void Register(HostMapPlotCell plot)
        {
            if (plot == null || !plot.IsPlantableField)
                return;
            var loc = plot.LocationId;
            if (string.IsNullOrEmpty(loc))
                return;
            if (!ByLocation.TryGetValue(loc, out var list))
            {
                list = new List<HostMapPlotCell>(64);
                ByLocation[loc] = list;
            }

            if (!list.Contains(plot))
                list.Add(plot);
        }

        public static bool HasField(string locationId) =>
            !string.IsNullOrEmpty(locationId) &&
            ByLocation.TryGetValue(locationId, out var list) &&
            list.Count > 0;

        public static bool TryGetPlots(string locationId, out IReadOnlyList<HostMapPlotCell> plots)
        {
            plots = null;
            if (string.IsNullOrEmpty(locationId) ||
                !ByLocation.TryGetValue(locationId, out var list) ||
                list.Count == 0)
                return false;
            // 清掉已销毁引用
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] == null)
                    list.RemoveAt(i);
            }

            if (list.Count == 0)
            {
                ByLocation.Remove(locationId);
                return false;
            }

            plots = list;
            return true;
        }

        /// <summary>
        /// 点击是否落在某一田格上（半格内）。勿用大半径，否则点田外绿底也会开工。
        /// </summary>
        public static bool TryFindLocationNear(Vector3 worldPoint, out string locationId, float maxDist = 0.55f)
        {
            locationId = null;
            HostMapPlotCell best = null;
            var bestD = maxDist * maxDist;
            foreach (var kv in ByLocation)
            {
                var list = kv.Value;
                if (list == null)
                    continue;
                for (var i = 0; i < list.Count; i++)
                {
                    var p = list[i];
                    if (p == null || !p.IsPlantableField)
                        continue;
                    var d = XySqr(p.transform.position, worldPoint);
                    if (d > bestD)
                        continue;
                    bestD = d;
                    best = p;
                    locationId = kv.Key;
                }
            }

            return best != null && !string.IsNullOrEmpty(locationId);
        }

        /// <summary>点击是否命中可耕作格（药田／农田同一套）。</summary>
        public static bool TryFindPlotAt(Vector3 worldPoint, out HostMapPlotCell plot, float maxDist = 0.55f)
        {
            plot = null;
            var bestD = maxDist * maxDist;
            foreach (var kv in ByLocation)
            {
                var list = kv.Value;
                if (list == null)
                    continue;
                for (var i = 0; i < list.Count; i++)
                {
                    var p = list[i];
                    if (p == null || !p.IsPlantableField)
                        continue;
                    var d = XySqr(p.transform.position, worldPoint);
                    if (d > bestD)
                        continue;
                    bestD = d;
                    plot = p;
                }
            }

            return plot != null;
        }

        static float XySqr(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dy = a.y - b.y;
            return dx * dx + dy * dy;
        }
    }

    /// <summary>田格农活优先级与产出。播种不需要种子；作物由田区 kind／区配置决定。</summary>
    public static class HostFarmFieldRules
    {
        public const float WorkSeconds = 2.4f;
        public const float TendGrowthGain = 0.34f;
        public const float PassiveGrowthPerSecond = 0.012f;
        public const float ArriveEpsilon = 0.55f;

        public const string HerbCropId = "crop_spirit_herb";
        public const string GrainCropId = "crop_grain";
        public const string HerbItemId = "base:resource_spirit_herb";
        public const string GrainItemId = "base:resource_grain";

        public static float XyDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dy = a.y - b.y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        public static float XySqrMagnitude(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dy = a.y - b.y;
            return dx * dx + dy * dy;
        }

        public static string CropIdForPlot(HostMapPlotCell plot)
        {
            if (plot == null)
                return GrainCropId;
            if (string.Equals(plot.Kind, "herbField", System.StringComparison.OrdinalIgnoreCase))
                return HerbCropId;
            return GrainCropId;
        }

        public static string HarvestItemId(HostMapPlotCell plot)
        {
            if (plot == null)
                return GrainItemId;
            if (string.Equals(plot.Kind, "herbField", System.StringComparison.OrdinalIgnoreCase))
                return HerbItemId;
            return GrainItemId;
        }

        public static bool IsFarmTaggedWorkArea(XianXia.Core.Npc.WorkAreaDefinition area)
        {
            if (area?.Tags == null || area.Tags.Count == 0)
                return false;
            for (var i = 0; i < area.Tags.Count; i++)
            {
                var t = area.Tags[i];
                if (string.IsNullOrEmpty(t))
                    continue;
                if (string.Equals(t, "farm", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t, "herb", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t, "grain", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static int JobPriority(PlotCropStage stage)
        {
            switch (stage)
            {
                case PlotCropStage.Mature: return 40;
                case PlotCropStage.Ruined: return 30;
                case PlotCropStage.Empty: return 20;
                case PlotCropStage.Growing: return 10;
                default: return 0;
            }
        }

        public static string JobVerb(PlotCropStage stage)
        {
            switch (stage)
            {
                case PlotCropStage.Mature: return "收获";
                case PlotCropStage.Ruined: return "清理";
                case PlotCropStage.Empty: return "播种";
                case PlotCropStage.Growing: return "照料";
                default: return "农作";
            }
        }

        /// <summary>在区内选一格：优先成熟→损坏→空闲→成长；避开已被占用格。</summary>
        public static bool TryPickJobCell(
            IReadOnlyList<HostMapPlotCell> plots,
            Vector3 fromWorld,
            HashSet<int> reservedInstanceIds,
            out HostMapPlotCell cell)
        {
            cell = null;
            if (plots == null || plots.Count == 0)
                return false;

            var bestPri = -1;
            var bestDist = float.MaxValue;
            for (var i = 0; i < plots.Count; i++)
            {
                var p = plots[i];
                if (p == null || !p.IsPlantableField)
                    continue;
                var iid = p.GetInstanceID();
                if (reservedInstanceIds != null && reservedInstanceIds.Contains(iid))
                    continue;
                var pri = JobPriority(p.CropStage);
                if (pri <= 0)
                    continue;
                var d = HostFarmFieldRules.XySqrMagnitude(p.transform.position, fromWorld);
                if (pri > bestPri || (pri == bestPri && d < bestDist))
                {
                    bestPri = pri;
                    bestDist = d;
                    cell = p;
                }
            }

            return cell != null;
        }
    }
}
