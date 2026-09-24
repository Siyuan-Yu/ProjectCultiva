using System;
using System.IO;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;
using XianXia.Data.Serialization;

namespace XianXia.Tests
{
    public sealed class PlayerPartySnapshotPhase3RestoreTests
    {
        const string FactionA = "test:faction_a";
        const string MapId = "test:map_cave";

        static EntityId SpawnCharacter(SimulationWorld world, string name)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            created.Value.Get<FactionMembershipComponent>().Assign(FactionA, FactionRoleKind.Member);
            return created.Value.Id;
        }

        [Test]
        public void PlayerPartyRestoreFromSnapshotPreservesActiveAndMembers()
        {
            var world = new SimulationWorld();
            var a = SpawnCharacter(world, "A");
            var b = SpawnCharacter(world, "B");
            var c = SpawnCharacter(world, "C");
            var party = new PlayerPartyRuntime();
            party.BindWorld(world);

            Assert.IsTrue(party.TryRestoreFromSnapshot(a, new[] { a, b, c }, out _));
            var dto = StrategicSnapshotHelper.Capture(world, party);
            Assert.AreEqual(a.Value, dto.PlayerParty.ActiveCharacterId);
            Assert.AreEqual(3, dto.PlayerParty.MemberCharacterIds.Count);

            var restored = new PlayerPartyRuntime();
            restored.BindWorld(world);
            PlayerPartySnapshotRestore.Apply(world, restored, dto.PlayerParty);
            Assert.AreEqual(3, restored.Count);
            Assert.AreEqual(a, restored.ActiveCharacterId);
            Assert.IsTrue(restored.IsMember(b));
            Assert.IsTrue(restored.IsMember(c));
        }

        [Test]
        public void SeparateSpacePlacementCaptureRestoresEverySavedExactLocalPosition()
        {
            var world = new SimulationWorld();
            var a = SpawnCharacter(world, "A");
            var b = SpawnCharacter(world, "B");
            world.LocalMap.EstablishSeparateSpace(
                MapId, "test:places", SeparateSpaceKind.Cave,
                "test:entrance", "test:entrance", "test");
            world.LocalMap.SetOccupants(new[] { a, b });

            SetLocalPosition(world, a, 12.5f, -7.25f);
            SetLocalPosition(world, b, 3f, 9f);
            var dto = StrategicSnapshotHelper.Capture(world, null);
            Assert.AreEqual(2, dto.LoadedLocalMapCharacterPlacements.Count);

            ClearLocalPosition(world, a);
            ClearLocalPosition(world, b);
            LoadedLocalMapPlacementSnapshotRestore.BeginRestoreFromSnapshot(dto);
            Assert.AreEqual(2,
                LoadedLocalMapPlacementSnapshotRestore.ApplySavedPlacementsToDomain(world, MapId));

            AssertLocalPosition(world, a, 12.5f, -7.25f);
            AssertLocalPosition(world, b, 3f, 9f);
        }

        [Test]
        public void SavedSeparateSpacePlacementCanBeReadWithoutChangingMembership()
        {
            var dto = new StrategicSnapshotDto();
            dto.LoadedLocalMapCharacterPlacements.Add(
                new LoadedLocalMapCharacterPlacementSnapshotDto
                {
                    CharacterId = 7,
                    LocalMapId = MapId,
                    LocalX = 23.4f,
                    LocalZ = 17.8f
                });
            LoadedLocalMapPlacementSnapshotRestore.BeginRestoreFromSnapshot(dto);

            Assert.IsTrue(LoadedLocalMapPlacementSnapshotRestore.TryGetPlacement(
                new EntityId(7), MapId, out var x, out var z));
            Assert.AreEqual(23.4f, x, 0.001f);
            Assert.AreEqual(17.8f, z, 0.001f);
        }

        [Test]
        public void CurrentSnapshotDirectRestorePreservesSurfaceSquadAndExactPosition()
        {
            var root = Environment.GetEnvironmentVariable("XIANXIA_BASEGAME") ??
                       Path.GetFullPath("Content/BaseGame");
            var loaded = new ContentPackageLoader().Load(new[] { root });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            var started = new PlayableDayBootstrap().Start(
                loaded.Value,
                new PlayableDayOptions { OpeningScenarioId = "base:scenario_ch01_reference" });
            Assert.IsTrue(started.IsSuccess,
                started.IsFailure ? started.Error.ToString() : string.Empty);

            var world = started.Value.World;
            var active = started.Value.CharacterIds[0];
            SquadMembershipService.EnsureSingletonsForUnassignedCharacters(world);
            Assert.IsTrue(CharacterStrategicQuery.TryGetSquad(world, active, out var squad));
            var party = new PlayerPartyRuntime();
            party.BindWorld(world);
            Assert.IsTrue(party.TryBindControlledSquad(squad.SquadId, active, out var bindError), bindError);

            var savedSurface = world.PlayerPartyTravel.SurfaceId;
            var savedPosition = new WorldVec2(
                world.PlayerPartyTravel.WorldPosition.X + 0.013f,
                world.PlayerPartyTravel.WorldPosition.Y + 0.017f);
            world.PlayerPartyTravel.SetAtSurfacePosition(savedSurface, savedPosition);
            world.WorldPresence.SetAtWorldPosition(active, savedPosition, savedSurface);
            // This test isolates spatial authority. PlayableDayBootstrap may present the authored
            // opening dialogue, and SAVE-01 correctly rejects capture while any event is active.
            world.ContentEvents.ClearActive();

            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, started.Value.Loop, party);
            Assert.IsTrue(json.IsSuccess, json.IsFailure ? json.Error.ToString() : string.Empty);
            var restored = service.RestoreJson(json.Value);
            Assert.IsTrue(restored.IsSuccess,
                restored.IsFailure ? restored.Error.ToString() : string.Empty);

            var restoredWorld = restored.Value.world;
            Assert.AreEqual(savedSurface, restoredWorld.PlayerPartyTravel.SurfaceId);
            Assert.AreEqual(savedPosition.X, restoredWorld.PlayerPartyTravel.WorldPosition.X, 0.000001f);
            Assert.AreEqual(savedPosition.Y, restoredWorld.PlayerPartyTravel.WorldPosition.Y, 0.000001f);
            Assert.IsTrue(restoredWorld.Strategic.Squads.TryGet(squad.SquadId, out var restoredSquad));
            Assert.IsTrue(restoredSquad.Contains(active));
            Assert.IsTrue(restoredWorld.WorldPresence.TryGet(active, out var restoredPresence));
            Assert.AreEqual(savedSurface, restoredPresence.PersonalSurfaceId);
            Assert.AreEqual(savedPosition.X, restoredPresence.WorldPosX, 0.000001f);
            Assert.AreEqual(savedPosition.Y, restoredPresence.WorldPosY, 0.000001f);
        }

        [Test]
        public void OldArmyWirePayloadFailsWithSnapshotInvalidAndOfflineConversionMessage()
        {
            var dto = new StrategicSnapshotDto
            {
                HasSquadSnapshotAuthority = true,
                HasSquadWorldMotionSnapshotAuthority = true
            };
            dto.FormalArmies.Add(new FormalArmySnapshotDto
            {
                ArmyId = "old:army",
                FactionId = "old:faction"
            });

            var result = StrategicSnapshotHelper.Restore(new SimulationWorld(), dto);
            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual(XianXia.Core.Results.ErrorCode.SnapshotInvalid, result.Error.Code);
            StringAssert.Contains("offline conversion", result.Error.Message);
        }

        static void SetLocalPosition(SimulationWorld world, EntityId id, float x, float z)
        {
            Assert.IsTrue(world.Entities.TryGet(id, out var entity));
            var location = new EntityLocationComponent();
            location.SetPresentationOverride(x, z);
            entity.AddComponent(location);
        }

        static void ClearLocalPosition(SimulationWorld world, EntityId id)
        {
            Assert.IsTrue(world.Entities.TryGet(id, out var entity));
            Assert.IsTrue(entity.TryGet<EntityLocationComponent>(out var location));
            location.ClearPresence();
        }

        static void AssertLocalPosition(
            SimulationWorld world, EntityId id, float expectedX, float expectedZ)
        {
            Assert.IsTrue(world.Entities.TryGet(id, out var entity));
            Assert.IsTrue(entity.TryGet<EntityLocationComponent>(out var location));
            Assert.AreEqual(expectedX, location.PresentationOverrideX, 0.001f);
            Assert.AreEqual(expectedZ, location.PresentationOverrideZ, 0.001f);
        }
    }
}
