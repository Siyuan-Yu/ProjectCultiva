using System;
using System.Collections.Generic;

namespace XianXia.Core.Exploration
{
    public enum OutdoorFarmCropStage
    {
        Empty = 0,
        Growing = 1,
        Mature = 2,
        Ruined = 3
    }

    /// <summary>Canonical physical position used only to derive current administration.</summary>
    public sealed class OutdoorAdministrativeAssetAnchor
    {
        public OutdoorAdministrativeAssetAnchor(
            string stableAssetId, string surfaceId, float worldX, float worldY, string kind,
            string boundLocationId = null)
        {
            StableAssetId = stableAssetId ?? string.Empty;
            SurfaceId = surfaceId ?? string.Empty;
            WorldX = worldX;
            WorldY = worldY;
            Kind = kind ?? string.Empty;
            BoundLocationId = boundLocationId ?? string.Empty;
        }

        public string StableAssetId { get; }
        public string SurfaceId { get; }
        public float WorldX { get; }
        public float WorldY { get; }
        public string Kind { get; }
        /// <summary>Physical/content grouping identity. Never a manager or owner.</summary>
        public string BoundLocationId { get; }
    }

    /// <summary>Authored/runtime-placement-derived index. It is rebuilt after load and never persisted.</summary>
    public sealed class OutdoorAdministrativeAssetAnchorBoard
    {
        readonly Dictionary<string, OutdoorAdministrativeAssetAnchor> _anchors =
            new Dictionary<string, OutdoorAdministrativeAssetAnchor>(StringComparer.Ordinal);
        readonly Dictionary<string, List<string>> _idsByLocation =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, OutdoorAdministrativeAssetAnchor> Anchors => _anchors;

        public void Clear() { _anchors.Clear(); _idsByLocation.Clear(); }
        public bool Remove(string stableAssetId)
        {
            if (!_anchors.TryGetValue(stableAssetId, out var anchor) || !_anchors.Remove(stableAssetId)) return false;
            if (!string.IsNullOrEmpty(anchor.BoundLocationId) && _idsByLocation.TryGetValue(anchor.BoundLocationId, out var ids))
            {
                ids.Remove(stableAssetId);
                if (ids.Count == 0) _idsByLocation.Remove(anchor.BoundLocationId);
            }
            return true;
        }

        public bool TryRegister(OutdoorAdministrativeAssetAnchor anchor)
        {
            if (anchor == null || string.IsNullOrWhiteSpace(anchor.StableAssetId) ||
                string.IsNullOrWhiteSpace(anchor.SurfaceId) || string.IsNullOrWhiteSpace(anchor.Kind) ||
                !OutdoorAdministrativeAssetSemantics.IsAdministrativeAssetKind(anchor.Kind) ||
                float.IsNaN(anchor.WorldX) || float.IsInfinity(anchor.WorldX) ||
                float.IsNaN(anchor.WorldY) || float.IsInfinity(anchor.WorldY))
                return false;
            if (_anchors.ContainsKey(anchor.StableAssetId))
                return false;
            _anchors.Add(anchor.StableAssetId, anchor);
            if (!string.IsNullOrEmpty(anchor.BoundLocationId))
            {
                if (!_idsByLocation.TryGetValue(anchor.BoundLocationId, out var ids))
                { ids = new List<string>(); _idsByLocation.Add(anchor.BoundLocationId, ids); }
                var index = ids.BinarySearch(anchor.StableAssetId, StringComparer.Ordinal);
                ids.Insert(index < 0 ? ~index : index, anchor.StableAssetId);
            }
            return true;
        }

        public bool TryGetByLocation(string locationId, out IReadOnlyList<OutdoorAdministrativeAssetAnchor> anchors)
        {
            anchors = null;
            if (string.IsNullOrEmpty(locationId) || !_idsByLocation.TryGetValue(locationId, out var ids)) return false;
            var result = new List<OutdoorAdministrativeAssetAnchor>(ids.Count);
            for (var i = 0; i < ids.Count; i++)
                if (_anchors.TryGetValue(ids[i], out var anchor)) result.Add(anchor);
            anchors = result;
            return result.Count > 0;
        }

        public bool TryGet(string stableAssetId, out OutdoorAdministrativeAssetAnchor anchor)
        {
            anchor = null;
            return !string.IsNullOrEmpty(stableAssetId) &&
                   _anchors.TryGetValue(stableAssetId, out anchor) && anchor != null;
        }


    }

    /// <summary>Domain identity semantics shared by Data bootstrap and Host presentation.</summary>
    public static class OutdoorStatefulObjectSemantics
    {
        public static bool IsFarmPlotKind(string kind) =>
            string.Equals(kind, "herbField", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "grainField", StringComparison.OrdinalIgnoreCase);

        public static bool IsStatefulKind(string kind) =>
            IsFarmPlotKind(kind) || IsDestructibleKind(kind);

        public static bool UsesPerCellIdentity(string kind) =>
            IsFarmPlotKind(kind) || IsPerCellDestructibleKind(kind);

        public static bool IsDestructibleKind(string kind) =>
            string.Equals(kind, "wall", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "treeS", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "treeM", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "treeL", StringComparison.OrdinalIgnoreCase);

        public static bool IsPerCellDestructibleKind(string kind) =>
            string.Equals(kind, "wall", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Explicit opt-in for objects that receive organized Site administration.
    /// Persistent world state alone never grants administrative-asset semantics.
    /// </summary>
    public static class OutdoorAdministrativeAssetSemantics
    {
        public static bool IsAdministrativeAssetKind(string kind) =>
            string.Equals(kind, "herbField", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "grainField", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>WorldTick authority for natural crop growth; independent of manager and streaming.</summary>
    public static class OutdoorFarmGrowthService
    {
        public const float PassiveGrowthPerWorldTick = 0.012f;

        public static int Advance(Simulation.SimulationWorld world, ulong ticks = 1)
        {
            if (world?.OutdoorStatefulObjects == null || ticks == 0)
                return 0;
            var ids = new List<string>(world.OutdoorStatefulObjects.FarmPlots.Keys);
            var changed = 0;
            for (var i = 0; i < ids.Count; i++)
            {
                if (!world.OutdoorStatefulObjects.TryGetFarmPlot(ids[i], out var state) ||
                    state.Stage != OutdoorFarmCropStage.Growing)
                    continue;
                var growth = Math.Min(1f, state.Growth + PassiveGrowthPerWorldTick * ticks);
                var stage = growth >= 1f ? OutdoorFarmCropStage.Mature : OutdoorFarmCropStage.Growing;
                world.OutdoorStatefulObjects.SetFarmPlot(ids[i], state.CropId, stage, growth);
                changed++;
            }
            return changed;
        }
    }
}
