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
        /// Phase 5R-B6.3：WorldSite departure 的正式 approach 点（可靠版）。
        /// 背景（B6.2→B6.3）：旧实现从 ExitCenter 沿 inward 退 inset 后 clamp 进 SlotRect，
        /// 对正常边 connection 有效；但对<b>角 / 双邻接 connection</b>（LocalDirection 斜对角，
        /// perimeter 射线落在另一条边，导致 SlotRect 沿边 span 可能越出 playable bounds）会出两类
        /// 确定性失败：① approach 被 clamp 到 SlotRect 内边缘，停点（arriveEpsilon≈0.2）偏内即
        /// 滑出触发带；② approach 沿边坐标落在 bounds 外（OOB cell），A* 不可达。人工观测 ~20%
        /// = 荒村 2/10 条角 connection 确定性失败。
        /// 修复：approach = <b>SlotRect 深度方向中点</b>（窄维度，距两缘 depth/2 = 0.625 &gt;
        /// 停点余量 0.3）+ <b>沿边方向取 SlotRect ∩ playable bounds 的中点</b>（恒在 walkable 边界内）。
        /// 数学保证：approach 及其停点区间（±0.2）都 ∈ SlotRect ∩ bounds —— 到达即 crossing。
        /// 不 teleport、不追 exact perimeter pixel；A* 终点 = 带内 walkable 点。
        /// </summary>
        public static void ResolveWorldSiteExitApproachLocalPoint(
            SurfaceExitConnection connection,
            WorldSiteSpatialMapping.WorldSiteLocalMapBounds bounds,
            float exitTriggerDepth,
            out float localX,
            out float localY)
        {
            var slot = connection.SlotRect;
            if (slot.Width <= slot.Height)
            {
                // X 主导（East/West 贴边带）：深度方向 = x（取中点），沿边方向 = y（slot∩bounds 中点）。
                localX = (slot.MinX + slot.MaxX) * 0.5f;
                var lo = Math.Max(slot.MinY, bounds.MinY);
                var hi = Math.Min(slot.MaxY, bounds.MaxY);
                localY = (lo + hi) * 0.5f;
            }
            else
            {
                // Y 主导（North/South 贴边带）：深度方向 = y（取中点），沿边方向 = x（slot∩bounds 中点）。
                localY = (slot.MinY + slot.MaxY) * 0.5f;
                var lo = Math.Max(slot.MinX, bounds.MinX);
                var hi = Math.Min(slot.MaxX, bounds.MaxX);
                localX = (lo + hi) * 0.5f;
            }

            // 防御：无效沿边区间（slot 完全在 bounds 外）时退回 slot 深度中点 + bounds 中心。
            if (float.IsNaN(localX) || float.IsNaN(localY))
            {
                localX = bounds.CenterX;
                localY = bounds.CenterY;
            }

            // 防御 clamp 到 playable bounds（SlotRect 由同一 bounds 派生，理论上已在其内）。
            localX = Math.Max(bounds.MinX, Math.Min(bounds.MaxX, localX));
            localY = Math.Max(bounds.MinY, Math.Min(bounds.MaxY, localY));
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
            {
                // Phase 5R-B3B.1/B7A: WorldSite → 正式 BoundaryContact Ingress。
                // destinationHex 是 approach 按距 start 最近方向选取的 footprint 格（多 Hex
                // footprint 不强制 Anchor）。目标 Site 仍完成 Travel；非目标 Site 则保持同一
                // HexPath / Destination，并从路径中解析正式 egress，进入 Site LocalMap 后继续。
                // 无正式 connection → 明确失败，不静默回退中心点。
                // 注：变量命名避开外层方法体块的 hexSize/derived（CS0136：子块不得与外层块同名）。
                var siteHexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                    ? world.HexWorld.HexSize
                    : 1f;
                if (!WorldSiteFootprintExitConnectionResolver.TryResolveFormalIngressConnection(
                        world,
                        destSite,
                        destinationHex,
                        motion.CurrentHex,
                        siteHexSize,
                        out var ingressConnection))
                    return Result.Failure(
                        ErrorCode.InvalidOperation,
                        "No formal site ingress connection from hex " + motion.CurrentHex +
                        " into site footprint hex " + destinationHex + " (5R-B3B.1).");

                var boundary = new WorldVec2(
                    ingressConnection.BoundaryContactWorldX,
                    ingressConnection.BoundaryContactWorldY);
                var ingressDerived = HexMath.WorldToHex(boundary.X, boundary.Y, siteHexSize);

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
                motion.SurfaceEdgeGate?.SetIngressContext(ingressConnection);
                var isDestinationSite =
                    !string.IsNullOrEmpty(motion.DestinationSiteId) &&
                    string.Equals(
                        motion.DestinationSiteId,
                        destSite.SiteId,
                        System.StringComparison.Ordinal);
                if (isDestinationSite)
                {
                    motion.SetWorldPositionInternal(boundary, ingressDerived);
                    ApplyTravelingMembersAtHex(world, ingressDerived);
                    return PlayerPartyHexTravelService.EnterWorldSiteAsParty(
                        world, party, destSite, destinationHex);
                }

                PlayerPartySiteIngressTrace.BeginIngress(
                    destSite.SiteId,
                    boundary,
                    motion.CurrentHex,
                    destinationHex);
                if (!PlayerPartyHexTravelService.TryCommitThroughSitePassage(
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
                        "No formal through-Site egress in active HexPath.");
                }

                motion.SetSegment(ingressPathIndex, 0f);
                ApplyTravelingMembersAtSite(world, destSite.SiteId);
                PlayerPartySiteIngressTrace.Log(
                    "AtSiteTransitCommit",
                    "site=" + destSite.SiteId +
                    " ingress=" + destinationHex +
                    " egress=" + motion.SiteDepartureExitHex);
                return WorldTravelService.EnterWorldSiteScene(
                    world, destSite.SiteId, string.Empty);
            }

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
        ///  - Context：AtWorldSite → AtWorldPosition（<see cref="PlayerPartyWorldMotion.SetWorldPositionInternal"/>）；
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
            if (!motion.IsSiteDeparturePending)
                return Result.Failure(ErrorCode.InvalidOperation, "No site departure pending.");
            if (!IsActiveLocalVisibleAutoTravel(motion))
                return Result.Failure(ErrorCode.InvalidOperation, "LocalVisible AutoTravel required.");

            var sourceFootprint = connection.SourceHex;
            var external = connection.DestinationHex;
            if (!motion.SiteDepartureFootprintHex.Equals(sourceFootprint))
                return Result.Failure(ErrorCode.InvalidOperation, "Exit connection source is not the departure footprint hex.");
            if (!motion.SiteDepartureExitHex.Equals(external))
                return Result.Failure(ErrorCode.InvalidOperation, "Exit connection destination is not the departure exit hex.");
            if (!IsGroundPassable(world.HexWorld, external))
                return Result.Failure(ErrorCode.InvalidOperation, "External hex is impassable.");

            var boundary = new WorldVec2(connection.BoundaryContactWorldX, connection.BoundaryContactWorldY);

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
            motion.SetWorldPositionInternal(boundary, connection.DestinationHex);
            // Route progress 对齐到已提交 connection 的 DestinationHex（不重复推进、不跳过下一段）。
            PlayerPartyHexTravelService.AlignRouteProgressAfterSiteEgress(motion, connection.DestinationHex);
            ApplyTravelingMembersAtHex(world, connection.DestinationHex);

            if (!WildernessLocalMapFallback.TryResolve(world, external, out var mapId) ||
                string.IsNullOrEmpty(mapId))
                return Result.Failure(ErrorCode.InvalidOperation, "No wilderness fallback LocalMap for exit hex.");

            return WorldTravelService.EnterWildernessLocalMap(world, external, mapId);
        }
    }
}
