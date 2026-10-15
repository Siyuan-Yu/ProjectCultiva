using System;
using System.Collections.Generic;
using System.Globalization;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Core.Npc;
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

        /// <summary>
        /// Authored placements that are normalized onto the constructed-asset board at bootstrap.
        /// Legacy authored grain fields remain Surface placements; player-built grain fields still
        /// use the board through ConstructionService.
        /// </summary>
        public static bool IsAuthoredRuntimeAssetKind(string kind) =>
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
            if (IsBed(candidate) && !TryResolveAccessPoint(board, candidate, out _))
                return Result.Failure(ErrorCode.InvalidOperation, "床边没有位于房间净内区的合法使用格。");
            return Result.Success();
        }

        public static bool TryResolveAccessPoint(
            OutdoorConstructedAssetBoard board,
            OutdoorConstructedAssetState bed,
            out WorldVec2 point)
        {
            point = default;
            if (board == null || !IsBed(bed) || bed.CellsW <= 0 || bed.CellsH <= 0) return false;
            OutdoorConstructedAssetState room = null;
            if (!string.IsNullOrEmpty(bed.ParentRoomId) &&
                (!board.Assets.TryGetValue(bed.ParentRoomId, out room) || !FitsUsableInterior(room, bed)))
                return false;

            var cellX = room != null ? room.WorldWidth / room.CellsW : bed.WorldWidth / bed.CellsW;
            var cellY = room != null ? room.WorldHeight / room.CellsH : bed.WorldHeight / bed.CellsH;
            var candidates = new[] {
                new WorldVec2(bed.WorldX + bed.WorldWidth * .5f, bed.WorldY - cellY * .5f),
                new WorldVec2(bed.WorldX + bed.WorldWidth + cellX * .5f, bed.WorldY + bed.WorldHeight * .5f),
                new WorldVec2(bed.WorldX - cellX * .5f, bed.WorldY + bed.WorldHeight * .5f),
                new WorldVec2(bed.WorldX + bed.WorldWidth * .5f, bed.WorldY + bed.WorldHeight + cellY * .5f)
            };
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (room != null && !CellFitsInterior(room, candidate, cellX, cellY)) continue;
                if (CellOverlapsFurniture(board, bed, room, candidate, cellX, cellY)) continue;
                point = candidate;
                return true;
            }
            return false;
        }

        static bool CellFitsInterior(OutdoorConstructedAssetState room, WorldVec2 point, float cellX, float cellY)
        {
            var roomCellX = room.WorldWidth / room.CellsW;
            var roomCellY = room.WorldHeight / room.CellsH;
            var epsilon = Math.Min(roomCellX, roomCellY) * .001f;
            return point.X - cellX * .5f >= room.WorldX + roomCellX - epsilon &&
                   point.Y - cellY * .5f >= room.WorldY + roomCellY - epsilon &&
                   point.X + cellX * .5f <= room.WorldX + room.WorldWidth - roomCellX + epsilon &&
                   point.Y + cellY * .5f <= room.WorldY + room.WorldHeight - roomCellY + epsilon;
        }

        static bool CellOverlapsFurniture(
            OutdoorConstructedAssetBoard board,
            OutdoorConstructedAssetState bed,
            OutdoorConstructedAssetState room,
            WorldVec2 point,
            float cellX,
            float cellY)
        {
            var left = point.X - cellX * .5f;
            var bottom = point.Y - cellY * .5f;
            var epsilon = Math.Min(cellX, cellY) * .001f;
            foreach (var asset in board.Assets.Values)
            {
                if (ReferenceEquals(asset, bed) || ReferenceEquals(asset, room) ||
                    asset.StableAssetId == bed.StableAssetId || room != null && asset.StableAssetId == room.StableAssetId)
                    continue;
                if (asset.SurfaceId == bed.SurfaceId && left < asset.WorldX + asset.WorldWidth - epsilon &&
                    asset.WorldX < left + cellX - epsilon && bottom < asset.WorldY + asset.WorldHeight - epsilon &&
                    asset.WorldY < bottom + cellY - epsilon) return true;
            }
            return false;
        }
    }

    /// <summary>Room-level usage and bed assignment/reservation/occupancy authority.</summary>
    public static class RoomBedFacilityService
    {
        public static Result SetRoomUsage(SimulationWorld world, string roomId, RoomUsage usage,
            bool clearDormantAssignments = false)
        {
            if (world == null || !world.OutdoorConstructedAssets.Assets.TryGetValue(roomId ?? string.Empty, out var room) ||
                !RoomBedGeometry.IsRoom(room))
                return Result.Failure(ErrorCode.NotFound, "房间不存在。", roomId);
            if (!CanManageRoom(world, room, out var permissionReason))
                return Result.Failure(ErrorCode.InvalidOperation, permissionReason);
            var assigned = false;
            foreach (var bed in BedsInRoom(world, roomId))
            {
                if (!bed.OccupantCharacterId.IsNone || !bed.ReservedCharacterId.IsNone)
                    return Result.Failure(ErrorCode.InvalidOperation, "请先结束房内床位的占用或预留。");
                assigned |= !bed.AssignedCharacterId.IsNone;
            }
            if (assigned && !clearDormantAssignments)
                return Result.Failure(ErrorCode.InvalidOperation, "房内存在长期床位分配；请确认解除分配后再切换用途。");
            if (assigned)
                foreach (var bed in BedsInRoom(world, roomId)) bed.AssignedCharacterId = EntityId.None;
            room.RoomUsage = usage;
            return Result.Success();
        }

        public static bool HasDormantAssignments(SimulationWorld world, string roomId)
        {
            foreach (var bed in BedsInRoom(world, roomId))
                if (!bed.AssignedCharacterId.IsNone) return true;
            return false;
        }

        public static Result AssignBed(SimulationWorld world, string bedId, EntityId characterId)
        {
            if (world == null || characterId.IsNone || !world.Entities.TryGet(characterId, out _) ||
                !world.OutdoorConstructedAssets.Assets.TryGetValue(bedId ?? string.Empty, out var bed) ||
                !RoomBedGeometry.IsBed(bed))
                return Result.Failure(ErrorCode.InvalidArgument, "床位或人物无效。");
            if (!BedHasUsage(world, bed, RoomUsage.Residential))
                return Result.Failure(ErrorCode.InvalidOperation, "只有住宅床可以长期分配给住户。");
            var siteId = bed.BoundWorldSiteId;
            if (string.IsNullOrWhiteSpace(siteId) && !string.IsNullOrWhiteSpace(bed.ParentRoomId) &&
                world.OutdoorConstructedAssets.Assets.TryGetValue(bed.ParentRoomId, out var room))
                siteId = room.BoundWorldSiteId;
            if (string.IsNullOrWhiteSpace(siteId) || !world.Strategic.Sites.TryGet(siteId, out var site) ||
                !string.Equals(site.OwnerFactionId, world.Strategic.PlayerFactionId, StringComparison.Ordinal))
                return Result.Failure(ErrorCode.InvalidOperation, "只能管理玩家控制地点内的床位。");
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
            OutdoorConstructedAssetState assigned = null;
            foreach (var candidate in Beds(world))
            {
                if (candidate.AssignedCharacterId == characterId) { assigned = candidate; break; }
            }
            if (assigned != null && usage == RoomUsage.Residential)
            {
                if (BedHasUsage(world, assigned, usage) && CanUseBed(world, assigned, characterId, out _))
                    bed = assigned;
                return bed != null;
            }
            foreach (var candidate in Beds(world))
                if (candidate.AssignedCharacterId.IsNone && BedHasUsage(world, candidate, usage) &&
                    CanUseBed(world, candidate, characterId, out _)) { bed = candidate; break; }
            return bed != null;
        }

        public static bool TryGetAssignedBed(SimulationWorld world, EntityId characterId,
            out OutdoorConstructedAssetState bed)
        {
            bed = null;
            if (world == null || characterId.IsNone) return false;
            foreach (var candidate in Beds(world))
                if (candidate.AssignedCharacterId == characterId) { bed = candidate; return true; }
            return false;
        }

        public static bool TryGetAssignedHome(
            SimulationWorld world,
            EntityId characterId,
            out OutdoorConstructedAssetState bed,
            out OutdoorConstructedAssetState room,
            out WorldVec2 accessPoint)
        {
            room = null; accessPoint = default;
            if (!TryGetAssignedBed(world, characterId, out bed) ||
                !BedHasUsage(world, bed, RoomUsage.Residential) ||
                !world.OutdoorConstructedAssets.Assets.TryGetValue(bed.ParentRoomId, out room) ||
                !RoomBedGeometry.IsRoom(room)) return false;
            return TryResolveAccessPoint(world, bed, out accessPoint);
        }

        public static bool TryReserve(SimulationWorld world, EntityId characterId, RoomUsage usage,
            out OutdoorConstructedAssetState bed)
        {
            if (!TryFindBed(world, characterId, usage, out bed)) return false;
            bed.ReservedCharacterId = characterId;
            return true;
        }

        public static bool TryReserveInRoom(SimulationWorld world, string roomId, EntityId characterId,
            out OutdoorConstructedAssetState bed)
        {
            bed = null;
            if (world == null || characterId.IsNone) return false;
            foreach (var candidate in BedsInRoom(world, roomId))
            {
                if ((!candidate.AssignedCharacterId.IsNone && candidate.AssignedCharacterId != characterId) ||
                    !CanUseBed(world, candidate, characterId, out _)) continue;
                bed = candidate;
                if (candidate.AssignedCharacterId == characterId) break;
            }
            if (bed == null) return false;
            bed.ReservedCharacterId = characterId;
            return true;
        }

        public static bool TryOccupy(SimulationWorld world, string bedId, EntityId characterId)
        {
            if (world == null || characterId.IsNone ||
                !world.OutdoorConstructedAssets.Assets.TryGetValue(bedId ?? string.Empty, out var bed) ||
                !RoomBedGeometry.IsBed(bed) || !CanUseBed(world, bed, characterId, out _) ||
                !IsAtAccessPoint(world, bed, characterId)) return false;
            bed.ReservedCharacterId = characterId;
            bed.OccupantCharacterId = characterId;
            return true;
        }

        /// <summary>
        /// Returns the real bed currently occupied by this character. Reservation alone is not sleep.
        /// </summary>
        public static bool TryGetOccupiedBed(
            SimulationWorld world, EntityId characterId, RoomUsage usage,
            out OutdoorConstructedAssetState bed)
        {
            bed = null;
            if (world == null || characterId.IsNone) return false;
            foreach (var candidate in Beds(world))
                if (candidate.OccupantCharacterId == characterId && BedHasUsage(world, candidate, usage))
                { bed = candidate; return true; }
            return false;
        }

        public static bool IsValidOccupiedBed(
            SimulationWorld world, EntityId characterId, RoomUsage usage,
            out OutdoorConstructedAssetState bed)
        {
            return TryGetOccupiedBed(world, characterId, usage, out bed) &&
                   CanUseBed(world, bed, characterId, out _) &&
                   IsAtAccessPoint(world, bed, characterId);
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

        public static bool TryResolveAccessPoint(
            SimulationWorld world, OutdoorConstructedAssetState bed, out WorldVec2 point)
        {
            point = default;
            return world != null && RoomBedGeometry.TryResolveAccessPoint(world.OutdoorConstructedAssets, bed, out point);
        }

        public static bool IsAtAccessPoint(SimulationWorld world, OutdoorConstructedAssetState bed, EntityId characterId)
        {
            if (!TryResolveAccessPoint(world, bed, out var access) ||
                !CharacterWorldPresenceQuery.TryResolve(world, characterId, out var presence) ||
                !presence.HasWorldPosition || presence.SurfaceId != bed.SurfaceId) return false;
            var cell = world.SurfaceSpatial.TryGet(bed.SurfaceId, out var metric) && metric != null
                ? metric.CellSize : Math.Min(bed.WorldWidth / bed.CellsW, bed.WorldHeight / bed.CellsH);
            var dx = presence.WorldPosition.X - access.X;
            var dy = presence.WorldPosition.Y - access.Y;
            return dx * dx + dy * dy <= cell * cell * .75f * .75f;
        }

        public static bool CanUseBed(
            SimulationWorld world, OutdoorConstructedAssetState bed, EntityId characterId, out string reason)
        {
            reason = string.Empty;
            if (world == null || !world.Entities.TryGet(characterId, out _) || !RoomBedGeometry.IsBed(bed))
            { reason = "床位或人物无效。"; return false; }
            if (!bed.AssignedCharacterId.IsNone && bed.AssignedCharacterId != characterId)
            { reason = "该床已分配给其他住户。"; return false; }
            if (!bed.ReservedCharacterId.IsNone && bed.ReservedCharacterId != characterId)
            { reason = "该床已被其他人物预约。"; return false; }
            if (!bed.OccupantCharacterId.IsNone && bed.OccupantCharacterId != characterId)
            { reason = "该床正在被其他人物使用。"; return false; }
            if (!TryResolveAccessPoint(world, bed, out _))
            { reason = "床边没有合法使用格。"; return false; }
            if (!CharacterWorldPresenceQuery.TryResolve(world, characterId, out var presence) ||
                !presence.HasWorldPosition || presence.SurfaceId != bed.SurfaceId)
            { reason = "人物与床不在同一处世界空间。"; return false; }
            var bedSiteId = ResolveBedSiteId(world, bed);
            if (!string.IsNullOrEmpty(bedSiteId) &&
                (!WorldSitePhysicalRegionQuery.TryResolve(world, presence.WorldPosition, out var personSite) ||
                 !string.Equals(personSite.SiteId, bedSiteId, StringComparison.Ordinal)))
            { reason = "人物不在床所属地点。"; return false; }
            return true;
        }

        public static bool CanManageBed(SimulationWorld world, OutdoorConstructedAssetState bed, out string reason)
        {
            reason = string.Empty;
            var siteId = ResolveBedSiteId(world, bed);
            if (world == null || string.IsNullOrWhiteSpace(siteId) ||
                !world.Strategic.Sites.TryGet(siteId, out var site) ||
                !string.Equals(site.OwnerFactionId, world.Strategic.PlayerFactionId, StringComparison.Ordinal))
            { reason = "只能修改玩家控制地点内的床位。"; return false; }
            return true;
        }

        public static bool CanManageRoom(SimulationWorld world, OutdoorConstructedAssetState room, out string reason)
        {
            reason = string.Empty;
            if (world == null || !RoomBedGeometry.IsRoom(room) || string.IsNullOrWhiteSpace(room.BoundWorldSiteId) ||
                !world.Strategic.Sites.TryGet(room.BoundWorldSiteId, out var site) ||
                !string.Equals(site.OwnerFactionId, world.Strategic.PlayerFactionId, StringComparison.Ordinal))
            { reason = "只能修改玩家控制地点内的房间。"; return false; }
            return true;
        }

        public static string ResolveBedSiteId(SimulationWorld world, OutdoorConstructedAssetState bed)
        {
            if (bed == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(bed.BoundWorldSiteId)) return bed.BoundWorldSiteId;
            return world != null && !string.IsNullOrWhiteSpace(bed.ParentRoomId) &&
                   world.OutdoorConstructedAssets.Assets.TryGetValue(bed.ParentRoomId, out var room)
                ? room.BoundWorldSiteId ?? string.Empty : string.Empty;
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
