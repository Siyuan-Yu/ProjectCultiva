using XianXia.Core.World;
using System;
using System.Collections.Generic;
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
            float arrivalRadius)
        {
            if (world == null || party == null || !party.HasActive)
                return Result.Failure(ErrorCode.InvalidArgument, "Invalid Surface travel args.");
            var motion = world.PlayerPartyTravel;
            if (motion == null || !motion.HasPosition)
                return Result.Failure(ErrorCode.InvalidOperation, "Surface travel requires a canonical WorldPosition.");
            if (!ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world) ||
                !world.SurfaceGround.TryResolveShared(motion.WorldPosition, destination, out var navigation))
                return Result.Failure(ErrorCode.InvalidOperation, "Continuous Surface navigation is not ready for this route.");

            RouteScratch.Clear();
            var status = navigation.TryFindRoute(motion.WorldPosition, destination, RouteScratch);
            if (status == SurfaceGroundRouteStatus.BlockedGoal)
                return Result.Failure(ErrorCode.InvalidArgument, "不可步行到达的地面目标。");
            if (status != SurfaceGroundRouteStatus.Found)
                return Result.Failure(ErrorCode.InvalidOperation, "Surface route unavailable: " + status);

            // A Site departure keeps the exact authored position, but its normal travel state is outdoors.
            motion.SetAtSurfacePosition(motion.WorldPosition);
            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);
            motion.SetCurrentOutdoorWorldSiteContext(string.Empty);
            motion.BeginSurfaceAutoTravel(destination, targetSiteId,
                Math.Max(0.001f, arrivalRadius), RouteScratch);
            PlayerPartyTransitionMembership.ReconcilePlayerPartyMemberWorldPresenceFromMotion(
                world, party, "PlayerPartySurfaceTravelService.BeginTravel");
            return Result.Success();
        }

        public static bool TryResumeAfterRestore(SimulationWorld world,
            WorldVec2 destination, string targetSiteId, float arrivalRadius)
        {
            var motion = world?.PlayerPartyTravel;
            if (motion == null || !motion.HasPosition || !ContinuousOutdoorGameplayPolicy.IsNormalContinuousOutdoor(world) ||
                !world.SurfaceGround.TryResolveShared(motion.WorldPosition, destination, out var navigation))
                return false;
            RouteScratch.Clear();
            if (navigation.TryFindRoute(motion.WorldPosition, destination, RouteScratch) != SurfaceGroundRouteStatus.Found)
                return false;
            motion.BeginSurfaceAutoTravel(destination, targetSiteId,
                Math.Max(0.001f, arrivalRadius), RouteScratch);
            return true;
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

        public static void HoldForLocalVisibleExecution(SimulationWorld world)
        {
            var motion = world?.PlayerPartyTravel;
            if (motion == null) return;
            motion.SetExecutionMode(motion.IsMoving
                ? PlayerPartyTravelExecutionMode.LocalVisible
                : PlayerPartyTravelExecutionMode.None);
        }

        public static Result CompleteSurfaceArrival(SimulationWorld world)
        {
            var motion = world?.PlayerPartyTravel;
            if (motion == null || !motion.IsMoving ||
                motion.ExecutionMode != PlayerPartyTravelExecutionMode.LocalVisible ||
                motion.LocationKind != PlayerPartyLocationKind.AtWorldPosition ||
                !motion.HasContinuousPhysicalDestination)
                return Result.Failure(ErrorCode.InvalidOperation,
                    "Continuous Surface final arrival state is invalid.");
            if (WorldSitePhysicalRegionQuery.TryResolve(world, motion.WorldPosition, out var site))
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
