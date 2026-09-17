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
        Loot = 3,
        Recovery = 4,
        Storage = 5
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
        static readonly Dictionary<string, List<HostInteractSpot>> DynamicByOwner =
            new Dictionary<string, List<HostInteractSpot>>(System.StringComparer.Ordinal);
        static readonly HostInteractSpot[] Empty = System.Array.Empty<HostInteractSpot>();
        static bool _flattenDirty;
        static string _currentOwner = string.Empty;

        /// <summary>
        /// flatten／index 的代数。每次真正重建 Dynamic 时递增；NPC 日程用它判断缓存的目标几何
        /// 是否过期（而不是每帧重算 interact spot）。
        /// </summary>
        public static int LayoutGeneration { get; private set; }

        public static bool HasDynamicPlots
        {
            get { EnsureFlattened(); return Dynamic.Count > 0; }
        }

        /// <summary>当前已 flatten 的互动格数量（性能诊断）。</summary>
        public static int LoadedSpotCount => Dynamic.Count;

        /// <summary>
        /// 只返回当前 Surface 或独立 LocalMap 已生成的交互点。
        /// </summary>
        public static IReadOnlyList<HostInteractSpot> GetSpots(SimulationWorld world)
        {
            EnsureFlattened();
            if (Dynamic.Count > 0)
                return Dynamic;
            return Empty;
        }

        public static void BeginLayoutRebuild()
        {
            ClearAll();
        }

        public static void ClearAll()
        {
            Dynamic.Clear();
            DynamicByOwner.Clear();
            _flattenDirty = false;
            _currentOwner = string.Empty;
            LayoutGeneration++;
        }

        public static void BeginOwnerBuild(string ownerKey)
        {
            _currentOwner = ownerKey ?? string.Empty;
            RemoveOwner(_currentOwner);
        }

        /// <summary>
        /// 批量 owner build 结束：flatten/index 只重建一次。
        /// Continuous chunk build 会一次注册几十／上百个互动格；逐格 rebuild 是 O(N²)，
        /// 会在 chunk 边界 streaming 时形成明显卡顿尖峰。
        /// </summary>
        public static void EndOwnerBuild() => _flattenDirty = true;

        public static void RemoveOwner(string ownerKey)
        {
            ownerKey = ownerKey ?? string.Empty;
            if (!DynamicByOwner.Remove(ownerKey))
                return;
            _flattenDirty = true;
        }

        public static void RegisterPlot(HostInteractSpot spot) => RegisterPlot(_currentOwner, spot);

        public static void RegisterPlot(string ownerKey, HostInteractSpot spot)
        {
            ownerKey = ownerKey ?? string.Empty;
            if (!DynamicByOwner.TryGetValue(ownerKey, out var list))
            {
                list = new List<HostInteractSpot>();
                DynamicByOwner[ownerKey] = list;
            }
            if (!list.Contains(spot))
                list.Add(spot);
            // 只标脏；真正的 flatten 在 EndOwnerBuild／下次 query 时做一次。
            _flattenDirty = true;
        }

        static void EnsureFlattened()
        {
            if (!_flattenDirty)
                return;
            _flattenDirty = false;
            LayoutGeneration++;
            RebuildDynamicFlattened();
        }

        static void RebuildDynamicFlattened()
        {
            Dynamic.Clear();
            foreach (var owner in DynamicByOwner)
                Dynamic.AddRange(owner.Value);
        }

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

        /// <summary>
        /// 第 slotIndex 个匹配 spot。**零分配**：先数匹配数，再用一次遍历取第 n 个
        /// （旧实现每次调用 new List + Add，NPC 日程每帧 query 都会产生 GC）。
        /// </summary>
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
            var list = GetSpots(world);
            var count = 0;
            for (var i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s.Kind != kind)
                    continue;
                if (!string.Equals(s.LocationId, locationId, System.StringComparison.Ordinal))
                    continue;
                count++;
            }

            if (count == 0)
                return false;
            var idx = slotIndex % count;
            if (idx < 0)
                idx += count;
            var seen = 0;
            for (var i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (s.Kind != kind)
                    continue;
                if (!string.Equals(s.LocationId, locationId, System.StringComparison.Ordinal))
                    continue;
                if (seen++ != idx)
                    continue;
                spot = s;
                return true;
            }

            return false;
        }

        public static Vector3 RingOffset(int slotIndex)
        {
            var a = slotIndex * 2.399963f;
            var r = 0.55f + (slotIndex % 3) * 0.35f;
            return new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
        }
    }
}
