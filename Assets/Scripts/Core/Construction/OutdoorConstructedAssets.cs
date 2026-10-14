using System;
using System.Collections.Generic;
using System.Globalization;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
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
        public const string PrisonDropOffKind = "prisonDropOff";
        public const string StorageRoomKind = "storageRoom";
        public const string RoomKind = "room";
        public const string BedKind = "bed";
        public const string WallSegmentKind = "wallSegment";

        public static bool IsSupportedRuntimeKind(string kind) =>
            string.Equals(kind, "grainField", StringComparison.Ordinal) ||
            string.Equals(kind, RecoverySpotKind, StringComparison.Ordinal) ||
            string.Equals(kind, PrisonDropOffKind, StringComparison.Ordinal) ||
            string.Equals(kind, StorageRoomKind, StringComparison.Ordinal) ||
            string.Equals(kind, RoomKind, StringComparison.Ordinal) ||
            string.Equals(kind, BedKind, StringComparison.Ordinal) ||
            string.Equals(kind, WallSegmentKind, StringComparison.Ordinal);
    }

    public enum RoomUsage
    {
        Residential = 0,
        Medical = 1,
        Prison = 2
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
        public int QuarterTurns { get; set; }
        public string ParentRoomId { get; set; } = string.Empty;
        public RoomUsage RoomUsage { get; set; }
        public EntityId AssignedCharacterId { get; set; }
        public EntityId ReservedCharacterId { get; set; }
        public EntityId OccupantCharacterId { get; set; }
        public string InitialAssigneeSpawnKey { get; set; } = string.Empty;
        public int StructureDurability { get; set; }

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
            !string.IsNullOrWhiteSpace(SurfaceId) &&
            (!string.IsNullOrWhiteSpace(BoundLocationId) ||
             string.Equals(Kind, OutdoorConstructedAssetSemantics.RecoverySpotKind, StringComparison.Ordinal)) &&
            OutdoorConstructedAssetSemantics.IsSupportedRuntimeKind(Kind) &&
            (!string.Equals(Kind, OutdoorConstructedAssetSemantics.StorageRoomKind, StringComparison.Ordinal) ||
             !string.IsNullOrWhiteSpace(BoundWorldSiteId)) &&
            QuarterTurns >= 0 && QuarterTurns < 4 && CellsW > 0 && CellsH > 0 &&
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
                    : string.Equals(kind, OutdoorConstructedAssetSemantics.RoomKind, StringComparison.Ordinal)
                        ? "asset:runtime:room:"
                        : string.Equals(kind, OutdoorConstructedAssetSemantics.BedKind, StringComparison.Ordinal)
                            ? "asset:runtime:bed:"
                            : string.Equals(kind, OutdoorConstructedAssetSemantics.WallSegmentKind, StringComparison.Ordinal)
                                ? "asset:runtime:wall:"
                                : "asset:runtime:farm:";
            return prefix + NextSequence.ToString(CultureInfo.InvariantCulture);
        }
        public void Clear() { _assets.Clear(); NextSequence = 1; }
        public bool TryRegister(OutdoorConstructedAssetState asset)
        {
            if (asset == null || !asset.IsValid || _assets.ContainsKey(asset.StableAssetId)) return false;
            foreach (var existing in _assets.Values)
                if ((!string.IsNullOrWhiteSpace(asset.BoundLocationId) &&
                     existing.BoundLocationId == asset.BoundLocationId &&
                     !string.Equals(asset.Kind, OutdoorConstructedAssetSemantics.BedKind, StringComparison.Ordinal) &&
                     !string.Equals(existing.Kind, OutdoorConstructedAssetSemantics.BedKind, StringComparison.Ordinal) &&
                     !string.Equals(asset.Kind, OutdoorConstructedAssetSemantics.RecoverySpotKind, StringComparison.Ordinal) &&
                     !string.Equals(existing.Kind, OutdoorConstructedAssetSemantics.RecoverySpotKind, StringComparison.Ordinal)) ||
                    (Overlaps(existing, asset) && !RoomBedGeometry.IsAllowedNestedPair(existing, asset))) return false;
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
                const string roomPrefix = "asset:runtime:room:";
                const string bedPrefix = "asset:runtime:bed:";
                const string wallPrefix = "asset:runtime:wall:";
                var prefix = id.StartsWith(farmPrefix, StringComparison.Ordinal) ? farmPrefix :
                    id.StartsWith(recoveryPrefix, StringComparison.Ordinal) ? recoveryPrefix :
                    id.StartsWith(storagePrefix, StringComparison.Ordinal) ? storagePrefix :
                    id.StartsWith(roomPrefix, StringComparison.Ordinal) ? roomPrefix :
                    id.StartsWith(bedPrefix, StringComparison.Ordinal) ? bedPrefix :
                    id.StartsWith(wallPrefix, StringComparison.Ordinal) ? wallPrefix : string.Empty;
                if (prefix.Length == 0)
                {
                    if (id.StartsWith("base:site_", StringComparison.Ordinal)) continue;
                    return false;
                }
                if (
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

    public static class RoomBedGeometry
    {
        public static bool IsRoom(OutdoorConstructedAssetState asset) => asset != null &&
            string.Equals(asset.Kind, OutdoorConstructedAssetSemantics.RoomKind, StringComparison.Ordinal);

        public static bool IsBed(OutdoorConstructedAssetState asset) => asset != null &&
            string.Equals(asset.Kind, OutdoorConstructedAssetSemantics.BedKind, StringComparison.Ordinal);

        public static bool IsAllowedNestedPair(OutdoorConstructedAssetState a, OutdoorConstructedAssetState b)
        {
            var room = IsRoom(a) ? a : IsRoom(b) ? b : null;
            var bed = IsBed(a) ? a : IsBed(b) ? b : null;
            return room != null && bed != null && bed.ParentRoomId == room.StableAssetId && FitsUsableInterior(room, bed);
        }

        public static bool FitsUsableInterior(OutdoorConstructedAssetState room, OutdoorConstructedAssetState bed)
        {
            if (!IsRoom(room) || !IsBed(bed) || room.SurfaceId != bed.SurfaceId || room.CellsW < 3 || room.CellsH < 3)
                return false;
            var cellX = room.WorldWidth / room.CellsW;
            var cellY = room.WorldHeight / room.CellsH;
            var epsilon = Math.Min(cellX, cellY) * .001f;
            return bed.WorldX >= room.WorldX + cellX - epsilon &&
                bed.WorldY >= room.WorldY + cellY - epsilon &&
                bed.WorldX + bed.WorldWidth <= room.WorldX + room.WorldWidth - cellX + epsilon &&
                bed.WorldY + bed.WorldHeight <= room.WorldY + room.WorldHeight - cellY + epsilon;
        }

        public static Result ValidatePlacement(OutdoorConstructedAssetBoard board, OutdoorConstructedAssetState candidate)
        {
            if (board == null || candidate == null)
                return Result.Failure(ErrorCode.InvalidArgument, "房间或家具位置无效。");
            if (IsBed(candidate) && !string.IsNullOrEmpty(candidate.ParentRoomId))
            {
                if (!board.Assets.TryGetValue(candidate.ParentRoomId, out var room) || !FitsUsableInterior(room, candidate))
                    return Result.Failure(ErrorCode.InvalidOperation, "床必须完整位于指定房间的可用内区。");
            }
            foreach (var existing in board.Assets.Values)
                if (OutdoorConstructedAssetBoard.Overlaps(existing, candidate) && !IsAllowedNestedPair(existing, candidate))
                    return Result.Failure(ErrorCode.InvalidOperation, "位置与已有房间或家具重叠。");
            return Result.Success();
        }
    }

    /// <summary>Room-level usage and bed assignment/reservation/occupancy authority.</summary>
    public static class RoomBedFacilityService
    {
        public static Result SetRoomUsage(SimulationWorld world, string roomId, RoomUsage usage)
        {
            if (world == null || !world.OutdoorConstructedAssets.Assets.TryGetValue(roomId ?? string.Empty, out var room) ||
                !RoomBedGeometry.IsRoom(room))
                return Result.Failure(ErrorCode.NotFound, "房间不存在。", roomId);
            if (room.RoomUsage == RoomUsage.Prison && usage != RoomUsage.Prison)
                foreach (var bed in BedsInRoom(world, roomId))
                    if (!bed.OccupantCharacterId.IsNone)
                        return Result.Failure(ErrorCode.InvalidOperation, "请先释放或转移房内囚犯。");
            room.RoomUsage = usage;
            return Result.Success();
        }

        public static Result AssignBed(SimulationWorld world, string bedId, EntityId characterId)
        {
            if (world == null || characterId.IsNone || !world.Entities.TryGet(characterId, out _) ||
                !world.OutdoorConstructedAssets.Assets.TryGetValue(bedId ?? string.Empty, out var bed) ||
                !RoomBedGeometry.IsBed(bed))
                return Result.Failure(ErrorCode.InvalidArgument, "床位或人物无效。");
            foreach (var other in Beds(world))
                if (other.AssignedCharacterId == characterId && other.StableAssetId != bed.StableAssetId)
                    other.AssignedCharacterId = EntityId.None;
            bed.AssignedCharacterId = characterId;
            return Result.Success();
        }

        public static bool HasAvailableBed(SimulationWorld world, EntityId characterId, RoomUsage usage) =>
            TryFindBed(world, characterId, usage, out _);

        public static bool TryFindBed(SimulationWorld world, EntityId characterId, RoomUsage usage,
            out OutdoorConstructedAssetState bed)
        {
            bed = null;
            if (world == null || characterId.IsNone) return false;
            foreach (var candidate in Beds(world))
            {
                if (!BedHasUsage(world, candidate, usage) ||
                    (!candidate.OccupantCharacterId.IsNone && candidate.OccupantCharacterId != characterId) ||
                    (!candidate.ReservedCharacterId.IsNone && candidate.ReservedCharacterId != characterId)) continue;
                if (candidate.AssignedCharacterId == characterId) { bed = candidate; return true; }
                if (bed == null && candidate.AssignedCharacterId.IsNone) bed = candidate;
            }
            return bed != null;
        }

        public static bool TryReserve(SimulationWorld world, EntityId characterId, RoomUsage usage,
            out OutdoorConstructedAssetState bed)
        {
            if (!TryFindBed(world, characterId, usage, out bed)) return false;
            bed.ReservedCharacterId = characterId;
            return true;
        }

        public static bool TryOccupy(SimulationWorld world, string bedId, EntityId characterId)
        {
            if (world == null || characterId.IsNone ||
                !world.OutdoorConstructedAssets.Assets.TryGetValue(bedId ?? string.Empty, out var bed) ||
                !RoomBedGeometry.IsBed(bed) ||
                (!bed.ReservedCharacterId.IsNone && bed.ReservedCharacterId != characterId) ||
                (!bed.OccupantCharacterId.IsNone && bed.OccupantCharacterId != characterId)) return false;
            bed.ReservedCharacterId = characterId;
            bed.OccupantCharacterId = characterId;
            return true;
        }

        public static void Release(SimulationWorld world, EntityId characterId)
        {
            if (world == null || characterId.IsNone) return;
            foreach (var bed in Beds(world))
            {
                if (bed.ReservedCharacterId == characterId) bed.ReservedCharacterId = EntityId.None;
                if (bed.OccupantCharacterId == characterId) bed.OccupantCharacterId = EntityId.None;
            }
        }

        public static WorldVec2 AccessPoint(OutdoorConstructedAssetState bed)
        {
            return new WorldVec2(bed.WorldX + bed.WorldWidth * .5f, bed.WorldY - Math.Max(.1f, bed.WorldHeight / bed.CellsH * .5f));
        }

        public static bool BedHasUsage(SimulationWorld world, OutdoorConstructedAssetState bed, RoomUsage usage)
        {
            if (!RoomBedGeometry.IsBed(bed)) return false;
            if (string.IsNullOrEmpty(bed.ParentRoomId)) return usage == RoomUsage.Residential;
            return world.OutdoorConstructedAssets.Assets.TryGetValue(bed.ParentRoomId, out var room) &&
                   RoomBedGeometry.IsRoom(room) && room.RoomUsage == usage;
        }

        public static IEnumerable<OutdoorConstructedAssetState> BedsInRoom(SimulationWorld world, string roomId)
        {
            foreach (var bed in Beds(world)) if (bed.ParentRoomId == roomId) yield return bed;
        }

        static IEnumerable<OutdoorConstructedAssetState> Beds(SimulationWorld world)
        {
            if (world == null) yield break;
            foreach (var asset in world.OutdoorConstructedAssets.Assets.Values)
                if (RoomBedGeometry.IsBed(asset)) yield return asset;
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
