using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Core.World.Surface;
using XianXia.Data.Content;

namespace XianXia.Tests
{
    /// <summary>Pure C# FINAL-SEAL ownership, casualty, position and Surface provenance sanity.</summary>
    public sealed class FinalSealConsolidatedSanityTests
    {
        [Test]
        public void CurrentBaseGameContentReferencesRemainValid()
        {
            var root = Environment.GetEnvironmentVariable("XIANXIA_BASEGAME") ??
                       Path.GetFullPath("Content/BaseGame");
            var loaded = new ContentPackageLoader().Load(new[] { root });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            var report = new ContentReferenceValidator().Validate(loaded.Value.Registry);
            Assert.IsTrue(report.IsValid,
                report.IsValid || report.Errors.Count == 0 ? string.Empty : report.Errors[0].ToString());
        }

        [Test]
        public void SingletonMembershipDoesNotOwnPersonalTravel()
        {
            var world = CreateTwoSurfaceWorld();
            var character = CreateCharacter(world, "singleton");
            SquadMembershipService.EnsureSingletonsForUnassignedCharacters(world);
            world.WorldPresence.SetAtWorldPosition(
                character, new WorldVec2(2f, 2f), "surface:a");

            Assert.IsTrue(CharacterStrategicQuery.TryGetSquad(world, character, out _));
            Assert.IsFalse(SquadWorldMotionService.OwnsCharacter(world, character));
            Assert.IsTrue(CharacterWorldMovementAuthorityQuery.CanStartBackgroundTravel(
                world, character, null, out var error), error);

            var site = new WorldSite
            {
                SiteId = "site:a", DisplayName = "A", UsesContinuousOutdoorSurface = true
            };
            world.Strategic.Sites.Register(site);
            world.SurfaceGround.RegisterSiteArrival("surface:a", site.SiteId, new WorldVec2(8f, 8f));
            var begun = BackgroundCharacterTravelService.BeginTravelToWorldSite(
                world, character, site.SiteId);
            Assert.IsTrue(begun.IsSuccess, begun.IsFailure ? begun.Error.ToString() : string.Empty);
            Assert.IsTrue(world.BackgroundCharacterTravel.TryGet(character, out var motion));
            Assert.AreEqual("surface:a", motion.SurfaceId);
        }

        [Test]
        public void GroupOwnedCharacterRejectsPersonalTravelAndExplicitSurfaceWinsOverlap()
        {
            var world = CreateTwoSurfaceWorld();
            var first = CreateCharacter(world, "first");
            var second = CreateCharacter(world, "second");
            var squad = SquadMembershipService.Create(
                world, "squad:test", new[] { first, second }, first,
                command: SquadCommandKind.SquadWorldMotion);
            Assert.IsTrue(squad.IsSuccess, squad.IsFailure ? squad.Error.ToString() : string.Empty);
            Assert.IsTrue(SquadWorldMotionService.Initialize(
                world, squad.Value.SquadId, "surface:a", new WorldVec2(2f, 2f)).IsSuccess);

            Assert.IsTrue(SquadWorldMotionService.OwnsCharacter(world, second));
            Assert.IsFalse(CharacterWorldMovementAuthorityQuery.CanStartBackgroundTravel(
                world, second, null, out _));

            var move = SquadWorldMotionService.MoveToWorldPosition(
                world, squad.Value.SquadId, new WorldVec2(7f, 7f));
            Assert.IsTrue(move.IsSuccess, move.IsFailure ? move.Error.ToString() : string.Empty);
            Assert.IsTrue(world.Strategic.SquadWorldMotions.TryGet(squad.Value.SquadId, out var motion));
            Assert.AreEqual("surface:a", motion.SurfaceId);

            var crossSurface = SquadWorldMotionService.MoveToWorldPosition(
                world, squad.Value.SquadId, new WorldVec2(12f, 2f));
            Assert.IsTrue(crossSurface.IsFailure);
            Assert.AreEqual("surface:a", motion.SurfaceId);
        }

        [Test]
        public void IncapacitatedThenDeadKeepsFirstPersonalHandoffAfterSquadMoves()
        {
            var world = CreateTwoSurfaceWorld();
            var survivor = CreateCharacter(world, "survivor");
            var casualty = CreateCharacter(world, "casualty");
            var squad = SquadMembershipService.Create(
                world, "squad:casualty", new[] { survivor, casualty }, survivor,
                command: SquadCommandKind.SquadWorldMotion);
            Assert.IsTrue(squad.IsSuccess);
            Assert.IsTrue(SquadWorldMotionService.Initialize(
                world, squad.Value.SquadId, "surface:a", new WorldVec2(2f, 2f)).IsSuccess);
            Assert.IsTrue(world.Entities.TryGet(casualty, out var casualtyEntity));

            Assert.IsTrue(CombatLifeStateService.TryEnterIncapacitated(world, casualtyEntity));
            Assert.IsTrue(world.WorldPresence.TryGet(casualty, out var handedOff));
            var atA = handedOff.ContinuousWorldPosition;
            Assert.AreEqual("surface:a", handedOff.PersonalSurfaceId);
            Assert.IsFalse(SquadWorldMotionService.OwnsCharacter(world, casualty));

            Assert.IsTrue(SquadWorldMotionService.MoveToWorldPosition(
                world, squad.Value.SquadId, new WorldVec2(8f, 8f)).IsSuccess);
            SquadWorldMotionService.AdvanceAll(world, 200);
            Assert.IsTrue(CombatLifeStateService.TryConfirmDeath(
                world, EntityId.None, casualtyEntity, out var confirmed));
            Assert.IsTrue(confirmed);
            Assert.IsTrue(world.WorldPresence.TryGet(casualty, out var afterDeath));
            Assert.AreEqual(atA.X, afterDeath.WorldPosX, .0001f);
            Assert.AreEqual(atA.Y, afterDeath.WorldPosY, .0001f);
            Assert.AreEqual("surface:a", afterDeath.PersonalSurfaceId);

            var snapshot = StrategicSnapshotHelper.Capture(world, null);
            var saved = snapshot.CharacterWorldPresences.Find(p => p.CharacterId == casualty.Value);
            Assert.NotNull(saved);
            Assert.AreEqual(atA.X, saved.WorldX, .0001f);
            Assert.AreEqual(atA.Y, saved.WorldY, .0001f);
            Assert.AreEqual("surface:a", saved.PersonalSurfaceId);
        }

        [Test]
        public void ExactPositionWithoutDerivedCacheIsReadableAndZeroZeroIsValid()
        {
            var world = CreateTwoSurfaceWorld();
            var character = CreateCharacter(world, "origin");
            var presence = world.WorldPresence.GetOrCreate(character);
            presence.Mode = PartyWorldPresenceMode.AtWorldPosition;
            presence.HasContinuousWorldPosition = true;
            presence.WorldPosX = 0f;
            presence.WorldPosY = 0f;
            presence.PersonalSurfaceId = "surface:a";
            Assert.IsTrue(CharacterWorldPresenceQuery.TryResolve(
                world, character, out var resolved));
            Assert.AreEqual(
                CharacterWorldPresenceQuery.PresenceState.AtWorldPosition,
                resolved.State);
            Assert.AreEqual(string.Empty, resolved.SiteId);
            Assert.AreEqual("surface:a", resolved.SurfaceId);
            Assert.IsTrue(resolved.HasWorldPosition);
            Assert.AreEqual(0f, resolved.WorldPosition.X);
            Assert.AreEqual(0f, resolved.WorldPosition.Y);
        }

        [Test]
        public void PlayerPartyContextBlocksNullPartyArgumentAndUsesExplicitSurface()
        {
            var world = CreateTwoSurfaceWorld();
            var character = CreateCharacter(world, "player");
            var squad = SquadMembershipService.Create(
                world, SquadMembershipService.PlayerSquadId, new[] { character }, character);
            Assert.IsTrue(squad.IsSuccess);
            var party = new PlayerPartyRuntime();
            party.BindWorld(world);
            Assert.IsTrue(party.TryBindControlledSquad(squad.Value.SquadId, character, out var error), error);
            world.PlayerPartyTravel.SetAtSurfacePosition(
                "surface:a", new WorldVec2(2f, 2f));

            Assert.IsFalse(CharacterWorldMovementAuthorityQuery.CanStartBackgroundTravel(
                world, character, null, out _));
            var begun = PlayerPartySurfaceTravelService.BeginTravel(
                world, party, new WorldVec2(7f, 7f), string.Empty, .25f);
            Assert.IsTrue(begun.IsSuccess, begun.IsFailure ? begun.Error.ToString() : string.Empty);
            Assert.AreEqual("surface:a", world.PlayerPartyTravel.SurfaceId);

            PlayerPartySurfaceTravelService.Cancel(world);
            var cross = PlayerPartySurfaceTravelService.BeginTravel(
                world, party, new WorldVec2(12f, 2f), string.Empty, .25f);
            Assert.IsTrue(cross.IsFailure);
            Assert.AreEqual("surface:a", world.PlayerPartyTravel.SurfaceId);
        }

        [Test]
        public void BackgroundSnapshotFinalizationUsesExplicitSurfaceAndDoesNotRestartIdle()
        {
            var world = CreateTwoSurfaceWorld();
            var moving = CreateCharacter(world, "moving");
            var idle = CreateCharacter(world, "idle");
            SquadMembershipService.EnsureSingletonsForUnassignedCharacters(world);
            var site = new WorldSite
            {
                SiteId = "site:restore", DisplayName = "Restore",
                UsesContinuousOutdoorSurface = true
            };
            world.Strategic.Sites.Register(site);
            world.SurfaceGround.RegisterSiteArrival(
                "surface:a", site.SiteId, new WorldVec2(8f, 8f));
            world.WorldPresence.SetAtWorldPosition(
                moving, new WorldVec2(2f, 2f), "surface:a");
            world.WorldPresence.SetAtWorldPosition(
                idle, new WorldVec2(3f, 3f), "surface:a");

            var records = new List<BackgroundCharacterTravelSnapshotDto>
            {
                new BackgroundCharacterTravelSnapshotDto
                {
                    CharacterId = moving.Value,
                    LocationKind = (int)BackgroundCharacterLocationKind.AtWorldPosition,
                    WorldX = 2f, WorldY = 2f, SurfaceId = "surface:a",
                    IsTraveling = true, IsSurfaceRoute = true,
                    DestinationSiteId = site.SiteId,
                    SurfaceDestinationX = 8f, SurfaceDestinationY = 8f
                },
                new BackgroundCharacterTravelSnapshotDto
                {
                    CharacterId = idle.Value,
                    LocationKind = (int)BackgroundCharacterLocationKind.AtWorldPosition,
                    // Idle compatibility record intentionally omits Surface and carries stale
                    // coordinates. Current CharacterWorldPresence remains the primary authority.
                    WorldX = 99f, WorldY = 99f, SurfaceId = string.Empty,
                    IsTraveling = false, IsSurfaceRoute = true
                }
            };

            var restored = StrategicSnapshotHelper.RestoreBackgroundSurfaceTravels(world, records);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : string.Empty);
            Assert.IsTrue(world.BackgroundCharacterTravel.TryGet(moving, out var motion));
            Assert.IsTrue(motion.IsMoving);
            Assert.AreEqual("surface:a", motion.SurfaceId);
            Assert.IsFalse(world.BackgroundCharacterTravel.IsTraveling(idle));
            Assert.IsTrue(world.WorldPresence.TryGet(idle, out var idlePresence));
            Assert.AreEqual("surface:a", idlePresence.PersonalSurfaceId);
            Assert.AreEqual(3f, idlePresence.WorldPosX, .0001f);
            Assert.AreEqual(3f, idlePresence.WorldPosY, .0001f);
            Assert.IsEmpty(records[1].SurfaceId, "normalization must not rewrite the DTO");
            Assert.AreEqual(99f, records[1].WorldX, .0001f);
            Assert.AreEqual(99f, records[1].WorldY, .0001f);

            records[0].DestinationSiteId = string.Empty;
            var missingDestination = StrategicSnapshotHelper.RestoreBackgroundSurfaceTravels(
                world, records);
            Assert.IsTrue(missingDestination.IsFailure);

            records[0].DestinationSiteId = site.SiteId;
            records[0].SurfaceId = string.Empty;
            var ambiguousLegacy = StrategicSnapshotHelper.RestoreBackgroundSurfaceTravels(
                world, records);
            Assert.IsTrue(ambiguousLegacy.IsFailure);

            var noAuthority = CreateCharacter(world, "idle_without_authority");
            var missingPrimary = StrategicSnapshotHelper.RestoreBackgroundSurfaceTravels(
                world,
                new List<BackgroundCharacterTravelSnapshotDto>
                {
                    new BackgroundCharacterTravelSnapshotDto
                    {
                        CharacterId = noAuthority.Value,
                        LocationKind = (int)BackgroundCharacterLocationKind.AtWorldPosition,
                        SurfaceId = string.Empty,
                        IsTraveling = false
                    }
                });
            Assert.IsTrue(missingPrimary.IsFailure);
        }

        static SimulationWorld CreateTwoSurfaceWorld()
        {
            var world = new SimulationWorld();
            world.SurfaceGround.Register(CreateSurface("surface:a", 0f, 0f, 10, 10));
            // Overlaps A from x=0..10, but also extends to x=15 for cross-Surface rejection.
            world.SurfaceGround.Register(CreateSurface("surface:b", 0f, 0f, 15, 10));
            return world;
        }

        static SurfaceGroundNavigation CreateSurface(
            string id, float x, float y, int width, int height)
        {
            var cells = new List<SurfaceGroundCellKind>(width * height);
            for (var i = 0; i < width * height; i++) cells.Add(SurfaceGroundCellKind.Ground);
            return new SurfaceGroundNavigation(id, "rev", id, x, y, 1f, width, height, cells);
        }

        static EntityId CreateCharacter(SimulationWorld world, string id)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", id), id);
            Assert.IsTrue(created.IsSuccess, created.IsFailure ? created.Error.ToString() : string.Empty);
            return created.Value.Id;
        }
    }
}
