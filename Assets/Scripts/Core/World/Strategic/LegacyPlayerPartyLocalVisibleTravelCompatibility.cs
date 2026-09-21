using XianXia.Core.World;
using System;
using System.Collections.Generic;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// LEGACY OUTDOOR LOCALMAP COMPATIBILITY ONLY. ExecutionMode.LocalVisible is the explicit
    /// gate; normal Continuous SurfaceVisible travel never calls this executor.
    /// </summary>
    public static class LegacyPlayerPartyLocalVisibleTravelCompatibility
    {
        public static bool IsActiveLocalVisibleAutoTravel(PlayerPartyWorldMotion motion) =>
            motion != null &&
            motion.IsMoving &&
            motion.ExecutionMode == PlayerPartyTravelExecutionMode.LocalVisible;

        /// <summary>Formal LegacyHexPath current leg: path[LegacyHexSegmentIndex] -> path[LegacyHexSegmentIndex+1].</summary>
        public static bool TryResolveActiveLeg(
            PlayerPartyWorldMotion motion,
            out HexCoord currentHex,
            out HexCoord nextHex,
            out int directionIndex)
        {
            currentHex = default;
            nextHex = default;
            directionIndex = 0;
            if (motion == null || !motion.IsMoving || motion.LegacyHexPathCount < 2)
                return false;
            if (motion.LegacyHexSegmentIndex < 0 || motion.LegacyHexSegmentIndex >= motion.LegacyHexPathCount - 1)
                return false;

            currentHex = motion.LegacyHexPath[motion.LegacyHexSegmentIndex];
            nextHex = motion.LegacyHexPath[motion.LegacyHexSegmentIndex + 1];
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
            if (world?.LegacyHexWorld == null)
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

        /// <summary>
        /// Phase 5R-B6.3：WorldSite departure 的正式 approach 点（可靠版）。
        /// approach = SlotRect 深度方向中点 + SlotRect 与 playable bounds 沿边交集的中点。
        /// </summary>
        public static void ResolveWorldSiteExitApproachLocalPoint(
            SurfaceExitConnection connection,
            WorldSiteHexFootprintSpatialMapping.WorldSiteLocalMapBounds bounds,
            float exitTriggerDepth,
            out float localX,
            out float localY)
        {
            var slot = connection.SlotRect;
            if (slot.Width <= slot.Height)
            {
                localX = (slot.MinX + slot.MaxX) * 0.5f;
                var lo = Math.Max(slot.MinY, bounds.MinY);
                var hi = Math.Min(slot.MaxY, bounds.MaxY);
                localY = (lo + hi) * 0.5f;
            }
            else
            {
                localY = (slot.MinY + slot.MaxY) * 0.5f;
                var lo = Math.Max(slot.MinX, bounds.MinX);
                var hi = Math.Min(slot.MaxX, bounds.MaxX);
                localX = (lo + hi) * 0.5f;
            }

            if (float.IsNaN(localX) || float.IsNaN(localY))
            {
                localX = bounds.CenterX;
                localY = bounds.CenterY;
            }

            localX = Math.Max(bounds.MinX, Math.Min(bounds.MaxX, localX));
            localY = Math.Max(bounds.MinY, Math.Min(bounds.MaxY, localY));
        }

        /// <summary>
        /// Project continuous WorldPosition onto formal segment geometry; write LegacyHexSegmentProgress (keep LegacyHexSegmentIndex).
        /// </summary>
        public static void SyncSegmentProgressFromWorldPosition(
            PlayerPartyWorldMotion motion,
            float hexSize)
        {
            if (motion == null || !motion.IsMoving || !motion.HasPosition)
                return;
            if (!motion.TryGetActiveLegacyHexStep(out var fromHex, out var toHex))
                return;

            var size = hexSize > 0f ? hexSize : 1f;
            HexMath.ToWorldPosition(fromHex, size, out var fx, out var fy);
            HexMath.ToWorldPosition(toHex, size, out var tx, out var ty);
            var dx = tx - fx;
            var dy = ty - fy;
            var lenSq = dx * dx + dy * dy;
            if (lenSq < 1e-8f)
            {
                motion.SetLegacyHexSegment(motion.LegacyHexSegmentIndex, 1f);
                return;
            }

            var wx = motion.WorldPosition.X - fx;
            var wy = motion.WorldPosition.Y - fy;
            var t = (wx * dx + wy * dy) / lenSq;
            if (t < 0f)
                t = 0f;
            else if (t > 1f)
                t = 1f;
            motion.SetLegacyHexSegment(motion.LegacyHexSegmentIndex, t);
        }

        /// <summary>
        /// Wilderness hex cross under LocalVisible AutoTravel:
        /// keeps LegacyHexPath / Destination / AutoTravel / ExecutionMode; advances Segment so the
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
            if (!IsNeighborHex(motion.LegacyCurrentHex, destinationHex))
                return Result.Failure(ErrorCode.InvalidOperation, "Destination hex is not a neighbor.");
            if (!IsGroundPassable(world.LegacyHexWorld, destinationHex))
                return Result.Failure(ErrorCode.InvalidOperation, "Neighbor hex is impassable.");
            if (world.Strategic?.Sites != null &&
                world.Strategic.Sites.TryGetAtLegacyHex(destinationHex, out var destSite) &&
                destSite != null)
            {
                var admission = StrategicWorldSiteAccessService.CanTransitionPlayerPartyIntoWorldSite(
                    world, destSite.SiteId);
                if (admission.IsFailure)
                    return admission;
                var destinationMapId = WorldTravelService.ResolveWorldSiteLocalMapId(destSite);
                // Phase 5R-B3B.1/B7A: WorldSite → 正式 BoundaryContact Ingress。
                // destinationHex 是 approach 按距 start 最近方向选取的 footprint 格（多 Hex
                // footprint 不强制 Anchor）。目标 Site 仍完成 Travel；非目标 Site 则保持同一
                // LegacyHexPath / Destination，并从路径中解析正式 egress，进入 Site LocalMap 后继续。
                // 无正式 connection → 明确失败，不静默回退中心点。
                // 注：变量命名避开外层方法体块的 hexSize/derived（CS0136：子块不得与外层块同名）。
                var siteHexSize = world.LegacyHexWorld != null && world.LegacyHexWorld.HexSize > 0f
                    ? world.LegacyHexWorld.HexSize
                    : 1f;
                if (!WorldSiteFootprintExitConnectionResolver.TryResolveFormalIngressConnection(
                        world,
                        destSite,
                        destinationHex,
                        motion.LegacyCurrentHex,
                        siteHexSize,
                        out var ingressConnection))
                    return Result.Failure(
                        ErrorCode.InvalidOperation,
                        "No formal site ingress connection from hex " + motion.LegacyCurrentHex +
                        " into site footprint hex " + destinationHex + " (5R-B3B.1).");

                var boundary = new WorldVec2(
                    ingressConnection.BoundaryContactWorldX,
                    ingressConnection.BoundaryContactWorldY);

                PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);
                PlayerPartyTransitionMembership.LogPartyTransition(
                    world,
                    party,
                    "CrossWildernessEdge.LocalVisibleSiteIngress",
                    destinationHex,
                    world.PartyWorld?.LocalMapId);

                // Phase 5R-B3C1.2：保存正式 ingress connection 的 transient context（见
                // PlayerPartySurfaceEdgeGate.SetIngressContext），供 Materialize 的 Safe Landing 解析
                // inward 方向。保留 path / AutoTravel / ExecutionMode；只移动位置（不 Clear）。
                // committed hex = 正式 topology destinationHex（BoundaryContact 位于 perimeter 中点，
                // WorldToHex 有 tie 歧义；不再用 ingressDerived 猜）。
                motion.LegacySurfaceEdgeGate?.SetIngressContext(ingressConnection);
                var isDestinationSite =
                    !string.IsNullOrEmpty(motion.LegacyDestinationSiteId) &&
                    string.Equals(
                        motion.LegacyDestinationSiteId,
                        destSite.SiteId,
                        System.StringComparison.Ordinal);
                if (isDestinationSite)
                {
                    motion.SetLegacyWorldPositionAndHex(boundary, destinationHex);
                    ApplyTravelingMembersAtHex(world, destinationHex);
                    return LegacyPlayerPartyHexTravelCompatibility.EnterWorldSiteAsParty(
                        world, party, destSite, destinationHex);
                }

                PlayerPartySiteIngressTrace.BeginIngress(
                    destSite.SiteId,
                    boundary,
                    motion.LegacyCurrentHex,
                    destinationHex);
                if (!LegacyPlayerPartyHexTravelCompatibility.TryCommitThroughSitePassage(
                        world,
                        motion,
                        destSite,
                        boundary,
                        destinationHex,
                        siteHexSize,
                        out var ingressPathIndex))
                {
                    PlayerPartySiteIngressTrace.Log(
                        "IngressAborted",
                        "reason=NoThroughSiteEgress site=" + destSite.SiteId);
                    return Result.Failure(
                        ErrorCode.InvalidOperation,
                        "No formal through-Site egress in active LegacyHexPath.");
                }

                motion.SetLegacyHexSegment(ingressPathIndex, 0f);
                ApplyTravelingMembersAtSite(world, destSite.SiteId);
                PlayerPartySiteIngressTrace.Log(
                    "AtSiteTransitCommit",
                    "site=" + destSite.SiteId +
                    " ingress=" + destinationHex +
                    " egress=" + motion.LegacySiteDepartureExitHex);
                return WorldTravelService.ActivatePreparedWorldSiteScene(
                    world, destSite, destinationMapId);
            }

            var hexSize = world.LegacyHexWorld.HexSize > 0f ? world.LegacyHexWorld.HexSize : 1f;
            var newWorldPos = WildernessLocalWorldProjection.ComputeCrossEdgeWorldPosition(
                motion.LegacyCurrentHex,
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

            // Preserve path / AutoTravel / ExecutionMode; only move position (never SetAtLegacyWorldPosition).
            motion.SetLegacyWorldPositionAndHex(newWorldPos, derived);
            ApplyTravelingMembersAtHex(world, derived);

            if (!LegacyWildernessLocalMapFallback.TryResolve(world, destinationHex, out var mapId) ||
                string.IsNullOrEmpty(mapId))
                return Result.Failure(ErrorCode.InvalidOperation, "No wilderness fallback LocalMap for exit hex.");

            // Advance Segment so the Host pauses after crossing (5C-W1 stops after one hex).
            if (motion.LegacyHexSegmentIndex + 1 < motion.LegacyHexPathCount)
                motion.SetLegacyHexSegment(motion.LegacyHexSegmentIndex + 1, 0f);

            return WorldTravelService.EnterLegacyWildernessLocalMap(world, destinationHex, mapId);
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
            XianXia.Core.World.Surface.SurfaceGroundNavigation navigation = null;
            var normalSurface = ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world) &&
                                world.SurfaceGround.TryResolveContaining(
                                    world.PlayerPartyTravel.WorldPosition, out navigation);
            for (var i = 0; i < members.Count; i++)
            {
                var id = members[i];
                if (id.IsNone)
                    continue;
                if (normalSurface)
                    world.WorldPresence.SetAtWorldPosition(
                        id, world.PlayerPartyTravel.WorldPosition, hex, navigation.SurfaceId);
                else
                    world.WorldPresence.SetLegacyAtHex(id, hex);
            }
        }

        static void ApplyTravelingMembersAtSite(SimulationWorld world, string siteId)
        {
            if (world?.WorldPresence == null ||
                world.PlayerPartyTravel == null ||
                string.IsNullOrEmpty(siteId))
                return;
            var members = world.PlayerPartyTravel.TravelingMembers;
            for (var i = 0; i < members.Count; i++)
            {
                var id = members[i];
                if (id.IsNone)
                    continue;
                world.WorldPresence.SetAtSite(id, siteId);
            }
        }

        /// <summary>
        /// Phase 5R-B6：WorldSite 正式 egress（LocalVisible 模式下）—— 对称于
        /// <see cref="TryCrossWildernessEdgePreservingLocalVisibleAutoTravel"/>。
        /// 角色在 Site LocalMap 内已走到正式 <see cref="SurfaceExitConnection"/> 出口：
        ///  - Canonical 置为 <c>BoundaryContactWorld</c>（严格位于 footprint perimeter，B3C3.1）；
        ///  - Context：AtWorldSite → AtWorldPosition（<see cref="PlayerPartyWorldMotion.SetLegacyWorldPositionAndHex"/>）；
        ///  - 保留 path / AutoTravel / ExecutionMode（不 Cancel / 不 Snap / 不 CompleteMove）；
        ///  - 推进 Segment（进入 exitHex → 下一段），随后展开外部 Wilderness LocalMap，
        ///    原 route 由既有 AtWorldPosition LocalVisible 驱动继续。
        /// 失败（非 AtWorldSite / 无 departure / connection 不匹配 / 外部格不可通行 / 无图）→
        /// 明确失败，不 teleport、不 fallback Anchor/Presence/hex center。
        /// </summary>
        public static Result TryCrossWorldSiteEdgePreservingLocalVisibleAutoTravel(
            SimulationWorld world,
            PlayerPartyRuntime party,
            SurfaceExitConnection connection)
        {
            if (world == null || party == null || !party.HasActive)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid world site egress args.");
            var motion = world.PlayerPartyTravel;
            if (motion == null || !motion.HasPosition)
                return Result.Failure(ErrorCode.InvalidOperation, "Party has no world position.");
            if (motion.LocationKind != PlayerPartyLocationKind.AtWorldSite ||
                string.IsNullOrEmpty(motion.SiteId))
                return Result.Failure(ErrorCode.InvalidOperation, "Not at a WorldSite.");
            if (!motion.IsLegacySiteDeparturePending)
                return Result.Failure(ErrorCode.InvalidOperation, "No site departure pending.");
            if (!IsActiveLocalVisibleAutoTravel(motion))
                return Result.Failure(ErrorCode.InvalidOperation, "LocalVisible AutoTravel required.");

            var external = connection.DestinationHex;
            if (!motion.LegacySiteDepartureExitHex.Equals(external))
                return Result.Failure(ErrorCode.InvalidOperation, "Exit connection destination is not the departure exit hex.");
            if (!IsGroundPassable(world.LegacyHexWorld, external))
                return Result.Failure(ErrorCode.InvalidOperation, "External hex is impassable.");

            var prepare = SurfaceExitTraversalService.TryPrepareTraversal(
                world, party, connection, out var prepared);
            if (prepare.IsFailure)
                return prepare;

            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);
            PlayerPartyTransitionMembership.LogPartyTransition(
                world,
                party,
                "ExitWorldSite.LocalVisiblePreserve",
                external,
                world.PartyWorld?.LocalMapId);

            // Phase 5R-B6.5-A：Canonical physical truth = boundary（BoundaryContactWorld）；
            // Route progress truth = FormalConnection.DestinationHex（已提交的 first outside hex）。
            // 不再用 WorldToHex(BoundaryContactWorld) 猜 route hex —— BoundaryContact 恰在 Hex
            // perimeter，multi-hex Site 内部 seam / corner 时天然可能 tie 回 footprint 格或邻格。
            var boundary = prepared.EntersWorldSite
                ? new WorldVec2(
                    prepared.DestinationIngress.BoundaryContactWorldX,
                    prepared.DestinationIngress.BoundaryContactWorldY)
                : new WorldVec2(connection.BoundaryContactWorldX, connection.BoundaryContactWorldY);
            motion.SetLegacyWorldPositionAndHex(boundary, prepared.DestinationHex);
            // Route progress 对齐到已提交 connection 的 DestinationHex（不重复推进、不跳过下一段）。
            LegacyPlayerPartyHexTravelCompatibility.AlignRouteProgressAfterSiteEgress(motion, prepared.DestinationHex);

            // LocalVisible AutoTravel 直连 Site→Site（external 属于另一 Site footprint）：与手动
            // exit 同规则 —— 必须建立目标 Site 正式 ingress context；无正式 destination ingress →
            // 明确失败（不 silent 进入、不依赖上一 Site 的 LastExitDirection 猜）。
            if (prepared.EntersWorldSite)
            {
                motion.LegacySurfaceEdgeGate?.SetIngressContext(prepared.DestinationIngress);
                return LegacyPlayerPartyHexTravelCompatibility.EnterWorldSiteAsParty(
                    world, party, prepared.DestinationSite, prepared.DestinationHex);
            }
            ApplyTravelingMembersAtHex(world, prepared.DestinationHex);
            return WorldTravelService.EnterLegacyWildernessLocalMap(
                world, prepared.DestinationHex, prepared.DestinationLocalMapId);
        }
    }
}


