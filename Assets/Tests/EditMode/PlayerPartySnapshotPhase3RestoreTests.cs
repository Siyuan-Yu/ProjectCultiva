using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Data.Serialization;

namespace XianXia.Tests
{
    /// <summary>Phase 3 Snapshot：PlayerParty Membership + Loaded LocalMap Placement。</summary>
    public sealed class PlayerPartySnapshotPhase3RestoreTests
    {
        const string FactionA = "test:faction_a";
        const string MapId = "test:map_site_a";

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            world.Strategic.PlayerFactionId = FactionA;
            HexTestWorldBootstrap.EnsureMinimalHexMap(world);
            world.LocalMap.ActiveMapLayoutId = MapId;
            world.LocalMap.OverworldMapLayoutId = MapId;
            world.PartyWorld.LocalMapId = MapId;
            return world;
        }

        static EntityId SpawnCharacter(SimulationWorld world, string name)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            var entity = created.Value;
            entity.Get<FactionMembershipComponent>().Assign(FactionA, FactionRoleKind.Member);
            return entity.Id;
        }

        [Test]
        public void SNAP_P3_01_PlayerPartyRestoreFromSnapshot_BypassesSameLocalMapJoinValidation()
        {
            var world = CreateWorld();
            var a = SpawnCharacter(world, "A");
            var b = SpawnCharacter(world, "B");
            var c = SpawnCharacter(world, "C");

            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryRestoreFromSnapshot(a, new[] { a, b, c }, out _));
            Assert.AreEqual(3, party.Count);
            Assert.AreEqual(a, party.ActiveCharacterId);
            Assert.IsTrue(party.IsMember(b));
            Assert.IsTrue(party.IsMember(c));
        }

        [Test]
        public void SNAP_P3_02_PlayerPartyCaptureRoundtrip_PersistsActiveAndMembers()
        {
            var world = CreateWorld();
            var a = SpawnCharacter(world, "A");
            var b = SpawnCharacter(world, "B");
            var party = new PlayerPartyRuntime();
            Assert.IsTrue(party.TryRestoreFromSnapshot(a, new[] { a, b }, out _));

            var dto = StrategicSnapshotHelper.Capture(world, party);
            Assert.IsNotNull(dto.PlayerParty);
            Assert.AreEqual(a.Value, dto.PlayerParty.ActiveCharacterId);
            Assert.AreEqual(2, dto.PlayerParty.MemberCharacterIds.Count);

            var serializer = new JsonSnapshotSerializer();
            var json = serializer.Serialize(new WorldSnapshot { Strategic = dto });
            Assert.IsTrue(json.IsSuccess);
            StringAssert.Contains("\"playerParty\"", json.Value);

            var parsed = serializer.Deserialize(json.Value);
            Assert.IsTrue(parsed.IsSuccess);
            Assert.AreEqual(2, parsed.Value.Strategic.PlayerParty.MemberCharacterIds.Count);

            var restoredParty = new PlayerPartyRuntime();
            PlayerPartySnapshotRestore.Apply(world, restoredParty, parsed.Value.Strategic.PlayerParty);
            Assert.AreEqual(2, restoredParty.Count);
            Assert.AreEqual(a, restoredParty.ActiveCharacterId);
        }

        [Test]
        public void SNAP_P3_03_LoadedLocalMapPlacement_CaptureRestoreAndMaterializeAtSavedPosition()
        {
            var world = CreateWorld();
            var a = SpawnCharacter(world, "A");
            var b = SpawnCharacter(world, "B");
            world.PlayerPartyTravel.SetAtWorldSite(
                Ch01HexPrototypeMapBuilder.SiteHuangcun,
                Ch01HexPrototypeMapBuilder.HuangcunHex,
                world.HexWorld.HexSize);
            world.PartyWorld.SiteId = Ch01HexPrototypeMapBuilder.SiteHuangcun;
            world.PartyWorld.LocalMapId = MapId;
            world.LocalMap.AddOccupant(a);
            world.LocalMap.AddOccupant(b);

            var locA = new EntityLocationComponent();
            locA.SetPresentationOverride(12.5f, -7.25f);
            world.Entities.TryGet(a, out var entA);
            entA.AddComponent(locA);

            var locB = new EntityLocationComponent();
            locB.SetPresentationOverride(3f, 9f);
            world.Entities.TryGet(b, out var entB);
            entB.AddComponent(locB);

            var dto = StrategicSnapshotHelper.Capture(world, null);
            Assert.AreEqual(2, dto.LoadedLocalMapCharacterPlacements.Count);

            foreach (var entity in world.Entities.All)
            {
                if (entity.TryGet<EntityLocationComponent>(out var loc))
                    loc.ClearPresence();
            }

            world.LocalMap.ClearOccupants();
            LoadedLocalMapPlacementSnapshotRestore.BeginRestoreFromSnapshot(dto);

            var party = new PlayerPartyRuntime();
            party.TryRestoreFromSnapshot(a, new[] { a, b }, out _);
            PlayerPartyLocalMapMaterializationService.MaterializePartyOnResolvedLocalMap(
                world, party.Members, null);

            Assert.IsTrue(world.Entities.TryGet(a, out entA));
            Assert.IsTrue(entA.TryGet<EntityLocationComponent>(out locA));
            Assert.AreEqual(12.5f, locA.PresentationOverrideX, 0.001f);
            Assert.AreEqual(-7.25f, locA.PresentationOverrideZ, 0.001f);

            Assert.IsTrue(world.Entities.TryGet(b, out entB));
            Assert.IsTrue(entB.TryGet<EntityLocationComponent>(out locB));
            Assert.AreEqual(3f, locB.PresentationOverrideX, 0.001f);
            Assert.AreEqual(9f, locB.PresentationOverrideZ, 0.001f);
        }

        [Test]
        public void SNAP_P3_04_WorldSiteSpawnPriority_PrefersSnapshotOverDefaultStart()
        {
            var dto = new StrategicSnapshotDto();
            dto.LoadedLocalMapCharacterPlacements.Add(new LoadedLocalMapCharacterPlacementSnapshotDto
            {
                CharacterId = 7,
                LocalMapId = MapId,
                LocalX = 23.4f,
                LocalZ = 17.8f
            });
            LoadedLocalMapPlacementSnapshotRestore.BeginRestoreFromSnapshot(dto);

            var resolved = LoadedLocalMapPlacementSnapshotRestore.TryResolveWorldSiteSpawnPosition(
                new EntityId(7),
                MapId,
                0f,
                0f,
                out var x,
                out var z,
                out var source);

            Assert.IsTrue(resolved);
            Assert.AreEqual(
                LoadedLocalMapPlacementSnapshotRestore.SpawnPlacementSource.SnapshotLocalPlacement,
                source);
            Assert.AreEqual(23.4f, x, 0.001f);
            Assert.AreEqual(17.8f, z, 0.001f);
        }
    }
}
