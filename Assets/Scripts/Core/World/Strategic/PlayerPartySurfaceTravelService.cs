using XianXia.Core.World;
using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Surface;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Normal Continuous Outdoor PlayerParty travel.  It intentionally has no HexWorld dependency.</summary>
    public static class PlayerPartySurfaceTravelService
    {
        static readonly List<WorldVec2> RouteScratch = new List<WorldVec2>(512);

        public static Result BeginTravel(
            SimulationWorld world,
            PlayerPartyRuntime party,
            WorldVec2 destination,
            string targetSiteId,
            float arrivalRadius) =>
            BeginTravelInternal(world, party, destination, targetSiteId, arrivalRadius,
                null, default, null, EntityId.None, false);

        /// <summary>Commit a route planned from an immutable Surface on a worker, after checking
        /// the exact authority snapshot again on the main thread.</summary>
        public static Result BeginTravelWithPreparedRoute(
            SimulationWorld world,
            PlayerPartyRuntime party,
            WorldVec2 destination,
            string targetSiteId,
            float arrivalRadius,
            IReadOnlyList<WorldVec2> preparedRoute,
            WorldVec2 expectedStart,
            string expectedSurfaceId,
            EntityId expectedActor,
            bool expectedAirborne) =>
            BeginTravelInternal(world, party, destination, targetSiteId, arrivalRadius,
                preparedRoute, expectedStart, expectedSurfaceId, expectedActor, expectedAirborne);

        static Result BeginTravelInternal(
            SimulationWorld world,
            PlayerPartyRuntime party,
            WorldVec2 destination,
            string targetSiteId,
            float arrivalRadius,
            IReadOnlyList<WorldVec2> preparedRoute,
            WorldVec2 expectedStart,
            string expectedSurfaceId,
            EntityId expectedActor,
            bool expectedAirborne)
        {
            if (world == null || party == null || !party.HasActive)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid Surface travel args.");
            if (!HuangcunOpeningAccess.AllowsPosition(world, world.PlayerPartyTravel?.SurfaceId, destination.X, destination.Y))
                return Result.Failure(ErrorCode.InvalidOperation, HuangcunOpeningAccess.BlockedMessage);
            if (world.Entities.TryGet(party.ActiveCharacterId, out var activeEntity))
            {
                if (XianXia.Core.Npc.PartyRestService.BlocksMovement(activeEntity) ||
                    activeEntity.TryGet<XianXia.Core.Npc.PersonalLifeComponent>(out var personal) &&
                    personal.Energy <= 0)
                    return Result.Failure(ErrorCode.InvalidOperation, "主控精力耗尽，恢复后才能行军。");
                if (XianXia.Core.Combat.FlightService.IsAirborne(activeEntity) &&
                    !XianXia.Core.Combat.FlightService.CanSustain(activeEntity))
                    return Result.Failure(ErrorCode.InvalidOperation,
                        "斗气化翼已无法维持，请落地或恢复状态后再规划旅行。");
            }
            var motion = world.PlayerPartyTravel;
            if (motion == null)
                return Result.Failure(ErrorCode.InvalidOperation, "Surface travel state is unavailable.");
            var canonical = TryCanonicalizeNormalContinuousAuthority(world, motion);
            if (canonical.IsFailure)
                return canonical;
            if (!motion.HasPosition)
                return Result.Failure(ErrorCode.InvalidOperation, "Surface travel requires a canonical WorldPosition.");
            if (!ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world) ||
                string.IsNullOrEmpty(motion.SurfaceId) ||
                !world.SurfaceGround.TryGet(motion.SurfaceId, out var navigation) || navigation == null ||
                !navigation.Contains(motion.WorldPosition.X, motion.WorldPosition.Y) ||
                !navigation.Contains(destination.X, destination.Y))
                return Result.Failure(ErrorCode.InvalidOperation, "Continuous Surface navigation is not ready for this route.");

            RouteScratch.Clear();
            SurfaceGroundRouteStatus status;
            if (preparedRoute != null)
            {
                if (!motion.WorldPosition.Equals(expectedStart) ||
                    !string.Equals(motion.SurfaceId, expectedSurfaceId, StringComparison.Ordinal) ||
                    party.ActiveCharacterId != expectedActor ||
                    XianXia.Core.Combat.FlightService.IsAirborne(world, expectedActor) != expectedAirborne ||
                    preparedRoute.Count == 0 ||
                    !preparedRoute[0].Equals(expectedStart) ||
                    !preparedRoute[preparedRoute.Count - 1].Equals(destination))
                    return Result.Failure(ErrorCode.InvalidOperation,
                        "规划期间主控或位置已变化，请重新选择目标。");
                for (var i = 0; i < preparedRoute.Count; i++)
                {
                    var point = preparedRoute[i];
                    if (!navigation.Contains(point.X, point.Y) ||
                        (!expectedAirborne && i > 0 &&
                         (i > 1 || navigation.IsWalkable(expectedStart.X, expectedStart.Y)) &&
                         !navigation.IsSegmentWalkable(preparedRoute[i - 1].X,
                             preparedRoute[i - 1].Y, point.X, point.Y)))
                        return Result.Failure(ErrorCode.InvalidOperation,
                            "规划路线已失效，请重新选择目标。");
                    RouteScratch.Add(point);
                }
                status = SurfaceGroundRouteStatus.Found;
            }
            else
                status = XianXia.Core.Combat.FlightService.Route(world,
                    world.Strategic.PlayerPartyContext.ActiveCharacterId, navigation,
                    motion.WorldPosition, destination, RouteScratch);
            if (status == SurfaceGroundRouteStatus.BlockedGoal)
                return Result.Failure(ErrorCode.InvalidArgument, "不可步行到达的地面目标。");
            if (status != SurfaceGroundRouteStatus.Found)
                return Result.Failure(ErrorCode.InvalidOperation, "Surface route unavailable: " + status);
            for (var i = 0; i < RouteScratch.Count; i++)
                if (!HuangcunOpeningAccess.AllowsPosition(world, navigation.SurfaceId, RouteScratch[i].X, RouteScratch[i].Y))
                    return Result.Failure(ErrorCode.InvalidOperation, HuangcunOpeningAccess.BlockedMessage);
            for (var i = 0; i < RouteScratch.Count; i++)
                if (!HuangcunOpeningAccess.AllowsPlayerMove(world, navigation.SurfaceId,
                    i == 0 ? motion.WorldPosition.X : RouteScratch[i - 1].X,
                    i == 0 ? motion.WorldPosition.Y : RouteScratch[i - 1].Y,
                    RouteScratch[i].X, RouteScratch[i].Y))
                    return Result.Failure(ErrorCode.InvalidOperation, HuangcunOpeningAccess.PatrolBlockedMessage);

            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);
            motion.SetCurrentOutdoorWorldSiteContext(string.Empty);
            motion.BeginSurfaceAutoTravel(navigation.SurfaceId, destination, targetSiteId,
                Math.Max(0.001f, arrivalRadius), RouteScratch);
            PlayerPartyTransitionMembership.ReconcilePlayerPartyMemberWorldPresenceFromMotion(
                world, party);
            return Result.Success();
        }

        public static bool TryResumeAfterRestore(SimulationWorld world,
            WorldVec2 destination, string targetSiteId, float arrivalRadius)
        {
            var motion = world?.PlayerPartyTravel;
            if (motion == null || !motion.HasPosition ||
                !HuangcunOpeningAccess.AllowsPosition(world, motion.SurfaceId, destination.X, destination.Y) ||
                !ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world) ||
                string.IsNullOrEmpty(motion.SurfaceId) ||
                !world.SurfaceGround.TryGet(motion.SurfaceId, out var navigation) || navigation == null ||
                !navigation.Contains(motion.WorldPosition.X, motion.WorldPosition.Y) ||
                !navigation.Contains(destination.X, destination.Y))
                return false;
            RouteScratch.Clear();
            if (XianXia.Core.Combat.FlightService.Route(world, world.Strategic.PlayerPartyContext.ActiveCharacterId, navigation, motion.WorldPosition, destination, RouteScratch) != SurfaceGroundRouteStatus.Found)
                return false;
            for (var i = 0; i < RouteScratch.Count; i++)
                if (!HuangcunOpeningAccess.AllowsPosition(world, navigation.SurfaceId, RouteScratch[i].X, RouteScratch[i].Y)) return false;
            for (var i = 0; i < RouteScratch.Count; i++)
                if (!HuangcunOpeningAccess.AllowsPlayerMove(world, navigation.SurfaceId,
                    i == 0 ? motion.WorldPosition.X : RouteScratch[i - 1].X,
                    i == 0 ? motion.WorldPosition.Y : RouteScratch[i - 1].Y,
                    RouteScratch[i].X, RouteScratch[i].Y)) return false;
            motion.BeginSurfaceAutoTravel(navigation.SurfaceId, destination, targetSiteId,
                Math.Max(0.001f, arrivalRadius), RouteScratch);
            return true;
        }

        public static bool IsActiveSurfaceTravel(PlayerPartyWorldMotion motion) =>
            motion != null && motion.IsMoving &&
            motion.ExecutionMode == PlayerPartyTravelExecutionMode.SurfaceVisible &&
            motion.LocationKind == PlayerPartyLocationKind.AtWorldPosition &&
            !string.IsNullOrEmpty(motion.SurfaceId) &&
            motion.HasContinuousPhysicalDestination;

        public static Result TrySyncWorldPosition(
            SimulationWorld world, PlayerPartyRuntime party, WorldVec2 position)
        {
            var motion = world?.PlayerPartyTravel;
            if (!HuangcunOpeningAccess.AllowsPosition(world, motion?.SurfaceId, position.X, position.Y))
                return Result.Failure(ErrorCode.InvalidOperation, HuangcunOpeningAccess.BlockedMessage);
            if (motion != null && !HuangcunOpeningAccess.AllowsPlayerMove(world, motion.SurfaceId,
                motion.WorldPosition.X, motion.WorldPosition.Y, position.X, position.Y))
            {
                if (motion.IsMoving)
                    PlayerPartyTravelRuntimeService.CancelTravel(world, party);
                return Result.Failure(ErrorCode.InvalidOperation, HuangcunOpeningAccess.PatrolBlockedMessage);
            }
            if (motion == null || party == null || string.IsNullOrEmpty(motion.SurfaceId) ||
                motion.LocationKind != PlayerPartyLocationKind.AtWorldPosition ||
                !world.SurfaceGround.TryGet(motion.SurfaceId, out var navigation) || navigation == null)
                return Result.Failure(ErrorCode.InvalidOperation,
                    "PlayerParty has no active Continuous Surface authority.");
            if (!navigation.Contains(motion.WorldPosition.X, motion.WorldPosition.Y) ||
                !navigation.Contains(position.X, position.Y) ||
                (!XianXia.Core.Combat.FlightService.IsAirborne(world, party.ActiveCharacterId) && !navigation.IsSegmentWalkable(
                    motion.WorldPosition.X, motion.WorldPosition.Y, position.X, position.Y)))
                return Result.Failure(ErrorCode.InvalidOperation,
                    "Continuous Surface movement segment is blocked.");

            motion.UpdateSurfaceWorldPosition(navigation.SurfaceId, position);
            motion.SetCurrentOutdoorWorldSiteContext(
                WorldSitePhysicalRegionQuery.TryResolve(world, position, out var site)
                    ? site.SiteId
                    : string.Empty);
            PlayerPartyTransitionMembership.ReconcilePlayerPartyMemberWorldPresenceFromMotion(
                world, party);
            return Result.Success();
        }

        /// <summary>One-way compatibility repair before accepting a normal Surface command.</summary>
        public static Result TryCanonicalizeNormalContinuousAuthority(
            SimulationWorld world, PlayerPartyWorldMotion motion)
        {
            if (world == null || motion == null ||
                !ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world) ||
                !motion.HasPosition)
                return Result.Failure(ErrorCode.InvalidOperation,
                    "PlayerParty has no normal Continuous Surface position.");

            var position = motion.WorldPosition;
            if (motion.LocationKind == PlayerPartyLocationKind.AtWorldPosition)
            {
                SurfaceGroundNavigation surface;
                if (!string.IsNullOrEmpty(motion.SurfaceId))
                {
                    if (!world.SurfaceGround.TryGet(motion.SurfaceId, out surface) || surface == null ||
                        !surface.Contains(position.X, position.Y))
                        return Result.Failure(ErrorCode.InvalidOperation,
                            "PlayerParty SurfaceId conflicts with its exact position.");
                }
                else
                {
                    if (!TryResolveUniqueContainingSurface(world, position, out surface))
                        return Result.Failure(ErrorCode.InvalidOperation,
                            "PlayerParty exact position does not identify one registered Surface.");
                    motion.SetAtSurfacePosition(surface.SurfaceId, position);
                }
                return Result.Success();
            }

            if (motion.LocationKind != PlayerPartyLocationKind.AtWorldSite ||
                string.IsNullOrEmpty(motion.SiteId) ||
                !world.Strategic.Sites.TryGet(motion.SiteId, out var site) || site == null ||
                !WorldSiteOutdoorMigrationPolicy.UsesContinuousOutdoorSurface(site))
                return Result.Failure(ErrorCode.InvalidOperation,
                    "PlayerParty location is not a migratable Continuous Site state.");

            var siteId = motion.SiteId;
            SurfaceGroundNavigation navigation = null;
            if (!string.IsNullOrEmpty(motion.SurfaceId))
            {
                if (!world.SurfaceGround.TryGet(motion.SurfaceId, out navigation) || navigation == null ||
                    !navigation.Contains(position.X, position.Y))
                    return Result.Failure(ErrorCode.InvalidOperation,
                        "PlayerParty Site position conflicts with its explicit SurfaceId.");
            }
            else if (!TryResolveUniqueContainingSurface(world, position, out navigation))
            {
                if (!world.SurfaceGround.TryResolveSiteArrival(siteId, out var arrivalSurfaceId, out position) ||
                    !world.SurfaceGround.TryGet(arrivalSurfaceId, out navigation) || navigation == null ||
                    !navigation.Contains(position.X, position.Y))
                    return Result.Failure(ErrorCode.InvalidOperation,
                        "Continuous Site has no trustworthy exact position or SiteArrival.");
            }

            motion.SetAtSurfacePosition(navigation.SurfaceId, position);
            motion.SetCurrentOutdoorWorldSiteContext(siteId);
            return Result.Success();
        }

        static bool TryResolveUniqueContainingSurface(
            SimulationWorld world, WorldVec2 position, out SurfaceGroundNavigation navigation)
        {
            navigation = null;
            var count = 0;
            foreach (var pair in world.SurfaceGround.Registered)
            {
                var candidate = pair.Value;
                if (candidate == null || !candidate.Contains(position.X, position.Y)) continue;
                navigation = candidate;
                count++;
                if (count > 1)
                {
                    navigation = null;
                    return false;
                }
            }
            return count == 1;
        }

        public static void Cancel(SimulationWorld world) =>
            PlayerPartyTravelRuntimeService.CancelTravel(world);
    }

    /// <summary>Shared continuous travel lifecycle used by Host and combat commands.</summary>
    public static class PlayerPartyTravelRuntimeService
    {
        public static float WorldUnitsPerTick(float worldScale)
        {
            var scale = worldScale > 0.0001f ? worldScale : 1f;
            return scale * (float)Math.Sqrt(3.0) / 8f;
        }

        public static Result CancelTravel(SimulationWorld world, PlayerPartyRuntime party = null)
        {
            var motion = world?.PlayerPartyTravel;
            if (motion == null)
                return Result.Failure(ErrorCode.InvalidArgument, "No party travel state.");
            if (!motion.IsMoving)
                return Result.Failure(ErrorCode.InvalidOperation, "PlayerParty is not traveling.");
            motion.CancelAutoTravelPreservePosition();
            SyncTravelingMembers(world);
            return Result.Success();
        }

        public static Result CompleteSurfaceArrival(SimulationWorld world)
        {
            var motion = world?.PlayerPartyTravel;
            if (motion == null || !motion.IsMoving ||
                motion.ExecutionMode != PlayerPartyTravelExecutionMode.SurfaceVisible ||
                motion.LocationKind != PlayerPartyLocationKind.AtWorldPosition ||
                string.IsNullOrEmpty(motion.SurfaceId) ||
                !motion.HasContinuousPhysicalDestination)
                return Result.Failure(ErrorCode.InvalidOperation,
                    "Continuous Surface final arrival state is invalid.");
            if (!string.IsNullOrEmpty(motion.DestinationSiteId))
                motion.SetCurrentOutdoorWorldSiteContext(motion.DestinationSiteId);
            else if (WorldSitePhysicalRegionQuery.TryResolve(world, motion.WorldPosition, out var site))
                motion.SetCurrentOutdoorWorldSiteContext(site.SiteId);
            motion.CancelAutoTravelPreservePosition();
            SyncTravelingMembers(world);
            return Result.Success();
        }

        static void SyncTravelingMembers(SimulationWorld world)
        {
            var members = world.PlayerPartyTravel.TravelingMembers;
            for (var i = 0; i < members.Count; i++)
                PlayerPartyTransitionMembership.SyncMemberPresenceFromMotion(world, members[i]);
        }
    }
}
