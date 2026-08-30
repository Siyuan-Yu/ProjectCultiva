using System;
using System.Collections.Generic;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// 固定 Canonical Exit Trigger Geometry（Connection + PlayableBounds + ExitTriggerDepth）。
    /// 禁止依赖角色位置 / EntryDirection / WorldPosition。
    /// </summary>
    public readonly struct SurfaceExitZoneGeometry
    {
        public SurfaceExitZoneGeometry(
            SurfaceExitConnection connection,
            WildernessLocalWorldProjection.WildernessLocalMapBounds playableBounds,
            float exitTriggerDepth)
        {
            Connection = connection;
            PlayableBounds = playableBounds;
            ExitTriggerDepth = exitTriggerDepth;
        }

        public SurfaceExitConnection Connection { get; }
        public WildernessLocalWorldProjection.WildernessLocalMapBounds PlayableBounds { get; }
        public float ExitTriggerDepth { get; }
        public int DirectionIndex => Connection.DirectionIndex;

        public bool Contains(float localX, float localY) =>
            SurfaceExitZoneCalculator.PointBelongsToConnection(
                localX, localY, Connection, ExitTriggerDepth);
    }

    /// <summary>Runtime 可用性（可变）；不改变 Geometry。</summary>
    public readonly struct SurfaceExitAvailability
    {
        public SurfaceExitAvailability(int directionIndex, bool isPassable, HexCoord destinationHex)
        {
            DirectionIndex = directionIndex;
            IsPassable = isPassable;
            DestinationHex = destinationHex;
        }

        public int DirectionIndex { get; }
        public bool IsPassable { get; }
        public HexCoord DestinationHex { get; }
    }

    /// <summary>可见 Active Zone = 合法 Connection（已含 Canonical Geometry）。</summary>
    public readonly struct SurfaceExitVisibleZone
    {
        public SurfaceExitVisibleZone(SurfaceExitConnection connection)
        {
            Connection = connection;
        }

        public SurfaceExitConnection Connection { get; }
        public HexCoord DestinationHex => Connection.DestinationHex;
        public int DirectionIndex => Connection.DirectionIndex;

        public bool Contains(float localX, float localY) =>
            SurfaceExitZoneCalculator.PointBelongsToConnection(
                localX, localY, Connection, SurfaceExitZoneCalculator.DefaultExitTriggerDepth);
    }

    /// <summary>
    /// Surface Exit 真源：Actual Connections 由 Hex 邻接推导；Zone 位置由世界方向向量投射。
    /// </summary>
    public static class SurfaceExitZoneCalculator
    {
        /// <summary>默认 ExitTriggerDepth（world units）。</summary>
        public const float DefaultExitTriggerDepth = 1.25f;

        /// <summary>Exit Zone 沿边跨度比例（推荐默认约 1/4～1/3）。</summary>
        public const float DefaultSlotSpanFraction = 0.30f;

        /// <summary>最小沿边占用（1/6）。</summary>
        public const float MinSlotSpanFraction = 1f / 6f;

        /// <summary>最大沿边占用（1/2）。</summary>
        public const float MaxSlotSpanFraction = 0.5f;

        static readonly List<SurfaceExitConnection> ScratchConnections = new List<SurfaceExitConnection>(6);

        public static float NormalizeDepth(
            float authoredDepth,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds)
        {
            var depth = authoredDepth > 0.0001f ? authoredDepth : DefaultExitTriggerDepth;
            var maxAllowed = Math.Min(bounds.HalfWidth, bounds.HalfHeight) * 0.35f;
            if (maxAllowed < DefaultExitTriggerDepth)
                maxAllowed = DefaultExitTriggerDepth;
            if (depth > maxAllowed)
                depth = maxAllowed;
            if (depth < 0.05f)
                depth = 0.05f;
            return depth;
        }

        public static float EffectiveSlotSpan(float edgeLength, float spanFraction)
        {
            var frac = spanFraction > 0.0001f ? spanFraction : DefaultSlotSpanFraction;
            frac = Math.Max(MinSlotSpanFraction, Math.Min(MaxSlotSpanFraction, frac));
            return Math.Max(0.01f, edgeLength * frac);
        }

        public static float ResolveDepthFromSession(
            SimulationWorld world,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds)
        {
            var authored = world?.LocalMap != null ? world.LocalMap.ExitTriggerDepth : 0f;
            return NormalizeDepth(authored, bounds);
        }

        public static bool ShouldPresent(SimulationWorld world)
        {
            if (world?.LocalMap == null)
                return false;
            if (world.LocalMap.IsInInterior)
                return false;
            if (string.IsNullOrWhiteSpace(world.LocalMap.ActiveMapLayoutId))
                return false;
            return true;
        }

        /// <summary>
        /// 收集当前 Context 的全部 Actual Surface Exit Connections（仅合法、可通行）。
        /// 普通 Hex：Connection 数 = 可通行 Neighbor 数（0–6）。
        /// </summary>
        public static int CollectConnections(
            SimulationWorld world,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float exitTriggerDepth,
            IList<SurfaceExitConnection> connectionsOut)
        {
            if (connectionsOut == null)
                return 0;
            connectionsOut.Clear();
            if (world?.PlayerPartyTravel == null || !world.PlayerPartyTravel.HasPosition)
                return 0;
            if (world.LocalMap != null && world.LocalMap.IsInInterior)
                return 0;

            var motion = world.PlayerPartyTravel;
            var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : 1f;
            var depth = NormalizeDepth(exitTriggerDepth, bounds);
            var spanFraction = DefaultSlotSpanFraction;

            if (motion.LocationKind == PlayerPartyLocationKind.AtWorldPosition)
            {
                CollectOrdinaryHexConnections(
                    world, motion.CurrentHex, hexSize, bounds, depth, spanFraction, connectionsOut);
            }
            else if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                     !string.IsNullOrEmpty(motion.SiteId))
            {
                CollectWorldSiteConnections(
                    world, motion.SiteId, hexSize, bounds, depth, spanFraction, connectionsOut);
            }

            return connectionsOut.Count;
        }

        public static int BuildCanonicalGeometries(
            SimulationWorld world,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float exitTriggerDepth,
            IList<SurfaceExitZoneGeometry> geometriesOut)
        {
            if (geometriesOut == null)
                return 0;
            geometriesOut.Clear();
            ScratchConnections.Clear();
            CollectConnections(world, bounds, exitTriggerDepth, ScratchConnections);
            var depth = NormalizeDepth(exitTriggerDepth, bounds);
            for (var i = 0; i < ScratchConnections.Count; i++)
            {
                geometriesOut.Add(new SurfaceExitZoneGeometry(
                    ScratchConnections[i], bounds, depth));
            }

            return geometriesOut.Count;
        }

        public static bool TryGetConnectionSlotRect(
            SurfaceExitConnection connection,
            out SurfaceExitCoverageRect rect)
        {
            rect = connection.SlotRect;
            return rect.Width > 0.0001f && rect.Height > 0.0001f;
        }

        /// <summary>兼容旧 API：按 directionIndex 查找 Connection slot。</summary>
        public static bool TryGetCanonicalSlotRect(
            SimulationWorld world,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float exitTriggerDepth,
            int directionIndex,
            out SurfaceExitCoverageRect rect)
        {
            rect = default;
            ScratchConnections.Clear();
            CollectConnections(world, bounds, exitTriggerDepth, ScratchConnections);
            var dir = NormalizeDirection(directionIndex);
            for (var i = 0; i < ScratchConnections.Count; i++)
            {
                if (ScratchConnections[i].DirectionIndex != dir)
                    continue;
                rect = ScratchConnections[i].SlotRect;
                return true;
            }

            return false;
        }

        public static bool PointBelongsToConnection(
            float localX,
            float localY,
            SurfaceExitConnection connection,
            float exitTriggerDepth)
        {
            _ = exitTriggerDepth;
            var slot = connection.SlotRect;
            return localX >= slot.MinX - 0.0001f &&
                   localX <= slot.MaxX + 0.0001f &&
                   localY >= slot.MinY - 0.0001f &&
                   localY <= slot.MaxY + 0.0001f;
        }

        public static bool PointBelongsToDirection(
            SimulationWorld world,
            float localX,
            float localY,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float exitTriggerDepth,
            int directionIndex)
        {
            if (WildernessLocalWorldProjection.IsOutsideBounds(localX, localY, bounds))
                return false;
            ScratchConnections.Clear();
            CollectConnections(world, bounds, exitTriggerDepth, ScratchConnections);
            var dir = NormalizeDirection(directionIndex);
            for (var i = 0; i < ScratchConnections.Count; i++)
            {
                var c = ScratchConnections[i];
                if (c.DirectionIndex != dir)
                    continue;
                return PointBelongsToConnection(localX, localY, c, exitTriggerDepth);
            }

            return false;
        }

        public static bool TryClassifyConnectionAtPoint(
            SimulationWorld world,
            float localX,
            float localY,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float exitTriggerDepth,
            out int directionIndex)
        {
            directionIndex = -1;
            if (!TryGetConnectionAtPoint(
                    world, localX, localY, bounds, exitTriggerDepth, out var connection))
                return false;
            directionIndex = connection.DirectionIndex;
            return true;
        }

        public static bool TryGetConnectionAtPoint(
            SimulationWorld world,
            float localX,
            float localY,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float exitTriggerDepth,
            out SurfaceExitConnection connection)
        {
            connection = default;
            if (WildernessLocalWorldProjection.IsOutsideBounds(localX, localY, bounds))
                return false;

            ScratchConnections.Clear();
            CollectConnections(world, bounds, exitTriggerDepth, ScratchConnections);
            SurfaceExitConnection? best = null;
            var bestDistSq = float.MaxValue;
            for (var i = 0; i < ScratchConnections.Count; i++)
            {
                var c = ScratchConnections[i];
                if (!PointBelongsToConnection(localX, localY, c, exitTriggerDepth))
                    continue;
                var dx = localX - c.ExitCenterLocalX;
                var dy = localY - c.ExitCenterLocalY;
                var distSq = dx * dx + dy * dy;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = c;
                }
            }

            if (!best.HasValue)
                return false;
            connection = best.Value;
            return true;
        }

        public static int AppendConnectionCoverageRects(
            SurfaceExitConnection connection,
            IList<SurfaceExitCoverageRect> rectsOut)
        {
            if (rectsOut == null || !TryGetConnectionSlotRect(connection, out var slot))
                return 0;
            rectsOut.Add(slot);
            return 1;
        }

        public static int AppendCoverageRects(
            SimulationWorld world,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float exitTriggerDepth,
            int directionIndex,
            IList<SurfaceExitCoverageRect> rectsOut)
        {
            if (rectsOut == null)
                return 0;
            if (!TryGetCanonicalSlotRect(world, bounds, exitTriggerDepth, directionIndex, out var slot))
                return 0;
            rectsOut.Add(slot);
            return 1;
        }

        public static int CollectAvailability(
            SimulationWorld world,
            IList<SurfaceExitAvailability> availabilityOut)
        {
            if (availabilityOut == null)
                return 0;
            availabilityOut.Clear();
            var motion = world?.PlayerPartyTravel;
            if (motion == null || !motion.HasPosition)
                return 0;

            if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite)
            {
                ScratchConnections.Clear();
                var bounds = WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(
                    0f, 0f, 1f, 16, 16);
                CollectConnections(world, bounds, DefaultExitTriggerDepth, ScratchConnections);
                for (var i = 0; i < ScratchConnections.Count; i++)
                {
                    var c = ScratchConnections[i];
                    availabilityOut.Add(new SurfaceExitAvailability(
                        c.DirectionIndex, true, c.DestinationHex));
                }

                return availabilityOut.Count;
            }

            for (var dir = 0; dir < 6; dir++)
            {
                if (!PlayerPartyWildernessTransitionService.TryEvaluateSurfaceExitLegality(
                        world, dir, out var neighbor, out var passable))
                {
                    availabilityOut.Add(new SurfaceExitAvailability(dir, false, default));
                    continue;
                }

                availabilityOut.Add(new SurfaceExitAvailability(dir, passable, neighbor));
            }

            return availabilityOut.Count;
        }

        /// <summary>LocalMap Materialize 时单次打印 Surface Exit Connections（非每帧）。</summary>
        public static void LogSurfaceExitConnectionsOnMaterialize(
            SimulationWorld world,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float exitTriggerDepth)
        {
            if (PlayerPartyWorldLocationDebug.Sink == null || !ShouldPresent(world))
                return;

            ScratchConnections.Clear();
            CollectConnections(world, bounds, exitTriggerDepth, ScratchConnections);
            var motion = world.PlayerPartyTravel;
            var context = motion != null ? motion.LocationKind.ToString() : "?";
            if (motion != null && motion.LocationKind == PlayerPartyLocationKind.AtWorldSite)
                context += " site=" + (motion.SiteId ?? string.Empty);

            PlayerPartyWorldLocationDebug.Sink(
                "[SurfaceExitConnections] " + context +
                " count=" + ScratchConnections.Count);
            for (var i = 0; i < ScratchConnections.Count; i++)
            {
                var c = ScratchConnections[i];
                PlayerPartyWorldLocationDebug.Sink(
                    "  [" + i + "] dest=" + c.DestinationHex +
                    " kind=" + c.DestinationKind +
                    " contact=(" + c.BoundaryContactWorldX.ToString("0.###") + "," +
                    c.BoundaryContactWorldY.ToString("0.###") + ")");
            }
        }

        public static int CollectVisibleZones(
            SimulationWorld world,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float exitTriggerDepth,
            IList<SurfaceExitVisibleZone> zonesOut)
        {
            if (zonesOut == null)
                return 0;
            zonesOut.Clear();
            if (!ShouldPresent(world))
                return 0;

            ScratchConnections.Clear();
            CollectConnections(world, bounds, exitTriggerDepth, ScratchConnections);
            for (var i = 0; i < ScratchConnections.Count; i++)
                zonesOut.Add(new SurfaceExitVisibleZone(ScratchConnections[i]));
            return zonesOut.Count;
        }

        static void CollectOrdinaryHexConnections(
            SimulationWorld world,
            HexCoord sourceHex,
            float hexSize,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float depth,
            float spanFraction,
            IList<SurfaceExitConnection> connectionsOut)
        {
            for (var dir = 0; dir < 6; dir++)
            {
                if (!TryBuildConnectionAlongDirection(
                        world, sourceHex, dir, hexSize, bounds, depth, spanFraction, out var connection))
                    continue;
                connectionsOut.Add(connection);
            }

            SurfaceExitZoneOverlapResolver.ResolveOrdinaryHexOverlaps(bounds, depth, connectionsOut);
        }

        static void CollectWorldSiteConnections(
            SimulationWorld world,
            string siteId,
            float hexSize,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float depth,
            float spanFraction,
            IList<SurfaceExitConnection> connectionsOut)
        {
            if (world?.Strategic?.Sites == null ||
                string.IsNullOrEmpty(siteId) ||
                !world.Strategic.Sites.TryGet(siteId, out var site) ||
                site == null)
                return;

            WorldSiteFootprintExitConnectionResolver.CollectConnections(
                world, site, hexSize, bounds, depth, spanFraction, connectionsOut);
        }

        static bool TryBuildConnectionAlongDirection(
            SimulationWorld world,
            HexCoord sourceHex,
            int directionIndex,
            float hexSize,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float depth,
            float spanFraction,
            out SurfaceExitConnection connection)
        {
            connection = default;
            var neighbor = HexMath.Neighbor(sourceHex, directionIndex);
            if (world.HexWorld == null ||
                !world.HexWorld.TryGetTile(neighbor, out var tile) ||
                tile == null)
                return false;
            if (!IsGroundPassable(world.HexWorld, neighbor))
                return false;

            return TryBuildConnectionBetweenHexes(
                world, sourceHex, neighbor, directionIndex, hexSize, bounds, depth, spanFraction, out connection);
        }

        public static bool TryBuildConnectionBetweenHexes(
            SimulationWorld world,
            HexCoord sourceHex,
            HexCoord destinationHex,
            int directionIndex,
            float hexSize,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float depth,
            float spanFraction,
            out SurfaceExitConnection connection)
        {
            connection = default;
            HexMath.ToWorldPosition(sourceHex, hexSize, out var sx, out var sy);
            HexMath.ToWorldPosition(destinationHex, hexSize, out var dx, out var dy);
            var contactX = (sx + dx) * 0.5f;
            var contactY = (sy + dy) * 0.5f;
            return TryBuildConnectionFromFootprintBoundary(
                world,
                sourceHex,
                destinationHex,
                contactX,
                contactY,
                sx,
                sy,
                hexSize,
                bounds,
                depth,
                spanFraction,
                out connection,
                NormalizeDirection(directionIndex));
        }

        public static bool TryBuildConnectionFromFootprintBoundary(
            SimulationWorld world,
            HexCoord sourceHex,
            HexCoord destinationHex,
            float boundaryContactWorldX,
            float boundaryContactWorldY,
            float directionOriginWorldX,
            float directionOriginWorldY,
            float hexSize,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float depth,
            float spanFraction,
            out SurfaceExitConnection connection,
            int directionIndexOverride = -1)
        {
            connection = default;
            var worldDx = boundaryContactWorldX - directionOriginWorldX;
            var worldDy = boundaryContactWorldY - directionOriginWorldY;
            LocalMapHexDirectionProjection.HexWorldDeltaToLocalPlane(
                worldDx, worldDy, out var ldx, out var ldy);
            if (!LocalMapHexDirectionProjection.TryProjectToPerimeterCenter(
                    bounds, ldx, ldy, out var cx, out var cy))
                return false;
            if (!LocalMapHexDirectionProjection.TryBuildSlotRect(
                    bounds, depth, spanFraction, cx, cy, ldx, ldy, out var slot))
                return false;

            var kind = SurfaceExitDestinationKind.WildernessHex;
            var siteId = string.Empty;
            if (world.Strategic?.Sites != null &&
                world.Strategic.Sites.TryGetAtHex(destinationHex, out var site) &&
                site != null)
            {
                kind = SurfaceExitDestinationKind.WorldSite;
                siteId = site.SiteId ?? string.Empty;
            }

            var len = (float)Math.Sqrt(ldx * ldx + ldy * ldy);
            if (len < 1e-6f)
                return false;

            var directionIndex = directionIndexOverride >= 0
                ? NormalizeDirection(directionIndexOverride)
                : DirectionIndexFromLocalPlane(ldx / len, ldy / len);

            connection = new SurfaceExitConnection(
                sourceHex,
                destinationHex,
                directionIndex,
                kind,
                siteId,
                ldx / len,
                ldy / len,
                cx,
                cy,
                slot,
                boundaryContactWorldX,
                boundaryContactWorldY);
            return true;
        }

        static int DirectionIndexFromLocalPlane(float localDirX, float localDirY)
        {
            var angle = (float)Math.Atan2(localDirY, localDirX);
            var best = 0;
            var bestDiff = float.MaxValue;
            for (var i = 0; i < 6; i++)
            {
                var neighbor = HexMath.Neighbor(new HexCoord(0, 0), i);
                HexMath.ToWorldPosition(neighbor, 1f, out var nx, out var ny);
                var dirAngle = (float)Math.Atan2(ny, nx);
                var diff = Math.Abs(NormalizeAngle(dirAngle - angle));
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    best = i;
                }
            }

            return best;
        }

        static float NormalizeAngle(float radians)
        {
            while (radians > Math.PI)
                radians -= (float)(Math.PI * 2d);
            while (radians < -Math.PI)
                radians += (float)(Math.PI * 2d);
            return Math.Abs(radians);
        }

        static bool IsGroundPassable(HexWorld grid, HexCoord coord)
        {
            if (grid == null || !grid.TryGetTile(coord, out var tile) || tile == null)
                return false;
            if (tile.Terrain == HexTerrainType.Water)
                return false;
            return tile.IsPassable;
        }

        static int NormalizeDirection(int directionIndex)
        {
            var d = directionIndex % 6;
            if (d < 0)
                d += 6;
            return d;
        }
    }

    /// <summary>
    /// WorldSite 全 Footprint 外围：唯一合法 Outside Neighbor → Surface Exit Connection。
    /// </summary>
    public static class WorldSiteFootprintExitConnectionResolver
    {
        struct BoundaryAggregate
        {
            public HexCoord Destination;
            public HexCoord RepresentativeSource;
            /// <summary>共享边段在 <see cref="ScratchSharedSegs"/> 的起始索引（每段 4 个 float：A.X/A.Y/B.X/B.Y）。</summary>
            public int SharedSegOffset;
            public int SharedSegCount;
        }

        /// <summary>共享边段 scratch（footprint×6 上限；构建期一次性，零分配复用）。</summary>
        static readonly float[] ScratchSharedSegs = new float[512];
        static int ScratchSharedSegCount;
        static readonly float[] ScratchCornerX = new float[6];
        static readonly float[] ScratchCornerY = new float[6];

        struct EdgeSeg
        {
            public WorldVec2 A;
            public WorldVec2 B;

            public EdgeSeg(WorldVec2 a, WorldVec2 b)
            {
                A = a;
                B = b;
            }

            public float Length =>
                (float)Math.Sqrt((B.X - A.X) * (B.X - A.X) + (B.Y - A.Y) * (B.Y - A.Y));
        }

        static readonly List<EdgeSeg> ScratchSegs = new List<EdgeSeg>(8);
        static readonly List<EdgeSeg> ScratchChain = new List<EdgeSeg>(8);
        static readonly List<EdgeSeg> ScratchBestChain = new List<EdgeSeg>(8);

        static readonly List<BoundaryAggregate> ScratchAggregates = new List<BoundaryAggregate>(16);

        /// <summary>统计 Footprint 外围唯一可通行 Outside Hex 数（与 Connection 数一致）。</summary>
        public static int CountUniqueTraversableOutsideNeighbors(SimulationWorld world, WorldSite site)
        {
            if (world?.HexWorld == null || site == null)
                return 0;

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            CollectAggregates(world, site, hexSize, ScratchAggregates);
            return ScratchAggregates.Count;
        }

        /// <summary>
        /// Phase 5R-B3B.1：LocalVisible Wilderness→WorldSite 的正式连续入口几何。
        /// 按 canonical connection identity（<see cref="SurfaceExitConnection.SourceHex"/>==footprint 格
        /// 且 <see cref="SurfaceExitConnection.DestinationHex"/>==当前荒野格）匹配唯一正式
        /// <see cref="SurfaceExitConnection"/>，返回其 <c>BoundaryContactWorldX/Y</c>（footprint 格
        /// 中心与外部荒野格中心的中点 = 真实 Hex 共享边中点）。
        ///
        /// 复用 <see cref="CollectConnections"/> 产出的正式 connection，不重算第二套 boundary。
        /// <paramref name="bounds"/> 仅用于 slot rect（Local 平面）几何，不影响 BoundaryContactWorld
        /// （完全由 footprint + HexMath 真实几何决定）；此处传名义 bounds，与既有
        /// <c>PlayerPartyWildernessTransitionService.TryFindSiteConnectionByDestination</c> 一致。
        /// 匹配失败（无合法 connection / footprint 格不在 Site / fromHex 不是外部格）→ 明确失败，
        /// 不静默回退 Presence/Anchor/ingressHex center。
        /// </summary>
        public static bool TryResolveFormalIngressConnection(
            SimulationWorld world,
            WorldSite site,
            HexCoord footprintHex,
            HexCoord fromWildernessHex,
            float hexSize,
            out SurfaceExitConnection connection)
        {
            connection = default;
            if (world?.HexWorld == null || site == null || hexSize <= 0.0001f)
                return false;
            if (!site.OccupiesHex(footprintHex) || site.OccupiesHex(fromWildernessHex))
                return false;

            var bounds = WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(
                0f, 0f, 1f, 16, 16);
            var scratch = new List<SurfaceExitConnection>(12);
            CollectConnections(
                world,
                site,
                hexSize,
                bounds,
                SurfaceExitZoneCalculator.DefaultExitTriggerDepth,
                SurfaceExitZoneCalculator.DefaultSlotSpanFraction,
                scratch);
            return TryMatchIngressConnection(scratch, footprintHex, fromWildernessHex, out connection);
        }

        /// <summary>
        /// 纯匹配（可单测）：在 connection 列表中按 canonical identity
        /// （SourceHex==footprintHex 且 DestinationHex==fromWildernessHex）找唯一匹配。
        /// 不按最近距离 / direction 猜测。
        /// </summary>
        public static bool TryMatchIngressConnection(
            IList<SurfaceExitConnection> connections,
            HexCoord footprintHex,
            HexCoord fromWildernessHex,
            out SurfaceExitConnection connection)
        {
            connection = default;
            if (connections == null)
                return false;
            for (var i = 0; i < connections.Count; i++)
            {
                var c = connections[i];
                if (c.SourceHex.Equals(footprintHex) && c.DestinationHex.Equals(fromWildernessHex))
                {
                    connection = c;
                    return true;
                }
            }

            return false;
        }

        public static int CollectConnections(
            SimulationWorld world,
            WorldSite site,
            float hexSize,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float depth,
            float spanFraction,
            IList<SurfaceExitConnection> connectionsOut)
        {
            if (connectionsOut == null || world?.HexWorld == null || site == null)
                return 0;

            connectionsOut.Clear();
            if (hexSize <= 0.0001f)
                hexSize = 1f;

            CollectAggregates(world, site, hexSize, ScratchAggregates);
            if (ScratchAggregates.Count == 0)
                return 0;

            ComputeFootprintWorldCenter(site, hexSize, out var centerX, out var centerY);

            for (var i = 0; i < ScratchAggregates.Count; i++)
            {
                var agg = ScratchAggregates[i];
                // Phase 5R-B3C3.1：BoundaryContact 必须位于真实 footprint perimeter。
                // 同一 destination 与 footprint 多条共享边时不得算术平均（双邻接在拐角处
                // 会落入 polygon 外侧 off-surface 点，导致 V2 inverse r>1 → local 越界）；
                // 取"共享边并集沿 perimeter 的弧长中点"（仍在真实边界上）。
                if (!ResolveBoundaryContactOnPerimeter(
                        agg.SharedSegOffset,
                        agg.SharedSegCount,
                        out var contactX,
                        out var contactY))
                    continue;
                if (!SurfaceExitZoneCalculator.TryBuildConnectionFromFootprintBoundary(
                        world,
                        agg.RepresentativeSource,
                        agg.Destination,
                        contactX,
                        contactY,
                        centerX,
                        centerY,
                        hexSize,
                        bounds,
                        depth,
                        spanFraction,
                        out var connection))
                    continue;
                connectionsOut.Add(connection);
            }

            SurfaceExitZoneOverlapResolver.ResolveOverlaps(bounds, depth, connectionsOut);
            LogLayoutOverflowIfNeeded(site, connectionsOut, bounds, depth);
            return connectionsOut.Count;
        }

        static void CollectAggregates(
            SimulationWorld world,
            WorldSite site,
            float hexSize,
            List<BoundaryAggregate> aggregatesOut)
        {
            aggregatesOut.Clear();
            ScratchSharedSegCount = 0;
            var indexByDestination = new Dictionary<HexCoord, int>();

            // 共享边段表必须按 destination 连续布局（同一 dest 的段可能被其它 dest 的段隔开，
            // 若按 (hex,dir) 全局顺序写，SharedSegOffset/Count 的连续读会串入别的 dest 的段）。
            // pass 1：统计每个 dest 的共享边数（并选 RepresentativeSource）；
            // pass 2：按 dest 前缀和 offset 连续填充段表。
            foreach (var footprintHex in site.EnumerateFootprintHexes())
            {
                for (var dir = 0; dir < 6; dir++)
                {
                    var neighbor = HexMath.Neighbor(footprintHex, dir);
                    if (site.OccupiesHex(neighbor))
                        continue;
                    if (!world.HexWorld.TryGetTile(neighbor, out var tile) || tile == null)
                        continue;
                    if (!IsGroundPassable(tile))
                        continue;

                    if (!indexByDestination.TryGetValue(neighbor, out var idx))
                    {
                        idx = aggregatesOut.Count;
                        indexByDestination[neighbor] = idx;
                        aggregatesOut.Add(new BoundaryAggregate
                        {
                            Destination = neighbor,
                            RepresentativeSource = footprintHex,
                            SharedSegOffset = 0,
                            SharedSegCount = 0,
                        });
                    }

                    var agg = aggregatesOut[idx];
                    agg.SharedSegCount++;
                    if (CompareHex(footprintHex, agg.RepresentativeSource) < 0)
                        agg.RepresentativeSource = footprintHex;
                    aggregatesOut[idx] = agg;
                }
            }

            var offset = 0;
            for (var i = 0; i < aggregatesOut.Count; i++)
            {
                var a = aggregatesOut[i];
                a.SharedSegOffset = offset;
                offset += a.SharedSegCount;
                aggregatesOut[i] = a;
            }

            var cursor = new int[aggregatesOut.Count];
            foreach (var footprintHex in site.EnumerateFootprintHexes())
            {
                for (var dir = 0; dir < 6; dir++)
                {
                    var neighbor = HexMath.Neighbor(footprintHex, dir);
                    if (site.OccupiesHex(neighbor))
                        continue;
                    if (!world.HexWorld.TryGetTile(neighbor, out var tile) || tile == null)
                        continue;
                    if (!IsGroundPassable(tile))
                        continue;

                    var idx = indexByDestination[neighbor];
                    var agg = aggregatesOut[idx];
                    WriteSharedSegAt(agg.SharedSegOffset + cursor[idx], footprintHex, dir, hexSize);
                    cursor[idx]++;
                }
            }

            aggregatesOut.Sort(CompareAggregates);
        }

        /// <summary>把一条共享 hex 边（footprint 格 dir 方向的外露边）端点写入 scratch 段表指定位置。</summary>
        static void WriteSharedSegAt(int segIndex, HexCoord hex, int dir, float hexSize)
        {
            HexMath.CollectCornerWorldPositions(hex, hexSize, ScratchCornerX, ScratchCornerY);
            var i = (5 - dir) % 6;
            var j = (i + 1) % 6;
            var baseIdx = segIndex * 4;
            if (baseIdx + 4 > ScratchSharedSegs.Length)
                return; // 防御：段表满（真实 footprint 远小于容量）
            ScratchSharedSegs[baseIdx] = ScratchCornerX[i];
            ScratchSharedSegs[baseIdx + 1] = ScratchCornerY[i];
            ScratchSharedSegs[baseIdx + 2] = ScratchCornerX[j];
            ScratchSharedSegs[baseIdx + 3] = ScratchCornerY[j];
            if (segIndex >= ScratchSharedSegCount)
                ScratchSharedSegCount = segIndex + 1;
        }

        /// <summary>
        /// Phase 5R-B3C3.1：BoundaryContact = 共享边并集沿 footprint perimeter 的弧长中点。
        /// 所有共享边段拼接为最长连续 chain（端点相接），取总弧长中点处插值——
        /// 单边 → 边中点；共线多边 → 并集中心（仍在线上）；拐角多边 → 公共角点。
        /// 保证结果永远位于真实 footprint perimeter（绝不做算术平均落入 polygon 外侧）。
        /// </summary>
        static bool ResolveBoundaryContactOnPerimeter(
            int segOffset,
            int segCount,
            out float contactX,
            out float contactY)
        {
            contactX = contactY = 0f;
            if (segCount <= 0)
                return false;

            ScratchSegs.Clear();
            for (var s = 0; s < segCount; s++)
            {
                var b = (segOffset + s) * 4;
                ScratchSegs.Add(new EdgeSeg(
                    new WorldVec2(ScratchSharedSegs[b], ScratchSharedSegs[b + 1]),
                    new WorldVec2(ScratchSharedSegs[b + 2], ScratchSharedSegs[b + 3])));
            }

            var n = ScratchSegs.Count;
            var used = new bool[n];
            ScratchBestChain.Clear();
            for (var start = 0; start < n; start++)
            {
                if (used[start])
                    continue;
                ScratchChain.Clear();
                var cur = ScratchSegs[start];
                used[start] = true;
                ScratchChain.Add(cur);
                var head = cur.A;
                var tail = cur.B;
                var grew = true;
                while (grew)
                {
                    grew = false;
                    for (var k = 0; k < n; k++)
                    {
                        if (used[k])
                            continue;
                        var cand = ScratchSegs[k];
                        if (Connect(tail, cand.A))
                        {
                            used[k] = true;
                            ScratchChain.Add(cand);
                            tail = cand.B;
                            grew = true;
                            break;
                        }

                        if (Connect(tail, cand.B))
                        {
                            used[k] = true;
                            var rev = new EdgeSeg(cand.B, cand.A);
                            ScratchChain.Add(rev);
                            tail = rev.B;
                            grew = true;
                            break;
                        }

                        if (Connect(head, cand.B))
                        {
                            used[k] = true;
                            var rev2 = new EdgeSeg(cand.B, cand.A);
                            ScratchChain.Insert(0, rev2);
                            head = rev2.A;
                            grew = true;
                            break;
                        }

                        if (Connect(head, cand.A))
                        {
                            used[k] = true;
                            ScratchChain.Insert(0, cand);
                            head = cand.A;
                            grew = true;
                            break;
                        }
                    }
                }

                if (ChainLength(ScratchChain) > ChainLength(ScratchBestChain))
                {
                    ScratchBestChain.Clear();
                    ScratchBestChain.AddRange(ScratchChain);
                }
            }

            if (ScratchBestChain.Count == 0)
                return false;

            var total = ChainLength(ScratchBestChain);
            var target = total * 0.5f;
            var acc = 0f;
            for (var k = 0; k < ScratchBestChain.Count; k++)
            {
                var seg = ScratchBestChain[k];
                var len = seg.Length;
                if (acc + len >= target - 1e-6f || k == ScratchBestChain.Count - 1)
                {
                    var t = len <= 1e-9f ? 0f : (target - acc) / len;
                    contactX = seg.A.X + t * (seg.B.X - seg.A.X);
                    contactY = seg.A.Y + t * (seg.B.Y - seg.A.Y);
                    return true;
                }

                acc += len;
            }

            contactX = ScratchBestChain[ScratchBestChain.Count - 1].B.X;
            contactY = ScratchBestChain[ScratchBestChain.Count - 1].B.Y;
            return true;
        }

        static bool Connect(WorldVec2 p, WorldVec2 q)
        {
            var dx = p.X - q.X;
            var dy = p.Y - q.Y;
            return dx * dx + dy * dy <= 1e-8f; // eps=1e-4 world
        }

        static float ChainLength(List<EdgeSeg> chain)
        {
            var total = 0f;
            for (var k = 0; k < chain.Count; k++)
                total += chain[k].Length;
            return total;
        }

        static int CompareAggregates(BoundaryAggregate a, BoundaryAggregate b)
        {
            var cmp = a.Destination.Q.CompareTo(b.Destination.Q);
            return cmp != 0 ? cmp : a.Destination.R.CompareTo(b.Destination.R);
        }

        static int CompareHex(HexCoord a, HexCoord b)
        {
            var cmp = a.Q.CompareTo(b.Q);
            return cmp != 0 ? cmp : a.R.CompareTo(b.R);
        }

        public static void ComputeFootprintWorldCenter(
            WorldSite site,
            float hexSize,
            out float centerX,
            out float centerY)
        {
            centerX = 0f;
            centerY = 0f;
            var count = 0;
            foreach (var hex in site.EnumerateFootprintHexes())
            {
                HexMath.ToWorldPosition(hex, hexSize, out var x, out var y);
                centerX += x;
                centerY += y;
                count++;
            }

            if (count <= 0)
                return;
            centerX /= count;
            centerY /= count;
        }

        static bool IsGroundPassable(HexCell tile)
        {
            if (tile == null)
                return false;
            if (tile.Terrain == HexTerrainType.Water)
                return false;
            return tile.IsPassable;
        }

        static void LogLayoutOverflowIfNeeded(
            WorldSite site,
            IList<SurfaceExitConnection> connections,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            float depth)
        {
            if (connections == null || connections.Count < 2 || PlayerPartyWorldLocationDebug.Sink == null)
                return;

            for (var i = 0; i < connections.Count; i++)
            {
                for (var j = i + 1; j < connections.Count; j++)
                {
                    if (!RectsOverlap(connections[i].SlotRect, connections[j].SlotRect))
                        continue;

                    PlayerPartyWorldLocationDebug.Sink(
                        "[WorldSiteExitLayout] overlap remains site=" + (site?.SiteId ?? "?") +
                        " connectionCount=" + connections.Count +
                        " minSpan=" + SurfaceExitZoneCalculator.MinSlotSpanFraction +
                        " perimeter=(" + bounds.MinX + "," + bounds.MinY + ")-(" +
                        bounds.MaxX + "," + bounds.MaxY + ")");
                    return;
                }
            }
        }

        static bool RectsOverlap(SurfaceExitCoverageRect a, SurfaceExitCoverageRect b)
        {
            return a.MinX < b.MaxX - 0.0001f &&
                   a.MaxX > b.MinX + 0.0001f &&
                   a.MinY < b.MaxY - 0.0001f &&
                   a.MaxY > b.MinY + 0.0001f;
        }
    }
}
