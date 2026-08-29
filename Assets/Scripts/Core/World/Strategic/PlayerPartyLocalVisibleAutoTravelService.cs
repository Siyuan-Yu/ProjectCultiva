using System;
using System.Collections.Generic;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase 5C-W1: Wilderness-only LocalVisible AutoTravel.
    /// When LocalMap is a WorldSite, this service refuses to act (keeps Phase 5B: stand still).
    /// All Exit geometry reuses the existing formal Wilderness Surface Exit (Phase 2C).
    /// </summary>
    public static class PlayerPartyLocalVisibleAutoTravelService
    {
        public static bool IsActiveLocalVisibleAutoTravel(PlayerPartyWorldMotion motion) =>
            motion != null &&
            motion.IsMoving &&
            motion.ExecutionMode == PlayerPartyTravelExecutionMode.LocalVisible;

        /// <summary>Formal HexPath current leg: path[SegmentIndex] -> path[SegmentIndex+1].</summary>
        public static bool TryResolveActiveLeg(
            PlayerPartyWorldMotion motion,
            out HexCoord currentHex,
            out HexCoord nextHex,
            out int directionIndex)
        {
            currentHex = default;
            nextHex = default;
            directionIndex = 0;
            if (motion == null || !motion.IsMoving || motion.HexPathCount < 2)
                return false;
            if (motion.SegmentIndex < 0 || motion.SegmentIndex >= motion.HexPathCount - 1)
                return false;

            currentHex = motion.HexPath[motion.SegmentIndex];
            nextHex = motion.HexPath[motion.SegmentIndex + 1];
            return TryResolveDirectionBetween(currentHex, nextHex, out directionIndex);
        }

        public static bool TryResolveDirectionBetween(HexCoord from, HexCoord to, out int directionIndex)
        {
            for (var i = 0; i < 6; i++)
            {
                if (HexMath.Neighbor(from, i).Equals(to))
                {
                    directionIndex = i;
                    return true;
                }
            }

            directionIndex = 0;
            return false;
        }

        static readonly List<SurfaceExitConnection> ConnectionScratch = new List<SurfaceExitConnection>(8);

        /// <summary>
        /// Wilderness-only Exit resolution（统一正式 Authority）。
        /// 与真实 Trigger / 半透明 Debug 方块同一真源：SurfaceExitZoneCalculator.CollectConnections
        /// （已含 ResolveOrdinaryHexOverlaps 重叠合并）。从正式 resolved connections 中精确匹配
        /// SourceHex == currentHex &amp;&amp; DestinationHex == nextHex 的那一个作为 LocalVisible 唯一 Exit。
        /// 不再直接调用 TryBuildConnectionBetweenHexes（未经 overlap 处理的原始 Connection）。
        /// </summary>
        public static bool TryResolveWildernessExitConnection(
            SimulationWorld world,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            HexCoord currentHex,
            HexCoord nextHex,
            int directionIndex,
            out SurfaceExitConnection connection)
        {
            connection = default;
            if (world?.HexWorld == null)
                return false;
            var motion = world.PlayerPartyTravel;
            if (motion == null ||
                motion.LocationKind != PlayerPartyLocationKind.AtWorldPosition)
                return false;

            var depth = SurfaceExitZoneCalculator.ResolveDepthFromSession(world, bounds);
            ConnectionScratch.Clear();
            SurfaceExitZoneCalculator.CollectConnections(world, bounds, depth, ConnectionScratch);
            for (var i = 0; i < ConnectionScratch.Count; i++)
            {
                var c = ConnectionScratch[i];
                if (c.SourceHex.Equals(currentHex) && c.DestinationHex.Equals(nextHex))
                {
                    connection = c;
                    return true;
                }
            }

            return false;
        }

        public static void GetExitApproachLocalPoint(
            SurfaceExitConnection connection,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            out float localX,
            out float localY)
        {
            localX = connection.ExitCenterLocalX;
            localY = connection.ExitCenterLocalY;

            var inset = Math.Max(0.35f, SurfaceExitZoneCalculator.DefaultExitTriggerDepth * 0.35f);
            localX -= connection.LocalDirectionX * inset;
            localY -= connection.LocalDirectionY * inset;

            localX = Math.Max(bounds.MinX + 0.05f, Math.Min(bounds.MaxX - 0.05f, localX));
            localY = Math.Max(bounds.MinY + 0.05f, Math.Min(bounds.MaxY - 0.05f, localY));
        }

        /// <summary>
        /// Project continuous WorldPosition onto formal segment geometry; write SegmentProgress (keep SegmentIndex).
        /// </summary>
        public static void SyncSegmentProgressFromWorldPosition(
            PlayerPartyWorldMotion motion,
            float hexSize)
        {
            if (motion == null || !motion.IsMoving || !motion.HasPosition)
                return;
            if (!motion.TryGetActiveStepHexes(out var fromHex, out var toHex))
                return;

            var size = hexSize > 0f ? hexSize : 1f;
            HexMath.ToWorldPosition(fromHex, size, out var fx, out var fy);
            HexMath.ToWorldPosition(toHex, size, out var tx, out var ty);
            var dx = tx - fx;
            var dy = ty - fy;
            var lenSq = dx * dx + dy * dy;
            if (lenSq < 1e-8f)
            {
                motion.SetSegment(motion.SegmentIndex, 1f);
                return;
            }

            var wx = motion.WorldPosition.X - fx;
            var wy = motion.WorldPosition.Y - fy;
            var t = (wx * dx + wy * dy) / lenSq;
            if (t < 0f)
                t = 0f;
            else if (t > 1f)
                t = 1f;
            motion.SetSegment(motion.SegmentIndex, t);
        }

        /// <summary>
        /// Wilderness hex cross under LocalVisible AutoTravel:
        /// keeps HexPath / Destination / AutoTravel / ExecutionMode; advances Segment so the
        /// Host driver pauses after one hex (no Phase 5D auto second leg).
        /// A WorldSite destination is rejected — 5C-W1 does not handle Site Egress.
        /// </summary>
        public static Result TryCrossWildernessEdgePreservingLocalVisibleAutoTravel(
            SimulationWorld world,
            PlayerPartyRuntime party,
            HexCoord destinationHex)
        {
            if (world == null || party == null || !party.HasActive)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid wilderness edge args.");
            var motion = world.PlayerPartyTravel;
            if (motion == null || !motion.HasPosition)
                return Result.Failure(ErrorCode.InvalidOperation, "Party has no world position.");
            if (motion.LocationKind != PlayerPartyLocationKind.AtWorldPosition)
                return Result.Failure(ErrorCode.InvalidOperation, "Not in continuous wilderness position.");
            if (!IsActiveLocalVisibleAutoTravel(motion))
                return Result.Failure(ErrorCode.InvalidOperation, "LocalVisible AutoTravel required.");

            if (!TryResolveActiveLeg(motion, out _, out var nextHex, out _))
                return Result.Failure(ErrorCode.InvalidOperation, "No active travel leg.");
            if (!nextHex.Equals(destinationHex))
                return Result.Failure(ErrorCode.InvalidOperation, "Exit destination is not the active NextHex.");
            if (!IsNeighborHex(motion.CurrentHex, destinationHex))
                return Result.Failure(ErrorCode.InvalidOperation, "Destination hex is not a neighbor.");
            if (!IsGroundPassable(world.HexWorld, destinationHex))
                return Result.Failure(ErrorCode.InvalidOperation, "Neighbor hex is impassable.");
            if (world.Strategic?.Sites != null &&
                world.Strategic.Sites.TryGetAtHex(destinationHex, out var destSite) &&
                destSite != null)
                return Result.Failure(ErrorCode.InvalidOperation, "Destination is a WorldSite (not in 5C-W1 scope).");

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var newWorldPos = WildernessLocalWorldProjection.ComputeCrossEdgeWorldPosition(
                motion.CurrentHex,
                destinationHex,
                motion.WorldPosition,
                hexSize);
            var derived = HexMath.WorldToHex(newWorldPos.X, newWorldPos.Y, hexSize);

            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);
            PlayerPartyTransitionMembership.LogPartyTransition(
                world,
                party,
                "CrossWildernessEdge.LocalVisiblePreserve",
                destinationHex,
                world.PartyWorld?.LocalMapId);

            // Preserve path / AutoTravel / ExecutionMode; only move position (never SetAtWorldPosition).
            motion.SetWorldPositionInternal(newWorldPos, derived);
            ApplyTravelingMembersAtHex(world, derived);

            if (!WildernessLocalMapFallback.TryResolve(world, destinationHex, out var mapId) ||
                string.IsNullOrEmpty(mapId))
                return Result.Failure(ErrorCode.InvalidOperation, "No wilderness fallback LocalMap for exit hex.");

            // Advance Segment so the Host pauses after crossing (5C-W1 stops after one hex).
            if (motion.SegmentIndex + 1 < motion.HexPathCount)
                motion.SetSegment(motion.SegmentIndex + 1, 0f);

            return WorldTravelService.EnterWildernessLocalMap(world, destinationHex, mapId);
        }

        static bool IsNeighborHex(HexCoord a, HexCoord b)
        {
            for (var d = 0; d < 6; d++)
            {
                if (HexMath.Neighbor(a, d).Equals(b))
                    return true;
            }

            return false;
        }

        static bool IsGroundPassable(HexWorld grid, HexCoord coord)
        {
            if (grid == null || !grid.TryGetTile(coord, out var tile) || tile == null)
                return false;
            if (tile.Terrain == HexTerrainType.Water)
                return false;
            return tile.IsPassable;
        }

        static void ApplyTravelingMembersAtHex(SimulationWorld world, HexCoord hex)
        {
            if (world?.WorldPresence == null || world.PlayerPartyTravel == null)
                return;
            var members = world.PlayerPartyTravel.TravelingMembers;
            for (var i = 0; i < members.Count; i++)
            {
                var id = members[i];
                if (id.IsNone)
                    continue;
                world.WorldPresence.SetAtHex(id, hex);
            }
        }
    }
}
