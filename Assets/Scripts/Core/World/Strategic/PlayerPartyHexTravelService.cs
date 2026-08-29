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
        static readonly List<HexCoord> GatewayPathScratch = new List<HexCoord>(64);

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

            // TargetHex V1：目的地语义为该格 canonical center（不存点击像素）。
            var goalHex = destination;
            if (!string.IsNullOrEmpty(destinationSiteId) &&
                world.Strategic.Sites.TryGet(destinationSiteId, out var targetSite) &&
                targetSite != null)
            {
                // Site 目标：路径落到 footprint 上最近可达格；进入后再聚合 PresenceHex。
                goalHex = ResolveDeterministicSiteApproachHex(world, startHex, targetSite);
            }

            if (startHex == goalHex &&
                !world.PlayerPartyTravel.IsMoving &&
                string.IsNullOrEmpty(destinationSiteId))
                return Result.Failure(ErrorCode.InvalidArgument, "Already at destination hex.");

            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            var motion = world.PlayerPartyTravel;
            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;

            // 非目标 WorldSite 的全部 footprint 格不可作为中转：路线必须绕开（保留目标 Site）。
            var blockedSiteHexes = BuildNonDestinationSiteBlockedHexes(world, destinationSiteId);

            if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(motion.SiteId) &&
                world.Strategic.Sites.TryGet(motion.SiteId, out var fromSite) &&
                fromSite != null)
            {
                // 出发 Site 自身的 footprint 是 departure 段合法路径（site 内部 → 边界），不阻塞。
                foreach (var hex in fromSite.EnumerateFootprintHexes())
                    blockedSiteHexes.Remove(hex);

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
                        "[GatewayB1Trace] 8a SiteLeaveNormalRouteSuccess=false");
                    // Phase 5D-B2: Site 出发普通 Route 失败 → Dynamic MandatoryTransit fallback
                    // （fromSite footprint 已在 blockedSiteHexes 移除；resolver 同样对其 exempt）。
                    if (WorldSiteTransitPolicy.TryResolveMandatoryTransitSite(
                            world, startHex, goalHex, destinationSiteId, mode,
                            motion.SiteId, GatewayPathScratch, out var gSite, out var gApproach))
                        return StartGatewayLeg(world, party, motion, hexSize, mode,
                            GatewayPathScratch, gSite, gApproach, goalHex, destinationSiteId);
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
                // Phase 5D-B2: 普通 Route 不可达时，动态解析单个 MandatoryTransitSite：
                // 存在某非目标 Site 单独放开 footprint 后 A→B 真正连通 → 当前 Leg 先去该 Site。
                if (WorldSiteTransitPolicy.TryResolveMandatoryTransitSite(
                        world, startHex, goalHex, destinationSiteId, mode,
                        motion.SiteId, GatewayPathScratch, out var gSite, out var gApproach))
                    return StartGatewayLeg(world, party, motion, hexSize, mode,
                        GatewayPathScratch, gSite, gApproach, goalHex, destinationSiteId);
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

        /// <summary>
        /// 委托共享 <see cref="WorldSiteTransitPolicy"/>：所有非目标 WorldSite footprint
        /// 不可作为普通中转 Hex；仅目标 Site 保留（允许到 ingress）。
        /// </summary>
        static HashSet<HexCoord> BuildNonDestinationSiteBlockedHexes(
            SimulationWorld world,
            string destinationSiteId) =>
            WorldSiteTransitPolicy.BuildBlockedFootprintHexes(world, destinationSiteId);

        /// <summary>
        /// PlayerParty 降级 UX 入口：普通 Route 失败时查询是否存在合法单 MandatoryTransitSite，
        /// 复用 <see cref="WorldSiteTransitPolicy.TryResolveMandatoryTransitSite"/> 同一 resolver
        /// （战略层公共，非 UI 专属）。不启动任何 Travel；由调用方决定自动 StartGatewayLeg
        /// （已存在）或弹确认提示后下达普通 PlayerParty → Transit Site 旅行。
        /// </summary>
        public static bool TryResolveGatewayTravelCandidate(
            SimulationWorld world,
            PlayerPartyRuntime party,
            HexCoord destinationHex,
            string destinationSiteId,
            out string gatewaySiteId,
            out string gatewayDisplayName,
            out HexCoord gatewayApproachHex)
        {
            gatewaySiteId = string.Empty;
            gatewayDisplayName = string.Empty;
            gatewayApproachHex = default;
            if (world == null || party == null || !party.HasActive)
                return false;
            if (!TryResolvePartyWorldHex(world, party, out var startHex))
                return false;

            var goalHex = destinationHex;
            if (!string.IsNullOrEmpty(destinationSiteId) &&
                world.Strategic?.Sites != null &&
                world.Strategic.Sites.TryGet(destinationSiteId, out var targetSite) &&
                targetSite != null)
                goalHex = ResolveDeterministicSiteApproachHex(world, startHex, targetSite);

            var fromSiteId = world.PlayerPartyTravel?.SiteId ?? string.Empty;
            var scratch = new List<HexCoord>(64);
            if (!WorldSiteTransitPolicy.TryResolveMandatoryTransitSite(
                    world, startHex, goalHex, destinationSiteId, HexTravelMode.Ground,
                    fromSiteId, scratch, out gatewaySiteId, out gatewayApproachHex))
            {
                gatewaySiteId = string.Empty;
                gatewayApproachHex = default;
                return false;
            }

            if (world.Strategic?.Sites != null &&
                world.Strategic.Sites.TryGet(gatewaySiteId, out var g) &&
                g != null)
                gatewayDisplayName = string.IsNullOrEmpty(g.DisplayName) ? g.SiteId : g.DisplayName;
            return true;
        }

        /// <summary>
        /// Phase 5D-B1: 用 Gateway Leg 路径开始 AutoTravel：Destination = Gateway（CurrentLeg），
        /// 玩家原始点击目标经 SetFinalDestination 保留；MandatoryWaypointSiteId = Gateway。
        /// </summary>
        static Result StartGatewayLeg(
            SimulationWorld world,
            PlayerPartyRuntime party,
            PlayerPartyWorldMotion motion,
            float hexSize,
            HexTravelMode mode,
            List<HexCoord> gatewayPath,
            string gatewaySiteId,
            HexCoord gatewayApproachHex,
            HexCoord finalGoalHex,
            string finalDestinationSiteId)
        {
            if (gatewayPath == null || gatewayPath.Count < 1)
                return Result.Failure(ErrorCode.InvalidOperation, "No gateway leg path.");

            PlayerPartyWorldLocationDebug.Sink?.Invoke(
                "[GatewayB1Trace] 10 FinalResult=StartedGatewayLeg gateway=" + gatewaySiteId +
                " legPathLen=" + gatewayPath.Count + " finalGoalHex=" + finalGoalHex);
            EnsureMotionHasContinuousStart(world, gatewayPath[0]);
            ApplyTravelingMembersPresence(world);
            ClearPartyWorldPresentationCacheForOpenWorld(world);
            PlayerPartyWorldLocationDebug.LogSnapshot(world, party, "BeginTravel.GatewayLeg");
            motion.BeginAutoTravel(gatewayPath, gatewayApproachHex, gatewaySiteId, mode, hexSize);
            motion.SetMandatoryWaypoint(gatewaySiteId);
            motion.SetFinalDestination(finalGoalHex, finalDestinationSiteId);
            ApplyTravelingMembersPresence(world);
            PlayerPartyWorldLocationDebug.LogSnapshot(world, party, "BeginTravel.AfterGatewayLeg");
            return Result.Success();
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

                var midDerived = HexMath.WorldToHex(pos.X, pos.Y, hexSize);
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

        static void CommitSiteDepartureBoundaryCrossing(
            SimulationWorld world,
            PlayerPartyWorldMotion motion,
            float hexSize)
        {
            if (!motion.IsSiteDeparturePending)
                return;

            var boundaryEntry = motion.SiteDepartureBoundaryEntry;
            var exitHex = motion.SiteDepartureExitHex;
            motion.SetWorldPositionInternal(boundaryEntry, exitHex);
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
                var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
                motion.CommitSiteArrivalAuthority(site.SiteId, pos, derived);
                return;
            }

            if (motion.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                motion.UsesTravelPresentation)
            {
                motion.SetTravelPresentation(pos, derived);
                return;
            }

            motion.SetWorldPositionInternal(pos, derived);
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

            // 普通 TargetHex：停在目标 canonical center，保持 AtWorldPosition。
            motion.SnapToHexCenter(destHex, hexSize);

            // 仅当旅行目标明确是 WorldSite 时才聚合；禁止用出发 Site / PartyWorld 复活。
            if (!string.IsNullOrEmpty(destSiteId) &&
                world.Strategic.Sites.TryGet(destSiteId, out var site) &&
                site != null)
            {
                motion.SetAtWorldSite(site.SiteId, site.PresenceHex, hexSize);
                ApplyTravelingMembersAtSite(world, site.SiteId);
                // Site 到达：Domain 已是 AtWorldSite；PartyWorld 仍等玩家关地图再 Expand。
                ClearPartyWorldPresentationCacheForOpenWorld(world);
            }
            else
            {
                // 普通 Hex：确保 LocationKind 仍为 AtWorldPosition，且无 SiteId。
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
                    world.PlayerPartyTravel.SetAtWorldSite(
                        focusSite.SiteId,
                        focusSite.PresenceHex,
                        world.HexWorld != null && world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f);
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

            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
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

            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            // Align Presence to derived hex only; never Snap / EnsureMotionHasContinuousStart.
            ApplyTravelingMembersPresence(world);

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

            // Phase 5D-B2a: 正式 Site Ingress 的权威位置。
            // 默认 site.PresenceHex（兼容旧调用）；多 Hex footprint 到来向对应格时，
            // 调用方传入 ingressFootprintHex（approach 已按距 start 最近方向选取），
            // 不再永远进 Anchor。仅接受 footprint 内的格，否则回退 PresenceHex。
            var ingressHex =
                ingressFootprintHex.HasValue && site.OccupiesHex(ingressFootprintHex.Value)
                    ? ingressFootprintHex.Value
                    : site.PresenceHex;

            world.PlayerPartyTravel.CaptureTravelingMembers(party.Members);
            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            world.PlayerPartyTravel.SetAtWorldSite(site.SiteId, ingressHex, hexSize);
            ApplyTravelingMembersAtSite(world, site.SiteId);
            PlayerPartyWorldLocationDebug.LogTransition(world, party, "EnterWorldSiteAsParty");
            return WorldTravelService.EnterWorldSiteScene(world, site.SiteId, string.Empty);
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
                world.PlayerPartyTravel.SetExecutionMode(PlayerPartyTravelExecutionMode.LocalVisible);
                PlayerPartyWorldLocationDebug.LogTransition(
                    world, party, "CloseWorldMapTakeover.PreserveAutoTravel");
                return EnterLocalViewAtCurrentHex(world, party, allowWhileTraveling: true);
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
            WorldSite site)
        {
            HexCoord best = site.PresenceHex;
            var bestDist = int.MaxValue;
            foreach (var hex in site.EnumerateFootprintHexes())
            {
                if (!world.HexWorld.TryGetTile(hex, out var tile) || tile == null || !tile.IsPassable)
                    continue;
                var d = HexMath.Distance(from, hex);
                if (d < bestDist ||
                    (d == bestDist && (hex.Q < best.Q || (hex.Q == best.Q && hex.R < best.R))))
                {
                    bestDist = d;
                    best = hex;
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
            world.PlayerPartyTravel?.CaptureTravelingMembers(party.Members);
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
            world.PlayerPartyTravel?.CaptureTravelingMembers(party.Members);
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
