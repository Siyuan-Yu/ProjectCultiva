using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Core.World.Surface;

namespace XianXia.Tests
{
    public sealed class PlayerPartyWorldMotionCurrentContractTests
    {
        const string SurfaceId = "test:surface";
        static readonly WorldVec2 Start = new WorldVec2(1.5f, 1.5f);
        static readonly WorldVec2 Midpoint = new WorldVec2(4.5f, 1.5f);
        static readonly WorldVec2 Destination = new WorldVec2(8.5f, 1.5f);

        [Test]
        public void StartReissueAndFrameUpdateInvalidateOnlyLogicalPlans()
        {
            var motion = new PlayerPartyWorldMotion();
            motion.SetAtSurfacePosition(SurfaceId, Start);
            Assert.AreEqual(1, motion.TravelPlanVersion);

            motion.BeginSurfaceAutoTravel(
                SurfaceId, Destination, "site:first", 0.1f, Route(Start, Destination));
            Assert.AreEqual(2, motion.TravelPlanVersion);

            motion.UpdateSurfaceWorldPosition(SurfaceId, Midpoint);
            motion.AdvanceContinuousSurfaceWaypoint();
            Assert.AreEqual(2, motion.TravelPlanVersion,
                "Per-frame position and route progress must not invalidate the active plan.");

            motion.BeginSurfaceAutoTravel(
                SurfaceId, Start, "site:reissued", 0.2f, Route(Midpoint, Start));
            Assert.AreEqual(3, motion.TravelPlanVersion,
                "A reissued order is one logical invalidation, not begin plus internal-clear.");
            Assert.AreEqual("site:reissued", motion.DestinationSiteId);
        }

        [Test]
        public void CancelPreservesExactPositionAndCurrentSiteContext()
        {
            var motion = new PlayerPartyWorldMotion();
            motion.SetAtSurfacePosition(SurfaceId, Start);
            motion.BeginSurfaceAutoTravel(
                SurfaceId, Destination, "site:destination", 0.1f, Route(Start, Destination));
            motion.UpdateSurfaceWorldPosition(SurfaceId, Midpoint);
            motion.SetCurrentOutdoorWorldSiteContext("site:current-region");
            var beforeCancel = motion.TravelPlanVersion;

            motion.CancelAutoTravelPreservePosition();

            Assert.AreEqual(beforeCancel + 1, motion.TravelPlanVersion);
            Assert.AreEqual(Midpoint, motion.WorldPosition);
            Assert.AreEqual("site:current-region", motion.CurrentOutdoorWorldSiteId);
            Assert.AreEqual(PlayerPartyLocationKind.AtWorldPosition, motion.LocationKind);
            Assert.AreEqual(SurfaceId, motion.SurfaceId);
            Assert.IsTrue(motion.HasPosition);
            Assert.IsFalse(motion.IsMoving);
            Assert.IsFalse(motion.HasContinuousPhysicalDestination);
        }

        [Test]
        public void DirectPlacementTakesOverOnceAndClearsStaleSiteContext()
        {
            var motion = new PlayerPartyWorldMotion();
            motion.SetAtSurfacePosition(SurfaceId, Start);
            motion.SetCurrentOutdoorWorldSiteContext("site:stale");
            motion.BeginSurfaceAutoTravel(
                SurfaceId, Destination, "site:destination", 0.1f, Route(Start, Destination));
            var beforeTakeover = motion.TravelPlanVersion;

            motion.SetAtSurfacePosition(SurfaceId, Midpoint);

            Assert.AreEqual(beforeTakeover + 1, motion.TravelPlanVersion);
            Assert.AreEqual(Midpoint, motion.WorldPosition);
            Assert.IsEmpty(motion.CurrentOutdoorWorldSiteId);
            Assert.IsFalse(motion.IsMoving);
            Assert.IsEmpty(motion.DestinationSiteId);
        }

        [Test]
        public void ServiceBeginCancelAndArrivalEachInvalidateExactlyOnce()
        {
            var world = CreateWorld();
            var party = CreateParty(world);
            world.PlayerPartyTravel.SetAtSurfacePosition(SurfaceId, Start);
            var placedVersion = world.PlayerPartyTravel.TravelPlanVersion;

            var begun = PlayerPartySurfaceTravelService.BeginTravel(
                world, party, Destination, "site:destination", 0.1f);
            Assert.IsTrue(begun.IsSuccess, begun.IsFailure ? begun.Error.ToString() : string.Empty);
            Assert.AreEqual(placedVersion + 1, world.PlayerPartyTravel.TravelPlanVersion);

            world.PlayerPartyTravel.UpdateSurfaceWorldPosition(SurfaceId, Midpoint);
            world.PlayerPartyTravel.SetCurrentOutdoorWorldSiteContext("site:current-region");
            var cancelVersion = world.PlayerPartyTravel.TravelPlanVersion;
            var cancelled = PlayerPartyTravelRuntimeService.CancelTravel(world, party);
            Assert.IsTrue(cancelled.IsSuccess);
            Assert.AreEqual(cancelVersion + 1, world.PlayerPartyTravel.TravelPlanVersion);
            Assert.AreEqual(Midpoint, world.PlayerPartyTravel.WorldPosition);
            Assert.AreEqual("site:current-region", world.PlayerPartyTravel.CurrentOutdoorWorldSiteId);

            begun = PlayerPartySurfaceTravelService.BeginTravel(
                world, party, Destination, "site:destination", 0.1f);
            Assert.IsTrue(begun.IsSuccess, begun.IsFailure ? begun.Error.ToString() : string.Empty);
            world.PlayerPartyTravel.UpdateSurfaceWorldPosition(SurfaceId, Destination);
            var arrivalVersion = world.PlayerPartyTravel.TravelPlanVersion;
            var arrived = PlayerPartyTravelRuntimeService.CompleteSurfaceArrival(world);

            Assert.IsTrue(arrived.IsSuccess);
            Assert.AreEqual(arrivalVersion + 1, world.PlayerPartyTravel.TravelPlanVersion);
            Assert.AreEqual("site:destination", world.PlayerPartyTravel.CurrentOutdoorWorldSiteId,
                "Arrival must retain the destination Site before ending the plan.");
            Assert.AreEqual(Destination, world.PlayerPartyTravel.WorldPosition);
            Assert.IsFalse(world.PlayerPartyTravel.IsMoving);
        }

        [Test]
        public void ResumeInvalidatesTheRestoredPlanOnce()
        {
            var world = CreateWorld();
            var motion = world.PlayerPartyTravel;
            motion.SetAtSurfacePosition(SurfaceId, Start);
            var placedVersion = motion.TravelPlanVersion;

            Assert.IsTrue(PlayerPartySurfaceTravelService.TryResumeAfterRestore(
                world, Destination, "site:destination", 0.1f));
            Assert.AreEqual(placedVersion + 1, motion.TravelPlanVersion,
                "Route restoration is one logical start.");
        }

        static IReadOnlyList<WorldVec2> Route(WorldVec2 start, WorldVec2 end) =>
            new[] { start, end };

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            var cells = new List<SurfaceGroundCellKind>(12 * 4);
            for (var i = 0; i < 12 * 4; i++)
                cells.Add(SurfaceGroundCellKind.Ground);
            world.SurfaceGround.Register(new SurfaceGroundNavigation(
                SurfaceId, "test", "test", 0f, 0f, 1f, 12, 4, cells));
            return world;
        }

        static PlayerPartyRuntime CreateParty(SimulationWorld world)
        {
            var created = world.Entities.CreateCharacter(
                new DefinitionId("test", "active"), "Active");
            Assert.IsTrue(created.IsSuccess, created.IsFailure ? created.Error.ToString() : string.Empty);
            var party = new PlayerPartyRuntime();
            party.BindWorld(world);
            Assert.IsTrue(party.TryInitialize(created.Value.Id, out var error), error);
            return party;
        }
    }
}
