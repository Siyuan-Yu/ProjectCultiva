using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Core.World.Surface;

namespace XianXia.Tests
{
    /// <summary>Current Continuous Surface travel contracts for personal and PlayerParty movement.</summary>
    public sealed class CurrentSurfaceTravelContractTests
    {
        const string SurfaceId = "test:surface";
        const string DestinationSiteId = "test:destination";
        static readonly WorldVec2 Start = new WorldVec2(1.5f, 1.5f);
        static readonly WorldVec2 Destination = new WorldVec2(12.5f, 1.5f);

        [Test]
        public void BackgroundTravelStartsFromExactSurfaceAndConsumesPartialAndMultipleSegments()
        {
            var world = CreateWorld();
            var character = CreateCharacter(world, "traveler");
            world.WorldPresence.SetAtWorldPosition(character, Start, SurfaceId);

            var begun = BackgroundCharacterTravelService.BeginTravelToWorldSite(
                world, character, DestinationSiteId);
            Assert.IsTrue(begun.IsSuccess, begun.IsFailure ? begun.Error.ToString() : string.Empty);
            Assert.IsTrue(world.BackgroundCharacterTravel.TryGet(character, out var motion));
            Assert.AreEqual(SurfaceId, motion.SurfaceId);

            BackgroundCharacterTravelService.AdvanceAll(world, 1);
            Assert.IsTrue(world.WorldPresence.TryGet(character, out var partial));
            Assert.Greater(WorldVec2.Distance(Start, partial.ContinuousWorldPosition), 0f);
            Assert.Less(WorldVec2.Distance(Start, partial.ContinuousWorldPosition),
                BackgroundSimulationScheduler.DistanceBudgetFromElapsedSimulationTicks(1f, 2));
            Assert.IsTrue(motion.IsMoving);

            Assert.Greater(motion.SurfacePath.Count, 2,
                "The obstacle must produce a multi-segment route.");
            BackgroundCharacterTravelService.AdvanceAll(world, 200);
            Assert.IsFalse(world.BackgroundCharacterTravel.IsTraveling(character),
                "A large WorldTick budget must consume all remaining route segments.");
            Assert.IsTrue(world.WorldPresence.TryGet(character, out var arrived));
            Assert.AreEqual(PartyWorldPresenceMode.AtSite, arrived.Mode);
            Assert.AreEqual(DestinationSiteId, arrived.SiteId);
            Assert.AreEqual(SurfaceId, arrived.PersonalSurfaceId);
            Assert.AreEqual(Destination, arrived.ContinuousWorldPosition);
        }

        [Test]
        public void BackgroundTravelPauseSpeedAndCancelPreserveWorldTickOrderingAndExactPosition()
        {
            var paused = CreateTravelingWorld("paused", out var pausedId);
            Assert.IsTrue(paused.WorldPresence.TryGet(pausedId, out var beforePause));
            var frozen = beforePause.ContinuousWorldPosition;

            Assert.IsTrue(paused.BackgroundCharacterTravel.IsTraveling(pausedId));
            Assert.AreEqual(frozen, paused.WorldPresence.GetOrCreate(pausedId).ContinuousWorldPosition,
                "Without a WorldTick budget, pause must not move the character.");

            var slow = CreateTravelingWorld("slow", out var slowId);
            var fast = CreateTravelingWorld("fast", out var fastId);
            BackgroundCharacterTravelService.AdvanceAll(slow, 2);
            BackgroundCharacterTravelService.AdvanceAll(fast, 8);
            var slowDistance = DistanceFromStart(slow, slowId);
            var fastDistance = DistanceFromStart(fast, fastId);
            Assert.Greater(fastDistance, slowDistance,
                "More elapsed WorldTicks must consume a larger movement budget.");

            Assert.IsTrue(BackgroundCharacterTravelService.CancelTravel(fast, fastId).IsSuccess);
            Assert.IsTrue(fast.WorldPresence.TryGet(fastId, out var cancelled));
            var cancelledPosition = cancelled.ContinuousWorldPosition;
            Assert.IsFalse(fast.BackgroundCharacterTravel.IsTraveling(fastId));
            Assert.AreEqual(cancelledPosition, fast.WorldPresence.GetOrCreate(fastId).ContinuousWorldPosition);
        }

        [Test]
        public void BackgroundTravelRejectsPlayerPartyOwnership()
        {
            var world = CreateWorld();
            var character = CreateCharacter(world, "player");
            world.WorldPresence.SetAtWorldPosition(character, Start, SurfaceId);
            var party = CreateParty(world, character);

            var result = BackgroundCharacterTravelService.BeginTravelToWorldSite(
                world, character, DestinationSiteId, party);

            Assert.IsTrue(result.IsFailure);
            Assert.IsFalse(world.BackgroundCharacterTravel.IsTraveling(character));
        }

        [Test]
        public void BackgroundTravelRejectsNonLivingMovementStates()
        {
            var states = new[]
            {
                LifecycleState.Incapacitated,
                LifecycleState.Captured,
                LifecycleState.Dead
            };
            foreach (var state in states)
            {
                var world = CreateWorld();
                var character = CreateCharacter(world, "life_" + state);
                world.WorldPresence.SetAtWorldPosition(character, Start, SurfaceId);
                Assert.IsTrue(world.Entities.TryGet(character, out var entity));
                entity.Get<LifecycleComponent>().State = state;

                var result = BackgroundCharacterTravelService.BeginTravelToWorldSite(
                    world, character, DestinationSiteId);

                Assert.IsTrue(result.IsFailure, state.ToString());
                Assert.IsFalse(world.BackgroundCharacterTravel.IsTraveling(character), state.ToString());
            }
        }

        [Test]
        public void BackgroundCurrentSnapshotRestoresMovingAndIdleExactSurfaceStates()
        {
            var world = CreateWorld();
            var moving = CreateCharacter(world, "moving");
            var idle = CreateCharacter(world, "idle");
            var records = new List<BackgroundCharacterTravelSnapshotDto>
            {
                new BackgroundCharacterTravelSnapshotDto
                {
                    CharacterId = moving.Value,
                    LocationKind = (int)BackgroundCharacterLocationKind.AtWorldPosition,
                    SurfaceId = SurfaceId,
                    WorldX = Start.X,
                    WorldY = Start.Y,
                    IsTraveling = true,
                    IsSurfaceRoute = true,
                    DestinationSiteId = DestinationSiteId,
                    SurfaceDestinationX = Destination.X,
                    SurfaceDestinationY = Destination.Y,
                    LastProcessedWorldTick = 7
                },
                new BackgroundCharacterTravelSnapshotDto
                {
                    CharacterId = idle.Value,
                    LocationKind = (int)BackgroundCharacterLocationKind.AtWorldPosition,
                    SurfaceId = SurfaceId,
                    WorldX = 2.25f,
                    WorldY = 3.75f,
                    IsTraveling = false,
                    IsSurfaceRoute = true
                }
            };

            var restored = StrategicSnapshotHelper.RestoreBackgroundSurfaceTravels(world, records);

            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : string.Empty);
            Assert.IsTrue(world.BackgroundCharacterTravel.TryGet(moving, out var movingMotion));
            Assert.IsTrue(movingMotion.IsMoving);
            Assert.AreEqual(SurfaceId, movingMotion.SurfaceId);
            Assert.AreEqual(7UL, movingMotion.LastProcessedWorldTick);
            Assert.IsFalse(world.BackgroundCharacterTravel.IsTraveling(idle));
            Assert.IsTrue(world.WorldPresence.TryGet(idle, out var idlePresence));
            Assert.AreEqual(SurfaceId, idlePresence.PersonalSurfaceId);
            Assert.AreEqual(2.25f, idlePresence.WorldPosX, 0.0001f);
            Assert.AreEqual(3.75f, idlePresence.WorldPosY, 0.0001f);
        }

        [Test]
        public void PlayerPartyBeginAndCancelKeepExactSurfaceAuthorityOverStaleFocus()
        {
            var world = CreateWorld();
            var active = CreateCharacter(world, "active");
            var party = CreateParty(world, active);
            world.PlayerPartyTravel.SetAtSurfacePosition(SurfaceId, Start);
            world.WorldPresence.SetAtWorldPosition(active, Start, SurfaceId);
            world.PartyWorld.Mode = PartyWorldPresenceMode.AtSite;
            world.PartyWorld.SiteId = "stale:site";
            world.PartyWorld.LocalMapId = "stale:map";

            var begun = PlayerPartySurfaceTravelService.BeginTravel(
                world, party, Destination, DestinationSiteId, 0.1f);
            Assert.IsTrue(begun.IsSuccess, begun.IsFailure ? begun.Error.ToString() : string.Empty);
            Assert.IsTrue(PlayerPartySurfaceTravelService.IsActiveSurfaceTravel(world.PlayerPartyTravel));
            Assert.Greater(world.PlayerPartyTravel.ContinuousSurfaceRoute.Count, 1);
            Assert.AreEqual(Start, world.PlayerPartyTravel.WorldPosition);

            var next = world.PlayerPartyTravel.ContinuousSurfaceRoute[1];
            Assert.IsTrue(PlayerPartySurfaceTravelService.TrySyncWorldPosition(
                world, party, next).IsSuccess);
            Assert.IsTrue(PlayerPartyTravelRuntimeService.CancelTravel(world, party).IsSuccess);
            Assert.AreEqual(next, world.PlayerPartyTravel.WorldPosition);
            Assert.IsFalse(world.PlayerPartyTravel.IsMoving);

            Assert.IsTrue(SnapshotActiveControlledLocalMapResolver.TryResolveRequiredLocalMap(
                world, party, out var resolved));
            Assert.AreEqual("PlayerPartyTravel", resolved.Source);
            SnapshotActiveControlledLocalMapResolver.ApplyResolvedPartyWorldFocus(world, in resolved);
            Assert.AreEqual(PartyWorldPresenceMode.AtWorldPosition, world.PartyWorld.Mode);
            Assert.IsEmpty(world.PartyWorld.LocalMapId);
            Assert.AreNotEqual("stale:site", world.PartyWorld.SiteId);
            Assert.AreEqual(next, world.PlayerPartyTravel.WorldPosition);
        }

        [Test]
        public void PlayerPartyCurrentSnapshotRebuildsMovingRouteAndLeavesIdleStopped()
        {
            var movingWorld = CreateWorld();
            var movingDto = new PlayerPartyTravelSnapshotDto
            {
                HasPosition = true,
                LocationKind = (int)PlayerPartyLocationKind.AtWorldPosition,
                SurfaceId = SurfaceId,
                WorldX = Start.X,
                WorldY = Start.Y,
                IsMoving = true,
                HasContinuousPhysicalDestination = true,
                DestinationWorldX = Destination.X,
                DestinationWorldY = Destination.Y,
                ArrivalRadius = 0.1f,
                DestinationSiteId = DestinationSiteId,
                ExecutionMode = (int)PlayerPartyTravelExecutionMode.SurfaceVisible
            };
            StrategicSnapshotHelper.RestorePlayerPartyTravel(movingWorld, movingDto);
            var movingResult = StrategicSnapshotHelper.FinalizePlayerPartyTravelAfterContentShell(
                movingWorld, movingDto);
            Assert.IsTrue(movingResult.IsSuccess,
                movingResult.IsFailure ? movingResult.Error.ToString() : string.Empty);
            Assert.IsTrue(PlayerPartySurfaceTravelService.IsActiveSurfaceTravel(
                movingWorld.PlayerPartyTravel));
            Assert.Greater(movingWorld.PlayerPartyTravel.ContinuousSurfaceRoute.Count, 1);

            var idleWorld = CreateWorld();
            var idleDto = new PlayerPartyTravelSnapshotDto
            {
                HasPosition = true,
                LocationKind = (int)PlayerPartyLocationKind.AtWorldPosition,
                SurfaceId = SurfaceId,
                WorldX = 3.25f,
                WorldY = 2.75f,
                IsMoving = false,
                ExecutionMode = (int)PlayerPartyTravelExecutionMode.None
            };
            StrategicSnapshotHelper.RestorePlayerPartyTravel(idleWorld, idleDto);
            var idleResult = StrategicSnapshotHelper.FinalizePlayerPartyTravelAfterContentShell(
                idleWorld, idleDto);
            Assert.IsTrue(idleResult.IsSuccess,
                idleResult.IsFailure ? idleResult.Error.ToString() : string.Empty);
            Assert.IsFalse(idleWorld.PlayerPartyTravel.IsMoving);
            Assert.AreEqual(PlayerPartyTravelExecutionMode.None,
                idleWorld.PlayerPartyTravel.ExecutionMode);
            Assert.AreEqual(new WorldVec2(3.25f, 2.75f),
                idleWorld.PlayerPartyTravel.WorldPosition);
        }

        static SimulationWorld CreateTravelingWorld(string name, out EntityId character)
        {
            var world = CreateWorld();
            character = CreateCharacter(world, name);
            world.WorldPresence.SetAtWorldPosition(character, Start, SurfaceId);
            var begun = BackgroundCharacterTravelService.BeginTravelToWorldSite(
                world, character, DestinationSiteId);
            Assert.IsTrue(begun.IsSuccess, begun.IsFailure ? begun.Error.ToString() : string.Empty);
            return world;
        }

        static float DistanceFromStart(SimulationWorld world, EntityId character)
        {
            Assert.IsTrue(world.WorldPresence.TryGet(character, out var presence));
            return WorldVec2.Distance(Start, presence.ContinuousWorldPosition);
        }

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld { ContinuousWorldMovementScale = 1f };
            const int width = 16;
            const int height = 6;
            var cells = new List<SurfaceGroundCellKind>(width * height);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var obstacle = x == 5 && y < height - 1;
                cells.Add(obstacle ? SurfaceGroundCellKind.Solid : SurfaceGroundCellKind.Ground);
            }
            world.SurfaceGround.Register(new SurfaceGroundNavigation(
                SurfaceId, "test", "test", 0f, 0f, 1f, width, height, cells));
            world.SurfaceGround.RegisterSiteArrival(SurfaceId, DestinationSiteId, Destination);
            world.Strategic.Sites.Register(new WorldSite
            {
                SiteId = DestinationSiteId,
                DisplayName = "Destination",
                UsesContinuousOutdoorSurface = true
            });
            return world;
        }

        static EntityId CreateCharacter(SimulationWorld world, string name)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess, created.IsFailure ? created.Error.ToString() : string.Empty);
            return created.Value.Id;
        }

        static PlayerPartyRuntime CreateParty(SimulationWorld world, EntityId active)
        {
            var party = new PlayerPartyRuntime();
            party.BindWorld(world);
            Assert.IsTrue(party.TryInitialize(active, out var error), error);
            return party;
        }
    }
}
