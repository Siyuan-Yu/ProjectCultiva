using System;
using System.Collections.Generic;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
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
            motion.SetAtWorldPosition(motion.WorldPosition, motion.CurrentHex);
            PlayerPartyTransitionMembership.CaptureTravelingMembersForPartyTransition(world, party);
            motion.SetCurrentOutdoorWorldSiteContext(string.Empty);
            // Null HexPath is intentional: the continuous route is the normal movement plan.
            motion.BeginContinuousAutoTravel(null, default, targetSiteId, HexTravelMode.Ground,
                destination, Math.Max(0.001f, arrivalRadius), RouteScratch);
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
            motion.BeginContinuousAutoTravel(null, default, targetSiteId, HexTravelMode.Ground,
                destination, Math.Max(0.001f, arrivalRadius), RouteScratch);
            return true;
        }

        public static void Cancel(SimulationWorld world) =>
            world?.PlayerPartyTravel?.CancelAutoTravelPreservePosition();
    }
}
