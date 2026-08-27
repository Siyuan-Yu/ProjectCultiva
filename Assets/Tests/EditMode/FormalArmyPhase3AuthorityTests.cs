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
        public void PlayerPartyMemberCannotBeAddedToFormalArmy()
        {
            var world = BuildWorld(out var site, out _);
            var active = Spawn(world, "Active");
            var follower = Spawn(world, "Follower");
            world.WorldPresence.SetAtSite(active, site.SiteId);
            world.WorldPresence.SetAtSite(follower, site.SiteId);
            var party = new PlayerPartyRuntime();
            party.TryInitialize(active, out _);
            party.TryAddMember(world, new List<EntityId> { active, follower }, follower, out _);

            var createFollower = ArmyService.CreateArmy(
                world, FactionA, site.SiteId, new List<EntityId> { follower }, follower, party);
            Assert.IsTrue(createFollower.IsFailure, createFollower.IsFailure ? createFollower.Error.ToString() : string.Empty);
            Assert.IsTrue(party.IsMember(follower), "Follower must remain in PlayerParty.");

            var createActive = ArmyService.CreateArmy(
                world, FactionA, site.SiteId, new List<EntityId> { active }, active, party);
            Assert.IsTrue(createActive.IsFailure, createActive.IsFailure ? createActive.Error.ToString() : string.Empty);
            Assert.IsTrue(party.IsMember(active), "Active must remain in PlayerParty.");
        }

        [Test]
        public void PlayerPartyMemberCannotBeAddedViaAddMember()
        {
            var world = BuildWorld(out var site, out _);
            var active = Spawn(world, "Active");
            var follower = Spawn(world, "Follower");
            var background = Spawn(world, "Background");
            world.WorldPresence.SetAtSite(active, site.SiteId);
            world.WorldPresence.SetAtSite(follower, site.SiteId);
            world.WorldPresence.SetAtSite(background, site.SiteId);
            var party = new PlayerPartyRuntime();
            party.TryInitialize(active, out _);
            party.TryAddMember(world, new List<EntityId> { active, follower }, follower, out _);

            var army = ArmyService.CreateArmy(
                world, FactionA, site.SiteId, new List<EntityId> { background }, background, party).Value;

            var addFollower = ArmyService.AddMember(world, army.ArmyId, follower, party);
            Assert.IsTrue(addFollower.IsFailure, addFollower.IsFailure ? addFollower.Error.ToString() : string.Empty);
            Assert.IsTrue(party.IsMember(follower));
            Assert.IsFalse(ArmyService.TryGetArmyForCharacter(world, follower, out _));
        }

        [Test]
        public void ActiveCharacterCannotJoinArmy_WhenPartyOmittedButActiveIdProvided()
        {
            var world = BuildWorld(out var site, out _);
            var active = Spawn(world, "Active");
            world.WorldPresence.SetAtSite(active, site.SiteId);

            var create = ArmyService.CreateArmy(
                world,
                FactionA,
                site.SiteId,
                new List<EntityId> { active },
                active,
                party: null,
                activeControlledCharacterId: active);
            Assert.IsTrue(create.IsFailure, create.IsFailure ? create.Error.ToString() : string.Empty);

            var follower = Spawn(world, "Follower");
            var background = Spawn(world, "Background");
            world.WorldPresence.SetAtSite(follower, site.SiteId);
            world.WorldPresence.SetAtSite(background, site.SiteId);
            var party = new PlayerPartyRuntime();
            party.TryInitialize(active, out _);
            party.TryAddMember(world, new List<EntityId> { active, follower }, follower, out _);
            var army = ArmyService.CreateArmy(
                world, FactionA, site.SiteId, new List<EntityId> { background }, background, party).Value;

            var addActive = ArmyService.AddMember(
                world, army.ArmyId, active, party: null, activeControlledCharacterId: active);
            Assert.IsTrue(addActive.IsFailure, addActive.IsFailure ? addActive.Error.ToString() : string.Empty);
            Assert.IsTrue(party.IsMember(active), "Active must remain in PlayerParty.");
        }

        [Test]
        public void FormerFollowerCanJoinArmyAfterLeavingPlayerParty()
        {
            var world = BuildWorld(out var site, out _);
            var active = Spawn(world, "Active");
            var follower = Spawn(world, "Follower");
            world.WorldPresence.SetAtSite(active, site.SiteId);
            world.WorldPresence.SetAtSite(follower, site.SiteId);
            var party = new PlayerPartyRuntime();
            party.TryInitialize(active, out _);
            party.TryAddMember(world, new List<EntityId> { active, follower }, follower, out _);

            Assert.IsTrue(party.TryRemoveMember(follower, out _), "Simulate Stop Follow / Leave Party.");

            var result = ArmyService.CreateArmy(
                world, FactionA, site.SiteId, new List<EntityId> { follower }, follower, party);
            Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : string.Empty);
            Assert.IsFalse(party.IsMember(follower));
            Assert.IsTrue(ArmyService.TryGetArmyForCharacter(world, follower, out _));
        }

        [Test]
        public void FormalArmyMemberCannotJoinPlayerParty()
        {
            var world = BuildWorld(out var site, out _);
            var active = Spawn(world, "Active");
            var soldier = Spawn(world, "Soldier");
            world.WorldPresence.SetAtSite(active, site.SiteId);
            world.WorldPresence.SetAtSite(soldier, site.SiteId);
            var party = new PlayerPartyRuntime();
            party.TryInitialize(active, out _);
            var roster = new List<EntityId> { active, soldier };

            var army = ArmyService.CreateArmy(
                world, FactionA, site.SiteId, new List<EntityId> { soldier }, soldier).Value;
            Assert.IsFalse(party.ValidateJoin(world, roster, soldier, out _));
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
        public void CannotCreateArmyAtNonPlayerControlledSite()
        {
            var world = BuildWorld(out _, out _);
            var hostileSite = new WorldSite
            {
                SiteId = "test:site_hostile",
                AnchorHex = new HexCoord(12, 4),
                PresenceHex = new HexCoord(12, 4),
                OwnerFactionId = "test:faction_other",
            };
            hostileSite.SetFootprint(new[] { hostileSite.AnchorHex });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, hostileSite);

            var a = Spawn(world, "A");
            world.WorldPresence.SetAtSite(a, hostileSite.SiteId);
            var result = ArmyService.CreateArmy(world, FactionA, hostileSite.SiteId, new List<EntityId> { a }, a);
            Assert.IsTrue(result.IsFailure);
        }

        [Test]
        public void SiteDepartureToAdjacentWildernessIsNotInstant()
        {
            var world = BuildWorld(out var site, out var mid);
            var adjacent = default(HexCoord);
            var found = false;
            foreach (var hex in site.EnumerateFootprintHexes())
            {
                for (var d = 0; d < 6; d++)
                {
                    var neighbor = HexMath.Neighbor(hex, d);
                    if (site.OccupiesHex(neighbor))
                        continue;
                    if (!world.HexWorld.TryGetTile(neighbor, out var tile) || tile == null || !tile.IsPassable)
                        continue;
                    adjacent = neighbor;
                    found = true;
                    break;
                }

                if (found)
                    break;
            }

            Assert.IsTrue(found, "Need adjacent wilderness hex.");
            var a = Spawn(world, "A");
            world.WorldPresence.SetAtSite(a, site.SiteId);
            var created = ArmyService.CreateArmy(world, FactionA, site.SiteId, new List<EntityId> { a }, a);
            Assert.IsTrue(created.IsSuccess);
            Assert.IsTrue(FormalArmyContinuousTravelService.MoveArmyToHex(
                world, created.Value.ArmyId, adjacent).IsSuccess);
            Assert.IsTrue(created.Value.WorldMotion.IsMoving);
            Assert.AreEqual(FormalArmyLocationKind.AtWorldSite, created.Value.WorldMotion.LocationKind);
            FormalArmyContinuousTravelService.AdvanceAll(world, 1);
            Assert.IsTrue(created.Value.WorldMotion.IsMoving);
            Assert.Greater(created.Value.WorldMotion.SegmentProgress, 0f);
        }

        [Test]
        public void ArmyTravelContinuesAcrossMultipleTicks()
        {
            var world = BuildWorld(out var site, out var mid);
            var a = Spawn(world, "A");
            world.WorldPresence.SetAtSite(a, site.SiteId);
            var created = ArmyService.CreateArmy(world, FactionA, site.SiteId, new List<EntityId> { a }, a);
            FormalArmyContinuousTravelService.MoveArmyToHex(world, created.Value.ArmyId, mid);
            var startSeg = created.Value.WorldMotion.SegmentIndex;
            FormalArmyContinuousTravelService.AdvanceAll(world, 8);
            Assert.IsTrue(created.Value.WorldMotion.IsMoving || created.Value.WorldMotion.LocationKind == FormalArmyLocationKind.AtWorldPosition);
            if (created.Value.WorldMotion.IsMoving)
                Assert.GreaterOrEqual(created.Value.WorldMotion.SegmentIndex, startSeg);
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

        [Test]
        public void CasualtyMemberStaysAtDetachPointWhileArmyContinuesTravel_G17()
        {
            var world = BuildWorld(out var site, out var mid);
            var a = Spawn(world, "A");
            var b = Spawn(world, "B");
            world.WorldPresence.SetAtSite(a, site.SiteId);
            world.WorldPresence.SetAtSite(b, site.SiteId);
            var army = ArmyService.CreateArmy(
                world, FactionA, site.SiteId, new List<EntityId> { a, b }, a).Value;
            Assert.IsTrue(FormalArmyContinuousTravelService.MoveArmyToHex(world, army.ArmyId, mid).IsSuccess);
            FormalArmyContinuousTravelService.AdvanceAll(world, 4);

            Assert.IsTrue(world.Entities.TryGet(b, out var bEnt));
            CombatDamageRules.EnsureVitals(bEnt);
            Assert.IsTrue(CombatLifeStateService.TryEnterIncapacitated(world, bEnt));
            ArmyService.SyncNonLivingMembers(world, army);

            Assert.IsTrue(world.WorldPresence.TryGet(b, out var bPresence));
            var detachHex = bPresence.ResidualHex;
            var armyHexBefore = army.WorldMotion.CurrentHex;
            FormalArmyContinuousTravelService.AdvanceAll(world, 8);

            Assert.IsFalse(army.ContainsMember(b));
            Assert.IsTrue(world.WorldPresence.TryGet(b, out var bAfter));
            Assert.AreEqual(detachHex, bAfter.ResidualHex, "Casualty must stay at detach point.");
            if (army.WorldMotion.IsMoving)
                Assert.AreNotEqual(detachHex, army.WorldMotion.CurrentHex, "Army should continue travel.");
        }
    }
}
