using System;
using System.Collections.Generic;
using System.Globalization;
using XianXia.Core.Exploration;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Construction
{
    /// <summary>Persistent physical placement. WorldX/Y are the lower-left corner; no manager is stored.</summary>
    public sealed class OutdoorConstructedAssetState
    {
        public string StableAssetId { get; set; } = string.Empty;
        public string BuildingId { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string SurfaceId { get; set; } = string.Empty;
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        public float WorldWidth { get; set; }
        public float WorldHeight { get; set; }
        public int CellsW { get; set; }
        public int CellsH { get; set; }
        public string BoundLocationId { get; set; } = string.Empty;

        public IEnumerable<OutdoorAdministrativeAssetAnchor> CellAnchors()
        {
            for (var y = 0; y < CellsH; y++)
            for (var x = 0; x < CellsW; x++)
                yield return new OutdoorAdministrativeAssetAnchor(
                    OutdoorStatefulObjectId.ForCell(StableAssetId, x, y), SurfaceId,
                    WorldX + (x + .5f) * WorldWidth / CellsW,
                    WorldY + (y + .5f) * WorldHeight / CellsH, Kind);
        }

        public bool IsValid => !string.IsNullOrWhiteSpace(StableAssetId) && !string.IsNullOrWhiteSpace(BuildingId) &&
            !string.IsNullOrWhiteSpace(SurfaceId) && !string.IsNullOrWhiteSpace(BoundLocationId) &&
            OutdoorAdministrativeAssetSemantics.IsAdministrativeAssetKind(Kind) && CellsW > 0 && CellsH > 0 &&
            (long)CellsW * CellsH <= int.MaxValue && Finite(WorldX) && Finite(WorldY) &&
            Finite(WorldWidth) && Finite(WorldHeight) && WorldWidth > 0 && WorldHeight > 0 &&
            Finite(WorldX + WorldWidth) && Finite(WorldY + WorldHeight);
        static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
    }

    public sealed class OutdoorConstructedAssetBoard
    {
        readonly Dictionary<string, OutdoorConstructedAssetState> _assets =
            new Dictionary<string, OutdoorConstructedAssetState>(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, OutdoorConstructedAssetState> Assets => _assets;
        public long NextSequence { get; private set; } = 1;
        public string NextId => "asset:runtime:farm:" + NextSequence.ToString(CultureInfo.InvariantCulture);
        public void Clear() { _assets.Clear(); NextSequence = 1; }
        public bool TryRegister(OutdoorConstructedAssetState asset)
        {
            if (asset == null || !asset.IsValid || _assets.ContainsKey(asset.StableAssetId)) return false;
            foreach (var existing in _assets.Values)
                if (existing.BoundLocationId == asset.BoundLocationId || Overlaps(existing, asset)) return false;
            _assets.Add(asset.StableAssetId, asset);
            return true;
        }
        internal void Remove(string id) => _assets.Remove(id);
        internal void AdvanceSequence() { NextSequence = checked(NextSequence + 1); }
        public bool RestoreSequence(long sequence)
        {
            if (sequence < 1 || sequence == long.MaxValue) return false;
            foreach (var id in _assets.Keys)
            {
                const string prefix = "asset:runtime:farm:";
                if (!id.StartsWith(prefix, StringComparison.Ordinal) ||
                    !long.TryParse(id.Substring(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ||
                    n < 1 || n >= sequence) return false;
            }
            NextSequence = sequence;
            return true;
        }
        public static bool Overlaps(OutdoorConstructedAssetState a, OutdoorConstructedAssetState b)
        {
            var epsilon = Math.Min(a.WorldWidth / a.CellsW, b.WorldWidth / b.CellsW) * .001f;
            return a.SurfaceId == b.SurfaceId && a.WorldX < b.WorldX + b.WorldWidth - epsilon &&
                b.WorldX < a.WorldX + a.WorldWidth - epsilon && a.WorldY < b.WorldY + b.WorldHeight - epsilon &&
                b.WorldY < a.WorldY + a.WorldHeight - epsilon;
        }
    }

    public static class OutdoorAdministrativeConstructionAuthorizationService
    {
        public static Result Validate(SimulationWorld world, string factionId, OutdoorConstructedAssetState candidate)
        {
            if (string.IsNullOrWhiteSpace(factionId)) return Result.Failure(ErrorCode.InvalidArgument, "建造势力无效。");
            var physical = ValidatePhysicalPlacement(world, candidate);
            if (physical.IsFailure) return physical;
            foreach (var cell in candidate.CellAnchors())
            {
                WorldSiteAdministrativeControlResolver.TryResolve(world, cell.SurfaceId, cell.WorldX, cell.WorldY, out var site, out _);
                if (site == null)
                    return Result.Failure(ErrorCode.InvalidOperation, "农田只能建在己方实际行政控制范围内。");
                if (!string.Equals(site.OwnerFactionId, factionId, StringComparison.Ordinal))
                    return Result.Failure(ErrorCode.InvalidOperation, "此处属于其他势力的实际行政控制范围。");
            }
            return Result.Success();
        }

        public static Result ValidatePhysicalPlacement(SimulationWorld world, OutdoorConstructedAssetState candidate)
        {
            if (world == null || candidate == null || !candidate.IsValid ||
                !world.SurfaceSpatial.TryGet(candidate.SurfaceId, out var metric))
                return Result.Failure(ErrorCode.InvalidArgument, "农田位置或 Surface 无效。");
            var epsilon = metric.CellSize * .001f;
            if (Math.Abs(candidate.WorldWidth - candidate.CellsW * metric.CellSize) > epsilon ||
                Math.Abs(candidate.WorldHeight - candidate.CellsH * metric.CellSize) > epsilon ||
                Math.Abs((candidate.WorldX - metric.OriginWorldX) / metric.CellSize - Math.Round((candidate.WorldX - metric.OriginWorldX) / metric.CellSize)) > .001f ||
                Math.Abs((candidate.WorldY - metric.OriginWorldY) / metric.CellSize - Math.Round((candidate.WorldY - metric.OriginWorldY) / metric.CellSize)) > .001f)
                return Result.Failure(ErrorCode.InvalidArgument, "农田必须对齐 Surface 网格。");
            foreach (var cell in candidate.CellAnchors())
            {
                if (!metric.ContainsWorldPosition(cell.WorldX, cell.WorldY))
                    return Result.Failure(ErrorCode.InvalidArgument, "农田超出有效 Surface。");
            }
            return Result.Success();
        }
    }
}
