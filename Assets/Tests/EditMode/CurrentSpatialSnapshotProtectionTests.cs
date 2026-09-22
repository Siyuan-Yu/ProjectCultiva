using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Surface;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    /// <summary>Current exact WorldPresence and Separate Space snapshot ownership protections.</summary>
    public sealed class CurrentSpatialSnapshotProtectionTests
    {
        const string SurfaceId = "test:surface";
        const string InteriorMapId = "test:interior";

        [Test]
        public void ExactWorldPresenceKeepsSurfaceAndZeroPositionWithoutSecondaryCache()
        {
            var world = new SimulationWorld();
            var character = CreateCharacter(world, "origin");
            world.WorldPresence.SetAtWorldPosition(
                character, new WorldVec2(0f, 0f), SurfaceId);

            Assert.IsTrue(CharacterWorldPresenceQuery.TryResolve(
                world, character, out var resolved));
            Assert.AreEqual(
                CharacterWorldPresenceQuery.PresenceState.AtWorldPosition,
                resolved.State);
            Assert.AreEqual(SurfaceId, resolved.SurfaceId);
            Assert.IsTrue(resolved.HasWorldPosition);
            Assert.AreEqual(new WorldVec2(0f, 0f), resolved.WorldPosition);

            world.WorldPresence.SetAtSiteWithAnchor(
                character, "test:site", new WorldVec2(4.25f, 6.75f), SurfaceId);
            Assert.IsTrue(CharacterWorldPresenceQuery.TryResolve(
                world, character, out var atSite));
            Assert.AreEqual(CharacterWorldPresenceQuery.PresenceState.AtWorldSite, atSite.State);
            Assert.AreEqual("test:site", atSite.SiteId);
            Assert.AreEqual(SurfaceId, atSite.SurfaceId);
            Assert.AreEqual(new WorldVec2(4.25f, 6.75f), atSite.WorldPosition);
        }

        [Test]
        public void SeparateSpaceSnapshotOverridesStaleOutdoorPresenceAndAuthorizesOnlyItsMap()
        {
            var source = new SimulationWorld();
            var sourceCharacter = CreateCharacter(source, "active");
            var sourceParty = CreateParty(source, sourceCharacter);
            source.LocalMap.EstablishSeparateSpace(
                InteriorMapId, "test:places", SeparateSpaceKind.Interior,
                "test:entry", "test:return", "snapshot");
            source.LocalMap.HasOutdoorReturn = true;
            source.LocalMap.ReturnSurfaceId = SurfaceId;
            source.LocalMap.ReturnWorldX = 7.25f;
            source.LocalMap.ReturnWorldY = 8.5f;
            source.LocalMap.AddOccupant(sourceCharacter);

            var dto = new StrategicSnapshotDto();
            SeparateSpaceSessionSnapshotRestore.Capture(source, dto, sourceParty);

            var restored = new SimulationWorld();
            var restoredCharacter = CreateCharacter(restored, "active");
            Assert.AreEqual(sourceCharacter, restoredCharacter);
            var restoredParty = CreateParty(restored, restoredCharacter);
            restored.WorldPresence.SetAtWorldPosition(
                restoredCharacter, new WorldVec2(99f, 99f), "stale:surface");

            var result = SeparateSpaceSessionSnapshotRestore.Restore(restored, dto);

            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : string.Empty);
            Assert.IsTrue(restored.LocalMap.IsActive);
            Assert.AreEqual(InteriorMapId, restored.LocalMap.ActiveMapLayoutId);
            Assert.AreEqual(SurfaceId, restored.LocalMap.ReturnSurfaceId);
            Assert.AreEqual(7.25f, restored.LocalMap.ReturnWorldX, 0.0001f);
            Assert.AreEqual(8.5f, restored.LocalMap.ReturnWorldY, 0.0001f);
            Assert.IsFalse(restored.WorldPresence.TryGet(restoredCharacter, out _),
                "Interior ownership must remove stale Outdoor personal presence.");
            Assert.IsTrue(CharacterWorldPresenceQuery.TryResolve(
                restored, restoredCharacter, out var resolvedPresence));
            Assert.AreEqual(
                CharacterWorldPresenceQuery.PresenceState.InSeparateSpace,
                resolvedPresence.State);

            Assert.IsTrue(SnapshotActiveControlledLocalMapResolver.TryResolveRequiredLocalMap(
                restored, restoredParty, out var resolvedMap));
            Assert.AreEqual(InteriorMapId, resolvedMap.LocalMapId);
            Assert.AreEqual("SeparateSpaceSession", resolvedMap.Source);
        }

        [Test]
        public void SeparateSpaceSnapshotRejectsIncompleteExactReturnBeforeActivation()
        {
            var world = new SimulationWorld();
            var dto = new StrategicSnapshotDto
            {
                SeparateSpace = new SeparateSpaceSessionSnapshotDto
                {
                    IsInSeparateSpace = true,
                    SpaceKind = (int)SeparateSpaceKind.Cave,
                    ActiveMapLayoutId = InteriorMapId,
                    HasOutdoorReturn = true,
                    ReturnSurfaceId = string.Empty,
                    ReturnWorldX = 1f,
                    ReturnWorldY = 2f
                }
            };

            var result = SeparateSpaceSessionSnapshotRestore.Restore(world, dto);

            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual(XianXia.Core.Results.ErrorCode.SnapshotInvalid, result.Error.Code);
            Assert.IsFalse(world.LocalMap.IsActive);
        }

        [Test]
        public void NonPhysicalSitePresenceRoundTripsWithoutFabricatedAnchor()
        {
            var world = new SimulationWorld();
            var character = CreateCharacter(world, "resident");
            world.Strategic.Sites.Register(new WorldSite
            {
                SiteId = "test:non_physical_site",
                UsesContinuousOutdoorSurface = false
            });
            var squad = SquadMembershipService.Create(
                world, "test:squad_resident", new[] { character }, character,
                SquadCommandKind.None);
            Assert.IsTrue(squad.IsSuccess, squad.IsFailure ? squad.Error.ToString() : string.Empty);

            world.WorldPresence.SetAtSite(character, "test:non_physical_site");
            var dto = StrategicSnapshotHelper.Capture(world, null);
            world.WorldPresence.Clear();

            var restored = StrategicSnapshotHelper.Restore(world, dto);

            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : string.Empty);
            Assert.IsTrue(world.WorldPresence.TryGet(character, out var presence));
            Assert.AreEqual(PartyWorldPresenceMode.AtSite, presence.Mode);
            Assert.AreEqual("test:non_physical_site", presence.SiteId);
            Assert.IsFalse(presence.HasContinuousWorldPosition);
            Assert.IsEmpty(presence.PersonalSurfaceId);
            var complete = StrategicSnapshotHelper.ValidateRestoredCharacterWorldPresences(
                world, dto, requireContentSpatialCompleteness: true);
            Assert.IsTrue(complete.IsSuccess, complete.IsFailure ? complete.Error.ToString() : string.Empty);
        }

        [Test]
        public void ContinuousOutdoorSiteCannotSerializeWithoutExactSurfaceAnchor()
        {
            var world = new SimulationWorld();
            var character = CreateCharacter(world, "outdoor");
            world.Strategic.Sites.Register(new WorldSite
            {
                SiteId = "test:outdoor_site",
                UsesContinuousOutdoorSurface = true
            });
            var squad = SquadMembershipService.Create(
                world, "test:squad_outdoor", new[] { character }, character,
                SquadCommandKind.None);
            Assert.IsTrue(squad.IsSuccess, squad.IsFailure ? squad.Error.ToString() : string.Empty);
            world.WorldPresence.SetAtSite(character, "test:outdoor_site");

            var dto = StrategicSnapshotHelper.Capture(world, null);
            var result = StrategicSnapshotHelper.ValidateCaptureForSerialization(world, dto);

            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual(XianXia.Core.Results.ErrorCode.SnapshotInvalid, result.Error.Code);
            StringAssert.Contains("exact Surface", result.Error.Message);
        }

        [Test]
        public void IdleBackgroundCaptureUsesPrimaryPresenceSurfaceWhenMotionSurfaceIsEmpty()
        {
            var world = new SimulationWorld();
            world.SurfaceGround.Register(CreateSurface());
            var character = CreateCharacter(world, "idle_background");
            world.WorldPresence.SetAtWorldPosition(
                character, new WorldVec2(2.5f, 3.5f), SurfaceId);
            var idleMotion = world.BackgroundCharacterTravel.GetOrCreate(character);
            Assert.IsFalse(idleMotion.IsMoving);
            Assert.IsEmpty(idleMotion.SurfaceId);

            var dto = StrategicSnapshotHelper.Capture(world, null);

            Assert.AreEqual(1, dto.BackgroundCharacterTravels.Count);
            Assert.AreEqual(SurfaceId, dto.BackgroundCharacterTravels[0].SurfaceId);
            Assert.IsFalse(dto.BackgroundCharacterTravels[0].IsTraveling);
            var valid = StrategicSnapshotHelper.ValidateCaptureForSerialization(world, dto);
            Assert.IsTrue(valid.IsSuccess, valid.IsFailure ? valid.Error.ToString() : string.Empty);
        }

        [Test]
        public void IdleSquadRestorePreservesSavedExactPositionInsteadOfSiteArrival()
        {
            var world = new SimulationWorld();
            world.SurfaceGround.Register(CreateSurface());
            world.Strategic.Sites.Register(new WorldSite
            {
                SiteId = "test:squad_site",
                UsesContinuousOutdoorSurface = true
            });
            world.SurfaceGround.RegisterSiteArrival(
                SurfaceId, "test:squad_site", new WorldVec2(1f, 1f));
            var character = CreateCharacter(world, "squad_member");
            var squad = SquadMembershipService.Create(
                world, "test:squad_motion", new[] { character }, character,
                SquadCommandKind.SquadWorldMotion);
            Assert.IsTrue(squad.IsSuccess, squad.IsFailure ? squad.Error.ToString() : string.Empty);
            var exact = new WorldVec2(6.25f, 7.5f);
            var initialized = SquadWorldMotionService.Initialize(
                world, squad.Value.SquadId, SurfaceId, exact, "test:squad_site");
            Assert.IsTrue(initialized.IsSuccess,
                initialized.IsFailure ? initialized.Error.ToString() : string.Empty);
            var dto = StrategicSnapshotHelper.Capture(world, null);
            world.Strategic.SquadWorldMotions.Clear();

            var restored = StrategicSnapshotHelper.RestoreSquadWorldMotions(world, dto);

            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : string.Empty);
            Assert.IsTrue(world.Strategic.SquadWorldMotions.TryGet(
                squad.Value.SquadId, out var motion));
            Assert.AreEqual(exact.X, motion.WorldPosition.X, .0001f);
            Assert.AreEqual(exact.Y, motion.WorldPosition.Y, .0001f);
            Assert.AreEqual("test:squad_site", motion.SiteId);
        }

        [Test]
        public void CoreBackgroundRestoreNormalizesOnlyIdleEmptySurfaceFromPrimaryPresence()
        {
            var source = new SimulationWorld();
            source.SurfaceGround.Register(CreateSurface());
            var character = CreateCharacter(source, "background_restore");
            SquadMembershipService.EnsureSingletonsForUnassignedCharacters(source);
            source.WorldPresence.SetAtWorldPosition(
                character, new WorldVec2(4.5f, 5.5f), SurfaceId);
            source.BackgroundCharacterTravel.GetOrCreate(character);
            var dto = StrategicSnapshotHelper.Capture(source, null);
            Assert.AreEqual(1, dto.BackgroundCharacterTravels.Count);
            dto.BackgroundCharacterTravels[0].SurfaceId = string.Empty;
            dto.BackgroundCharacterTravels[0].WorldX = 99f;
            dto.BackgroundCharacterTravels[0].WorldY = 99f;

            var idleTarget = new SimulationWorld();
            Assert.AreEqual(character, CreateCharacter(idleTarget, "background_restore"));
            var idle = StrategicSnapshotHelper.Restore(idleTarget, dto);
            Assert.IsTrue(idle.IsSuccess, idle.IsFailure ? idle.Error.ToString() : string.Empty);
            Assert.IsTrue(idleTarget.WorldPresence.TryGet(character, out var restoredPresence));
            Assert.AreEqual(SurfaceId, restoredPresence.PersonalSurfaceId);
            Assert.AreEqual(4.5f, restoredPresence.WorldPosX, .0001f);
            Assert.AreEqual(5.5f, restoredPresence.WorldPosY, .0001f);
            Assert.IsFalse(idleTarget.BackgroundCharacterTravel.IsTraveling(character));

            dto.BackgroundCharacterTravels[0].IsTraveling = true;
            dto.BackgroundCharacterTravels[0].IsSurfaceRoute = true;
            dto.BackgroundCharacterTravels[0].DestinationSiteId = "test:destination";
            var movingTarget = new SimulationWorld();
            Assert.AreEqual(character, CreateCharacter(movingTarget, "background_restore"));
            var moving = StrategicSnapshotHelper.Restore(movingTarget, dto);
            Assert.IsTrue(moving.IsFailure);

            dto.BackgroundCharacterTravels[0].IsTraveling = false;
            dto.BackgroundCharacterTravels[0].IsSurfaceRoute = false;
            dto.BackgroundCharacterTravels[0].DestinationSiteId = string.Empty;
            dto.CharacterWorldPresences.Clear();
            var missingTarget = new SimulationWorld();
            Assert.AreEqual(character, CreateCharacter(missingTarget, "background_restore"));
            var missing = StrategicSnapshotHelper.Restore(missingTarget, dto);
            Assert.IsTrue(missing.IsFailure);
        }

        static EntityId CreateCharacter(SimulationWorld world, string name)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess, created.IsFailure ? created.Error.ToString() : string.Empty);
            return created.Value.Id;
        }

        static PlayerPartyRuntime CreateParty(SimulationWorld world, EntityId active)
        {
            var created = SquadMembershipService.Create(
                world, SquadMembershipService.PlayerSquadId,
                new[] { active }, active, SquadCommandKind.FollowLeader);
            Assert.IsTrue(created.IsSuccess, created.IsFailure ? created.Error.ToString() : string.Empty);
            var party = new PlayerPartyRuntime();
            party.BindWorld(world);
            Assert.IsTrue(party.TryBindControlledSquad(
                created.Value.SquadId, active, out var error), error);
            return party;
        }

        static SurfaceGroundNavigation CreateSurface()
        {
            var cells = new List<SurfaceGroundCellKind>(100);
            for (var i = 0; i < 100; i++)
                cells.Add(SurfaceGroundCellKind.Ground);
            return new SurfaceGroundNavigation(
                SurfaceId, "rev", "hash", 0f, 0f, 1f, 10, 10, cells);
        }
    }
}
