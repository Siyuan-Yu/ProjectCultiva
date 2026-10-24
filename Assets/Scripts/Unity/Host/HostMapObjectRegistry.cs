using System.Collections.Generic;
using UnityEngine;

namespace XianXia.Unity.Host
{
    /// <summary>地图刷出时可检视／可破坏物体的运行时登记（随 TileMap Rebuild 清空）。</summary>
    public static class HostMapObjectRegistry
    {
        static readonly List<HostMapPlotCell> Plots = new List<HostMapPlotCell>(256);
        static readonly List<HostMapDestructible> Destructibles = new List<HostMapDestructible>(128);
        static readonly Dictionary<Object, string> OwnerByObject = new Dictionary<Object, string>();
        // 逐格注册时用 instance-id 集合做 O(1) 重复检查（旧 List.Contains 会把 chunk build 拉成 O(N²)）。
        static readonly HashSet<int> PlotIds = new HashSet<int>();
        static readonly HashSet<int> DestructibleIds = new HashSet<int>();
        // Loaded static props only. Rebuild lazily after chunk/geometry changes, never per hover.
        const float PickBucketSize = 4f;
        static readonly Dictionary<Vector2Int, List<int>> DestructibleBuckets = new Dictionary<Vector2Int, List<int>>();
        static readonly Stack<List<int>> BucketLists = new Stack<List<int>>();
        static readonly List<int> BroadDestructibles = new List<int>();
        static readonly Dictionary<Vector2Int, List<int>> PlotBuckets = new Dictionary<Vector2Int, List<int>>();
        static readonly List<int> BroadPlots = new List<int>();
        static readonly Dictionary<string, HostMapPlotCell> BedsById =
            new Dictionary<string, HostMapPlotCell>(System.StringComparer.Ordinal);
        static bool _plotIndexDirty = true;
        static bool _destructibleIndexDirty = true;
        public static void InvalidatePlotGeometry() => _plotIndexDirty = true;
        public static void InvalidateDestructibleGeometry() => _destructibleIndexDirty = true;

        static string _currentOwner = string.Empty;

        public static IReadOnlyList<HostMapPlotCell> AllPlots => Plots;
        public static IReadOnlyList<HostMapDestructible> AllDestructibles => Destructibles;

        /// <summary>Legacy single-map wrapper. New incremental users must use owner APIs.</summary>
        public static void BeginRebuild()
        {
            ClearAll();
        }

        public static void ClearAll()
        {
            Plots.Clear();
            Destructibles.Clear();
            ClearPlotIndex();
            _plotIndexDirty = true;
            ClearDestructibleIndex();
            _destructibleIndexDirty = true;
            OwnerByObject.Clear();
            PlotIds.Clear();
            DestructibleIds.Clear();
            _currentOwner = string.Empty;
        }

        public static void BeginOwnerBuild(string ownerKey)
        {
            _currentOwner = ownerKey ?? string.Empty;
            RemoveOwner(_currentOwner);
        }

        /// <summary>对称 API（供 chunk build 收尾）；register 已无逐格 rebuild，这里无事可做。</summary>
        public static void EndOwnerBuild()
        {
        }

        public static void RemoveOwner(string ownerKey)
        {
            _plotIndexDirty = true;
            _destructibleIndexDirty = true;
            ownerKey = ownerKey ?? string.Empty;
            for (var i = Plots.Count - 1; i >= 0; i--)
                if (Plots[i] == null || IsOwnedBy(Plots[i], ownerKey))
                {
                    if (Plots[i] != null)
                    {
                        PlotIds.Remove(Plots[i].GetInstanceID());
                        OwnerByObject.Remove(Plots[i]);
                    }
                    Plots.RemoveAt(i);
                }
            for (var i = Destructibles.Count - 1; i >= 0; i--)
                if (Destructibles[i] == null || IsOwnedBy(Destructibles[i], ownerKey))
                {
                    if (Destructibles[i] != null)
                    {
                        DestructibleIds.Remove(Destructibles[i].GetInstanceID());
                        OwnerByObject.Remove(Destructibles[i]);
                    }
                    Destructibles.RemoveAt(i);
                    _destructibleIndexDirty = true;
                }
        }

        public static void Register(HostMapPlotCell plot)
            => Register(_currentOwner, plot);

        public static void Register(string ownerKey, HostMapPlotCell plot)
        {
            if (plot == null)
                return;
            // Configure can update the stable ID/kind of an already registered component.
            _plotIndexDirty = true;
            OwnerByObject[plot] = ownerKey ?? string.Empty;
            if (!PlotIds.Add(plot.GetInstanceID()))
                return;
            Plots.Add(plot);
        }

        public static void Register(HostMapDestructible d)
            => Register(_currentOwner, d);

        public static void Register(string ownerKey, HostMapDestructible d)
        {
            if (d == null)
                return;
            if (!DestructibleIds.Add(d.GetInstanceID()))
                return;
            Destructibles.Add(d);
            _destructibleIndexDirty = true;
            OwnerByObject[d] = ownerKey ?? string.Empty;
        }

        public static void Unregister(HostMapPlotCell plot)
        {
            if (plot == null) return;
            _plotIndexDirty = true;
            PlotIds.Remove(plot.GetInstanceID());
            Plots.Remove(plot);
            OwnerByObject.Remove(plot);
        }

        public static void Unregister(HostMapDestructible d)
        {
            if (d == null)
                return;
            DestructibleIds.Remove(d.GetInstanceID());
            Destructibles.Remove(d);
            _destructibleIndexDirty = true;
            OwnerByObject.Remove(d);
        }

        public static bool TryPickPlot(Vector3 worldPoint, out HostMapPlotCell plot)
        {
            EnsurePlotIndex();
            plot = null;
            var bestArea = float.MaxValue;
            var bestIndex = -1;
            PlotBuckets.TryGetValue(PickBucket(worldPoint.x, worldPoint.y), out var candidates);
            PickPlotFrom(candidates, worldPoint, ref plot, ref bestArea, ref bestIndex);
            PickPlotFrom(BroadPlots, worldPoint, ref plot, ref bestArea, ref bestIndex);
            return plot != null;
        }

        /// <summary>First registered matching bed, as in the original forward plot scan.</summary>
        public static bool TryGetBedPlot(string stableId, out HostMapPlotCell plot)
        {
            EnsurePlotIndex();
            plot = null;
            if (string.IsNullOrEmpty(stableId) || !BedsById.TryGetValue(stableId, out plot)) return false;
            if (plot != null) return true;
            // A directly destroyed view can precede its owner's unload notification.
            // Rebuild once so a surviving duplicate retains the old forward-scan behavior.
            _plotIndexDirty = true;
            EnsurePlotIndex();
            return BedsById.TryGetValue(stableId, out plot) && plot != null;
        }

        static void ClearPlotIndex()
        {
            foreach (var list in PlotBuckets.Values) { list.Clear(); BucketLists.Push(list); }
            PlotBuckets.Clear();
            BroadPlots.Clear();
            BedsById.Clear();
        }

        static void EnsurePlotIndex()
        {
            if (!_plotIndexDirty) return;
            ClearPlotIndex();
            for (var i = 0; i < Plots.Count; i++)
            {
                var plot = Plots[i];
                if (plot == null) continue;
                if (plot.IsBed && !string.IsNullOrEmpty(plot.StableCellId) && !BedsById.ContainsKey(plot.StableCellId))
                    BedsById.Add(plot.StableCellId, plot);
                if (!plot.HasInteractionBounds) continue;
                var bounds = plot.InteractionBounds;
                var min = PickBucket(bounds.xMin, bounds.yMin);
                var max = PickBucket(bounds.xMax, bounds.yMax);
                var width = (long)max.x - min.x + 1;
                var height = (long)max.y - min.y + 1;
                // Large rooms retain exact picking without unbounded index expansion.
                if (width <= 0 || height <= 0 || width > 256 || height > 256 || width * height > 256)
                { BroadPlots.Add(i); continue; }
                for (var dy = 0; dy < height; dy++)
                for (var dx = 0; dx < width; dx++)
                {
                    var key = new Vector2Int(min.x + dx, min.y + dy);
                    if (!PlotBuckets.TryGetValue(key, out var list))
                        PlotBuckets.Add(key, list = BucketLists.Count > 0 ? BucketLists.Pop() : new List<int>(8));
                    list.Add(i);
                }
            }
            _plotIndexDirty = false;
        }

        static void PickPlotFrom(List<int> candidates, Vector3 point, ref HostMapPlotCell target,
            ref float bestArea, ref int bestIndex)
        {
            if (candidates == null) return;
            for (var n = candidates.Count - 1; n >= 0; n--)
            {
                var i = candidates[n];
                var plot = Plots[i];
                if (plot == null || !plot.gameObject.activeInHierarchy || !plot.ContainsInteractionPoint(point)) continue;
                var area = plot.InteractionBoundsArea;
                if (area >= float.MaxValue || area > bestArea || area == bestArea && i <= bestIndex) continue;
                target = plot; bestArea = area; bestIndex = i;
            }
        }

        static Vector2Int PickBucket(float x, float y) =>
            new Vector2Int(Mathf.FloorToInt(x / PickBucketSize), Mathf.FloorToInt(y / PickBucketSize));

        static void ClearDestructibleIndex()
        {
            foreach (var list in DestructibleBuckets.Values) { list.Clear(); BucketLists.Push(list); }
            DestructibleBuckets.Clear();
            BroadDestructibles.Clear();
        }

        static void EnsureDestructibleIndex()
        {
            if (!_destructibleIndexDirty) return;
            ClearDestructibleIndex();
            for (var i = 0; i < Destructibles.Count; i++)
            {
                var item = Destructibles[i];
                if (item == null || item.IsDestroyed || !item.HasInteractionBounds) continue;
                var bounds = item.InteractionBounds;
                var min = PickBucket(bounds.xMin, bounds.yMin);
                var max = PickBucket(bounds.xMax, bounds.yMax);
                // Preserve large props without expanding an accidentally huge index rectangle.
                var count = ((long)max.x-min.x+1) * ((long)max.y-min.y+1);
                if (count <= 0 || count > 256) { BroadDestructibles.Add(i); continue; }
                for (var y = min.y; y <= max.y; y++)
                for (var x = min.x; x <= max.x; x++)
                {
                    var key = new Vector2Int(x,y);
                    if (!DestructibleBuckets.TryGetValue(key, out var list))
                        DestructibleBuckets.Add(key, list = BucketLists.Count > 0 ? BucketLists.Pop() : new List<int>(8));
                    list.Add(i);
                }
            }
            _destructibleIndexDirty = false;
        }

        public static bool TryPickDestructible(Vector3 worldPoint, out HostMapDestructible target)
        {
            EnsureDestructibleIndex();
            target = null;
            var bestArea = float.MaxValue;
            var bestIndex = -1;
            DestructibleBuckets.TryGetValue(PickBucket(worldPoint.x, worldPoint.y), out var candidates);
            PickFrom(candidates, worldPoint, ref target, ref bestArea, ref bestIndex);
            PickFrom(BroadDestructibles, worldPoint, ref target, ref bestArea, ref bestIndex);
            return target != null;
        }

        static void PickFrom(List<int> candidates, Vector3 point, ref HostMapDestructible target,
            ref float bestArea, ref int bestIndex)
        {
            if (candidates == null) return;
            for (var n = candidates.Count-1; n >= 0; n--)
            {
                var i = candidates[n];
                var item = Destructibles[i];
                if (item == null || item.IsDestroyed || !item.gameObject.activeInHierarchy ||
                    !item.ContainsInteractionPoint(point)) continue;
                var area = item.InteractionBoundsArea;
                if (area > bestArea || area == bestArea && i <= bestIndex) continue;
                target = item; bestArea = area; bestIndex = i;
            }
        }

        /// <summary>Nearest destructible within radius; optional tree-only filter.</summary>
        public static bool TryFindNearestDestructible(
            Vector3 worldPoint,
            float maxRadius,
            out HostMapDestructible target,
            bool treesOnly = false,
            HostMapDestructible exclude = null)
        {
            target = null;
            var best = maxRadius * maxRadius;
            var excludeId = exclude != null ? exclude.GetInstanceID() : 0;
            for (var i = Destructibles.Count - 1; i >= 0; i--)
            {
                var d = Destructibles[i];
                if (d == null || d.IsDestroyed)
                {
                    if (d != null)
                        DestructibleIds.Remove(d.GetInstanceID());
                    Destructibles.RemoveAt(i);
                    _destructibleIndexDirty = true;
                    continue;
                }

                if (!d.gameObject.activeInHierarchy) continue;
                if (treesOnly && !d.IsTree)
                    continue;
                if (excludeId != 0 && d.GetInstanceID() == excludeId)
                    continue;

                var d2 = (d.transform.position - worldPoint).sqrMagnitude;
                if (d2 > best)
                    continue;
                best = d2;
                target = d;
            }

            return target != null;
        }

        static bool IsOwnedBy(Object item, string ownerKey) =>
            item != null && OwnerByObject.TryGetValue(item, out var owner) &&
            string.Equals(owner, ownerKey, System.StringComparison.Ordinal);
    }
}
