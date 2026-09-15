using System;
using System.Collections.Generic;
using System.Globalization;
using XianXia.Core.Exploration;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Strategic;

namespace XianXia.Core.Construction
{
    public readonly struct OutdoorConstructedAssetCell
    {
        public readonly string SurfaceId;
        public readonly float WorldX;
        public readonly float WorldY;

        public OutdoorConstructedAssetCell(string surfaceId, float worldX, float worldY)
        {
            SurfaceId = surfaceId ?? string.Empty;
            WorldX = worldX;
            WorldY = worldY;
        }
    }

    public static class OutdoorConstructedAssetSemantics
    {
        public const string RecoverySpotKind = "recoverySpot";
        public const string StorageRoomKind = "storageRoom";

        public static bool IsSupportedRuntimeKind(string kind) =>
            string.Equals(kind, "grainField", StringComparison.Ordinal) ||
            string.Equals(kind, RecoverySpotKind, StringComparison.Ordinal) ||
            string.Equals(kind, StorageRoomKind, StringComparison.Ordinal);
    }

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
        /// <summary>Stable Site identity for facilities whose meaning belongs to one WorldSite.</summary>
        public string BoundWorldSiteId { get; set; } = string.Empty;

        public IEnumerable<OutdoorConstructedAssetCell> EnumerateGridCells()
        {
            for (var y = 0; y < CellsH; y++)
            for (var x = 0; x < CellsW; x++)
                yield return new OutdoorConstructedAssetCell(SurfaceId,
                    WorldX + (x + .5f) * WorldWidth / CellsW,
                    WorldY + (y + .5f) * WorldHeight / CellsH);
        }

        public IEnumerable<OutdoorAdministrativeAssetAnchor> AdministrativeCellAnchors()
        {
            if (!OutdoorAdministrativeAssetSemantics.IsAdministrativeAssetKind(Kind))
                yield break;
            var index = 0;
            foreach (var cell in EnumerateGridCells())
            {
                var x = index % CellsW;
                var y = index / CellsW;
                index++;
                yield return new OutdoorAdministrativeAssetAnchor(
                    OutdoorStatefulObjectId.ForCell(StableAssetId, x, y), cell.SurfaceId,
                    cell.WorldX, cell.WorldY, Kind, BoundLocationId);
            }
        }

        public IEnumerable<OutdoorAdministrativeAssetAnchor> CellAnchors() => AdministrativeCellAnchors();

        public bool IsValid => !string.IsNullOrWhiteSpace(StableAssetId) && !string.IsNullOrWhiteSpace(BuildingId) &&
            !string.IsNullOrWhiteSpace(SurfaceId) && !string.IsNullOrWhiteSpace(BoundLocationId) &&
            OutdoorConstructedAssetSemantics.IsSupportedRuntimeKind(Kind) &&
            (!string.Equals(Kind, OutdoorConstructedAssetSemantics.StorageRoomKind, StringComparison.Ordinal) ||
             !string.IsNullOrWhiteSpace(BoundWorldSiteId)) && CellsW > 0 && CellsH > 0 &&
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
        public string NextId => NextIdForKind("grainField");
        public string NextRecoveryId => NextIdForKind(OutdoorConstructedAssetSemantics.RecoverySpotKind);
        public string NextIdForKind(string kind)
        {
            var prefix = string.Equals(kind, OutdoorConstructedAssetSemantics.RecoverySpotKind, StringComparison.Ordinal)
                ? "asset:runtime:recovery:"
                : string.Equals(kind, OutdoorConstructedAssetSemantics.StorageRoomKind, StringComparison.Ordinal)
                    ? "asset:runtime:storage:"
                    : "asset:runtime:farm:";
            return prefix + NextSequence.ToString(CultureInfo.InvariantCulture);
        }
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
                const string farmPrefix = "asset:runtime:farm:";
                const string recoveryPrefix = "asset:runtime:recovery:";
                const string storagePrefix = "asset:runtime:storage:";
                var prefix = id.StartsWith(farmPrefix, StringComparison.Ordinal) ? farmPrefix :
                    id.StartsWith(recoveryPrefix, StringComparison.Ordinal) ? recoveryPrefix :
                    id.StartsWith(storagePrefix, StringComparison.Ordinal) ? storagePrefix : string.Empty;
                if (prefix.Length == 0 ||
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

    public static class OutdoorFactionConstructionAuthorizationService
    {
        public static Result Validate(SimulationWorld world, string factionId, OutdoorConstructedAssetState candidate)
        {
            if (string.IsNullOrWhiteSpace(factionId)) return Result.Failure(ErrorCode.InvalidArgument, "建造势力无效。");
            var physical = ValidatePhysicalPlacement(world, candidate);
            if (physical.IsFailure) return physical;
            foreach (var cell in candidate.EnumerateGridCells())
            {
                WorldSiteAdministrativeControlResolver.TryResolve(world, cell.SurfaceId, cell.WorldX, cell.WorldY, out var site, out _);
                if (site == null)
                    return Result.Failure(ErrorCode.InvalidOperation, "只能建在己方实际行政控制范围内。");
                if (!string.Equals(site.OwnerFactionId, factionId, StringComparison.Ordinal))
                    return Result.Failure(ErrorCode.InvalidOperation, "此处属于其他势力的实际行政控制范围。");
            }
            return Result.Success();
        }

        public static Result ValidatePhysicalPlacement(SimulationWorld world, OutdoorConstructedAssetState candidate)
        {
            if (world == null || candidate == null || !candidate.IsValid ||
                !world.SurfaceSpatial.TryGet(candidate.SurfaceId, out var metric))
                return Result.Failure(ErrorCode.InvalidArgument, "建筑位置或 Surface 无效。");
            var epsilon = metric.CellSize * .001f;
            if (Math.Abs(candidate.WorldWidth - candidate.CellsW * metric.CellSize) > epsilon ||
                Math.Abs(candidate.WorldHeight - candidate.CellsH * metric.CellSize) > epsilon ||
                Math.Abs((candidate.WorldX - metric.OriginWorldX) / metric.CellSize - Math.Round((candidate.WorldX - metric.OriginWorldX) / metric.CellSize)) > .001f ||
                Math.Abs((candidate.WorldY - metric.OriginWorldY) / metric.CellSize - Math.Round((candidate.WorldY - metric.OriginWorldY) / metric.CellSize)) > .001f)
                return Result.Failure(ErrorCode.InvalidArgument, "建筑必须对齐 Surface 网格。");
            foreach (var cell in candidate.EnumerateGridCells())
                if (!metric.ContainsWorldPosition(cell.WorldX, cell.WorldY))
                    return Result.Failure(ErrorCode.InvalidArgument, "建筑超出有效 Surface。");
            return Result.Success();
        }
    }

    public static class OutdoorAdministrativeConstructionAuthorizationService
    {
        public static Result Validate(SimulationWorld world, string factionId, OutdoorConstructedAssetState candidate)
            => OutdoorFactionConstructionAuthorizationService.Validate(world, factionId, candidate);

        public static Result ValidatePhysicalPlacement(SimulationWorld world, OutdoorConstructedAssetState candidate)
            => OutdoorFactionConstructionAuthorizationService.ValidatePhysicalPlacement(world, candidate);
    }
}
