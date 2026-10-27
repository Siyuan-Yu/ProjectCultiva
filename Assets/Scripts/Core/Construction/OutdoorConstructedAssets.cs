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
            kind == XianXia.Core.Npc.PrivateCampService.MarkerKind ||
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
        /// <summary>Logical local subdivisions; scaled furniture keeps IDs while its physical rectangle grows.</summary>
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

        public IEnumerable<OutdoorConstructedAssetCell> EnumeratePhysicalCells(float originX, float originY, float cellSize)
        {
            if (cellSize <= 0f) yield break;
            var x0 = (int)Math.Floor((WorldX-originX)/cellSize + .002f);
            var y0 = (int)Math.Floor((WorldY-originY)/cellSize + .002f);
            var x1 = (int)Math.Ceiling((WorldX+WorldWidth-originX)/cellSize - .002f);
            var y1 = (int)Math.Ceiling((WorldY+WorldHeight-originY)/cellSize - .002f);
            for (var y = y0; y < y1; y++)
            for (var x = x0; x < x1; x++)
                yield return new OutdoorConstructedAssetCell(SurfaceId,
                    (Math.Max(WorldX, originX+x*cellSize)+Math.Min(WorldX+WorldWidth, originX+(x+1)*cellSize))*.5f,
                    (Math.Max(WorldY, originY+y*cellSize)+Math.Min(WorldY+WorldHeight, originY+(y+1)*cellSize))*.5f);
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
        const float SpatialBucketSize = 50f;
        readonly Dictionary<string, OutdoorConstructedAssetState> _assets =
            new Dictionary<string, OutdoorConstructedAssetState>(StringComparer.Ordinal);
        // Derived query acceleration only. Registered footprints are immutable; snapshot restore
        // uses Clear/TryRegister, so no spatial state is serialized or promoted to authority.
        readonly Dictionary<(string SurfaceId, int X, int Y), List<OutdoorConstructedAssetState>> _spatial =
            new Dictionary<(string, int, int), List<OutdoorConstructedAssetState>>();
        readonly Dictionary<string, OutdoorConstructedAssetState> _exclusiveLocations =
            new Dictionary<string, OutdoorConstructedAssetState>(StringComparer.Ordinal);
        readonly List<OutdoorConstructedAssetState> _beds = new List<OutdoorConstructedAssetState>();
        readonly Dictionary<string, List<OutdoorConstructedAssetState>> _bedsByRoom =
            new Dictionary<string, List<OutdoorConstructedAssetState>>(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, OutdoorConstructedAssetState> Assets => _assets;
        public IReadOnlyList<OutdoorConstructedAssetState> Beds => _beds;
        public IReadOnlyList<OutdoorConstructedAssetState> BedsInRoom(string roomId) =>
            roomId != null && _bedsByRoom.TryGetValue(roomId, out var beds)
                ? (IReadOnlyList<OutdoorConstructedAssetState>)beds
                : Array.Empty<OutdoorConstructedAssetState>();
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
        public void Clear()
        {
            _assets.Clear(); _spatial.Clear(); _exclusiveLocations.Clear();
            _beds.Clear(); _bedsByRoom.Clear(); NextSequence = 1;
        }
        public bool TryRegister(OutdoorConstructedAssetState asset)
        {
            if (asset == null || !asset.IsValid || _assets.ContainsKey(asset.StableAssetId)) return false;
            if (HasExclusiveLocation(asset) && _exclusiveLocations.ContainsKey(asset.BoundLocationId)) return false;
            foreach (var existing in QueryOverlapping(asset.SurfaceId,
                         asset.WorldX, asset.WorldY, asset.WorldWidth, asset.WorldHeight))
                if (Overlaps(existing, asset) && !RoomBedGeometry.IsAllowedNestedPair(existing, asset)) return false;
            _assets.Add(asset.StableAssetId, asset);
            if (HasExclusiveLocation(asset)) _exclusiveLocations.Add(asset.BoundLocationId, asset);
            AddToSpatial(asset);
            if (RoomBedGeometry.IsBed(asset))
            {
                _beds.Add(asset);
                if (!_bedsByRoom.TryGetValue(asset.ParentRoomId ?? string.Empty, out var roomBeds))
                    _bedsByRoom.Add(asset.ParentRoomId ?? string.Empty,
                        roomBeds = new List<OutdoorConstructedAssetState>());
                roomBeds.Add(asset);
            }
            return true;
        }
        internal void Remove(string id)
        {
            if (!_assets.TryGetValue(id, out var asset)) return;
            RemoveFromSpatial(asset);
            if (HasExclusiveLocation(asset)) _exclusiveLocations.Remove(asset.BoundLocationId);
            if (RoomBedGeometry.IsBed(asset))
            {
                _beds.Remove(asset);
                var roomId = asset.ParentRoomId ?? string.Empty;
                if (_bedsByRoom.TryGetValue(roomId, out var roomBeds))
                {
                    roomBeds.Remove(asset);
                    if (roomBeds.Count == 0) _bedsByRoom.Remove(roomId);
                }
            }
            _assets.Remove(id);
        }
        static bool HasExclusiveLocation(OutdoorConstructedAssetState asset) =>
            !string.IsNullOrWhiteSpace(asset.BoundLocationId) &&
            !string.Equals(asset.Kind, OutdoorConstructedAssetSemantics.BedKind, StringComparison.Ordinal) &&
            !string.Equals(asset.Kind, OutdoorConstructedAssetSemantics.RecoverySpotKind, StringComparison.Ordinal);

        static int Bucket(float world) => (int)Math.Floor(world / SpatialBucketSize);

        void AddToSpatial(OutdoorConstructedAssetState asset)
        {
            for (var y = Bucket(asset.WorldY); y <= Bucket(asset.WorldY + asset.WorldHeight); y++)
            for (var x = Bucket(asset.WorldX); x <= Bucket(asset.WorldX + asset.WorldWidth); x++)
            {
                var key = (asset.SurfaceId, x, y);
                if (!_spatial.TryGetValue(key, out var items))
                    _spatial.Add(key, items = new List<OutdoorConstructedAssetState>());
                items.Add(asset);
            }
        }

        void RemoveFromSpatial(OutdoorConstructedAssetState asset)
        {
            for (var y = Bucket(asset.WorldY); y <= Bucket(asset.WorldY + asset.WorldHeight); y++)
            for (var x = Bucket(asset.WorldX); x <= Bucket(asset.WorldX + asset.WorldWidth); x++)
            {
                var key = (asset.SurfaceId, x, y);
                if (!_spatial.TryGetValue(key, out var items)) continue;
                items.Remove(asset);
                if (items.Count == 0) _spatial.Remove(key);
            }
        }

        public IEnumerable<OutdoorConstructedAssetState> QueryOverlapping(
            string surfaceId, float worldX, float worldY, float width, float height)
        {
            if (string.IsNullOrEmpty(surfaceId) || width <= 0f || height <= 0f) yield break;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var y = Bucket(worldY); y <= Bucket(worldY + height); y++)
            for (var x = Bucket(worldX); x <= Bucket(worldX + width); x++)
            {
                if (!_spatial.TryGetValue((surfaceId, x, y), out var items)) continue;
                for (var i = 0; i < items.Count; i++)
                {
                    var asset = items[i];
                    if (seen.Add(asset.StableAssetId) &&
                        asset.WorldX < worldX + width && worldX < asset.WorldX + asset.WorldWidth &&
                        asset.WorldY < worldY + height && worldY < asset.WorldY + asset.WorldHeight)
                        yield return asset;
                }
            }
        }
        // Boolean bedside query: duplicate bucket visits are harmless. Unlike the
        // public enumeration this needs neither an iterator nor a dedup HashSet.
        internal bool AnyFurnitureAtAccessCell(OutdoorConstructedAssetState bed,
            OutdoorConstructedAssetState room, WorldVec2 point, float cellX, float cellY)
        {
            var left = (double)point.X - cellX * .5;
            var bottom = (double)point.Y - cellY * .5;
            for (var y = Bucket((float)bottom); y <= Bucket((float)(bottom + cellY)); y++)
            for (var x = Bucket((float)left); x <= Bucket((float)(left + cellX)); x++)
            {
                if (!_spatial.TryGetValue((bed.SurfaceId, x, y), out var items)) continue;
                for (var i = 0; i < items.Count; i++)
                {
                    var asset = items[i];
                    if (ReferenceEquals(asset, bed) || ReferenceEquals(asset, room) ||
                        asset.StableAssetId == bed.StableAssetId || room != null && asset.StableAssetId == room.StableAssetId)
                        continue;
                    var epsilon = 2 * RoomBedGeometry.CoordinateTolerance(Math.Min(cellX, cellY),
                        point.X, point.Y, asset.WorldX, asset.WorldY);
                    var dx = asset.WorldX - left;
                    var dy = asset.WorldY - bottom;
                    if (dx + asset.WorldWidth > epsilon && dx < cellX - epsilon &&
                        dy + asset.WorldHeight > epsilon && dy < cellY - epsilon) return true;
                }
            }
            return false;
        }

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
            var epsilon = RoomBedGeometry.CoordinateTolerance(
                Math.Min(a.WorldWidth / a.CellsW, b.WorldWidth / b.CellsW),
                a.WorldX, a.WorldY, b.WorldX, b.WorldY);
            var dx = (double)b.WorldX - a.WorldX;
            var dy = (double)b.WorldY - a.WorldY;
            return a.SurfaceId == b.SurfaceId && dx + b.WorldWidth > epsilon &&
                dx < a.WorldWidth - epsilon && dy + b.WorldHeight > epsilon && dy < a.WorldHeight - epsilon;
        }
    }

    public static class RoomBedGeometry
    {
        // A float origin past 512 has a larger ULP than .001 of a .028-world-unit cell.
        // Keep arithmetic local and double; also account for quantization of the stored float origins.
        internal static double CoordinateTolerance(double cellSize, double ax, double ay, double bx, double by)
        {
            var magnitude = Math.Max(Math.Max(Math.Abs(ax), Math.Abs(ay)), Math.Max(Math.Abs(bx), Math.Abs(by)));
            const double floatRelativeSpacing = 1.1920928955078125e-7; // 2^-23, not System.Single.Epsilon
            return Math.Max(cellSize * .001, magnitude * floatRelativeSpacing);
        }

        public static bool IsRoom(OutdoorConstructedAssetState asset) => asset != null &&
            string.Equals(asset.Kind, OutdoorConstructedAssetSemantics.RoomKind, StringComparison.Ordinal);

        public static bool IsBed(OutdoorConstructedAssetState asset) => asset != null &&
            string.Equals(asset.Kind, OutdoorConstructedAssetSemantics.BedKind, StringComparison.Ordinal);

        public static bool IsRoomWallCell(OutdoorConstructedAssetState room, int cellX, int cellY)
        {
            if (!IsRoom(room) || room.CellsW < 3 || room.CellsH < 3 ||
                cellX < 0 || cellY < 0 || cellX >= room.CellsW || cellY >= room.CellsH)
                return false;
            var perimeter = cellX == 0 || cellY == 0 ||
                            cellX == room.CellsW - 1 || cellY == room.CellsH - 1;
            var doorLeft = (room.CellsW - 2) / 2;
            var doorway = cellY == 0 && cellX >= doorLeft && cellX < doorLeft + 2;
            return perimeter && !doorway;
        }

        public static string RoomWallCellId(OutdoorConstructedAssetState room, int cellX, int cellY) =>
            OutdoorStatefulObjectId.ForCell((room?.StableAssetId ?? string.Empty) + ":wall", cellX, cellY);

        public static bool TryRoomWallRectangle(OutdoorConstructedAssetState room, int gx, int gy,
            float thickness, bool cornerSide, out float x, out float y, out float width, out float height)
        {
            x = y = width = height = 0;
            if (!IsRoomWallCell(room, gx, gy) || thickness <= 0) return false;
            var cw = room.WorldWidth / room.CellsW; var ch = room.WorldHeight / room.CellsH;
            thickness = Math.Min(thickness, Math.Min(cw, ch));
            var horizontal = gy == 0 || gy == room.CellsH - 1;
            var vertical = gx == 0 || gx == room.CellsW - 1;
            x = room.WorldX + gx*cw; y = room.WorldY + gy*ch;
            if (cornerSide)
            {
                if (!horizontal || !vertical || ch <= thickness) return false;
                if (gx != 0) x += cw-thickness;
                if (gy == 0) y += thickness;
                width = thickness; height = ch-thickness;
            }
            else if (horizontal)
            {
                if (gy != 0) y += ch-thickness;
                width = cw; height = thickness;
            }
            else
            {
                if (gx != 0) x += cw-thickness;
                width = thickness; height = ch;
            }
            return true;
        }

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
            var cellX = (double)room.WorldWidth / room.CellsW;
            var cellY = (double)room.WorldHeight / room.CellsH;
            var epsilon = CoordinateTolerance(Math.Min(cellX, cellY), room.WorldX, room.WorldY, bed.WorldX, bed.WorldY);
            var dx = (double)bed.WorldX - room.WorldX;
            var dy = (double)bed.WorldY - room.WorldY;
            return dx >= cellX - epsilon && dy >= cellY - epsilon &&
                dx + bed.WorldWidth <= room.WorldWidth - cellX + epsilon &&
                dy + bed.WorldHeight <= room.WorldHeight - cellY + epsilon;
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
            if (!TryResolveAccessGeometry(board, bed, out var room, out var cellX, out var cellY))
                return false;
            for (var i = 0; i < 4; i++)
            {
                var candidate = AccessCandidate(bed, cellX, cellY, i);
                if (!IsLegalAccessCandidate(board, bed, room, candidate, cellX, cellY)) continue;
                point = candidate;
                return true;
            }
            return false;
        }

        public static void CollectAccessPoints(
            OutdoorConstructedAssetBoard board,
            OutdoorConstructedAssetState bed,
            List<WorldVec2> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            into.Clear();
            if (!TryResolveAccessGeometry(board, bed, out var room, out var cellX, out var cellY))
                return;
            for (var i = 0; i < 4; i++)
            {
                var candidate = AccessCandidate(bed, cellX, cellY, i);
                if (IsLegalAccessCandidate(board, bed, room, candidate, cellX, cellY))
                    into.Add(candidate);
            }
        }

        public static bool IsAccessPoint(
            OutdoorConstructedAssetBoard board,
            OutdoorConstructedAssetState bed,
            WorldVec2 point,
            float epsilon = .001f)
        {
            if (!TryResolveAccessGeometry(board, bed, out var room, out var cellX, out var cellY))
                return false;
            for (var i = 0; i < 4; i++)
            {
                var candidate = AccessCandidate(bed, cellX, cellY, i);
                if (Math.Abs(candidate.X - point.X) <= epsilon &&
                    Math.Abs(candidate.Y - point.Y) <= epsilon &&
                    IsLegalAccessCandidate(board, bed, room, candidate, cellX, cellY))
                    return true;
            }
            return false;
        }

        internal static bool IsWithinAccessPointRange(
            OutdoorConstructedAssetBoard board,
            OutdoorConstructedAssetState bed,
            WorldVec2 point,
            float distanceSquared)
        {
            if (!TryResolveAccessGeometry(board, bed, out var room, out var cellX, out var cellY))
                return false;
            for (var i = 0; i < 4; i++)
            {
                var candidate = AccessCandidate(bed, cellX, cellY, i);
                var dx = point.X - candidate.X;
                var dy = point.Y - candidate.Y;
                // Distant candidates cannot match; only matching candidates need the
                // unchanged room-interior and furniture-overlap checks.
                if (dx * dx + dy * dy <= distanceSquared &&
                    IsLegalAccessCandidate(board, bed, room, candidate, cellX, cellY))
                    return true;
            }
            return false;
        }

        static bool TryResolveAccessGeometry(
            OutdoorConstructedAssetBoard board,
            OutdoorConstructedAssetState bed,
            out OutdoorConstructedAssetState room,
            out float cellX,
            out float cellY)
        {
            room = null;
            cellX = cellY = 0f;
            if (board == null || !IsBed(bed) || bed.CellsW <= 0 || bed.CellsH <= 0) return false;
            if (!string.IsNullOrEmpty(bed.ParentRoomId) &&
                (!board.Assets.TryGetValue(bed.ParentRoomId, out room) || !FitsUsableInterior(room, bed)))
                return false;
            cellX = room != null ? room.WorldWidth / room.CellsW : bed.WorldWidth / bed.CellsW;
            cellY = room != null ? room.WorldHeight / room.CellsH : bed.WorldHeight / bed.CellsH;
            return true;
        }

        static WorldVec2 AccessCandidate(OutdoorConstructedAssetState bed, float cellX, float cellY, int index)
        {
            // Preserve the authored preference: below, right, left, then above.
            switch (index)
            {
                case 0: return new WorldVec2((float)((double)bed.WorldX + bed.WorldWidth * .5), (float)((double)bed.WorldY - cellY * .5));
                case 1: return new WorldVec2((float)((double)bed.WorldX + bed.WorldWidth + cellX * .5), (float)((double)bed.WorldY + bed.WorldHeight * .5));
                case 2: return new WorldVec2((float)((double)bed.WorldX - cellX * .5), (float)((double)bed.WorldY + bed.WorldHeight * .5));
                default: return new WorldVec2((float)((double)bed.WorldX + bed.WorldWidth * .5), (float)((double)bed.WorldY + bed.WorldHeight + cellY * .5));
            }
        }

        static bool IsLegalAccessCandidate(
            OutdoorConstructedAssetBoard board,
            OutdoorConstructedAssetState bed,
            OutdoorConstructedAssetState room,
            WorldVec2 point,
            float cellX,
            float cellY) =>
            (room == null || CellFitsInterior(room, point, cellX, cellY)) &&
            !CellOverlapsFurniture(board, bed, room, point, cellX, cellY);

        static bool CellFitsInterior(OutdoorConstructedAssetState room, WorldVec2 point, float cellX, float cellY)
        {
            var roomCellX = (double)room.WorldWidth / room.CellsW;
            var roomCellY = (double)room.WorldHeight / room.CellsH;
            // The derived float access point has one additional rounding step beyond its stored bed origin.
            var epsilon = 2 * CoordinateTolerance(Math.Min(roomCellX, roomCellY), room.WorldX, room.WorldY, point.X, point.Y);
            var dx = (double)point.X - room.WorldX;
            var dy = (double)point.Y - room.WorldY;
            return dx - cellX * .5 >= roomCellX - epsilon && dy - cellY * .5 >= roomCellY - epsilon &&
                   dx + cellX * .5 <= room.WorldWidth - roomCellX + epsilon &&
                   dy + cellY * .5 <= room.WorldHeight - roomCellY + epsilon;
        }

        static bool CellOverlapsFurniture(
            OutdoorConstructedAssetBoard board,
            OutdoorConstructedAssetState bed,
            OutdoorConstructedAssetState room,
            WorldVec2 point,
            float cellX,
            float cellY)
            => board.AnyFurnitureAtAccessCell(bed, room, point, cellX, cellY);
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
            if (usage != RoomUsage.Prison && world.HuangcunSurvival.Enabled && !world.HuangcunSurvival.Released &&
                world.HuangcunSurvival.PrisonRoomId == roomId &&
                world.Entities.TryGet(new EntityId(world.HuangcunSurvival.PrisonerId), out var openingCaptive) &&
                openingCaptive.TryGet<XianXia.Core.Entities.LifecycleComponent>(out var captiveLife) && !captiveLife.IsDead && !captiveLife.IsRemoved)
                return Result.Failure(ErrorCode.InvalidOperation, "请先释放仍关在这座牢房中的同伴，再修改房间用途。");
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
            if (usage == RoomUsage.Prison)
                foreach (var retained in Beds(world))
                    if ((retained.ReservedCharacterId == characterId || retained.OccupantCharacterId == characterId) &&
                        BedHasUsage(world, retained, usage) && CanUseBed(world, retained, characterId, out _))
                    { bed = retained; return true; }
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
        /// Atomic carried-person placement. The carrier, rather than the carried character, must
        /// stand at the real bed access point; only after every check succeeds is the carried
        /// character moved onto the bed and recorded as its occupant.
        /// </summary>
        public static Result TryPlaceCarriedOccupant(
            SimulationWorld world,
            string bedId,
            EntityId carrierId,
            EntityId occupantId,
            RoomUsage requiredUsage)
        {
            if (world == null || carrierId.IsNone || occupantId.IsNone || carrierId == occupantId ||
                !world.Entities.TryGet(carrierId, out _) || !world.Entities.TryGet(occupantId, out _) ||
                !world.OutdoorConstructedAssets.Assets.TryGetValue(bedId ?? string.Empty, out var bed) ||
                !RoomBedGeometry.IsBed(bed))
                return Result.Failure(ErrorCode.InvalidArgument, "搬运安置需要有效的搬运者、人物和床位。");
            if (!BedHasUsage(world, bed, requiredUsage))
                return Result.Failure(ErrorCode.InvalidOperation, "目标床位用途已改变。");
            if (requiredUsage != RoomUsage.Prison &&
                !MortalCivilianService.CanReceiveAutomaticRescue(world, occupantId))
                return Result.Failure(ErrorCode.InvalidOperation, "受拘押或战场中的人物不能通过普通救护转移到床位。");
            if (bed.ReservedCharacterId != occupantId)
                return Result.Failure(ErrorCode.InvalidOperation, "目标床位没有由当前人物持有预约。");
            if (!CanUseBed(world, bed, occupantId, out var reason))
                return Result.Failure(ErrorCode.InvalidOperation, reason);
            if (!IsAtAccessPoint(world, bed, carrierId))
                return Result.Failure(ErrorCode.InvalidOperation, "搬运者尚未到达床边合法使用位置。");
            if (!CharacterWorldPresenceQuery.TryResolve(world, carrierId, out var carrier) ||
                !carrier.HasWorldPosition || carrier.SurfaceId != bed.SurfaceId ||
                !CharacterWorldPresenceQuery.TryResolve(world, occupantId, out var occupant) ||
                !occupant.HasWorldPosition || occupant.SurfaceId != bed.SurfaceId)
                return Result.Failure(ErrorCode.InvalidOperation, "搬运者、人物与床位不在同一世界空间。");
            var cell = world.SurfaceSpatial.TryGet(bed.SurfaceId, out var metric) && metric != null
                ? metric.CellSize
                : Math.Min(bed.WorldWidth / Math.Max(1, bed.CellsW),
                    bed.WorldHeight / Math.Max(1, bed.CellsH));
            var dx = occupant.WorldPosition.X - carrier.WorldPosition.X;
            var dy = occupant.WorldPosition.Y - carrier.WorldPosition.Y;
            if (dx * dx + dy * dy > cell * cell * 2.25f)
                return Result.Failure(ErrorCode.InvalidOperation, "被搬运人物没有真实跟随搬运者到达床边。");

            bed.OccupantCharacterId = occupantId;
            world.WorldPresence.SetAtWorldPosition(
                occupantId,
                new WorldVec2(bed.WorldX + bed.WorldWidth * .5f,
                    bed.WorldY + bed.WorldHeight * .5f),
                bed.SurfaceId);
            return Result.Success();
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
                   IsAtBed(world, bed, characterId);
        }

        public static bool IsAtBed(
            SimulationWorld world, OutdoorConstructedAssetState bed, EntityId characterId)
        {
            if (world == null || bed == null ||
                !CharacterWorldPresenceQuery.TryResolve(world, characterId, out var presence) ||
                !presence.HasWorldPosition || presence.SurfaceId != bed.SurfaceId) return false;
            var centerX = bed.WorldX + bed.WorldWidth * .5f;
            var centerY = bed.WorldY + bed.WorldHeight * .5f;
            var dx = presence.WorldPosition.X - centerX;
            var dy = presence.WorldPosition.Y - centerY;
            var cell = world.SurfaceSpatial.TryGet(bed.SurfaceId, out var metric) && metric != null
                ? metric.CellSize
                : Math.Min(bed.WorldWidth / Math.Max(1, bed.CellsW),
                    bed.WorldHeight / Math.Max(1, bed.CellsH));
            var rangeSquared = cell * cell * .75f * .75f;
            // Occupants displayed on the bed need no second presence lookup or
            // four-neighbour access scan. Access/ownership is still checked by CanUseBed.
            return dx * dx + dy * dy <= rangeSquared ||
                   RoomBedGeometry.IsWithinAccessPointRange(world.OutdoorConstructedAssets, bed,
                       presence.WorldPosition, rangeSquared);
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
            if (world == null || !CharacterWorldPresenceQuery.TryResolve(world, characterId, out var presence) ||
                !presence.HasWorldPosition || presence.SurfaceId != bed.SurfaceId) return false;
            var cell = world.SurfaceSpatial.TryGet(bed.SurfaceId, out var metric) && metric != null
                ? metric.CellSize : Math.Min(bed.WorldWidth / bed.CellsW, bed.WorldHeight / bed.CellsH);
            return RoomBedGeometry.IsWithinAccessPointRange(world.OutdoorConstructedAssets, bed,
                presence.WorldPosition, cell * cell * .75f * .75f);
        }

        public static bool CanUseBed(
            SimulationWorld world, OutdoorConstructedAssetState bed, EntityId characterId, out string reason)
        {
            reason = string.Empty;
            if (world == null || !world.Entities.TryGet(characterId, out _) || !RoomBedGeometry.IsBed(bed))
            { reason = "床位或人物无效。"; return false; }
            if (world.HuangcunSurvival.Enabled && bed.ParentRoomId == world.HuangcunSurvival.CampRoomId &&
                !string.IsNullOrEmpty(world.HuangcunSurvival.CampRoomId) &&
                (!world.Entities.TryGet(characterId,out var camper) || !camper.TryGet<XianXia.Core.Social.FactionMembershipComponent>(out var campFaction) ||
                 campFaction.FactionId != world.HuangcunSurvival.OwnerFactionId))
            { reason = "这是其他势力的私人营地床铺。"; return false; }
            if (PrivateCampService.IsPrivate(world,bed.StableAssetId) &&
                (!world.Entities.TryGet(characterId,out var visitor) || !visitor.TryGet<XianXia.Core.Social.FactionMembershipComponent>(out var visitorFaction) ||
                 !PrivateCampService.Owns(world,bed.StableAssetId,visitorFaction.FactionId)))
            { reason="不能使用他人的私人营地床位。"; return false; }
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
            // Travel eligibility is independent from current physical Site. Actual occupation and
            // carried-person placement still require the character/carrier at a legal bedside point.
            return true;
        }

        public static bool CanManageBed(SimulationWorld world, OutdoorConstructedAssetState bed, out string reason)
        {
            reason = string.Empty;
            if (bed != null && PrivateCampService.Owns(world,bed.StableAssetId,world.Strategic.PlayerFactionId)) return true;
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
            if (world != null && room != null && PrivateCampService.Owns(world,room.StableAssetId,world.Strategic.PlayerFactionId)) return true;
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
            if (world == null) yield break;
            foreach (var bed in world.OutdoorConstructedAssets.BedsInRoom(roomId)) yield return bed;
        }

        static IEnumerable<OutdoorConstructedAssetState> Beds(SimulationWorld world)
        {
            if (world == null) yield break;
            foreach (var bed in world.OutdoorConstructedAssets.Beds) yield return bed;
        }
    }

    public static class OutdoorFactionConstructionAuthorizationService
    {
        public static Result Validate(SimulationWorld world, string factionId, OutdoorConstructedAssetState candidate)
        {
            if (string.IsNullOrWhiteSpace(factionId)) return Result.Failure(ErrorCode.InvalidArgument, "建造势力无效。");
            var physical = ValidatePhysicalPlacement(world, candidate);
            if (physical.IsFailure) return physical;
            if (XianXia.Core.Npc.PrivateCampService.TryCover(world,factionId,candidate,out _)) return Result.Success();
            world.SurfaceSpatial.TryGet(candidate.SurfaceId, out var metric);
            foreach (var cell in candidate.EnumeratePhysicalCells(metric.OriginWorldX, metric.OriginWorldY, metric.CellSize))
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
            var scaledFurniture = World.Surface.SurfaceCellGeometry.IsScaledFurniture(
                candidate.Kind, candidate.WorldWidth, candidate.WorldHeight,
                candidate.CellsW, candidate.CellsH, metric.CellSize);
            if (!scaledFurniture && (Math.Abs(candidate.WorldWidth - candidate.CellsW * metric.CellSize) > epsilon ||
                Math.Abs(candidate.WorldHeight - candidate.CellsH * metric.CellSize) > epsilon ||
                Math.Abs((candidate.WorldX - metric.OriginWorldX) / metric.CellSize - Math.Round((candidate.WorldX - metric.OriginWorldX) / metric.CellSize)) > .001f ||
                Math.Abs((candidate.WorldY - metric.OriginWorldY) / metric.CellSize - Math.Round((candidate.WorldY - metric.OriginWorldY) / metric.CellSize)) > .001f) ||
                scaledFurniture && !World.Surface.SurfaceCellGeometry.IsAlignedFurnitureRectangle(
                    candidate.Kind, candidate.WorldX, candidate.WorldY, candidate.WorldWidth, candidate.WorldHeight,
                    candidate.CellsW, candidate.CellsH, metric.OriginWorldX, metric.OriginWorldY, metric.CellSize))
                return Result.Failure(ErrorCode.InvalidArgument, "建筑必须对齐 Surface 网格。");
            foreach (var cell in candidate.EnumeratePhysicalCells(metric.OriginWorldX, metric.OriginWorldY, metric.CellSize))
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
