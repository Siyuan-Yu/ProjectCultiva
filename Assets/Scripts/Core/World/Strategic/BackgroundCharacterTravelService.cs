using System;
using System.Collections.Generic;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Surface;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Background Character Continuous Surface travel.</summary>
    public static class BackgroundCharacterTravelService
    {
        static readonly List<WorldVec2> RouteScratch = new List<WorldVec2>(128);

        public static Result BeginTravelToWorldSite(
            SimulationWorld world,
            EntityId characterId,
            string siteId,
            PlayerPartyRuntime party = null,
            bool debugOverrideLocalOccupant = false)
        {
            if (world == null || characterId.IsNone || string.IsNullOrEmpty(siteId))
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid background Site travel.");
            var canStart = debugOverrideLocalOccupant
                ? CharacterWorldMovementAuthorityQuery.CanStartBackgroundTravelDebug(
                    world, characterId, party, out var authorityError)
                : CharacterWorldMovementAuthorityQuery.CanStartBackgroundTravel(
                    world, characterId, party, out authorityError);
            if (!canStart)
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    authorityError ?? "Cannot start background travel.");
            if (!TryResolveCharacterWorldLocation(
                    world, characterId, out _, out _, out var start, out var surfaceId))
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "Character has no exact Surface location.");
            if (!world.SurfaceGround.TryResolveSiteArrival(
                    siteId, out var destinationSurfaceId, out var destination) ||
                !string.Equals(surfaceId, destinationSurfaceId, StringComparison.Ordinal) ||
                !world.SurfaceGround.TryGet(surfaceId, out var navigation) ||
                navigation == null ||
                !navigation.Contains(start.X, start.Y) ||
                !navigation.Contains(destination.X, destination.Y))
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "No shared Continuous Surface for NPC Site travel.");

            RouteScratch.Clear();
            var routeStatus = navigation.TryFindRoute(start, destination, RouteScratch);
            if (routeStatus != SurfaceGroundRouteStatus.Found)
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "NPC Surface route unavailable: " + routeStatus);

            var motion = world.BackgroundCharacterTravel.GetOrCreate(characterId);
            motion.BeginSurfaceTravel(RouteScratch, surfaceId, destination, siteId);
            motion.LastProcessedWorldTick = world.Tick.Value;
            world.WorldPresence.SetAtWorldPosition(characterId, start, surfaceId);
            return Result.Success();
        }

        public static Result CancelTravel(SimulationWorld world, EntityId characterId)
        {
            if (world?.BackgroundCharacterTravel == null || characterId.IsNone)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid cancel args.");
            if (!world.BackgroundCharacterTravel.TryGet(characterId, out var motion) ||
                motion == null || !motion.IsMoving)
                return Result.Failure(
                    ErrorCode.InvalidOperation,
                    "Character is not background traveling.");
            motion.CancelTravelPreserveProgress();
            world.BackgroundCharacterTravel.Remove(characterId);
            return Result.Success();
        }

        public static void CancelTravelIfAny(SimulationWorld world, EntityId characterId)
        {
            if (world?.BackgroundCharacterTravel == null || characterId.IsNone ||
                !world.BackgroundCharacterTravel.TryGet(characterId, out var motion) ||
                motion == null)
                return;
            motion.CancelTravelPreserveProgress();
            world.BackgroundCharacterTravel.Remove(characterId);
        }

        public static void AdvanceAll(SimulationWorld world, int ticks)
        {
            if (ticks > 0)
                BackgroundSimulationScheduler.AdvanceTravelBatch(world, (ulong)ticks);
        }

        public static void AdvanceDistanceBudget(
            SimulationWorld world,
            EntityId characterId,
            float distanceBudget)
        {
            if (world?.BackgroundCharacterTravel == null || characterId.IsNone ||
                distanceBudget <= 0f ||
                !world.BackgroundCharacterTravel.TryGet(characterId, out var motion) ||
                motion == null || !motion.IsSurfaceRoute ||
                !world.WorldPresence.TryGet(characterId, out var presence) ||
                presence == null || !presence.HasContinuousWorldPosition ||
                !string.Equals(
                    presence.PersonalSurfaceId, motion.SurfaceId, StringComparison.Ordinal))
                return;
            if (world.Entities.TryGet(characterId, out var entity) &&
                !CombatLifeStateService.CanFight(entity))
            {
                CancelTravelIfAny(world, characterId);
                return;
            }

            var position = presence.ContinuousWorldPosition;
            var remaining = distanceBudget;
            var guard = 0;
            while (remaining > 0.0001f &&
                   motion.TryGetSurfaceWaypoint(out var waypoint) &&
                   guard++ < 128)
            {
                var distance = WorldVec2.Distance(position, waypoint);
                if (distance <= remaining + 0.0001f)
                {
                    position = waypoint;
                    remaining -= distance;
                    motion.IncrementPathIndex();
                }
                else
                {
                    var t = remaining / distance;
                    position = new WorldVec2(
                        position.X + (waypoint.X - position.X) * t,
                        position.Y + (waypoint.Y - position.Y) * t);
                    motion.SetSegmentProgress(t);
                    remaining = 0f;
                }
            }

            if (!motion.TryGetSurfaceWaypoint(out _))
            {
                position = motion.SurfaceDestination;
                var destinationSiteId = motion.DestinationSiteId;
                var surfaceId = motion.SurfaceId;
                world.BackgroundCharacterTravel.Remove(characterId);
                if (!string.IsNullOrEmpty(destinationSiteId))
                    world.WorldPresence.SetAtSiteWithAnchor(
                        characterId, destinationSiteId, position, surfaceId);
                else
                    world.WorldPresence.SetAtWorldPosition(characterId, position, surfaceId);
                return;
            }
            world.WorldPresence.SetAtWorldPosition(
                characterId, position, motion.SurfaceId);
        }

        public static bool TryResolveCharacterWorldLocation(
            SimulationWorld world,
            EntityId characterId,
            out BackgroundCharacterLocationKind kind,
            out string siteId,
            out WorldVec2 worldPosition,
            out string surfaceId)
        {
            kind = BackgroundCharacterLocationKind.Unknown;
            siteId = string.Empty;
            worldPosition = default;
            surfaceId = string.Empty;
            if (world == null || characterId.IsNone ||
                !world.WorldPresence.TryGet(characterId, out var presence) ||
                presence == null || !presence.HasContinuousWorldPosition ||
                string.IsNullOrEmpty(presence.PersonalSurfaceId))
                return false;
            if (!world.SurfaceGround.TryGet(
                    presence.PersonalSurfaceId, out var surface) ||
                surface == null ||
                !surface.Contains(presence.WorldPosX, presence.WorldPosY))
                return false;
            kind = presence.Mode == PartyWorldPresenceMode.AtSite
                ? BackgroundCharacterLocationKind.AtWorldSite
                : BackgroundCharacterLocationKind.AtWorldPosition;
            siteId = presence.SiteId ?? string.Empty;
            worldPosition = presence.ContinuousWorldPosition;
            surfaceId = surface.SurfaceId;
            return true;
        }
    }

    public enum BackgroundCharacterLocationKind
    {
        Unknown = 0,
        AtWorldSite = 1,
        AtWorldPosition = 2,
    }
}
