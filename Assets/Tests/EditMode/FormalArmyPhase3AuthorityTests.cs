using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Combat;
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
    public sealed class FormalArmyPhase3AuthorityTests
    {
        const string FactionA = "test:faction_a";
        const float FloatTol = 0.08f;

        static SimulationWorld BuildWorld(out WorldSite siteA, out HexCoord mid)
        {
            var world = new SimulationWorld();
            world.Strategic.PlayerFactionId = FactionA;
            world.HexWorld.FillRectangle(20, 12);
            for (var r = 0; r < 12; r++)
            for (var q = 0; q < 20; q++)
            {
                if (world.HexWorld.TryGetCell(new HexCoord(q, r), out var cell) && cell != null)
                    cell.IsPassable = true;
            }

            var anchor = new HexCoord(3, 4);
            siteA = new WorldSite
            {
                SiteId = "test:site_a",
                DisplayName = "SiteA",
                AnchorHex = anchor,
                PresenceHex = anchor,
                LocalMapId = "test:map",
                OwnerFactionId = FactionA,
            };
            siteA.SetFootprint(new[] { anchor, new HexCoord(4, 4), new HexCoord(3, 5), new HexCoord(4, 5) });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, siteA);
            mid = new HexCoord(10, 4);
            return world;
        }

        static EntityId Spawn(SimulationWorld world, string name)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            created.Value.Get<FactionMembershipComponent>().Assign(FactionA, FactionRoleKind.Member);
            return created.Value.Id;
        }

        [Test]
        public void ActiveCharacterCannotJoinArmy()
        {
            var world = BuildWorld(out var site, out _);
            var active = Spawn(world, "Active");
            var follower = Spawn(world, "Follower");
            world.WorldPresence.SetAtSite(active, site.SiteId);
            world.WorldPresence.SetAtSite(follower, site.SiteId);
            var party = new PlayerPartyRuntime();
            party.TryInitialize(active, out _);
            party.TryAddMember(world, new List<EntityId> { active, follower }, follower, out _);

            var result = ArmyService.CreateArmy(
                world, FactionA, site.SiteId, new List<EntityId> { active, follower }, active, party);
            Assert.IsTrue(result.IsFailure);
        }

        [Test]
        public void FollowerJoinsArmyAfterLeavingPlayerParty()
        {
            var world = BuildWorld(out var site, out _);
            var active = Spawn(world, "Active");
            var follower = Spawn(world, "Follower");
            world.WorldPresence.SetAtSite(active, site.SiteId);
            world.WorldPresence.SetAtSite(follower, site.SiteId);
            var party = new PlayerPartyRuntime();
            party.TryInitialize(active, out _);
            party.TryAddMember(world, new List<EntityId> { active, follower }, follower, out _);

            var result = ArmyService.CreateArmy(
                world, FactionA, site.SiteId, new List<EntityId> { follower }, follower, party);
            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : string.Empty);
            Assert.IsFalse(party.IsMember(follower));
            Assert.IsTrue(ArmyService.TryGetArmyForCharacter(world, follower, out _));
        }

        [Test]
        public void CharactersAtDifferentSitesCannotFormOneArmy()
        {
            var world = BuildWorld(out var siteA, out _);
            var siteBAnchor = new HexCoord(10, 4);
            var siteB = new WorldSite
            {
                SiteId = "test:site_b",
                AnchorHex = siteBAnchor,
                PresenceHex = siteBAnchor,
                OwnerFactionId = FactionA,
            };
            siteB.SetFootprint(new[] { siteBAnchor });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, siteB);

            var a = Spawn(world, "A");
            var b = Spawn(world, "B");
            world.WorldPresence.SetAtSite(a, siteA.SiteId);
            world.WorldPresence.SetAtSite(b, siteB.SiteId);

            var result = ArmyService.CreateArmy(
                world, FactionA, siteA.SiteId, new List<EntityId> { a, b }, a);
            Assert.IsTrue(result.IsFailure);
        }

        [Test]
        public void ArmyCannotDisbandInWilderness()
        {
            var world = BuildWorld(out var site, out var mid);
            var a = Spawn(world, "A");
            world.WorldPresence.SetAtSite(a, site.SiteId);
            var created = ArmyService.CreateArmy(world, FactionA, site.SiteId, new List<EntityId> { a }, a);
            Assert.IsTrue(created.IsSuccess);
            FormalArmyContinuousTravelService.MoveArmyToHex(world, created.Value.ArmyId, mid);
            FormalArmyContinuousTravelService.AdvanceAll(world, 64);

            var disband = ArmyService.DisbandArmy(world, created.Value.ArmyId);
            Assert.IsTrue(disband.IsFailure);
        }

        [Test]
        public void TravelToHexInsideFootprintCanonicalizesToWorldSite()
        {
            var world = BuildWorld(out var siteA, out var mid);
            var siteBAnchor = new HexCoord(10, 4);
            var siteB = new WorldSite
            {
                SiteId = "test:site_b",
                AnchorHex = siteBAnchor,
                PresenceHex = siteBAnchor,
                OwnerFactionId = FactionA,
            };
            siteB.SetFootprint(new[] { siteBAnchor, new HexCoord(11, 4) });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, siteB);

            var a = Spawn(world, "A");
            world.WorldPresence.SetAtSite(a, siteA.SiteId);
            var created = ArmyService.CreateArmy(world, FactionA, siteA.SiteId, new List<EntityId> { a }, a);
            Assert.IsTrue(created.IsSuccess);
            Assert.IsTrue(FormalArmyContinuousTravelService.MoveArmyToHex(
                world, created.Value.ArmyId, siteB.PresenceHex).IsSuccess);
            FormalArmyContinuousTravelService.AdvanceAll(world, 256);

            var army = created.Value;
            Assert.AreEqual(FormalArmyLocationKind.AtWorldSite, army.WorldMotion.LocationKind);
            Assert.AreEqual(siteB.SiteId, army.WorldMotion.SiteId);
        }

        [Test]
        public void ArmyWorldPositionTravelIsContinuous()
        {
            var world = BuildWorld(out var site, out var mid);
            var a = Spawn(world, "A");
            world.WorldPresence.SetAtSite(a, site.SiteId);
            var created = ArmyService.CreateArmy(world, FactionA, site.SiteId, new List<EntityId> { a }, a);
            Assert.IsTrue(created.IsSuccess);
            Assert.IsTrue(FormalArmyContinuousTravelService.MoveArmyToHex(world, created.Value.ArmyId, mid).IsSuccess);

            var before = created.Value.WorldMotion.WorldPosition;
            FormalArmyContinuousTravelService.AdvanceAll(world, 2);
            var after = created.Value.WorldMotion.WorldPosition;
            Assert.Greater(
                StrategicMathDistance(before, after),
                0.01f);
        }

        static float StrategicMathDistance(WorldVec2 a, WorldVec2 b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return (float)System.Math.Sqrt(dx * dx + dy * dy);
        }

        [Test]
        public void DisbandAtFriendlySiteRestoresBackgroundCharacters()
        {
            var world = BuildWorld(out var site, out _);
            var a = Spawn(world, "A");
            var b = Spawn(world, "B");
            world.WorldPresence.SetAtSite(a, site.SiteId);
            world.WorldPresence.SetAtSite(b, site.SiteId);
            var created = ArmyService.CreateArmy(
                world, FactionA, site.SiteId, new List<EntityId> { a, b }, a);
            Assert.IsTrue(created.IsSuccess);
            Assert.IsTrue(ArmyService.DisbandArmy(world, created.Value.ArmyId).IsSuccess);
            Assert.IsFalse(ArmyService.TryGetArmyForCharacter(world, a, out _));
            Assert.IsTrue(world.WorldPresence.TryGet(a, out var pa));
            Assert.AreEqual(PartyWorldPresenceMode.AtSite, pa.Mode);
            Assert.AreEqual(site.SiteId, pa.SiteId);
        }

        [Test]
        public void SaveLoadPreservesMidTravelArmyState()
        {
            var world = BuildWorld(out var site, out var mid);
            var a = Spawn(world, "A");
            world.WorldPresence.SetAtSite(a, site.SiteId);
            var created = ArmyService.CreateArmy(world, FactionA, site.SiteId, new List<EntityId> { a }, a);
            FormalArmyContinuousTravelService.MoveArmyToHex(world, created.Value.ArmyId, mid);
            FormalArmyContinuousTravelService.AdvanceAll(world, 4);
            var dto = StrategicSnapshotHelper.Capture(world);

            var loaded = BuildWorld(out _, out _);
            var a2 = Spawn(loaded, "A");
            dto.FormalArmies[0].MemberCharacterIds[0] = a2.Value;
            StrategicSnapshotHelper.Restore(loaded, dto);

            Assert.IsTrue(loaded.Strategic.FormalArmies.TryGet(dto.FormalArmies[0].ArmyId, out var army));
            Assert.IsTrue(army.WorldMotion.IsMoving || army.WorldMotion.HasPosition);
        }
    }
}
