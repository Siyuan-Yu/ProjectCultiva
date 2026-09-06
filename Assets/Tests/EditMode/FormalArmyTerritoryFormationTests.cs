using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class FormalArmyTerritoryFormationTests
    {
        const string Player = "faction:player";
        const string Enemy = "faction:enemy";

        static SimulationWorld World()
        {
            var world = new SimulationWorld();
            world.HexWorld.FillRectangle(12, 12, HexTerrainType.Plain);
            return world;
        }

        static EntityId CharacterAtHex(
            SimulationWorld world,
            string name,
            string factionId,
            HexCoord hex,
            float offsetX = 0f,
            float offsetY = 0f)
        {
            var entity = world.Entities.CreateCharacter(new DefinitionId("test", name), name).Value;
            entity.Get<FactionMembershipComponent>().Assign(factionId, FactionRoleKind.Member);
            HexMath.ToWorldPosition(hex, world.HexWorld.HexSize, out var x, out var y);
            world.WorldPresence.SetAtWorldPosition(
                entity.Id, new WorldVec2(x + offsetX, y + offsetY), hex);
            return entity.Id;
        }

        static EntityId CharacterAtSite(
            SimulationWorld world,
            string name,
            string factionId,
            WorldSite site)
        {
            var entity = world.Entities.CreateCharacter(new DefinitionId("test", name), name).Value;
            entity.Get<FactionMembershipComponent>().Assign(factionId, FactionRoleKind.Member);
            world.WorldPresence.SetAtSite(entity.Id, site.SiteId);
            return entity.Id;
        }

        static void AddFlag(
            SimulationWorld world,
            string id,
            string factionId,
            HexCoord anchor,
            long order)
        {
            Assert.IsTrue(world.Strategic.FactionFlags.Register(new FactionFlagState
            {
                FlagId = id,
                FactionId = factionId,
                AnchorHex = anchor,
                EstablishedOrder = order,
                CurrentHp = 100,
                MaxHp = 100
            }));
            StrategicTerritoryCoverageResolver.Rebuild(world);
        }

        [Test]
        public void WorldSiteTerritory_StillFormsAtWorldSite()
        {
            var world = World();
            var site = new WorldSite
            {
                SiteId = "site:home",
                OwnerFactionId = Player,
                AnchorHex = new HexCoord(3, 3),
                ControlEstablishedOrder = 1
            };
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, site);
            StrategicTerritoryCoverageResolver.Rebuild(world);
            site.EnsurePresenceHexValid();
            var a = CharacterAtSite(world, "site_a", Player, site);
            var b = CharacterAtSite(world, "site_b", Player, site);

            var created = ArmyService.CreateArmy(world, Player, site.PresenceHex, new[] { a, b });

            Assert.IsTrue(created.IsSuccess);
            Assert.AreEqual(FormalArmyLocationKind.AtWorldSite, created.Value.WorldMotion.LocationKind);
            Assert.AreEqual(site.SiteId, created.Value.WorldMotion.SiteId);
        }

        [Test]
        public void FactionFlagTerritory_FormsAtExactWildernessPosition()
        {
            var world = World();
            var hex = new HexCoord(5, 5);
            AddFlag(world, "flag:player", Player, hex, 1);
            var a = CharacterAtHex(world, "wild_a", Player, hex, 0.17f, -0.11f);
            var b = CharacterAtHex(world, "wild_b", Player, hex, -0.08f, 0.09f);
            var expected = world.WorldPresence.TryGet(a, out var presence)
                ? presence.ContinuousWorldPosition
                : default;

            var created = ArmyService.CreateArmy(world, Player, hex, new[] { a, b }, a);

            Assert.IsTrue(created.IsSuccess);
            Assert.AreEqual(FormalArmyLocationKind.AtWorldPosition, created.Value.WorldMotion.LocationKind);
            Assert.AreEqual(hex, created.Value.WorldMotion.CurrentHex);
            Assert.AreEqual(expected.X, created.Value.WorldMotion.WorldPosition.X, 0.0001f);
            Assert.AreEqual(expected.Y, created.Value.WorldMotion.WorldPosition.Y, 0.0001f);
        }

        [Test]
        public void NeutralEnemyAndEarlierOverlapController_RejectFormation()
        {
            var neutral = World();
            var hex = new HexCoord(5, 5);
            var n = CharacterAtHex(neutral, "neutral", Player, hex);
            Assert.IsTrue(ArmyService.CreateArmy(neutral, Player, hex, new[] { n }).IsFailure);

            var overlap = World();
            AddFlag(overlap, "flag:enemy_early", Enemy, hex, 1);
            AddFlag(overlap, "flag:player_late", Player, HexMath.Neighbor(hex, 0), 2);
            Assert.AreEqual(Enemy, TerritoryControlService.GetController(overlap, hex));
            var p = CharacterAtHex(overlap, "overlap", Player, hex);
            Assert.IsTrue(ArmyService.CreateArmy(overlap, Player, hex, new[] { p }).IsFailure);
        }

        [Test]
        public void MembersInDifferentControlledHexes_DoNotAutoRally()
        {
            var world = World();
            var aHex = new HexCoord(5, 5);
            var bHex = HexMath.Neighbor(aHex, 0);
            AddFlag(world, "flag:player", Player, aHex, 1);
            var a = CharacterAtHex(world, "split_a", Player, aHex);
            var b = CharacterAtHex(world, "split_b", Player, bHex);

            var created = ArmyService.CreateArmy(world, Player, aHex, new[] { a, b });

            Assert.IsTrue(created.IsFailure);
            StringAssert.Contains("same world hex", created.Error.Message);
        }

        [Test]
        public void WildernessRosterManagementAndDisband_UseControlledHexAndKeepPosition()
        {
            var world = World();
            var hex = new HexCoord(5, 5);
            AddFlag(world, "flag:player", Player, hex, 1);
            var a = CharacterAtHex(world, "roster_a", Player, hex, 0.12f, 0.07f);
            var b = CharacterAtHex(world, "roster_b", Player, hex);
            var c = CharacterAtHex(world, "roster_c", Player, hex);
            var elsewhere = CharacterAtHex(world, "elsewhere", Player, HexMath.Neighbor(hex, 0));
            var army = ArmyService.CreateArmy(world, Player, hex, new[] { a, b }, a).Value;
            var expected = army.WorldMotion.WorldPosition;

            Assert.IsTrue(ArmyService.ChangeLeader(world, army.ArmyId, b).IsSuccess);
            Assert.IsTrue(ArmyService.AddMember(world, army.ArmyId, c).IsSuccess);
            var wrongHex = ArmyService.AddMember(world, army.ArmyId, elsewhere);
            Assert.IsTrue(wrongHex.IsFailure);
            StringAssert.Contains("same world hex as army", wrongHex.Error.Message);
            Assert.IsTrue(ArmyService.RemoveMember(world, army.ArmyId, a).IsSuccess);
            Assert.IsTrue(ArmyService.DisbandArmy(world, army.ArmyId).IsSuccess);
            Assert.IsFalse(world.Strategic.FormalArmies.TryGet(army.ArmyId, out _));
            Assert.IsTrue(world.WorldPresence.TryGet(b, out var former));
            Assert.AreEqual(PartyWorldPresenceMode.AtWorldPosition, former.Mode);
            Assert.AreEqual(hex, former.DerivedHexFromWorldPosition);
            Assert.AreEqual(expected.X, former.WorldPosX, 0.0001f);
            Assert.AreEqual(expected.Y, former.WorldPosY, 0.0001f);
        }

        [Test]
        public void GarrisonRemainsWorldSiteOnly()
        {
            var wilderness = World();
            var wildHex = new HexCoord(5, 5);
            AddFlag(wilderness, "flag:player", Player, wildHex, 1);
            var wildMember = CharacterAtHex(wilderness, "wild_garrison", Player, wildHex);
            var wildArmy = ArmyService.CreateArmy(wilderness, Player, wildHex, new[] { wildMember }).Value;
            Assert.IsTrue(ArmyService.GarrisonArmy(wilderness, wildArmy.ArmyId).IsFailure);

            var siteWorld = World();
            var site = new WorldSite
            {
                SiteId = "site:garrison",
                OwnerFactionId = Player,
                AnchorHex = new HexCoord(3, 3),
                ControlEstablishedOrder = 1
            };
            WorldSiteRegistrationService.RegisterSiteOnGrid(siteWorld, site);
            StrategicTerritoryCoverageResolver.Rebuild(siteWorld);
            site.EnsurePresenceHexValid();
            var siteMember = CharacterAtSite(siteWorld, "site_garrison", Player, site);
            var siteArmy = ArmyService.CreateArmy(
                siteWorld, Player, site.PresenceHex, new[] { siteMember }).Value;
            Assert.IsTrue(ArmyService.GarrisonArmy(siteWorld, siteArmy.ArmyId).IsSuccess);
        }

        [Test]
        public void TerritoryLoss_BlocksRosterManagementWithoutRemovingArmy()
        {
            var world = World();
            var hex = new HexCoord(5, 5);
            AddFlag(world, "flag:temporary", Player, hex, 1);
            var a = CharacterAtHex(world, "loss_a", Player, hex);
            var b = CharacterAtHex(world, "loss_b", Player, hex);
            var army = ArmyService.CreateArmy(world, Player, hex, new[] { a, b }).Value;

            Assert.IsTrue(FactionFlagService.TryDestroy(world, "flag:temporary").IsSuccess);
            Assert.IsTrue(ArmyService.ChangeLeader(world, army.ArmyId, b).IsFailure);
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(army.ArmyId, out _));
        }

        [Test]
        public void WildernessBackgroundCharacter_RosterShowsHexLabel()
        {
            var world = World();
            var hex = new HexCoord(4, 6);
            var character = CharacterAtHex(world, "label", Player, hex);
            var rows = new List<StrategicCharacterRosterRow>();

            HostStrategicRosterQueries.CollectPlayerCharacters(
                world, Player, new[] { character }, rows);

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(hex.ToString(), rows[0].LocationLabel);
        }
    }
}
