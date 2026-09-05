using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// PlayerParty Hex／连续世界旅行（非 FormalArmy）。
    /// Phase 2C：AutoTravel 沿路径几何连续推进 WorldPosition；CurrentHex 派生。
    /// </summary>
    public static class PlayerPartyHexTravelService
    {
        static readonly List<HexCoord> PathScratch = new List<HexCoord>(64);

        /// <summary>相邻格心距折算为恒定速度的参考 tick 数（距离预算，非“每段固定 N tick”）。</summary>
        public const float GroundBaseStepTicks = 8f;

        public static bool TryResolvePartyWorldHex(
            SimulationWorld world,
            PlayerPartyRuntime party,
            out HexCoord worldHex)
        {
            worldHex = default;
            if (!PlayerPartyWorldLocationQuery.TryResolve(world, party, out var resolved))
                return false;
            worldHex = resolved.DerivedHex;
            return true;
        }

        public static bool TryResolvePartyWorldPosition(
            SimulationWorld world,
            PlayerPartyRuntime party,
            out WorldVec2 worldPos)
        {
            worldPos = default;
            if (!PlayerPartyWorldLocationQuery.TryResolve(world, party, out var resolved))
                return false;
            worldPos = resolved.WorldPosition;
            return true;
        }

        public static Result BeginTravel(
            SimulationWorld world,
            PlayerPartyRuntime party,
            HexCoord destination,
            HexTravelMode mode = HexTravelMode.Ground) =>
            BeginTravel(world, party, destination, string.Empty, mode);

        public static Result BeginTravel(
            SimulationWorld world,
            PlayerPartyRuntime party,
            HexCoord destination,
            string destinationSiteId,
            HexTravelMode mode = HexTravelMode.Ground)
        {
            if (world == null || party == null)
            {
                PlayerPartyWorldLocationDebug.Sink?.Invoke(
                    "[GatewayB1Trace] 7 BeginTravelEntered=true world/party null");
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid party travel args.");
            }

            PlayerPartyWorldLocationDebug.Sink?.Invoke(
                "[GatewayB1Trace] 7 BeginTravelEntered=true requestedDestinationHex=" + destination +
                " requestedDestinationSiteId=" + (destinationSiteId ?? string.Empty));
            if (!party.HasActive)
                return Result.Failure(ErrorCode.InvalidOperation, "PlayerParty has no active character.");
            if (mode != HexTravelMode.Ground)
                return Result.Failure(ErrorCode.InvalidOperation, "Only Ground travel is supported in V1.");
            if (!world.HexWorld.HasGrid)
                return Result.Failure(ErrorCode.InvalidOperation, "Hex grid not loaded.");
            if (!world.HexWorld.TryGetTile(destination, out var destTile) ||
                destTile == null ||
                !destTile.IsPassable)
                return Result.Failure(ErrorCode.InvalidArgument, "Destination hex is not passable.");

            if (!TryResolvePartyWorldHex(world, party, out var startHex))
                return Result.Failure(ErrorCode.InvalidOperation, "PlayerParty has no world hex.");

            destinationSiteId = TryCanonicalizeFootprintHexDestination(
                world, destination, destinationSiteId, out _);
            PlayerPartyWorldLocationDebug.Sink?.Invoke(
                "[GatewayB1Trace] 7b startHex=" + startHex +
                " canonicalDestinationSiteId=" + (destinationSiteId ?? string.Empty) +
                " fromSiteId=" + (world.PlayerPartyTravel != null
                    ? world.PlayerPartyTravel.SiteId
                    : string.Empty));

            // Phase 5R-B7A：WorldSite 是 Surface Context overlay，不是战略障碍。
            // 普通 PlayerParty route 只消费 HexWorld terrain / explicit passability；SiteId、
            // Anchor、Presence、是否为非目标 Site 均不改变 path topology。
            IReadOnlyCollection<HexCoord> blockedSiteHexes = null;

            // TargetHex V1：目的地语义为该格 canonical center（不存点击像素）。
            var goalHex = destination;
            if (!string.IsNullOrEmpty(destinationSiteId) &&
                world.Strategic.Sites.TryGet(destinationSiteId, out var targetSite) &&
                targetSite != null)
            {
                // Site 目标：路径落到 footprint 上 A* 实际代价最低的可达格（Phase 5R-B6.4，
                // 不再用 hex 直线距离）；进入后再聚合 PresenceHex。
                goalHex = ResolveDeterministicSiteApproachHex(
                    world, startHex, targetSite, blockedSiteHexes);
            }

            if (startHex == goalHex &&
                !world.PlayerPartyTravel.IsMoving &&
                string.IsNullOrEmpty(destinationSiteId))
                return Result.Failure(ErrorCode.InvalidArgument, "Already at destination hex.");

            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);
            var motion = world.PlayerPartyTravel;
            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;

            if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(motion.SiteId) &&
                world.Strategic.Sites.TryGet(motion.SiteId, out var fromSite) &&
                fromSite != null)
            {
                // 出发 Site 自身的 footprint 已在 blockedSiteHexes 中豁免（见上）；此处仅判断目标归属。
                // Phase 5R-B6 §十六：战略目标最终仍属于当前 Site footprint → 不启动 departure。
                if (fromSite.OccupiesHex(goalHex))
                    return Result.Failure(
                        ErrorCode.InvalidArgument,
                        "Target is inside current WorldSite; no egress required.");

                if (!TryBuildPathLeavingSite(
                        world,
                        fromSite,
                        startHex,
                        goalHex,
                        blockedSiteHexes,
                        PathScratch,
                        out var exitHex,
                        out var departureFootprintHex))
                {
                    PlayerPartyWorldLocationDebug.Sink?.Invoke(
                        "[GatewayB1Trace] 9e FinalResult=NoRoute");
                    return Result.Failure(ErrorCode.InvalidOperation, "No path leaving WorldSite.");
                }

                PlayerPartyWorldLocationDebug.Sink?.Invoke(
                    "[GatewayB1Trace] 8a SiteLeaveNormalRouteSuccess=true exitHex=" + exitHex);

                if (!BackgroundCharacterSiteDepartureResolver.TryResolveDepartureBoundaryEntryWorldPosition(
                        departureFootprintHex,
                        exitHex,
                        hexSize,
                        out var boundaryEntryPos))
                    boundaryEntryPos = HexCenter(exitHex, hexSize);

                var departureStartWorld = HexCenter(startHex, hexSize);
                ClearPartyWorldPresentationCacheForOpenWorld(world);
                PlayerPartyWorldLocationDebug.LogSnapshot(world, party, "BeginTravel.LeaveSiteOrOpenWorld");
                motion.BeginSiteDepartureTravel(
                    PathScratch,
                    goalHex,
                    destinationSiteId,
                    departureFootprintHex,
                    exitHex,
                    departureStartWorld,
                    boundaryEntryPos,
                    mode,
                    hexSize);
                ApplyTravelingMembersPresence(world);
                PlayerPartyWorldLocationDebug.LogSnapshot(world, party, "BeginTravel.AfterBeginAutoTravel");
                return Result.Success();
            }

            var normalRouteOk =
                HexPathfinder.TryFindPath(
                    world.HexWorld, startHex, goalHex, PathScratch, mode, blockedSiteHexes) &&
                PathScratch.Count >= 1;
            PlayerPartyWorldLocationDebug.Sink?.Invoke(
                "[GatewayB1Trace] 8b NormalRouteSuccess=" + normalRouteOk);
            if (!normalRouteOk)
            {
                PlayerPartyWorldLocationDebug.Sink?.Invoke(
                    "[GatewayB1Trace] 9e FinalResult=NoRoute");
                return Result.Failure(ErrorCode.InvalidOperation, "No hex path to destination.");
            }

            EnsureMotionHasContinuousStart(world, startHex);
            ApplyTravelingMembersPresence(world);

            // 正式离开 Site / 开始开世界旅行：清空 PartyWorld presentation cache，
            // 禁止残留 SiteId/LocalMapId 在 TravelComplete 后反写 Domain。
            ClearPartyWorldPresentationCacheForOpenWorld(world);
            PlayerPartyWorldLocationDebug.LogSnapshot(world, party, "BeginTravel.LeaveSiteOrOpenWorld");

            world.PlayerPartyTravel.BeginAutoTravel(
                PathScratch,
                goalHex,
                destinationSiteId,
                mode,
                hexSize);
            ApplyTravelingMembersPresence(world);
            PlayerPartyWorldLocationDebug.LogSnapshot(world, party, "BeginTravel.AfterBeginAutoTravel");
            return Result.Success();
        }

        /// <summary>LocalVisible Site egress 不可达时，仅替换 departure plan，不重置旅行意图。</summary>
        public static bool TryReplanCurrentWorldSiteDeparture(
            SimulationWorld world,
            PlayerPartyRuntime party,
            IReadOnlyCollection<HexCoord> locallyReachableExitHexes)
        {
            var motion = world?.PlayerPartyTravel;
            if (motion == null || party == null || !motion.IsMoving ||
                motion.LocationKind != PlayerPartyLocationKind.AtWorldSite ||
                !motion.IsSiteDeparturePending || string.IsNullOrEmpty(motion.SiteId) ||
                locallyReachableExitHexes == null || locallyReachableExitHexes.Count == 0 ||
                !world.Strategic.Sites.TryGet(motion.SiteId, out var site) || site == null)
                return false;
            if (!BackgroundCharacterSiteDepartureResolver.TryResolveDepartureHex(
                    world, site, motion.DestinationHex, locallyReachableExitHexes, out var exitHex) ||
                !BackgroundCharacterSiteDepartureResolver.TryResolveDepartureFootprintHex(site, exitHex, out var footprintHex))
                return false;
            PathScratch.Clear();
            if (!HexPathfinder.TryFindPath(world.HexWorld, motion.CurrentHex, footprintHex, PathScratch) ||
                PathScratch.Count < 1)
                return false;
            if (!PathScratch[PathScratch.Count - 1].Equals(exitHex))
                PathScratch.Add(exitHex);
            var tail = new List<HexCoord>(64);
            if (!exitHex.Equals(motion.DestinationHex) &&
                (!HexPathfinder.TryFindPath(world.HexWorld, exitHex, motion.DestinationHex, tail) || tail.Count < 1))
                return false;
            for (var i = 1; i < tail.Count; i++) PathScratch.Add(tail[i]);
            var size = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            BackgroundCharacterSiteDepartureResolver.TryResolveDepartureBoundaryEntryWorldPosition(
                footprintHex, exitHex, size, out var boundary);
            motion.ReplaceSiteDeparturePlan(PathScratch, footprintHex, exitHex, boundary);
            return true;
        }

        static string TryCanonicalizeFootprintHexDestination(
            SimulationWorld world,
            HexCoord destinationHex,
            string destinationSiteId,
            out bool canonicalizedFromFootprint)
        {
            canonicalizedFromFootprint = false;
            if (!string.IsNullOrEmpty(destinationSiteId))
                return destinationSiteId;

            if (world.Strategic?.Sites != null &&
                world.Strategic.Sites.TryGetAtHex(destinationHex, out var site) &&
                site != null)
            {
                canonicalizedFromFootprint = true;
                return site.SiteId;
            }

            return string.Empty;
        }

        static bool TryBuildPathLeavingSite(
            SimulationWorld world,
            WorldSite site,
            HexCoord startHex,
            HexCoord goalHex,
            IReadOnlyCollection<HexCoord> blocked,
            List<HexCoord> into,
            out HexCoord exitHex,
            out HexCoord departureFootprintHex)
        {
            into.Clear();
            exitHex = default;
            departureFootprintHex = default;
            if (!BackgroundCharacterSiteDepartureResolver.TryResolveDepartureHex(world, site, goalHex, out exitHex))
                return false;

            if (!BackgroundCharacterSiteDepartureResolver.TryResolveDepartureFootprintHex(
                    site,
                    exitHex,
                    out departureFootprintHex))
                return false;

            var scratch = new List<HexCoord>(64);
            if (!startHex.Equals(departureFootprintHex) &&
                HexPathfinder.TryFindPath(world.HexWorld, startHex, departureFootprintHex, scratch, HexTravelMode.Ground, blocked) &&
                scratch.Count >= 1)
            {
                for (var i = 0; i < scratch.Count; i++)
                {
                    if (into.Count == 0 || !into[into.Count - 1].Equals(scratch[i]))
                        into.Add(scratch[i]);
                }
            }
            else if (into.Count == 0 || !into[into.Count - 1].Equals(departureFootprintHex))
            {
                into.Add(departureFootprintHex);
            }

            if (!into[into.Count - 1].Equals(departureFootprintHex))
                into.Add(departureFootprintHex);

            if (!into[into.Count - 1].Equals(exitHex))
                into.Add(exitHex);

            if (exitHex == goalHex)
                return into.Count >= 2;

            scratch.Clear();
            if (!HexPathfinder.TryFindPath(
                    world.HexWorld, exitHex, goalHex, scratch, HexTravelMode.Ground, blocked) ||
                scratch.Count < 1)
                return false;

            for (var i = 1; i < scratch.Count; i++)
            {
                if (into.Count == 0 || !into[into.Count - 1].Equals(scratch[i]))
                    into.Add(scratch[i]);
            }

            return into.Count >= 2;
        }

        static WorldVec2 HexCenter(HexCoord hex, float hexSize)
        {
            HexMath.ToWorldPosition(hex, hexSize, out var x, out var y);
            return new WorldVec2(x, y);
        }

        /// <summary>
        /// PartyWorld 仅作已展开 LocalMap presentation 缓存；开世界旅行期间不得保留 Site 权威。
        /// </summary>
        public static void ClearPartyWorldPresentationCacheForOpenWorld(SimulationWorld world)
        {
            if (world?.PartyWorld == null)
                return;
            world.PartyWorld.ClearSiteFocus();
            world.PartyWorld.LocalMapId = string.Empty;
            world.PartyWorld.Mode = PartyWorldPresenceMode.AtHex;
            world.PartyWorld.EncounterId = string.Empty;
        }

        public static Result CancelTravel(SimulationWorld world, PlayerPartyRuntime party = null)
        {
            if (world?.PlayerPartyTravel == null)
                return Result.Failure(ErrorCode.InvalidArgument, "No party travel state.");
            if (!world.PlayerPartyTravel.IsMoving)
                return Result.Failure(ErrorCode.InvalidOperation, "PlayerParty is not traveling.");

            world.PlayerPartyTravel.CancelAutoTravelPreservePosition();
            ApplyTravelingMembersPresence(world);
            return Result.Success();
        }

        public static float WorldUnitsPerTick(float hexSize)
        {
            var size = hexSize > 0.0001f ? hexSize : 1f;
            var adjacentCenterDist = size * (float)Math.Sqrt(3.0);
            return adjacentCenterDist / GroundBaseStepTicks;
        }

        public static void AdvanceAll(SimulationWorld world, int ticks)
        {
            if (world?.PlayerPartyTravel == null || ticks < 1)
                return;
            if (!world.PlayerPartyTravel.IsMoving)
                return;
            // Phase 5B: LocalVisible => World Tick must not advance PlayerParty.
            if (world.PlayerPartyTravel.ExecutionMode == PlayerPartyTravelExecutionMode.LocalVisible)
                return;

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            AdvanceDistanceBudget(world, WorldUnitsPerTick(hexSize) * ticks);
        }

        /// <summary>沿路径几何消耗距离预算（可跨段，段边界不暂停）。</summary>
        public static void AdvanceDistanceBudget(SimulationWorld world, float distanceBudget)
        {
            if (world?.PlayerPartyTravel == null || distanceBudget <= 0f)
                return;
            if (!world.PlayerPartyTravel.IsMoving)
                return;
            if (world.PlayerPartyTravel.ExecutionMode == PlayerPartyTravelExecutionMode.LocalVisible)
                return;

            var motion = world.PlayerPartyTravel;
            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;

            // Phase 5R-B6.5-B：AtWorldSite + departure + World executor（WorldMap open + Running）
            // → Canonical 沿直线朝正式 BoundaryContactWorld 推进（唯一 physical truth），到达后正式
            // egress commit（AtWorldPosition + route 对齐 DestinationHex），后续段继续由 World
            // executor 推进。不再阻断（旧 B6：WorldMap open 期间仅形成 DeparturePlan，绝不把
            // Canonical 提前拉到 footprint 外 —— 现按 B6.5-B5/B6 改为 World executor 语义）。
            if (motion.IsSiteDeparturePending &&
                motion.LocationKind == PlayerPartyLocationKind.AtWorldSite)
            {
                AdvanceWorldSiteDepartureCanonical(world, motion, hexSize, distanceBudget);
                return;
            }
            var pos = motion.IsSiteDeparturePending &&
                      motion.LocationKind == PlayerPartyLocationKind.AtWorldSite
                ? motion.SiteDepartureVirtualPosition
                : motion.UsesTravelPresentation &&
                  motion.LocationKind == PlayerPartyLocationKind.AtWorldSite
                    ? motion.TravelPresentationPosition
                    : motion.WorldPosition;
            var previousDerived = motion.CurrentHex;
            var isSiteDepartureVirtual = motion.IsSiteDeparturePending &&
                                           motion.LocationKind == PlayerPartyLocationKind.AtWorldSite;

            var remainingBudget = distanceBudget;
            var guard = 0;
            while (remainingBudget > 0.0001f && motion.IsMoving && guard++ < 64)
            {
                if (!motion.TryGetActiveSegmentWorld(hexSize, out var fromPos, out var toPos))
                {
                    if (isSiteDepartureVirtual)
                    {
                        CommitSiteDepartureBoundaryCrossing(world, motion, hexSize);
                        if (!motion.IsMoving)
                            return;
                        pos = motion.WorldPosition;
                        previousDerived = motion.CurrentHex;
                        isSiteDepartureVirtual = false;
                        continue;
                    }

                    FinishArrival(world);
                    return;
                }

                var segmentLen = WorldVec2.Distance(fromPos, toPos);
                if (segmentLen < 0.0001f)
                {
                    motion.IncrementPathIndex();
                    if (motion.SegmentIndex >= motion.HexPathCount - 1)
                    {
                        if (isSiteDepartureVirtual)
                        {
                            CommitSiteDepartureBoundaryCrossing(world, motion, hexSize);
                            if (!motion.IsMoving)
                                return;
                            pos = motion.WorldPosition;
                            previousDerived = motion.CurrentHex;
                            isSiteDepartureVirtual = false;
                            continue;
                        }

                        FinishArrival(world);
                        return;
                    }

                    continue;
                }

                var remainingOnSegment = WorldVec2.Distance(pos, toPos);
                if (remainingOnSegment <= remainingBudget + 0.0001f)
                {
                    var previousPos = pos;
                    pos = toPos;
                    if (isSiteDepartureVirtual)
                    {
                        var previousHex = motion.CurrentHex;
                        motion.SetSiteDepartureVirtualPosition(pos, hexSize);
                        if (ShouldCommitSiteDepartureBoundaryCrossing(world, motion, previousHex, motion.CurrentHex))
                            CommitSiteDepartureBoundaryCrossing(world, motion, hexSize);
                        if (!motion.IsMoving)
                            return;
                        pos = ResolveTravelPosition(motion);
                        previousDerived = motion.CurrentHex;
                        isSiteDepartureVirtual = motion.IsSiteDeparturePending &&
                                                  motion.LocationKind == PlayerPartyLocationKind.AtWorldSite;
                        remainingBudget -= remainingOnSegment;
                        ApplyTravelingMembersPresence(world);
                        continue;
                    }

                    var derived = HexMath.WorldToHex(pos.X, pos.Y, hexSize);
                    TryCommitSiteArrivalIngress(world, motion, previousDerived, derived, pos, hexSize);
                    CommitCanonicalWorldPosition(world, motion, pos, derived);
                    previousDerived = motion.CurrentHex;
                    motion.SetSegment(motion.SegmentIndex, 1f);
                    remainingBudget -= remainingOnSegment;
                    motion.IncrementPathIndex();
                    ApplyTravelingMembersPresence(world);
                    if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                        motion.IsSiteDeparturePending)
                    {
                        // B7A through-Site：进入非目标 footprint 后，余下预算直接交给与
                        // LocalVisible 共用的正式 Site departure authority；不沿仅供战略
                        // topology 的 footprint hex-center 前缀移动。
                        if (remainingBudget > 0.0001f)
                            AdvanceWorldSiteDepartureCanonical(
                                world, motion, hexSize, remainingBudget);
                        return;
                    }
                    if (motion.SegmentIndex >= motion.HexPathCount - 1)
                    {
                        FinishArrival(world);
                        return;
                    }

                    pos = ResolveTravelPosition(motion);
                    continue;
                }

                var dirX = (toPos.X - pos.X) / remainingOnSegment;
                var dirY = (toPos.Y - pos.Y) / remainingOnSegment;
                var previousPosMid = pos;
                pos = new WorldVec2(pos.X + dirX * remainingBudget, pos.Y + dirY * remainingBudget);
                if (isSiteDepartureVirtual)
                {
                    var previousHex = motion.CurrentHex;
                    motion.SetSiteDepartureVirtualPosition(pos, hexSize);
                    if (ShouldCommitSiteDepartureBoundaryCrossing(world, motion, previousHex, motion.CurrentHex))
                        CommitSiteDepartureBoundaryCrossing(world, motion, hexSize);
                    if (!motion.IsMoving)
                        return;
                    if (motion.IsSiteDeparturePending &&
                        motion.LocationKind == PlayerPartyLocationKind.AtWorldSite)
                    {
                        var virtualProgress = 1f - WorldVec2.Distance(pos, toPos) / segmentLen;
                        motion.SetSegment(motion.SegmentIndex, virtualProgress);
                        ApplyTravelingMembersPresence(world);
                        remainingBudget = 0f;
                        continue;
                    }

                    pos = motion.WorldPosition;
                    previousDerived = motion.CurrentHex;
                    isSiteDepartureVirtual = false;
                    var used = WorldVec2.Distance(previousPosMid, pos);
                    remainingBudget = Math.Max(0f, remainingBudget - used);
                    continue;
                }

                // Phase 5R-B6.5-A：段内推进的 route hex = 当前段起点（route truth），不实时
                // WorldToHex(pos)。pos 在 hex perimeter 附近（典型：egress 后 WorldPosition =
                // BoundaryContact，恰在共享边中点）时 WorldToHex 会 tie 到错误格，把 CurrentHex
                // 从已提交的 DestinationHex 拉走 → 后续段推进错乱。跨段才更新（段完成分支用
                // toPos=下一 hex center，WorldToHex 无 tie）。
                var midDerived = motion.CurrentHex;
                TryCommitSiteArrivalIngress(world, motion, previousDerived, midDerived, pos, hexSize);
                CommitCanonicalWorldPosition(world, motion, pos, midDerived);
                previousDerived = motion.CurrentHex;
                pos = ResolveTravelPosition(motion);
                var progress = 1f - WorldVec2.Distance(pos, toPos) / segmentLen;
                motion.SetSegment(motion.SegmentIndex, progress);
                ApplyTravelingMembersPresence(world);
                remainingBudget = 0f;
            }
        }

        static WorldVec2 ResolveTravelPosition(PlayerPartyWorldMotion motion)
        {
            if (motion.IsSiteDeparturePending &&
                motion.LocationKind == PlayerPartyLocationKind.AtWorldSite)
                return motion.SiteDepartureVirtualPosition;
            if (motion.UsesTravelPresentation &&
                motion.LocationKind == PlayerPartyLocationKind.AtWorldSite)
                return motion.TravelPresentationPosition;
            return motion.WorldPosition;
        }

        /// <summary>
        /// Phase 5R-B6.5-B：World executor 的 AtWorldSite departure 推进（WorldMap open + Running）。
        /// Canonical（WorldPosition）是唯一 physical truth：沿直线朝正式 BoundaryContactWorld
        /// （SiteDepartureBoundaryEntry）消耗 distance budget 推进；到达后
        /// <see cref="CommitSiteDepartureBoundaryCrossing"/> 正式 egress（AtWorldPosition +
        /// CurrentHex=SiteDepartureExitHex + route 对齐），随后用剩余预算继续推进 AtWorldPosition 段。
        /// 不使用 SiteDepartureVirtualPosition（旧 presentation 状态，不重新变成 physical truth）；
        /// 不 teleport 到 hex center；不改 B4 Local sync；不新增第二套 route。
        /// </summary>
        static void AdvanceWorldSiteDepartureCanonical(
            SimulationWorld world,
            PlayerPartyWorldMotion motion,
            float hexSize,
            float distanceBudget)
        {
            var target = motion.SiteDepartureBoundaryEntry;
            if (float.IsNaN(target.X) || float.IsNaN(target.Y))
                return; // 无正式 Boundary（departure 尚未完整形成）：保持现状，等待 close 后 LocalVisible。

            var d = WorldVec2.Distance(motion.WorldPosition, target);
            if (d <= distanceBudget + 0.0001f)
            {
                CommitSiteDepartureBoundaryCrossing(world, motion, hexSize);
                if (!motion.IsMoving)
                    return;
                var remaining = Math.Max(0f, distanceBudget - d);
                if (remaining > 0.0001f)
                    AdvanceDistanceBudget(world, remaining);
                return;
            }

            var t = distanceBudget / d;
            var nx = motion.WorldPosition.X + (target.X - motion.WorldPosition.X) * t;
            var ny = motion.WorldPosition.Y + (target.Y - motion.WorldPosition.Y) * t;
            // Canonical 推进（AtWorldSite context 保留）：复用 B4 sync 的 context-preserving API。
            motion.TryUpdateWorldPositionWithinSite(motion.SiteId, new WorldVec2(nx, ny));
            ApplyTravelingMembersPresence(world);
        }

        /// <summary>
        /// Phase 5R-B6.5-A：egress commit 后把 Route Progress 对齐到已提交的 first outside hex
        /// （FormalConnection.DestinationHex）。在 HexPath 中定位该格并把 SegmentIndex 设为其起点段；
        /// 找不到（防御）才退回旧 +1 推进。exactly once、不重复推进、不跳过下一段、不依赖
        /// WorldToHex(BoundaryContactWorld) tie-break。
        /// 背景：LocalVisible departure 全程 SegmentIndex 恒 0（SyncSegmentProgressFromWorldPosition
        /// 只写 progress），旧 SetSegment(SegmentIndex+1,0) 在 HexPath 前部含多个 footprint hex
        /// （multi-hex Site 内部 seam 出发）时会落在 footprint 内部段 → 后续 Wilderness Exit 匹配
        /// 失败 → 出了 Site 就停下。
        /// LocalVisible egress（TryCrossWorldSiteEdgePreservingLocalVisibleAutoTravel）与 World
        /// executor commit（CommitSiteDepartureBoundaryCrossing）共用。
        /// </summary>
        public static void AlignRouteProgressAfterSiteEgress(
            PlayerPartyWorldMotion motion,
            HexCoord committedOutsideHex)
        {
            if (motion == null)
                return;
            var path = motion.HexPath;
            var idx = -1;
            for (var i = 0; i < path.Count; i++)
            {
                if (path[i].Equals(committedOutsideHex))
                {
                    idx = i;
                    break;
                }
            }

            if (idx >= 0)
            {
                motion.SetSegment(idx, 0f);
                return;
            }

            if (motion.SegmentIndex + 1 < motion.HexPathCount)
                motion.SetSegment(motion.SegmentIndex + 1, 0f);
        }

        static void CommitSiteDepartureBoundaryCrossing(
            SimulationWorld world,
            PlayerPartyWorldMotion motion,
            float hexSize)
        {
            if (!motion.IsSiteDeparturePending)
                return;

            var boundaryEntry = motion.SiteDepartureBoundaryEntry;
            var exitHex = motion.SiteDepartureExitHex;
            // Phase 5R-B6.5-A：Route progress truth = SiteDepartureExitHex（= FormalConnection.
            // DestinationHex，已提交的 first outside hex），不依赖 WorldToHex(BoundaryContactWorld)
            // 的 perimeter tie。Canonical physical truth = boundaryEntry（WorldPosition）。
            motion.SetWorldPositionInternal(boundaryEntry, exitHex);
            AlignRouteProgressAfterSiteEgress(motion, exitHex);
            motion.ClearSiteDeparturePending();
        }

        static bool ShouldCommitSiteDepartureBoundaryCrossing(
            SimulationWorld world,
            PlayerPartyWorldMotion motion,
            HexCoord previousHex,
            HexCoord newHex)
        {
            if (!motion.IsSiteDeparturePending || string.IsNullOrEmpty(motion.SiteId))
                return false;
            if (!world.Strategic.Sites.TryGet(motion.SiteId, out var site) || site == null)
                return false;
            return site.OccupiesHex(previousHex) && !site.OccupiesHex(newHex);
        }

        static void TryCommitSiteArrivalIngress(
            SimulationWorld world,
            PlayerPartyWorldMotion motion,
            HexCoord previousDerived,
            HexCoord newDerived,
            WorldVec2 presentationPos,
            float hexSize)
        {
            if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite)
                return;

            var destSiteId = motion.DestinationSiteId ?? string.Empty;
            if (string.IsNullOrEmpty(destSiteId))
                destSiteId = TryCanonicalizeFootprintHexDestination(
                    world, motion.DestinationHex, destSiteId, out _);

            if (!WorldSiteFootprintLocationAuthority.TryDetectDestinationSiteIngress(
                    world,
                    previousDerived,
                    newDerived,
                    destSiteId,
                    out var site) ||
                site == null)
                return;

            motion.CommitSiteArrivalAuthority(site.SiteId, presentationPos, newDerived);
        }

        static void CommitCanonicalWorldPosition(
            SimulationWorld world,
            PlayerPartyWorldMotion motion,
            WorldVec2 pos,
            HexCoord derived)
        {
            if (WorldSiteFootprintLocationAuthority.TryGetSiteAtHex(world, derived, out var site) &&
                site != null &&
                motion.LocationKind != PlayerPartyLocationKind.AtWorldSite)
            {
                var isDestination =
                    !string.IsNullOrEmpty(motion.DestinationSiteId) &&
                    string.Equals(
                        motion.DestinationSiteId,
                        site.SiteId,
                        StringComparison.Ordinal);
                if (isDestination)
                {
                    motion.CommitSiteArrivalAuthority(site.SiteId, pos, derived);
                    return;
                }

                var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
                // 非目标 Site：Context 进入，但保留同一个 Travel Order，并立即由 HexPath
                // 解析正式 through-egress。若路线没有合法离开该 Site 的相邻段，则不伪造
                // Context/teleport，继续保持普通 Surface position。
                if (TryCommitThroughSitePassage(
                        world, motion, site, pos, derived, hexSize, out _))
                    return;
            }

            if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                motion.UsesTravelPresentation)
            {
                if (world.Strategic.Sites.TryGet(motion.SiteId, out var currentSite) &&
                    currentSite != null &&
                    currentSite.OccupiesHex(derived))
                {
                    motion.SetTravelPresentation(pos, derived);
                    return;
                }

                // 防御：World executor 已离开 through-Site footprint，但 departure transient
                // 未接管时也必须恢复 Surface Context；保留 Travel，不 CompleteMove。
                motion.SetWorldPositionInternal(pos, derived);
                return;
            }

            motion.SetWorldPositionInternal(pos, derived);
        }

        /// <summary>
        /// Phase 5R-B7A：把 HexPath 中连续的非目标 Site footprint 段解释为一次
        /// Wilderness→Site→Wilderness Context passage。路径与 Destination 原样保留；
        /// 只建立正式 ingress 后的 departure transient。World executor 与 LocalVisible ingress
        /// 共用，避免两套 through-Site topology。
        /// </summary>
        public static bool TryCommitThroughSitePassage(
            SimulationWorld world,
            PlayerPartyWorldMotion motion,
            WorldSite site,
            WorldVec2 ingressWorldPosition,
            HexCoord ingressFootprintHex,
            float hexSize,
            out int ingressPathIndex)
        {
            ingressPathIndex = -1;
            if (world == null || motion == null || site == null || !motion.IsMoving)
                return false;
            if (!site.OccupiesHex(ingressFootprintHex))
                return false;
            if (!string.IsNullOrEmpty(motion.DestinationSiteId) &&
                string.Equals(motion.DestinationSiteId, site.SiteId, StringComparison.Ordinal))
                return false;

            var path = motion.HexPath;
            var searchFrom = Math.Max(0, motion.SegmentIndex);
            for (var i = searchFrom; i < path.Count; i++)
            {
                if (path[i].Equals(ingressFootprintHex))
                {
                    ingressPathIndex = i;
                    break;
                }
            }

            if (ingressPathIndex < 0)
                return false;

            var lastFootprintIndex = ingressPathIndex;
            while (lastFootprintIndex + 1 < path.Count &&
                   site.OccupiesHex(path[lastFootprintIndex + 1]))
                lastFootprintIndex++;
            if (lastFootprintIndex + 1 >= path.Count)
                return false;

            var footprintHex = path[lastFootprintIndex];
            var exitHex = path[lastFootprintIndex + 1];
            var adjacent = false;
            for (var d = 0; d < HexMath.DirectionCount; d++)
            {
                if (HexMath.Neighbor(footprintHex, d).Equals(exitHex))
                {
                    adjacent = true;
                    break;
                }
            }

            if (!adjacent ||
                !BackgroundCharacterSiteDepartureResolver
                    .TryResolveDepartureBoundaryEntryWorldPosition(
                        footprintHex,
                        exitHex,
                        hexSize,
                        out var boundaryEntry))
                return false;

            motion.CommitSiteArrivalAuthority(
                site.SiteId,
                ingressWorldPosition,
                ingressFootprintHex);
            motion.PlanThroughSiteDeparture(footprintHex, exitHex, boundaryEntry);
            return true;
        }

        /// <summary>
        /// Phase 5C-W2 LocalVisible Final Wilderness Arrival（专用完成路径）。
        /// 仅当 Active 已真实走到目标 Wilderness LocalMap 中心附近后才调用。
        /// 只结束 AutoTravel（Idle / ExecutionMode=None / clear path），保留
        /// WorldPosition / PartyWorld.LocalMapId / 当前 LocalMap / Occupants / Presentation：
        /// 不 SnapToHexCenter、不 ClearPartyWorldPresentationCacheForOpenWorld、不重新 Materialize。
        /// 旧 FinishArrival 保留给 World Executor 使用。
        /// </summary>
        public static Result CompleteWildernessFinalArrival(SimulationWorld world)
        {
            if (world?.PlayerPartyTravel == null)
                return Result.Failure(ErrorCode.InvalidArgument, "No party travel state.");
            var motion = world.PlayerPartyTravel;
            if (!motion.IsMoving)
                return Result.Failure(ErrorCode.InvalidOperation, "Travel already ended.");
            if (motion.ExecutionMode != PlayerPartyTravelExecutionMode.LocalVisible)
                return Result.Failure(ErrorCode.InvalidOperation, "Final arrival requires LocalVisible.");
            if (motion.LocationKind != PlayerPartyLocationKind.AtWorldPosition)
                return Result.Failure(ErrorCode.InvalidOperation, "Final arrival requires wilderness position.");
            if (!motion.CurrentHex.Equals(motion.DestinationHex))
                return Result.Failure(ErrorCode.InvalidOperation, "Not at destination hex yet.");
            if (!string.IsNullOrEmpty(motion.DestinationSiteId))
                return Result.Failure(ErrorCode.InvalidOperation, "Destination is a WorldSite (out of scope).");

            var destHex = motion.DestinationHex;

            // 只结束 AutoTravel：保留 WorldPosition / LocalMap / Occupants / Presentation。
            motion.CancelAutoTravelPreservePosition();

            // Presence 保持当前目标 Wilderness Hex（不 ClearPartyWorldPresentationCacheForOpenWorld）。
            ApplyTravelingMembersAtHex(world, destHex);

            return Result.Success();
        }

        static void FinishArrival(SimulationWorld world)
        {
            var motion = world.PlayerPartyTravel;
            if (motion == null)
                return;

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var kindBefore = motion.LocationKind;
            var siteBefore = motion.SiteId ?? string.Empty;
            var posBefore = motion.WorldPosition;
            var hexBefore = motion.CurrentHex;
            var destSiteId = motion.DestinationSiteId ?? string.Empty;
            var destHex = motion.DestinationHex;
            if (string.IsNullOrEmpty(destSiteId))
                destSiteId = TryCanonicalizeFootprintHexDestination(
                    world, destHex, destSiteId, out _);

            // Phase 5R-B3B：Site 目标<b>不</b> SnapToHexCenter —— Physical Position 保持段行进中
            // TryCommitSiteArrivalIngress → CommitSiteArrivalAuthority 已提交的跨边界连续位置。
            if (!string.IsNullOrEmpty(destSiteId) &&
                world.Strategic.Sites.TryGet(destSiteId, out var site) &&
                site != null)
            {
                // 正常路径：行进中已跨入 footprint → AtWorldSite 已成立，仅收尾 Presence / Context。
                if (motion.LocationKind != PlayerPartyLocationKind.AtWorldSite ||
                    !string.Equals(motion.SiteId, site.SiteId, StringComparison.Ordinal))
                {
                    // 退化：目标 Site 但从未在行进中跨入（应极少）。用当前连续 Canonical WorldPosition
                    // 补提交；无位置时记录链路缺口。<b>5R-D 暂存：</b>仅此异常路径保留旧 hex-center snap
                    // 语义作为 legacy compatibility，与正常 PlayerParty ingress 主链隔离 —— 主链无
                    // position 时 EnterWorldSiteAsParty 直接 Failure，绝不静默 snap（B3B.1 §六）。
                    if (motion.HasPosition)
                    {
                        if (!motion.TrySetAtWorldSitePreservingWorldPosition(
                                site.SiteId,
                                motion.WorldPosition))
                            PlayerPartyWorldLocationDebug.LogBeforeAfter(
                                world, null, "FinishArrival.SiteCommitFailed",
                                kindBefore, siteBefore, posBefore, hexBefore);
                    }
                    else
                    {
                        PlayerPartyWorldLocationDebug.LogBeforeAfter(
                            world, null, "FinishArrival.NoCanonicalGap",
                            kindBefore, siteBefore, posBefore, hexBefore);
                        // Legacy snap（5R-D 删除）：仅异常退化路径兑底，不属正式 ingress 主链。
                        motion.SetAtWorldSite(site.SiteId, destHex, hexSize);
                    }
                }

                ApplyTravelingMembersAtSite(world, site.SiteId);
                // Site 到达：Domain 已是 AtWorldSite；PartyWorld 仍等玩家关地图再 Expand。
                ClearPartyWorldPresentationCacheForOpenWorld(world);
            }
            else
            {
                // 普通 TargetHex：停在目标 canonical center，保持 AtWorldPosition。
                motion.SnapToHexCenter(destHex, hexSize);
                ClearPartyWorldPresentationCacheForOpenWorld(world);
                ApplyTravelingMembersAtHex(world, destHex);
            }

            motion.CompleteMove();
            // Idle 后再次确保 Presence 与 LocationKind 一致（禁止 Complete 后再被 Site 覆盖）。
            ApplyTravelingMembersPresence(world);
            PlayerPartyWorldLocationDebug.LogBeforeAfter(
                world,
                null,
                "TravelComplete",
                kindBefore,
                siteBefore,
                posBefore,
                hexBefore);
        }

        public static Result EnterLocalViewAtCurrentHex(
            SimulationWorld world,
            PlayerPartyRuntime party) =>
            EnterLocalViewAtCurrentHex(world, party, allowWhileTraveling: false);

        /// <param name="allowWhileTraveling">
        /// Phase 5B: Close WorldMap Preserve Travel — Mid-Segment enter without Cancel/Snap.
        /// </param>
        public static Result EnterLocalViewAtCurrentHex(
            SimulationWorld world,
            PlayerPartyRuntime party,
            bool allowWhileTraveling)
        {
            if (world == null || party == null || !party.HasActive)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid party enter args.");

            var moving = world.PlayerPartyTravel != null && world.PlayerPartyTravel.IsMoving;
            if (moving && !allowWhileTraveling)
                return Result.Failure(ErrorCode.InvalidOperation, "Stop travel before entering local view.");

            if (moving && allowWhileTraveling)
                return EnterLocalViewPreservingAutoTravel(world, party);

            if (!PlayerPartyWorldLocationQuery.TryResolve(world, party, out var resolved))
                return Result.Failure(ErrorCode.InvalidOperation, "PlayerParty has no world location.");

            if (resolved.LocationKind == PlayerPartyLocationKind.AtWorldSite)
            {
                if (!world.Strategic.Sites.TryGet(resolved.SiteId, out var focusSite) || focusSite == null)
                    return Result.Failure(ErrorCode.NotFound, "WorldSite missing.", resolved.SiteId);

                // Peek：已在该 Site LocalMap → 不重进、不重置 LocalPosition。
                if (string.Equals(world.PartyWorld?.SiteId, focusSite.SiteId, System.StringComparison.Ordinal) &&
                    string.Equals(
                        world.PartyWorld?.LocalMapId?.Trim() ?? string.Empty,
                        focusSite.LocalMapId?.Trim() ?? string.Empty,
                        System.StringComparison.Ordinal))
                {
                    var hexSizePeek = world.HexWorld != null && world.HexWorld.HexSize > 0f
                        ? world.HexWorld.HexSize
                        : 1f;
                    // Phase 5R-B3B：Site LocalMap 重开不 snap —— 有可信 Canonical 时保持连续位置；
                    // 无位置时才回退 legacy PresenceHex（兼容，随 5R-D 删除）。
                    if (!world.PlayerPartyTravel.HasPosition ||
                        !world.PlayerPartyTravel.TrySetAtWorldSitePreservingWorldPosition(
                            focusSite.SiteId,
                            world.PlayerPartyTravel.ResolveTravelPresentationWorld(hexSizePeek)))
                    {
                        world.PlayerPartyTravel.SetAtWorldSite(
                            focusSite.SiteId,
                            focusSite.PresenceHex,
                            hexSizePeek);
                    }
                    ApplyTravelingMembersAtSite(world, focusSite.SiteId);
                    PlayerPartyWorldLocationDebug.LogTransition(world, party, "EnterLocalView.SitePeekRestore");
                    return Result.Success();
                }

                return EnterWorldSiteAsParty(world, party, focusSite);
            }

            // AtWorldPosition：唯一依据 DerivedHex → Terrain Fallback。
            var hex = resolved.DerivedHex;
            if (!WildernessLocalMapFallback.TryResolve(world, hex, out var mapId) ||
                string.IsNullOrEmpty(mapId))
                return Result.Failure(ErrorCode.InvalidOperation, "No wilderness fallback LocalMap for hex.");

            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);
            EnsureMotionHasContinuousStart(world, hex);
            ApplyTravelingMembersPresence(world);
            PlayerPartyWorldLocationDebug.LogTransition(world, party, "EnterLocalView.Wilderness");
            return WorldTravelService.EnterWildernessLocalMap(world, hex, mapId);
        }

        /// <summary>
        /// Phase 5B Mid-Segment Takeover: keep AutoTravel fields; expand Wilderness LocalMap at continuous position.
        /// Forbids Cancel / CompleteMove / Snap / BeginTravel / SetAtWorldSite (clears path).
        /// </summary>
        static Result EnterLocalViewPreservingAutoTravel(
            SimulationWorld world,
            PlayerPartyRuntime party)
        {
            var motion = world.PlayerPartyTravel;
            if (motion == null || !motion.IsMoving)
                return Result.Failure(ErrorCode.InvalidOperation, "Preserve enter requires AutoTravel.");

            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);
            // Align Presence to derived hex only; never Snap / EnsureMotionHasContinuousStart.
            ApplyTravelingMembersPresence(world);

            if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(motion.SiteId))
            {
                if (!world.Strategic.Sites.TryGet(motion.SiteId, out var site) || site == null)
                    return Result.Failure(ErrorCode.NotFound, "WorldSite missing.", motion.SiteId);
                PlayerPartyWorldLocationDebug.LogTransition(
                    world, party, "EnterLocalView.PreserveAutoTravelThroughSite");
                return WorldTravelService.EnterWorldSiteScene(world, site.SiteId, string.Empty);
            }

            var hex = motion.CurrentHex;
            if (!WildernessLocalMapFallback.TryResolve(world, hex, out var mapId) ||
                string.IsNullOrEmpty(mapId))
                return Result.Failure(ErrorCode.InvalidOperation, "No wilderness fallback LocalMap for hex.");

            PlayerPartyWorldLocationDebug.LogTransition(
                world, party, "EnterLocalView.PreserveAutoTravel");
            return WorldTravelService.EnterWildernessLocalMap(world, hex, mapId);
        }

        public static Result EnterWorldSiteAsParty(
            SimulationWorld world,
            PlayerPartyRuntime party,
            WorldSite site,
            HexCoord? ingressFootprintHex = null)
        {
            if (world == null || party == null || site == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid site enter args.");
            var admission = StrategicWorldSiteAccessService.CanTransitionPlayerPartyIntoWorldSite(
                world, site.SiteId);
            if (admission.IsFailure)
                return admission;
            var preparedLocalMapId = WorldTravelService.ResolveWorldSiteLocalMapId(site);

            // Phase 5R-B3B：ingressHex 仅保留 routing/topology/debug 职责（确认从 footprint
            // 哪一侧/哪一格进入）；<b>不再决定 Physical Position</b>（B3B invariant：
            // Context change 不 snap Physical Position）。
            var ingressHex =
                ingressFootprintHex.HasValue && site.OccupiesHex(ingressFootprintHex.Value)
                    ? ingressFootprintHex.Value
                    : site.PresenceHex;
            _ = ingressHex; // routing/debug 用途（不参与物理位置）

            var motion = world.PlayerPartyTravel;
            if (motion == null)
                return Result.Failure(ErrorCode.InvalidOperation, "No party travel state for site enter.");
            if (!motion.HasPosition)
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "EnterWorldSiteAsParty: no canonical physical position for context-preserving ingress (5R-B3B gap).");

            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);

            // Phase 5R-B3B.1 Ingress Physical Continuity：
            // Physical Position 来自调用方跨边前已显式设置的 <b>Canonical WorldPosition</b>（正式
            // SurfaceExitConnection.BoundaryContactWorld 或连续位置：TryCrossWildernessEdge /
            // LocalVisibleAutoTravel 均已先 SetWorldPositionInternal/SetAtWorldPosition(boundary)）。
            // 不再用 ResolveTravelPresentationWorld（presentation 是 travel 中 transient，B3B.1 后
            // 不参与 final context-preserving commit）。禁止 fallback PresenceHex / AnchorHex center；
            // 无位置时明确失败并报告链路缺口（不用 magic offset 修）。
            // Phase 5R-B3B.4：统一 ingress trace id —— [1 IngressBoundary]（含 boundary /
            // fromHex=进入前 CurrentHex / ingressFootprintHex），后续节点（AtSiteCommit /
            // MaterializeDecision / WorldToLocal / IngressAborted）均带同一 id（ActiveIngressId），
            // Materialize 完成后 EndIngress 清空。不改变行为。
            PlayerPartySiteIngressTrace.BeginIngress(
                site.SiteId,
                motion.WorldPosition,
                motion.CurrentHex,
                ingressHex);
            if (!motion.TrySetAtWorldSitePreservingWorldPosition(
                    site.SiteId,
                    motion.WorldPosition))
            {
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "EnterWorldSiteAsParty: context-preserving AtSite commit failed.");
            }

            PlayerPartySiteIngressTrace.Log(
                "AtSiteCommit",
                "kind=" + motion.LocationKind + " site=" + motion.SiteId +
                " world=" + motion.WorldPosition + " hasPos=" + motion.HasPosition);

            ApplyTravelingMembersAtSite(world, site.SiteId);
            PlayerPartyTransitionMembership.ReconcilePlayerPartyMemberWorldPresenceFromMotion(
                world, party, "EnterWorldSiteAsParty");
            PlayerPartyWorldLocationDebug.LogTransition(world, party, "EnterWorldSiteAsParty");
            return WorldTravelService.ActivatePreparedWorldSiteScene(
                world, site, preparedLocalMapId);
        }

        /// <summary>
        /// Close WorldMap → local view takeover.
        /// Idle: same as Phase 2C (Enter Local).
        /// AutoTravel (Phase 5B): Preserve Travel + ExecutionMode=LocalVisible; do not Cancel.
        /// Phase 5C-W2: 接管前先把派生 CurrentHex canonicalize 到 TravelPlan 当前段起点，避免
        /// segment boundary 附近 WorldToHex 提前切 NextHex 导致 LocalMap / presence / Exit 解析分叉。
        /// </summary>
        public static Result CloseWorldMapTakeover(
            SimulationWorld world,
            PlayerPartyRuntime party)
        {
            if (world == null || party == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid takeover args.");

            if (world.PlayerPartyTravel != null && world.PlayerPartyTravel.IsMoving)
            {
                CanonicalizeTakeoverHexToActiveSegment(world);
                // 先完成 LocalMap 进入准备；失败时保持 World executor，绝不遗留 LocalVisible 半状态。
                var enter = EnterLocalViewAtCurrentHex(world, party, allowWhileTraveling: true);
                if (enter.IsFailure)
                    return enter;
                world.PlayerPartyTravel.SetExecutionMode(PlayerPartyTravelExecutionMode.LocalVisible);
                PlayerPartyWorldLocationDebug.LogTransition(
                    world, party, "CloseWorldMapTakeover.PreserveAutoTravel");
                return Result.Success();
            }

            PlayerPartyWorldLocationDebug.LogTransition(world, party, "CloseWorldMapTakeover");
            return EnterLocalViewAtCurrentHex(world, party);
        }

        /// <summary>
        /// Phase 5C-W2 Takeover Canonical State：World → LocalVisible 前，把派生 CurrentHex 对齐到
        /// 正式 TravelPlan 当前段起点（path[SegmentIndex]）。World 推进时 CommitCanonicalWorldPosition
        /// 用 WorldToHex(实时位置) 派生 CurrentHex，在段尾（progress &lt; 1）可能提前切到 nextHex；
        /// 段未完成时权威所在 Hex 必须是 leg 起点，否则 LocalMap 加载 / presence / Exit 解析三者各认
        /// 各的 Hex，LocalVisible 会 NoExit 卡住。
        /// 不动 WorldPosition / Path / Segment；不 Snap、不 Teleport、不重置路线。
        /// </summary>
        static void CanonicalizeTakeoverHexToActiveSegment(SimulationWorld world)
        {
            var motion = world?.PlayerPartyTravel;
            if (motion == null || !motion.IsMoving)
                return;
            if (motion.SegmentProgress >= 1f)
                return; // 段已正式完成：World 推进已 Commit + IncrementPathIndex，状态一致。
            if (!motion.TryGetActiveStepHexes(out var fromHex, out _))
                return;
            if (motion.CurrentHex.Equals(fromHex))
                return; // 已一致。

            motion.AlignCurrentHex(fromHex);
            ApplyTravelingMembersPresence(world);
        }

        /// <summary>
        /// Re-open WorldMap: LocalVisible → World; Path / Progress / WorldPosition unchanged.
        /// </summary>
        public static void ResumeWorldTravelExecutionIfNeeded(SimulationWorld world)
        {
            var motion = world?.PlayerPartyTravel;
            if (motion == null)
                return;
            if (motion.ExecutionMode != PlayerPartyTravelExecutionMode.LocalVisible)
                return;

            // Phase 5R-B6.5-B：不再保留 departure 特例 —— WorldMap open（World executor）时
            // AtWorldSite departure 由 AdvanceWorldSiteDepartureCanonical 推进（WorldMap open 强制
            // ManualPaused；用户 Space Resume 后下一 tick 即推进 Canonical 朝正式 BoundaryContact）。
            // close 后 LocalVisible 接管继续同一 departure。
            motion.SetExecutionMode(
                motion.IsMoving
                    ? PlayerPartyTravelExecutionMode.World
                    : PlayerPartyTravelExecutionMode.None);
        }

        /// <summary>
        /// 若权威位置与当前 PartyWorld LocalMap 已一致，关闭地图无需 Expand／Materialize。
        /// </summary>
        public static bool PartyLocalMapMatchesAuthoritativeLocation(
            SimulationWorld world,
            PlayerPartyRuntime party)
        {
            if (!PlayerPartyWorldLocationQuery.TryResolve(world, party, out var resolved))
                return false;
            var focusMap = world.PartyWorld?.LocalMapId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(focusMap) || string.IsNullOrEmpty(resolved.ResolvedLocalMapId))
                return false;
            if (!string.Equals(focusMap, resolved.ResolvedLocalMapId.Trim(), System.StringComparison.Ordinal))
                return false;

            if (resolved.LocationKind == PlayerPartyLocationKind.AtWorldSite)
            {
                return string.Equals(
                    world.PartyWorld?.SiteId,
                    resolved.SiteId,
                    System.StringComparison.Ordinal);
            }

            return string.IsNullOrEmpty(world.PartyWorld?.SiteId);
        }

        static void EnsureMotionHasContinuousStart(SimulationWorld world, HexCoord startHex)
        {
            var motion = world.PlayerPartyTravel;
            if (motion == null)
                return;
            // Phase 5B: Mid-Segment / in-flight AutoTravel must not Snap or SetAtWorldSite.
            if (motion.IsMoving)
                return;

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite)
            {
                // BeginAutoTravel 将把 Site 转为 Presence 中心上的 AtWorldPosition。
                if (world.Strategic.Sites.TryResolveSitePresenceHex(motion.SiteId, out var presence))
                    motion.SetAtWorldSite(motion.SiteId, presence, hexSize);
                else
                    motion.SetAtWorldSite(motion.SiteId, startHex, hexSize);
                return;
            }

            if (!motion.HasPosition)
                motion.SnapToHexCenter(startHex, hexSize);
        }

        static HexCoord ResolveDeterministicSiteApproachHex(
            SimulationWorld world,
            HexCoord from,
            WorldSite site,
            IReadOnlyCollection<HexCoord> blocked)
        {
            // Phase 5R-B6.4：目标 Site goal = footprint 中「A* 实际路径代价」最低的 walkable 格
            // （§四 总代价最低合法 ingress）。不再用 hex 直线距离 —— 真实地形/阻塞下直线最近格
            // 可能实际绕路（ch01 实测 site_daoguan/site_b 各 2 次次优）。Anchor/Presence 不参与。
            HexCoord best = site.PresenceHex;
            var bestDist = int.MaxValue;
            var bestCost = int.MaxValue;
            var pathScratch = new List<HexCoord>(64);
            foreach (var hex in site.EnumerateFootprintHexes())
            {
                if (!world.HexWorld.TryGetTile(hex, out var tile) || tile == null || !tile.IsPassable)
                    continue;
                if (!HexPathfinder.TryFindPath(
                        world.HexWorld, from, hex, pathScratch, HexTravelMode.Ground, blocked) ||
                    pathScratch.Count < 1)
                    continue;
                var cost = pathScratch.Count;
                var dist = HexMath.Distance(from, hex);
                if (cost < bestCost ||
                    (cost == bestCost && dist < bestDist))
                {
                    bestCost = cost;
                    bestDist = dist;
                    best = hex;
                }
            }

            // 全部不可达（极端）：退回 hex 距离最近 walkable 格（保底；正常数据不会走到）。
            if (bestCost == int.MaxValue)
            {
                best = site.PresenceHex;
                bestDist = int.MaxValue;
                foreach (var hex in site.EnumerateFootprintHexes())
                {
                    if (!world.HexWorld.TryGetTile(hex, out var tile) || tile == null || !tile.IsPassable)
                        continue;
                    var d = HexMath.Distance(from, hex);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = hex;
                    }
                }
            }

            return best;
        }

        static void ApplyTravelingMembersPresence(SimulationWorld world)
        {
            var motion = world.PlayerPartyTravel;
            if (motion == null)
                return;
            if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(motion.SiteId))
            {
                ApplyTravelingMembersAtSite(world, motion.SiteId);
                return;
            }

            ApplyTravelingMembersAtHex(world, motion.CurrentHex);
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
            if (world?.WorldPresence == null || world.PlayerPartyTravel == null || string.IsNullOrEmpty(siteId))
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

        public static void ApplyMembersAtHex(
            SimulationWorld world,
            PlayerPartyRuntime party,
            HexCoord hex)
        {
            if (world?.WorldPresence == null || party == null)
                return;
            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);
            var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : 1f;
            world.PlayerPartyTravel?.SnapToHexCenter(hex, hexSize);
            ApplyTravelingMembersAtHex(world, hex);
        }

        public static void ApplyMembersAtSite(
            SimulationWorld world,
            PlayerPartyRuntime party,
            string siteId)
        {
            if (world?.WorldPresence == null || party == null || string.IsNullOrEmpty(siteId))
                return;
            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);
            if (world.Strategic.Sites.TryResolveSitePresenceHex(siteId, out var presence))
            {
                var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                    ? world.HexWorld.HexSize
                    : 1f;
                world.PlayerPartyTravel?.SetAtWorldSite(siteId, presence, hexSize);
            }

            ApplyTravelingMembersAtSite(world, siteId);
        }
    }
}
