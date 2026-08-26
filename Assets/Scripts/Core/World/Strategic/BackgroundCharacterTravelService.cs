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
            if (!world.Strategic.Sites.TryResolveSiteHex(siteId, out var approach))
                return Result.Failure(ErrorCode.NotFound, "Site not found.", siteId);
            return BeginTravel(world, characterId, approach, siteId, party, debugOverrideLocalOccupant);
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
            if (!world.HexWorld.TryGetTile(destinationHex, out var destTile) ||
                destTile == null ||
                !destTile.IsPassable)
                return Result.Failure(ErrorCode.InvalidArgument, "Destination hex is not passable.");

            var canStart = debugOverrideLocalOccupant
                ? CharacterWorldMovementAuthorityQuery.CanStartBackgroundTravelDebug(
                    world, characterId, party, out var authErr)
                : CharacterWorldMovementAuthorityQuery.CanStartBackgroundTravel(
                    world, characterId, party, out authErr);
            if (!canStart)
                return Result.Failure(ErrorCode.InvalidOperation, authErr ?? "Cannot start background travel.");

            if (!TryResolveCharacterWorldLocation(world, characterId, out var startKind, out var startSiteId, out var startPos, out var startHex))
                return Result.Failure(ErrorCode.InvalidOperation, "Character has no world location.");

            var goalHex = destinationHex;
            if (!string.IsNullOrEmpty(destinationSiteId) &&
                world.Strategic.Sites.TryGet(destinationSiteId, out var targetSite) &&
                targetSite != null)
                goalHex = ResolveDeterministicSiteApproachHex(world, startHex, targetSite);

            if (startKind == BackgroundCharacterLocationKind.AtWorldSite &&
                !string.IsNullOrEmpty(startSiteId) &&
                world.Strategic.Sites.TryGet(startSiteId, out var fromSite) &&
                fromSite != null)
            {
                if (!TryBuildPathLeavingSite(world, fromSite, goalHex, FullPathScratch, out var exitHex))
                    return Result.Failure(ErrorCode.InvalidOperation, "No path leaving WorldSite.");
                startPos = HexCenter(exitHex, world.HexWorld.HexSize);
                startHex = exitHex;
            }
            else
            {
                if (!HexPathfinder.TryFindPath(world.HexWorld, startHex, goalHex, FullPathScratch) ||
                    FullPathScratch.Count < 1)
                    return Result.Failure(ErrorCode.InvalidOperation, "No hex path to destination.");
            }

            if (debugOverrideLocalOccupant && world.LocalMap != null)
                world.LocalMap.RemoveOccupant(characterId);

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var derived = HexMath.WorldToHex(startPos.X, startPos.Y, hexSize);
            world.WorldPresence.SetAtWorldPosition(characterId, startPos, derived);

            var motion = world.BackgroundCharacterTravel.GetOrCreate(characterId);
            motion.BeginTravel(FullPathScratch, goalHex, destinationSiteId, HexTravelMode.Ground);
            return Result.Success();
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
            if (world?.BackgroundCharacterTravel == null || ticks < 1)
                return;

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var budget = PlayerPartyHexTravelService.WorldUnitsPerTick(hexSize) * ticks;
            if (budget <= 0f)
                return;

            var traveling = new List<EntityId>(world.BackgroundCharacterTravel.All.Count);
            foreach (var kv in world.BackgroundCharacterTravel.All)
            {
                if (kv.Value != null && kv.Value.IsMoving)
                    traveling.Add(new EntityId(kv.Key));
            }

            for (var i = 0; i < traveling.Count; i++)
                AdvanceDistanceBudget(world, traveling[i], budget);
        }

        public static void AdvanceDistanceBudget(SimulationWorld world, EntityId characterId, float distanceBudget)
        {
            if (world?.BackgroundCharacterTravel == null || characterId.IsNone || distanceBudget <= 0f)
                return;
            if (!world.BackgroundCharacterTravel.TryGet(characterId, out var motion) || motion == null || !motion.IsMoving)
                return;
            if (!world.WorldPresence.TryGet(characterId, out var presence) ||
                presence == null ||
                !presence.HasContinuousWorldPosition)
                return;

            if (world.Entities.TryGet(characterId, out var entity) &&
                !CombatLifeStateService.CanFight(entity))
            {
                CancelTravelIfAny(world, characterId);
                return;
            }

            var hexSize = world.HexWorld.HexSize > 0f ? world.HexWorld.HexSize : 1f;
            var pos = presence.ContinuousWorldPosition;
            var remainingBudget = distanceBudget;
            var guard = 0;
            while (remainingBudget > 0.0001f && motion.IsMoving && guard++ < 64)
            {
                if (!motion.TryGetActiveSegmentWorld(pos, hexSize, out var fromPos, out var toPos))
                {
                    FinishArrival(world, characterId, motion, hexSize);
                    return;
                }

                var segmentLen = WorldVec2.Distance(fromPos, toPos);
                if (segmentLen < 0.0001f)
                {
                    motion.IncrementPathIndex();
                    if (motion.SegmentIndex >= motion.HexPathCount - 1)
                    {
                        FinishArrival(world, characterId, motion, hexSize);
                        return;
                    }

                    continue;
                }

                var remainingOnSegment = WorldVec2.Distance(pos, toPos);
                if (remainingOnSegment <= remainingBudget + 0.0001f)
                {
                    pos = toPos;
                    var derived = HexMath.WorldToHex(pos.X, pos.Y, hexSize);
                    world.WorldPresence.SetAtWorldPosition(characterId, pos, derived);
                    motion.SetSegment(motion.SegmentIndex, 1f);
                    remainingBudget -= remainingOnSegment;
                    motion.IncrementPathIndex();
                    if (motion.SegmentIndex >= motion.HexPathCount - 1)
                    {
                        FinishArrival(world, characterId, motion, hexSize);
                        return;
                    }

                    continue;
                }

                var dirX = (toPos.X - pos.X) / remainingOnSegment;
                var dirY = (toPos.Y - pos.Y) / remainingOnSegment;
                pos = new WorldVec2(pos.X + dirX * remainingBudget, pos.Y + dirY * remainingBudget);
                var midDerived = HexMath.WorldToHex(pos.X, pos.Y, hexSize);
                world.WorldPresence.SetAtWorldPosition(characterId, pos, midDerived);
                var progress = 1f - WorldVec2.Distance(pos, toPos) / segmentLen;
                motion.SetSegment(motion.SegmentIndex, progress);
                remainingBudget = 0f;
            }
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
            float hexSize)
        {
            var destHex = motion.DestinationHex;
            var destSiteId = motion.DestinationSiteId ?? string.Empty;
            var center = HexCenter(destHex, hexSize);

            if (!string.IsNullOrEmpty(destSiteId) &&
                world.Strategic.Sites.TryGet(destSiteId, out var site) &&
                site != null)
            {
                world.WorldPresence.SetAtSite(characterId, site.SiteId);
            }
            else
            {
                var derived = HexMath.WorldToHex(center.X, center.Y, hexSize);
                world.WorldPresence.SetAtWorldPosition(characterId, center, derived);
            }

            motion.ClearTravel();
            world.BackgroundCharacterTravel.Remove(characterId);
        }

        static bool TryBuildPathLeavingSite(
            SimulationWorld world,
            WorldSite site,
            HexCoord goalHex,
            List<HexCoord> into,
            out HexCoord exitHex)
        {
            into.Clear();
            exitHex = default;
            if (!BackgroundCharacterSiteDepartureResolver.TryResolveDepartureHex(world, site, goalHex, out exitHex))
                return false;

            into.Add(exitHex);
            if (exitHex == goalHex)
                return true;

            PathScratch.Clear();
            if (!HexPathfinder.TryFindPath(world.HexWorld, exitHex, goalHex, PathScratch) ||
                PathScratch.Count < 1)
                return false;

            for (var i = 0; i < PathScratch.Count; i++)
            {
                if (i == 0 && PathScratch[i] == exitHex)
                    continue;
                into.Add(PathScratch[i]);
            }

            return into.Count >= 1;
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
