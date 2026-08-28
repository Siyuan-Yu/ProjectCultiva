using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    public sealed class SnapshotActiveControlledLocalMapResolverTests
    {
        const string FactionPlayer = "base:faction_player";

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            world.Strategic.PlayerFactionId = FactionPlayer;
            HexTestWorldBootstrap.EnsureMinimalHexMap(world);
            return world;
        }

        static EntityId SpawnCharacter(SimulationWorld world, string name)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            created.Value.Get<FactionMembershipComponent>().Assign(FactionPlayer, FactionRoleKind.Member);
            return created.Value.Id;
        }

        [Test]
        public void SNAP_ACTIVE_01_AtSiteActive_ResolvesSiteLocalMap()
        {
            var world = CreateWorld();
            Ch01ScenarioStrategicSetup.EnsureLevelTesterFixtures(world);
            var active = SpawnCharacter(world, "Active");
            var party = new PlayerPartyRuntime();
            party.TryRestoreFromSnapshot(active, new[] { active }, out _);

            world.WorldPresence.SetAtSite(active, Ch01HexPrototypeMapBuilder.SitePlayerCamp);
            world.PlayerPartyTravel.SetAtWorldSite(
                Ch01HexPrototypeMapBuilder.SiteHuangcun,
                Ch01HexPrototypeMapBuilder.HuangcunHex,
                world.HexWorld.HexSize);

            Assert.IsTrue(SnapshotActiveControlledLocalMapResolver.TryResolveRequiredLocalMap(
                world, party, out var resolved));
            Assert.AreEqual(Ch01HexPrototypeMapBuilder.PlayerCampLocalMapId, resolved.LocalMapId);
            Assert.AreEqual("ActiveWorldPresence.AtSite", resolved.Source);
        }

        [Test]
        public void SNAP_ACTIVE_02_WildernessActive_ResolvesWildernessFallbackMap()
        {
            var world = CreateWorld();
            var active = SpawnCharacter(world, "Active");
            var party = new PlayerPartyRuntime();
            party.TryRestoreFromSnapshot(active, new[] { active }, out _);

            var hex = Ch01HexPrototypeMapBuilder.HuangcunHex;
            var hexSize = world.HexWorld.HexSize;
            HexMath.ToWorldPosition(hex, hexSize, out var wx, out var wy);
            world.WorldPresence.SetAtWorldPosition(active, new WorldVec2(wx, wy), hex);
            world.PlayerPartyTravel.SetAtWorldPosition(new WorldVec2(wx, wy), hex);

            Assert.IsTrue(SnapshotActiveControlledLocalMapResolver.TryResolveRequiredLocalMap(
                world, party, out var resolved));
            Assert.IsFalse(string.IsNullOrEmpty(resolved.LocalMapId));
            Assert.AreNotEqual(Ch01HexPrototypeMapBuilder.PlayerCampLocalMapId, resolved.LocalMapId);
            StringAssert.Contains("ActiveWorldPresence", resolved.Source);
        }

        [Test]
        public void SNAP_ACTIVE_03_ApplyResolvedPartyWorldFocus_SyncsPartyWorldMap()
        {
            var world = CreateWorld();
            Ch01ScenarioStrategicSetup.EnsureLevelTesterFixtures(world);
            var active = SpawnCharacter(world, "Active");
            var party = new PlayerPartyRuntime();
            party.TryRestoreFromSnapshot(active, new[] { active }, out _);
            world.WorldPresence.SetAtSite(active, Ch01HexPrototypeMapBuilder.SitePlayerCamp);
            world.PartyWorld.LocalMapId = "stale:wrong_map";

            Assert.IsTrue(SnapshotActiveControlledLocalMapResolver.TryResolveRequiredLocalMap(
                world, party, out var resolved));
            SnapshotActiveControlledLocalMapResolver.ApplyResolvedPartyWorldFocus(world, in resolved);

            Assert.AreEqual(Ch01HexPrototypeMapBuilder.PlayerCampLocalMapId, world.PartyWorld.LocalMapId);
            Assert.AreEqual(Ch01HexPrototypeMapBuilder.SitePlayerCamp, world.PartyWorld.SiteId);
        }
    }
}
