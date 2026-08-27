using System;
using System.Collections.Generic;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase 3：FormalArmy 连续世界旅行（组织级一次路径；成员 Presence 派生）。
    /// </summary>
    public static class FormalArmyContinuousTravelService
    {
        static readonly List<HexCoord> PathScratch = new List<HexCoord>(64);
        static readonly List<HexCoord> FullPathScratch = new List<HexCoord>(64);

        public static Result MoveArmyToHex(SimulationWorld world, string armyId, HexCoord destination) =>
            BeginTravel(world, armyId, destination, string.Empty, FormalArmyOrderKind.TravelToHex);

        public static Result MoveArmyToWorldSite(SimulationWorld world, string armyId, string siteId)
        {
            if (world == null || string.IsNullOrWhiteSpace(armyId) || string.IsNullOrEmpty(siteId))
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid army site travel.");
            if (!world.Strategic.Sites.TryGet(siteId, out _))
                return Result.Failure(ErrorCode.NotFound, "Site not found.", siteId);
            return BeginTravel(world, armyId, default, siteId, FormalArmyOrderKind.TravelToWorldSite);
        }

        public static void InitializeAtWorldSite(SimulationWorld world, FormalArmy army, string siteId)
        {
            if (world == null || army == null || string.IsNullOrEmpty(siteId))
                return;
            if (!world.Strategic.Sites.TryGet(siteId, out var site) || site == null)
                return;

            site.EnsurePresenceHexValid();
            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            army.UsesHexStrategicPosition = true;
            army.WorldMotion.SetAtWorldSite(siteId, site.AnchorHex, hexSize);
            army.SyncLegacyFromWorldMotion();
            army.State = FormalArmyState.Idle;
            FormalArmyMemberPresenceSync.SyncAll(world, army);
        }

        static Result BeginTravel(
            SimulationWorld world,
            string armyId,
            HexCoord destinationHex,
            string destinationSiteId,
            FormalArmyOrderKind orderKind)
        {
            if (world == null || string.IsNullOrWhiteSpace(armyId))
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid army travel.");
            if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
                return Result.Failure(ErrorCode.NotFound, "Army not found.", armyId);
            if (!world.HexWorld.HasGrid)
                return Result.Failure(ErrorCode.InvalidOperation, "Hex grid not loaded.");
            if (army.State == FormalArmyState.Moving || army.WorldMotion.IsMoving)
                return Result.Failure(ErrorCode.InvalidOperation, "Army is already traveling.");

            if (!FormalArmyWorldLocationQuery.TryResolve(
                    world, army, out var startKind, out var startSiteId, out var startPos, out var startHex))
                return Result.Failure(ErrorCode.InvalidOperation, "Army has no world location.");

            destinationSiteId = TryCanonicalizeFootprintHexDestination(
                world, destinationHex, destinationSiteId, out _);

            var goalHex = destinationHex;
            if (!string.IsNullOrEmpty(destinationSiteId) &&
                world.Strategic.Sites.TryGet(destinationSiteId, out var targetSite) &&
                targetSite != null)
                goalHex = ResolveDeterministicSiteApproachHex(world, startHex, targetSite);

            if (!world.HexWorld.TryGetTile(goalHex, out var destTile) ||
                destTile == null ||
                !destTile.IsPassable)
                return Result.Failure(ErrorCode.InvalidArgument, "Destination hex is not passable.");

            var motion = army.WorldMotion;
            if (startKind == FormalArmyLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(startSiteId) &&
                world.Strategic.Sites.TryGet(startSiteId, out var fromSite) &&
                fromSite != null)
            {
                if (!TryBuildPathLeavingSite(
                        world,
                        fromSite,
                        startHex,
                        goalHex,
                        FullPathScratch,
                        out var exitHex,
                        out var departureFootprintHex))
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

                var departureStartWorld = HexCenter(startHex, hexSizeForDeparture);
                motion.BeginSiteDepartureTravel(
                    orderKind,
                    FullPathScratch,
                    goalHex,
                    destinationSiteId,
                    departureFootprintHex,
                    exitHex,
                    departureStartWorld,
                    boundaryEntryPos,
                    HexTravelMode.Ground);
                motion.LastProcessedWorldTick = world.Tick.Value;
                army.SyncLegacyFromWorldMotion();
                return Result.Success();
            }

            if (!HexPathfinder.TryFindPath(world.HexWorld, startHex, goalHex, FullPathScratch) ||
                FullPathScratch.Count < 1)
                return Result.Failure(ErrorCode.InvalidOperation, "No hex path to destination.");

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            if (startKind == FormalArmyLocationKind.AtWorldSite &&
                world.Strategic.Sites.TryResolveSitePresenceHex(startSiteId, out var presenceHex))
            {
                var center = HexCenter(presenceHex, hexSize);
                motion.SetAtWorldPosition(center, presenceHex);
            }

            motion.BeginAutoTravel(orderKind, FullPathScratch, goalHex, destinationSiteId, HexTravelMode.Ground);
            motion.LastProcessedWorldTick = world.Tick.Value;
            army.SyncLegacyFromWorldMotion();
            return Result.Success();
        }

        public static void AdvanceAll(SimulationWorld world, int ticks)
        {
            if (world?.Strategic?.FormalArmies == null || ticks < 1)
                return;

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var budget = PlayerPartyHexTravelService.WorldUnitsPerTick(hexSize) * ticks;
            foreach (var kv in world.Strategic.FormalArmies.Armies)
            {
                var army = kv.Value;
                if (army == null || !army.WorldMotion.IsMoving)
                    continue;
                AdvanceDistanceBudget(world, army, budget);
                ArmyService.SyncNonLivingMembers(world, army);
            }
        }

        public static void AdvanceDistanceBudget(SimulationWorld world, FormalArmy army, float distanceBudget)
        {
            if (world == null || army == null || distanceBudget <= 0f)
                return;

            var motion = army.WorldMotion;
            if (!motion.IsMoving)
                return;

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var pos = motion.IsSiteDeparturePending &&
                      motion.LocationKind == FormalArmyLocationKind.AtWorldSite
                ? motion.SiteDepartureVirtualPosition
                : motion.WorldPosition;
            var previousDerived = motion.CurrentHex;
            var isSiteDepartureVirtual = motion.IsSiteDeparturePending &&
                                         motion.LocationKind == FormalArmyLocationKind.AtWorldSite;

            var remainingBudget = distanceBudget;
            var guard = 0;
            while (remainingBudget > 0.0001f && motion.IsMoving && guard++ < 64)
            {
                if (!motion.TryGetActiveSegmentWorld(hexSize, out var fromPos, out var toPos))
                {
                    if (isSiteDepartureVirtual)
                    {
                        CommitSiteDepartureBoundaryCrossing(world, army, motion);
                        if (!motion.IsMoving)
                            return;
                        pos = motion.WorldPosition;
                        previousDerived = motion.CurrentHex;
                        isSiteDepartureVirtual = false;
                        continue;
                    }

                    FinishArrival(world, army, motion, hexSize);
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
                            CommitSiteDepartureBoundaryCrossing(world, army, motion);
                            if (!motion.IsMoving)
                                return;
                            pos = motion.WorldPosition;
                            previousDerived = motion.CurrentHex;
                            isSiteDepartureVirtual = false;
                            continue;
                        }

                        FinishArrival(world, army, motion, hexSize);
                        return;
                    }

                    continue;
                }

                var remainingOnSegment = WorldVec2.Distance(pos, toPos);
                if (remainingOnSegment <= remainingBudget + 0.0001f)
                {
                    pos = toPos;
                    if (isSiteDepartureVirtual)
                    {
                        var previousHex = motion.CurrentHex;
                        motion.SetSiteDepartureVirtualPosition(pos, hexSize);
                        if (ShouldCommitSiteDepartureBoundaryCrossing(world, army, motion, previousHex, motion.CurrentHex))
                            CommitSiteDepartureBoundaryCrossing(world, army, motion);
                        if (!motion.IsMoving)
                            return;
                        pos = motion.WorldPosition;
                        previousDerived = motion.CurrentHex;
                        isSiteDepartureVirtual = motion.IsSiteDeparturePending &&
                                                  motion.LocationKind == FormalArmyLocationKind.AtWorldSite;
                        remainingBudget -= remainingOnSegment;
                        motion.IncrementPathIndex();
                        if (motion.SegmentIndex >= motion.HexPathCount - 1 && !isSiteDepartureVirtual)
                        {
                            FinishArrival(world, army, motion, hexSize);
                            return;
                        }

                        continue;
                    }

                    var derived = HexMath.WorldToHex(pos.X, pos.Y, hexSize);
                    motion.SetWorldPositionInternal(pos, derived);
                    motion.SetSegment(motion.SegmentIndex, 1f);
                    remainingBudget -= remainingOnSegment;
                    motion.IncrementPathIndex();
                    if (motion.SegmentIndex >= motion.HexPathCount - 1)
                    {
                        FinishArrival(world, army, motion, hexSize);
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
                    var previousHex = motion.CurrentHex;
                    motion.SetSiteDepartureVirtualPosition(pos, hexSize);
                    if (ShouldCommitSiteDepartureBoundaryCrossing(world, army, motion, previousHex, motion.CurrentHex))
                        CommitSiteDepartureBoundaryCrossing(world, army, motion);
                    if (!motion.IsMoving)
                        return;
                    if (motion.IsSiteDeparturePending &&
                        motion.LocationKind == FormalArmyLocationKind.AtWorldSite)
                    {
                        var virtualProgress = 1f - WorldVec2.Distance(pos, toPos) / segmentLen;
                        motion.SetSegment(motion.SegmentIndex, virtualProgress);
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
                motion.SetWorldPositionInternal(pos, midDerived);
                var progress = 1f - WorldVec2.Distance(pos, toPos) / segmentLen;
                motion.SetSegment(motion.SegmentIndex, progress);
                remainingBudget = 0f;
            }

            army.SyncLegacyFromWorldMotion();
            FormalArmyMemberPresenceSync.SyncAll(world, army);
        }

        static void CommitSiteDepartureBoundaryCrossing(
            SimulationWorld world,
            FormalArmy army,
            FormalArmyWorldMotion motion)
        {
            if (!motion.IsSiteDeparturePending)
                return;

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var boundaryEntry = motion.SiteDepartureBoundaryEntry;
            var exitHex = motion.SiteDepartureExitHex;
            motion.SetWorldPositionInternal(boundaryEntry, exitHex);
            motion.ClearSiteDeparturePending();
            army.SyncLegacyFromWorldMotion();
            FormalArmyMemberPresenceSync.SyncAll(world, army);
        }

        static bool ShouldCommitSiteDepartureBoundaryCrossing(
            SimulationWorld world,
            FormalArmy army,
            FormalArmyWorldMotion motion,
            HexCoord previousHex,
            HexCoord newHex)
        {
            if (!motion.IsSiteDeparturePending || string.IsNullOrEmpty(motion.SiteId))
                return false;
            if (!world.Strategic.Sites.TryGet(motion.SiteId, out var site) || site == null)
                return false;
            return site.OccupiesHex(previousHex) && !site.OccupiesHex(newHex);
        }

        static void FinishArrival(
            SimulationWorld world,
            FormalArmy army,
            FormalArmyWorldMotion motion,
            float hexSize)
        {
            var destSiteId = motion.DestinationSiteId ?? string.Empty;
            if (string.IsNullOrEmpty(destSiteId))
                destSiteId = TryCanonicalizeFootprintHexDestination(
                    world, motion.DestinationHex, destSiteId, out _);

            if (!string.IsNullOrEmpty(destSiteId) &&
                world.Strategic.Sites.TryGet(destSiteId, out var site) &&
                site != null)
            {
                site.EnsurePresenceHexValid();
                motion.SetAtWorldSite(site.SiteId, site.AnchorHex, hexSize);
            }
            else
            {
                var center = HexCenter(motion.DestinationHex, hexSize);
                var derived = HexMath.WorldToHex(center.X, center.Y, hexSize);
                motion.SetAtWorldPosition(center, derived);
            }

            motion.ClearTravel();
            army.SyncLegacyFromWorldMotion();
            army.State = FormalArmyState.Idle;
            FormalArmyMemberPresenceSync.SyncAll(world, army);
        }

        static bool TryBuildPathLeavingSite(
            SimulationWorld world,
            WorldSite site,
            HexCoord startHex,
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

            PathScratch.Clear();
            if (!startHex.Equals(departureFootprintHex) &&
                HexPathfinder.TryFindPath(world.HexWorld, startHex, departureFootprintHex, PathScratch) &&
                PathScratch.Count >= 1)
            {
                for (var i = 0; i < PathScratch.Count; i++)
                {
                    if (into.Count == 0 || !into[into.Count - 1].Equals(PathScratch[i]))
                        into.Add(PathScratch[i]);
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

            PathScratch.Clear();
            if (!HexPathfinder.TryFindPath(world.HexWorld, exitHex, goalHex, PathScratch) ||
                PathScratch.Count < 1)
                return false;

            for (var i = 1; i < PathScratch.Count; i++)
            {
                if (into.Count == 0 || !into[into.Count - 1].Equals(PathScratch[i]))
                    into.Add(PathScratch[i]);
            }

            return into.Count >= 2;
        }

        static HexCoord ResolveDeterministicSiteApproachHex(
            SimulationWorld world,
            HexCoord from,
            WorldSite site)
        {
            HexCoord best = site.AnchorHex;
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
}
