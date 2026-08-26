using System;
using System.Collections.Generic;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase 2D：Background Character 纯数据层 HexWorld 旅行（非 Party / 非 FormalArmy）。
    /// </summary>
    public static class BackgroundCharacterTravelService
    {
        static readonly List<HexCoord> PathScratch = new List<HexCoord>(64);
        static readonly List<HexCoord> FullPathScratch = new List<HexCoord>(64);

        public static Result BeginTravelToHex(
            SimulationWorld world,
            EntityId characterId,
            HexCoord destination,
            PlayerPartyRuntime party = null,
            bool debugOverrideLocalOccupant = false) =>
            BeginTravel(world, characterId, destination, string.Empty, party, debugOverrideLocalOccupant);

        public static Result BeginTravelToWorldSite(
            SimulationWorld world,
            EntityId characterId,
            string siteId,
            PlayerPartyRuntime party = null,
            bool debugOverrideLocalOccupant = false)
        {
            if (world == null || characterId.IsNone || string.IsNullOrEmpty(siteId))
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid background site travel.");
            if (!world.Strategic.Sites.TryGet(siteId, out _))
                return Result.Failure(ErrorCode.NotFound, "Site not found.", siteId);
            // Destination 真源是 WorldSiteId；路径入口由 Footprint Boundary 解析，不用 Anchor/Presence 作唯一入口。
            return BeginTravel(world, characterId, default, siteId, party, debugOverrideLocalOccupant);
        }

        public static Result BeginTravel(
            SimulationWorld world,
            EntityId characterId,
            HexCoord destinationHex,
            string destinationSiteId,
            PlayerPartyRuntime party,
            bool debugOverrideLocalOccupant)
        {
            if (world?.BackgroundCharacterTravel == null)
                return Result.Failure(ErrorCode.InvalidOperation, "Background travel board missing.");
            if (!world.HexWorld.HasGrid)
                return Result.Failure(ErrorCode.InvalidOperation, "Hex grid not loaded.");

            var canStart = debugOverrideLocalOccupant
                ? CharacterWorldMovementAuthorityQuery.CanStartBackgroundTravelDebug(
                    world, characterId, party, out var authErr)
                : CharacterWorldMovementAuthorityQuery.CanStartBackgroundTravel(
                    world, characterId, party, out authErr);
            if (!canStart)
                return Result.Failure(ErrorCode.InvalidOperation, authErr ?? "Cannot start background travel.");

            if (!TryResolveCharacterWorldLocation(world, characterId, out var startKind, out var startSiteId, out var startPos, out var startHex))
                return Result.Failure(ErrorCode.InvalidOperation, "Character has no world location.");

            var requestedHex = destinationHex;
            destinationSiteId = TryCanonicalizeFootprintHexDestination(
                world, destinationHex, destinationSiteId, out var canonicalizedFromFootprint);

            var goalHex = destinationHex;
            if (!string.IsNullOrEmpty(destinationSiteId) &&
                world.Strategic.Sites.TryGet(destinationSiteId, out var targetSite) &&
                targetSite != null)
                goalHex = ResolveDeterministicSiteApproachHex(world, startHex, targetSite);

            if (!world.HexWorld.TryGetTile(goalHex, out var destTile) ||
                destTile == null ||
                !destTile.IsPassable)
                return Result.Failure(ErrorCode.InvalidArgument, "Destination hex is not passable.");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var traceId = BackgroundBgTravelFullTrace.BeginTrace();
            var sourceLocation = startKind == BackgroundCharacterLocationKind.AtWorldSite && !string.IsNullOrEmpty(startSiteId)
                ? "AtWorldSite(" + startSiteId + ")"
                : "AtWorldPosition(" + startHex + ")";
            var destinationKind = string.IsNullOrEmpty(destinationSiteId)
                ? "WildernessHex"
                : "WorldSite(" + destinationSiteId + ")";
            if (canonicalizedFromFootprint)
                destinationKind += " RequestedHex=" + requestedHex + "→ResolvedWorldSite";
            BackgroundBgTravelFullTrace.LogIntent(
                characterId,
                sourceLocation,
                goalHex,
                destinationKind);
#endif

            if (startKind == BackgroundCharacterLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(startSiteId) &&
                world.Strategic.Sites.TryGet(startSiteId, out var fromSite) &&
                fromSite != null)
            {
                if (!TryBuildPathLeavingSite(world, fromSite, goalHex, FullPathScratch, out var exitHex, out var departureFootprintHex))
                    return Result.Failure(ErrorCode.InvalidOperation, "No path leaving WorldSite.");

                var hexSizeForDeparture = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
                if (!BackgroundCharacterSiteDepartureResolver.TryResolveDepartureBoundaryEntryWorldPosition(
                        departureFootprintHex,
                        exitHex,
                        hexSizeForDeparture,
                        out var boundaryEntryPos))
                {
                    boundaryEntryPos = HexCenter(exitHex, hexSizeForDeparture);
                }

                if (debugOverrideLocalOccupant && world.LocalMap != null)
                    world.LocalMap.RemoveOccupant(characterId);

                var motion = world.BackgroundCharacterTravel.GetOrCreate(characterId);
                var footprintCenter = HexCenter(departureFootprintHex, hexSizeForDeparture);
                motion.BeginSiteDepartureTravel(
                    FullPathScratch,
                    goalHex,
                    destinationSiteId,
                    departureFootprintHex,
                    exitHex,
                    footprintCenter,
                    boundaryEntryPos,
                    HexTravelMode.Ground);
                motion.LastProcessedWorldTick = world.Tick.Value;
                var isTravelingAfterBegin = motion.IsMoving;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var derivedBeforeCommit = fromSite.PresenceHex;
                var previousLocationDesc = "AtWorldSite(" + startSiteId + ")";
                var segmentStart = FullPathScratch.Count >= 1
                    ? FullPathScratch[0].ToString()
                    : "None";
                var segmentEnd = FullPathScratch.Count >= 1
                    ? FullPathScratch[FullPathScratch.Count - 1].ToString()
                    : "None";
                var departureDistance = WorldVec2.Distance(footprintCenter, boundaryEntryPos);
                var segmentDistance = WorldVec2.Distance(boundaryEntryPos, HexCenter(exitHex, hexSizeForDeparture));
                BackgroundBgTravelFullTrace.LogLocationCommit(
                    previousLocationDesc,
                    "AtWorldSite(" + startSiteId + ") [SiteDeparturePending]",
                    derivedBeforeCommit,
                    derivedBeforeCommit,
                    "BeginTravel.SiteDeparturePending");
                BackgroundBgTravelFullTrace.LogRoute(
                    BackgroundSiteDepartureTravelTrace.FormatRoute(FullPathScratch),
                    FullPathScratch.Count,
                    exitHex,
                    departureFootprintHex,
                    motion.HexPathCount >= 2 ? motion.HexPathCount - 1 : 0,
                    segmentStart,
                    segmentEnd,
                    isTravelingAfterBegin);
                BackgroundBgTravelFullTrace.Log(
                    "SiteDeparture",
                    "FootprintCenter=" + BackgroundSiteDepartureTravelTrace.FormatWorldVec(footprintCenter) +
                    " BoundaryEntry=" + BackgroundSiteDepartureTravelTrace.FormatWorldVec(boundaryEntryPos) +
                    " ExitCenter=" + BackgroundSiteDepartureTravelTrace.FormatWorldVec(HexCenter(exitHex, hexSizeForDeparture)) +
                    " DepartureDistance=" + departureDistance.ToString("0.###") +
                    " BoundaryToExitCenterDistance=" + segmentDistance.ToString("0.###"));
                BackgroundBgTravelFullTrace.LogTravelComplete(
                    !isTravelingAfterBegin,
                    "BeginTravel.AfterSiteDepartureBegin");
                BackgroundSiteDepartureTravelTrace.Log(new BackgroundSiteDepartureTravelTrace.Snapshot(
                    characterId,
                    previousLocationDesc,
                    goalHex,
                    exitHex,
                    departureFootprintHex,
                    BackgroundCharacterSiteDepartureResolver.ResolveDirectionBetween(departureFootprintHex, exitHex),
                    BackgroundSiteDepartureTravelTrace.FormatRoute(FullPathScratch),
                    motion.HexPathCount >= 2 ? motion.HexPathCount - 1 : 0,
                    BackgroundSiteDepartureTravelTrace.FormatWorldVec(footprintCenter),
                    BackgroundSiteDepartureTravelTrace.FormatWorldVec(boundaryEntryPos),
                    worldLocationCommitted: false,
                    enteredHexRaised: false,
                    travelCompleteRaised: !isTravelingAfterBegin,
                    materializeRequested: false,
                    isTravelingAfterBegin: isTravelingAfterBegin));
#endif

                return Result.Success();
            }

            if (!HexPathfinder.TryFindPath(world.HexWorld, startHex, goalHex, FullPathScratch) ||
                FullPathScratch.Count < 1)
                return Result.Failure(ErrorCode.InvalidOperation, "No hex path to destination.");

            if (debugOverrideLocalOccupant && world.LocalMap != null)
                world.LocalMap.RemoveOccupant(characterId);

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var derivedBeforeCommitWild = startHex;
            var previousLocationDescWild = startKind == BackgroundCharacterLocationKind.AtWorldSite && !string.IsNullOrEmpty(startSiteId)
                ? "AtWorldSite(" + startSiteId + ")"
                : "AtWorldPosition(" + startHex + ")";
            var derivedWild = HexMath.WorldToHex(startPos.X, startPos.Y, hexSize);
            world.WorldPresence.SetAtWorldPosition(characterId, startPos, derivedWild);

            var motionWild = world.BackgroundCharacterTravel.GetOrCreate(characterId);
            motionWild.BeginTravel(FullPathScratch, goalHex, destinationSiteId, HexTravelMode.Ground);
            motionWild.LastProcessedWorldTick = world.Tick.Value;
            var isTravelingAfterBeginWild = motionWild.IsMoving;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var segmentStartWild = FullPathScratch.Count >= 1
                ? FullPathScratch[0].ToString()
                : "None";
            var segmentEndWild = FullPathScratch.Count >= 1
                ? FullPathScratch[FullPathScratch.Count - 1].ToString()
                : "None";
            BackgroundBgTravelFullTrace.LogLocationCommit(
                previousLocationDescWild,
                "AtWorldPosition(" + derivedWild + ")",
                derivedBeforeCommitWild,
                derivedWild,
                "BeginTravel.SetAtWorldPosition");
            BackgroundBgTravelFullTrace.LogRoute(
                BackgroundSiteDepartureTravelTrace.FormatRoute(FullPathScratch),
                FullPathScratch.Count,
                startHex,
                startHex,
                motionWild.HexPathCount >= 2 ? motionWild.HexPathCount - 1 : 0,
                segmentStartWild,
                segmentEndWild,
                isTravelingAfterBeginWild);
            BackgroundBgTravelFullTrace.LogTravelComplete(
                !isTravelingAfterBeginWild,
                "BeginTravel.AfterMotionBegin");
#endif

            return Result.Success();
        }

        static void CommitSiteDepartureBoundaryCrossing(
            SimulationWorld world,
            EntityId characterId,
            BackgroundCharacterTravelMotion motion,
            PlayerPartyRuntime party)
        {
            if (!motion.IsSiteDeparturePending)
                return;

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var boundaryEntry = motion.SiteDepartureBoundaryEntry;
            var enteredHex = motion.SiteDepartureExitHex;
            var ingressFromHex = motion.SiteDepartureFootprintHex;
            var derived = HexMath.WorldToHex(boundaryEntry.X, boundaryEntry.Y, hexSize);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (BackgroundBgTravelFullTrace.ActiveTraceId > 0)
            {
                BackgroundBgTravelFullTrace.LogLocationCommit(
                    "AtWorldSite [SiteDeparturePending]",
                    "AtWorldPosition(" + derived + ")",
                    ingressFromHex,
                    enteredHex,
                    "CommitSiteDepartureBoundaryCrossing.SetAtWorldPosition");
            }
#endif

            world.WorldPresence.SetAtWorldPosition(characterId, boundaryEntry, derived);
            motion.ClearSiteDeparturePending();

            BackgroundCharacterWildernessLocalMapMaterialization.NotifyEnteredWorldHex(
                world,
                characterId,
                enteredHex,
                ingressFromHex,
                party);
        }

        static bool ShouldCommitSiteDepartureBoundaryCrossing(
            BackgroundCharacterTravelMotion motion,
            WorldVec2 previousPos,
            WorldVec2 newPos,
            float hexSize)
        {
            if (!motion.IsSiteDeparturePending)
                return false;

            var size = hexSize > 0f ? hexSize : 1f;
            HexMath.ToWorldPosition(motion.SiteDepartureFootprintHex, size, out var fx, out var fy);
            var footprintCenter = new WorldVec2(fx, fy);
            var boundary = motion.SiteDepartureBoundaryEntry;
            var dPrev = WorldVec2.Distance(footprintCenter, previousPos);
            var dNew = WorldVec2.Distance(footprintCenter, newPos);
            var dBoundary = WorldVec2.Distance(footprintCenter, boundary);
            return dPrev + 0.0001f < dBoundary && dNew + 0.0001f >= dBoundary;
        }

        static bool TryResolveSiteDepartureTravelPosition(
            SimulationWorld world,
            EntityId characterId,
            BackgroundCharacterTravelMotion motion,
            out WorldVec2 pos,
            out HexCoord previousDerived,
            out bool isSiteDepartureVirtual)
        {
            pos = default;
            previousDerived = default;
            isSiteDepartureVirtual = false;
            if (!world.WorldPresence.TryGet(characterId, out var presence) || presence == null)
                return false;

            if (motion.IsSiteDeparturePending &&
                presence.Mode == PartyWorldPresenceMode.AtSite)
            {
                pos = motion.SiteDepartureVirtualPosition;
                if (world.Strategic.Sites.TryResolveSitePresenceHex(presence.SiteId, out var siteHex))
                    previousDerived = siteHex;
                isSiteDepartureVirtual = true;
                return true;
            }

            if (!presence.HasContinuousWorldPosition)
                return false;

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            pos = presence.ContinuousWorldPosition;
            previousDerived = HexMath.WorldToHex(pos.X, pos.Y, hexSize);
            return true;
        }

        public static Result CancelTravel(SimulationWorld world, EntityId characterId)
        {
            if (world?.BackgroundCharacterTravel == null || characterId.IsNone)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid cancel args.");
            if (!world.BackgroundCharacterTravel.TryGet(characterId, out var motion) || motion == null || !motion.IsMoving)
                return Result.Failure(ErrorCode.InvalidOperation, "Character is not background traveling.");

            motion.CancelTravelPreserveProgress();
            world.BackgroundCharacterTravel.Remove(characterId);
            return Result.Success();
        }

        public static void CancelTravelIfAny(SimulationWorld world, EntityId characterId)
        {
            if (world?.BackgroundCharacterTravel == null || characterId.IsNone)
                return;
            if (!world.BackgroundCharacterTravel.IsTraveling(characterId))
                return;
            world.BackgroundCharacterTravel.GetOrCreate(characterId)?.CancelTravelPreserveProgress();
            world.BackgroundCharacterTravel.Remove(characterId);
        }

        public static void AdvanceAll(SimulationWorld world, int ticks)
        {
            if (ticks < 1)
                return;
            BackgroundSimulationScheduler.AdvanceTravelBatch(world, (ulong)ticks);
        }

        public static void AdvanceDistanceBudget(
            SimulationWorld world,
            EntityId characterId,
            float distanceBudget,
            PlayerPartyRuntime party = null)
        {
            if (world?.BackgroundCharacterTravel == null || characterId.IsNone || distanceBudget <= 0f)
                return;
            if (!world.BackgroundCharacterTravel.TryGet(characterId, out var motion) || motion == null || !motion.IsMoving)
                return;
            if (!TryResolveSiteDepartureTravelPosition(
                    world,
                    characterId,
                    motion,
                    out var pos,
                    out var previousDerived,
                    out var isSiteDepartureVirtual))
                return;

            if (world.Entities.TryGet(characterId, out var entity) &&
                !CombatLifeStateService.CanFight(entity))
            {
                CancelTravelIfAny(world, characterId);
                return;
            }

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var remainingBudget = distanceBudget;
            var guard = 0;
            while (remainingBudget > 0.0001f && motion.IsMoving && guard++ < 64)
            {
                if (!motion.TryGetActiveSegmentWorld(pos, hexSize, out var fromPos, out var toPos))
                {
                    if (isSiteDepartureVirtual)
                    {
                        CommitSiteDepartureBoundaryCrossing(world, characterId, motion, party);
                        if (!world.BackgroundCharacterTravel.IsTraveling(characterId))
                            return;
                        if (!TryResolveSiteDepartureTravelPosition(
                                world,
                                characterId,
                                motion,
                                out pos,
                                out previousDerived,
                                out isSiteDepartureVirtual))
                            return;
                        continue;
                    }

                    FinishArrival(world, characterId, motion, hexSize, party);
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
                            CommitSiteDepartureBoundaryCrossing(world, characterId, motion, party);
                            if (!world.BackgroundCharacterTravel.IsTraveling(characterId))
                                return;
                            if (!TryResolveSiteDepartureTravelPosition(
                                    world,
                                    characterId,
                                    motion,
                                    out pos,
                                    out previousDerived,
                                    out isSiteDepartureVirtual))
                                return;
                            continue;
                        }

                        FinishArrival(world, characterId, motion, hexSize, party);
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
                        motion.SetSiteDepartureVirtualPosition(pos);
                        if (ShouldCommitSiteDepartureBoundaryCrossing(motion, previousPos, pos, hexSize))
                            CommitSiteDepartureBoundaryCrossing(world, characterId, motion, party);
                        if (!world.BackgroundCharacterTravel.IsTraveling(characterId))
                            return;
                        if (!TryResolveSiteDepartureTravelPosition(
                                world,
                                characterId,
                                motion,
                                out pos,
                                out previousDerived,
                                out isSiteDepartureVirtual))
                            return;

                        remainingBudget -= remainingOnSegment;
                        continue;
                    }

                    var derived = HexMath.WorldToHex(pos.X, pos.Y, hexSize);
                    world.WorldPresence.SetAtWorldPosition(characterId, pos, derived);
                    NotifyWildernessHexEnteredIfChanged(
                        world, characterId, derived, previousDerived, party);
                    previousDerived = derived;
                    motion.SetSegment(motion.SegmentIndex, 1f);
                    remainingBudget -= remainingOnSegment;
                    motion.IncrementPathIndex();
                    if (!world.BackgroundCharacterTravel.IsTraveling(characterId))
                        return;
                    if (motion.SegmentIndex >= motion.HexPathCount - 1)
                    {
                        FinishArrival(world, characterId, motion, hexSize, party);
                        return;
                    }

                    continue;
                }

                var dirX = (toPos.X - pos.X) / remainingOnSegment;
                var dirY = (toPos.Y - pos.Y) / remainingOnSegment;
                var previousPosMid = pos;
                pos = new WorldVec2(pos.X + dirX * remainingBudget, pos.Y + dirY * remainingBudget);
                if (isSiteDepartureVirtual)
                {
                    motion.SetSiteDepartureVirtualPosition(pos);
                    if (ShouldCommitSiteDepartureBoundaryCrossing(motion, previousPosMid, pos, hexSize))
                        CommitSiteDepartureBoundaryCrossing(world, characterId, motion, party);
                    if (!world.BackgroundCharacterTravel.IsTraveling(characterId))
                        return;
                    if (TryResolveSiteDepartureTravelPosition(
                            world,
                            characterId,
                            motion,
                            out pos,
                            out previousDerived,
                            out isSiteDepartureVirtual) &&
                        !isSiteDepartureVirtual &&
                        remainingBudget > 0.0001f &&
                        motion.IsMoving)
                    {
                        var used = WorldVec2.Distance(previousPosMid, pos);
                        remainingBudget = Math.Max(0f, remainingBudget - used);
                        continue;
                    }

                    var virtualProgress = 1f - WorldVec2.Distance(pos, toPos) / segmentLen;
                    motion.SetSegment(motion.SegmentIndex, virtualProgress);
                    remainingBudget = 0f;
                    continue;
                }

                var midDerived = HexMath.WorldToHex(pos.X, pos.Y, hexSize);
                world.WorldPresence.SetAtWorldPosition(characterId, pos, midDerived);
                NotifyWildernessHexEnteredIfChanged(
                    world, characterId, midDerived, previousDerived, party);
                if (!world.BackgroundCharacterTravel.IsTraveling(characterId))
                    return;
                previousDerived = midDerived;
                var progress = 1f - WorldVec2.Distance(pos, toPos) / segmentLen;
                motion.SetSegment(motion.SegmentIndex, progress);
                remainingBudget = 0f;
            }
        }

        static void NotifyWildernessHexEnteredIfChanged(
            SimulationWorld world,
            EntityId characterId,
            HexCoord derived,
            HexCoord previousDerived,
            PlayerPartyRuntime party = null)
        {
            if (derived.Equals(previousDerived))
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (BackgroundBgTravelFullTrace.ActiveTraceId > 0)
            {
                BackgroundBgTravelFullTrace.LogLocationCommit(
                    "AtWorldPosition(" + previousDerived + ")",
                    "AtWorldPosition(" + derived + ")",
                    previousDerived,
                    derived,
                    "AdvanceDistanceBudget.SetAtWorldPosition");
            }
#endif

            BackgroundCharacterWildernessLocalMapMaterialization.NotifyEnteredWorldHex(
                world,
                characterId,
                derived,
                previousDerived,
                party);
        }

        public static bool TryDescribeTravel(
            SimulationWorld world,
            EntityId characterId,
            PlayerPartyRuntime party,
            out CharacterWorldMovementAuthority authority,
            out BackgroundCharacterLocationKind locationKind,
            out string siteId,
            out WorldVec2 worldPos,
            out HexCoord derivedHex,
            out BackgroundCharacterTravelMovementKind travelKind,
            out HexCoord destinationHex,
            out string destinationSiteId,
            out int segmentIndex,
            out float segmentProgress)
        {
            authority = CharacterWorldMovementAuthority.None;
            locationKind = BackgroundCharacterLocationKind.Unknown;
            siteId = string.Empty;
            worldPos = default;
            derivedHex = default;
            travelKind = BackgroundCharacterTravelMovementKind.Idle;
            destinationHex = default;
            destinationSiteId = string.Empty;
            segmentIndex = 0;
            segmentProgress = 0f;

            if (world == null || characterId.IsNone)
                return false;

            CharacterWorldMovementAuthorityQuery.TryGetAuthority(world, characterId, party, out authority);
            if (!TryResolveCharacterWorldLocation(world, characterId, out locationKind, out siteId, out worldPos, out derivedHex))
                return false;

            if (world.BackgroundCharacterTravel.TryGet(characterId, out var motion) && motion != null)
            {
                travelKind = motion.MovementKind;
                destinationHex = motion.DestinationHex;
                destinationSiteId = motion.DestinationSiteId ?? string.Empty;
                segmentIndex = motion.SegmentIndex;
                segmentProgress = motion.SegmentProgress;
            }

            return true;
        }

        static void FinishArrival(
            SimulationWorld world,
            EntityId characterId,
            BackgroundCharacterTravelMotion motion,
            float hexSize,
            PlayerPartyRuntime party = null)
        {
            BackgroundTravelArrivalContext.TryFromMotion(world, motion, out var arrivalContext);

            var destHex = motion.DestinationHex;
            var destSiteId = motion.DestinationSiteId ?? string.Empty;
            if (string.IsNullOrEmpty(destSiteId))
                destSiteId = TryCanonicalizeFootprintHexDestination(
                    world, destHex, destSiteId, out _);

            var center = HexCenter(destHex, hexSize);

            if (!string.IsNullOrEmpty(destSiteId) &&
                world.Strategic.Sites.TryGet(destSiteId, out var site) &&
                site != null)
            {
                world.WorldPresence.SetAtSite(characterId, site.SiteId);
                motion.ClearTravel();
                world.BackgroundCharacterTravel.Remove(characterId);
                var request = LoadedDestinationArrivalMaterializer.LoadedLocalMapMaterializationRequest
                    .ForRuntimeSiteArrival(in arrivalContext);
                LoadedDestinationArrivalMaterializer.TryMaterializeCharacterIntoLoadedLocalMap(
                    world,
                    characterId,
                    party,
                    in request);
                return;
            }

            var derived = HexMath.WorldToHex(center.X, center.Y, hexSize);
            motion.ClearTravel();
            world.BackgroundCharacterTravel.Remove(characterId);

            if (world.LocalMap.ContainsOccupant(characterId))
                return;

            world.WorldPresence.SetAtWorldPosition(characterId, center, derived);
            var wildernessRequest = LoadedDestinationArrivalMaterializer.LoadedLocalMapMaterializationRequest
                .ForRuntimeArrival(stopBackgroundTravel: false);
            LoadedDestinationArrivalMaterializer.TryMaterializeCharacterIntoLoadedLocalMap(
                world,
                characterId,
                party,
                in wildernessRequest);
        }

        static bool TryBuildPathLeavingSite(
            SimulationWorld world,
            WorldSite site,
            HexCoord goalHex,
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

            into.Add(departureFootprintHex);
            into.Add(exitHex);
            if (exitHex == goalHex)
                return into.Count >= 2;

            PathScratch.Clear();
            if (!HexPathfinder.TryFindPath(world.HexWorld, exitHex, goalHex, PathScratch) ||
                PathScratch.Count < 1)
                return false;

            for (var i = 1; i < PathScratch.Count; i++)
                into.Add(PathScratch[i]);

            return into.Count >= 2;
        }

        public static bool TryResolveCharacterWorldLocation(
            SimulationWorld world,
            EntityId characterId,
            out BackgroundCharacterLocationKind kind,
            out string siteId,
            out WorldVec2 worldPos,
            out HexCoord derivedHex)
        {
            kind = BackgroundCharacterLocationKind.Unknown;
            siteId = string.Empty;
            worldPos = default;
            derivedHex = default;
            if (world == null || characterId.IsNone)
                return false;

            if (!world.WorldPresence.TryGet(characterId, out var presence) || presence == null)
                return false;

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;

            if (presence.Mode == PartyWorldPresenceMode.AtSite &&
                !string.IsNullOrEmpty(presence.SiteId))
            {
                kind = BackgroundCharacterLocationKind.AtWorldSite;
                siteId = presence.SiteId;
                if (world.Strategic.Sites.TryResolveSitePresenceHex(siteId, out derivedHex))
                {
                    worldPos = HexCenter(derivedHex, hexSize);
                    return true;
                }

                return false;
            }

            if (presence.Mode == PartyWorldPresenceMode.AtWorldPosition &&
                presence.HasContinuousWorldPosition)
            {
                kind = BackgroundCharacterLocationKind.AtWorldPosition;
                worldPos = presence.ContinuousWorldPosition;
                derivedHex = HexMath.WorldToHex(worldPos.X, worldPos.Y, hexSize);
                if (derivedHex != presence.DerivedHexFromWorldPosition)
                    world.WorldPresence.SetAtWorldPosition(characterId, worldPos, derivedHex);
                return true;
            }

            if (presence.UsesHexPresence)
            {
                kind = BackgroundCharacterLocationKind.AtWorldPosition;
                derivedHex = presence.ResidualHex;
                worldPos = HexCenter(derivedHex, hexSize);
                return true;
            }

            return false;
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

        /// <summary>
        /// Travel To Hex 命中 WorldSite Footprint 时 canonicalize 为 TargetWorldSite(siteId)。
        /// </summary>
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

        static WorldVec2 HexCenter(HexCoord hex, float hexSize)
        {
            HexMath.ToWorldPosition(hex, hexSize, out var x, out var y);
            return new WorldVec2(x, y);
        }
    }

    public enum BackgroundCharacterLocationKind
    {
        Unknown = 0,
        AtWorldSite = 1,
        AtWorldPosition = 2,
    }
}
