using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Data.Serialization;
using XianXia.Data.Content;

namespace XianXia.Tests
{
    public sealed class SnapshotContinuityBugfixTests
    {
        const string Player = "test:player";
        const float Tol = 0.0001f;

        static SimulationWorld World()
        {
            var world = new SimulationWorld();
            world.Strategic.PlayerFactionId = Player;
            world.HexWorld.MapId = "test:snapshot_continuity";
            world.HexWorld.HexSize = 1f;
            world.HexWorld.FillRectangle(12, 12, HexTerrainType.Plain);
            for (var r = 0; r < 12; r++)
            for (var q = 0; q < 12; q++)
                if (world.HexWorld.TryGetCell(new HexCoord(q, r), out var cell) && cell != null)
                    cell.IsPassable = true;
            return world;
        }

        static EntityId Character(SimulationWorld world, string name, HexCoord hex, bool npc = false)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            created.Value.Get<FactionMembershipComponent>().Assign(Player, FactionRoleKind.Member);
            if (npc) created.Value.Tags |= EntityTag.Npc;
            HexMath.ToWorldPosition(hex, 1f, out var x, out var y);
            world.WorldPresence.SetAtWorldPosition(created.Value.Id, new WorldVec2(x + .17f, y - .09f), hex);
            return created.Value.Id;
        }

        static void AddFlag(SimulationWorld world, string id, string faction, HexCoord hex, long order,
            int hp = 100, bool local = false, float x = 0f, float z = 0f)
        {
            Assert.IsTrue(world.Strategic.FactionFlags.Register(new FactionFlagState
            {
                FlagId = id, FactionId = faction, AnchorHex = hex, EstablishedOrder = order,
                CurrentHp = hp, MaxHp = 100, HasLocalPosition = local, LocalX = x, LocalZ = z
            }));
        }

        static WorldSite AddSite(SimulationWorld world)
        {
            var site = new WorldSite
            {
                SiteId = "test:site", DisplayName = "Site", OwnerFactionId = Player,
                AnchorHex = new HexCoord(3, 3), PresenceHex = new HexCoord(3, 3),
                LocalMapId = "test:site_map", ControlEstablishedOrder = 1
            };
            site.SetFootprint(new[] { site.AnchorHex });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, site);
            StrategicTerritoryCoverageResolver.Rebuild(world);
            return site;
        }

        [Test]
        public void FactionFlagActiveSetAndEffectiveControllersRoundTripExactly()
        {
            var world = World();
            AddFlag(world, "flag:authored_a", "test:a", new HexCoord(3, 3), 1);
            AddFlag(world, "flag:authored_b", "test:b", new HexCoord(8, 8), 2);
            Assert.IsTrue(world.Strategic.FactionFlags.Remove("flag:authored_b"));
            AddFlag(world, "flag:player_1", Player, new HexCoord(4, 3), 3, 61, true, 2.5f, 4.75f);
            AddFlag(world, "flag:player_2", Player, new HexCoord(7, 7), 4);
            StrategicTerritoryCoverageResolver.Rebuild(world);

            var beforeControllers = new Dictionary<HexCoord, string>();
            for (var r = 0; r < 12; r++)
            for (var q = 0; q < 12; q++)
            {
                var h = new HexCoord(q, r);
                beforeControllers[h] = TerritoryControlService.GetController(world, h);
            }
            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, new SimulationLoop(world));
            Assert.IsTrue(json.IsSuccess);
            var parsed = new JsonSnapshotSerializer().Deserialize(json.Value);
            Assert.IsTrue(parsed.IsSuccess);
            CollectionAssert.AreEquivalent(
                new[] { "flag:authored_a", "flag:player_1", "flag:player_2" },
                parsed.Value.Strategic.FactionFlags.ConvertAll(f => f.FlagId));

            var restored = service.RestoreJson(json.Value);
            Assert.IsTrue(restored.IsSuccess);
            var loaded = restored.Value.world;
            loaded.HexWorld.MapId = world.HexWorld.MapId;
            loaded.HexWorld.HexSize = 1f;
            loaded.HexWorld.FillRectangle(12, 12, HexTerrainType.Plain);
            Assert.IsTrue(StrategicSnapshotHelper.RestoreHexPoliticalState(loaded, parsed.Value.Strategic).IsSuccess);
            CollectionAssert.AreEquivalent(
                new[] { "flag:authored_a", "flag:player_1", "flag:player_2" },
                loaded.Strategic.FactionFlags.Flags.Keys);
            Assert.IsFalse(loaded.Strategic.FactionFlags.Flags.ContainsKey("flag:authored_b"));
            var p1 = loaded.Strategic.FactionFlags.Flags["flag:player_1"];
            Assert.AreEqual(Player, p1.FactionId);
            Assert.AreEqual(new HexCoord(4, 3), p1.AnchorHex);
            Assert.AreEqual(3, p1.EstablishedOrder);
            Assert.AreEqual(61, p1.CurrentHp);
            Assert.IsTrue(p1.HasLocalPosition);
            Assert.AreEqual(2.5f, p1.LocalX, Tol);
            Assert.AreEqual(4.75f, p1.LocalZ, Tol);
            foreach (var pair in beforeControllers)
                Assert.AreEqual(pair.Value, TerritoryControlService.GetController(loaded, pair.Key), pair.Key.ToString());
        }

        [Test]
        public void WildernessDisbandNpcMembersRoundTripAndMaterializeWithoutArmyResurrection()
        {
            var world = World();
            var hex = new HexCoord(5, 5);
            AddFlag(world, "flag:player", Player, hex, 1);
            StrategicTerritoryCoverageResolver.Rebuild(world);
            var a = Character(world, "npc_a", hex, true);
            var b = Character(world, "npc_b", hex, true);
            var army = ArmyService.CreateArmy(world, Player, hex, new[] { a, b }, a).Value;
            var exact = army.WorldMotion.WorldPosition;
            var oldArmyId = army.ArmyId;
            Assert.IsTrue(ArmyService.DisbandArmy(world, oldArmyId).IsSuccess);
            Assert.IsFalse(world.Strategic.FormalArmies.TryGet(oldArmyId, out _));

            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, new SimulationLoop(world));
            var parsed = new JsonSnapshotSerializer().Deserialize(json.Value);
            Assert.IsTrue(parsed.IsSuccess);
            Assert.IsFalse(parsed.Value.Strategic.FormalArmies.Exists(x => x.ArmyId == oldArmyId));
            Assert.IsFalse(parsed.Value.Strategic.ArmyMemberships.Exists(x => x.ArmyId == oldArmyId));
            var restored = service.RestoreJson(json.Value);
            Assert.IsTrue(restored.IsSuccess);
            var loaded = restored.Value.world;
            loaded.HexWorld.MapId = world.HexWorld.MapId;
            loaded.HexWorld.HexSize = 1f;
            loaded.HexWorld.FillRectangle(12, 12, HexTerrainType.Plain);
            Assert.IsFalse(loaded.Strategic.FormalArmies.TryGet(oldArmyId, out _));
            foreach (var id in new[] { a, b })
            {
                Assert.IsFalse(ArmyService.TryGetArmyForCharacter(loaded, id, out _));
                Assert.IsTrue(loaded.WorldPresence.TryGet(id, out var p));
                Assert.AreEqual(exact.X, p.WorldPosX, Tol);
                Assert.AreEqual(exact.Y, p.WorldPosY, Tol);
                Assert.AreEqual(hex, p.DerivedHexFromWorldPosition);
            }

            Assert.IsTrue(WildernessLocalMapFallback.TryResolve(loaded, hex, out var mapId));
            loaded.LocalMap.ActiveMapLayoutId = mapId;
            loaded.LocalMap.OverworldMapLayoutId = mapId;
            loaded.LocalMap.SetPlayableBounds(0f, 0f, 1f, 16, 16);
            loaded.PartyWorld.LocalMapId = mapId;
            loaded.PartyWorld.Mode = PartyWorldPresenceMode.AtHex;
            HexMath.ToWorldPosition(hex, 1f, out var partyX, out var partyY);
            loaded.PlayerPartyTravel.SetAtWorldPosition(new WorldVec2(partyX, partyY), hex);
            var materialized = LoadedDestinationArrivalMaterializer.MaterializeEligibleWildernessCharactersOnLocalMap(
                loaded, null, WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(0f, 0f, 1f, 16, 16));
            Assert.AreEqual(2, materialized);
            Assert.IsTrue(loaded.LocalMap.ContainsOccupant(a));
            Assert.IsTrue(loaded.LocalMap.ContainsOccupant(b));
        }

        [Test]
        public void FormalArmySiteAndWorldPositionRestoreInStageTwoExactly()
        {
            var world = World();
            var site = AddSite(world);
            var atSite = Character(world, "at_site", site.AnchorHex);
            world.WorldPresence.SetAtSite(atSite, site.SiteId);
            var siteArmy = ArmyService.CreateArmy(world, Player, site.SiteId, new[] { atSite }, atSite).Value;
            var wildHex = new HexCoord(5, 5);
            AddFlag(world, "flag:wild", Player, wildHex, 2);
            StrategicTerritoryCoverageResolver.Rebuild(world);
            var atWild = Character(world, "at_wild", wildHex);
            var wildArmy = ArmyService.CreateArmy(world, Player, wildHex, new[] { atWild }, atWild).Value;
            var exact = new WorldVec2(0f, 0f);
            FormalArmyContinuousTravelService.InitializeAtWorldPosition(world, wildArmy, exact, wildHex);

            var dto = StrategicSnapshotHelper.Capture(world);
            var loaded = new SimulationWorld(world.Entities);
            loaded.HexWorld.MapId = world.HexWorld.MapId;
            loaded.HexWorld.HexSize = 1f;
            loaded.HexWorld.FillRectangle(12, 12, HexTerrainType.Plain);
            AddSite(loaded);
            Assert.IsTrue(StrategicSnapshotHelper.Restore(loaded, dto).IsSuccess);
            Assert.IsTrue(StrategicSnapshotHelper.RestoreFormalArmyMotions(loaded, dto).IsSuccess);
            Assert.IsTrue(loaded.Strategic.FormalArmies.TryGet(siteArmy.ArmyId, out var loadedSite));
            Assert.AreEqual(FormalArmyLocationKind.AtWorldSite, loadedSite.WorldMotion.LocationKind);
            Assert.AreEqual(site.SiteId, loadedSite.WorldMotion.SiteId);
            Assert.IsTrue(loaded.Strategic.FormalArmies.TryGet(wildArmy.ArmyId, out var loadedWild));
            Assert.AreEqual(FormalArmyLocationKind.AtWorldPosition, loadedWild.WorldMotion.LocationKind);
            Assert.AreEqual(0f, loadedWild.WorldMotion.WorldPosition.X, Tol);
            Assert.AreEqual(0f, loadedWild.WorldMotion.WorldPosition.Y, Tol);
            Assert.AreEqual(wildHex, loadedWild.WorldMotion.CurrentHex);
        }

        [Test]
        public void SiteDeparturePendingMotionRoundTripsExactly()
        {
            var world = World();
            var site = AddSite(world);
            var member = Character(world, "departing", site.AnchorHex);
            world.WorldPresence.SetAtSite(member, site.SiteId);
            var army = ArmyService.CreateArmy(world, Player, site.SiteId, new[] { member }, member).Value;
            var exit = new HexCoord(4, 3);
            var destination = new HexCoord(7, 3);
            var path = new[] { site.AnchorHex, exit, new HexCoord(5, 3), new HexCoord(6, 3), destination };
            var virtualPos = new WorldVec2(3.21f, 4.32f);
            var boundary = new WorldVec2(3.91f, 4.76f);
            army.WorldMotion.BeginSiteDepartureTravel(
                FormalArmyOrderKind.TravelToHex, path, destination, string.Empty,
                site.AnchorHex, exit, virtualPos, boundary, HexTravelMode.Ground);
            army.WorldMotion.SetSegment(0, .43f);
            army.WorldMotion.SetSiteDepartureVirtualPosition(new WorldVec2(3.44f, 4.55f));

            var dto = StrategicSnapshotHelper.Capture(world);
            var loaded = new SimulationWorld(world.Entities);
            loaded.HexWorld.MapId = world.HexWorld.MapId;
            loaded.HexWorld.HexSize = 1f;
            loaded.HexWorld.FillRectangle(12, 12, HexTerrainType.Plain);
            AddSite(loaded);
            Assert.IsTrue(StrategicSnapshotHelper.Restore(loaded, dto).IsSuccess);
            Assert.IsTrue(StrategicSnapshotHelper.RestoreFormalArmyMotions(loaded, dto).IsSuccess);
            Assert.IsTrue(loaded.Strategic.FormalArmies.TryGet(army.ArmyId, out var restored));
            var motion = restored.WorldMotion;
            Assert.IsTrue(motion.IsSiteDeparturePending);
            Assert.AreEqual(.43f, motion.SegmentProgress, Tol);
            Assert.AreEqual(3.44f, motion.SiteDepartureVirtualPosition.X, Tol);
            Assert.AreEqual(4.55f, motion.SiteDepartureVirtualPosition.Y, Tol);
            Assert.AreEqual(boundary.X, motion.SiteDepartureBoundaryEntry.X, Tol);
            Assert.AreEqual(boundary.Y, motion.SiteDepartureBoundaryEntry.Y, Tol);
            Assert.AreEqual(site.AnchorHex, motion.SiteDepartureFootprintHex);
            Assert.AreEqual(exit, motion.SiteDepartureExitHex);
            Assert.AreEqual(HexTravelMode.Ground, motion.TravelMode);
        }

        [Test]
        public void PlacementAuthorizationRemainsVisibleGateAndExplainsExistingBuilding()
        {
            var world = World();
            var hex = new HexCoord(5, 5);
            AddFlag(world, "flag:enemy", "test:enemy", hex, 1);
            var result = FactionFlagPlacementAuthorization.CanBeginPlacement(world, Player, hex, out _);
            Assert.IsTrue(FactionFlagPlacementAuthorization.AlwaysHasPlacementTool);
            Assert.IsTrue(result.IsFailure);
            StringAssert.Contains("需要先移除当前控制建筑", result.Error.Message);
        }

        [Test]
        public void InvalidFactionFlagSnapshotDoesNotPartiallyReplaceBoard()
        {
            var world = World();
            AddFlag(world, "flag:existing", Player, new HexCoord(2, 2), 1);
            var dto = new StrategicSnapshotDto { HasFactionFlagSnapshotAuthority = true };
            dto.FactionFlags.Add(new FactionFlagSnapshotDto
            {
                FlagId = "flag:new_a", FactionId = Player, AnchorQ = 4, AnchorR = 4,
                EstablishedOrder = 2, CurrentHp = 100, MaxHp = 100
            });
            dto.FactionFlags.Add(new FactionFlagSnapshotDto
            {
                FlagId = "flag:new_b", FactionId = Player, AnchorQ = 4, AnchorR = 4,
                EstablishedOrder = 3, CurrentHp = 100, MaxHp = 100
            });
            var result = FactionFlagSnapshotRestore.TryApplyAuthoritativeSet(world, dto);
            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual(1, world.Strategic.FactionFlags.Flags.Count);
            Assert.IsTrue(world.Strategic.FactionFlags.Flags.ContainsKey("flag:existing"));
        }

        [Test]
        public void InvalidHexContentPreflightLeavesTargetWorldUnchanged()
        {
            var world = World();
            world.HexWorld.MapId = "unchanged";
            AddFlag(world, "flag:existing", Player, new HexCoord(2, 2), 1);
            var definition = new HexWorldContentDefinition
            {
                Id = new DefinitionId("test", "invalid_hex"), Width = 6, Height = 6,
                DefaultTerrain = "Plain", DefaultPassable = true
            };
            definition.FactionFlags.Add(new FactionFlagContentDefinition
            {
                FlagId = "flag:a", FactionId = Player, AnchorQ = 2, AnchorR = 2, EstablishedOrder = 5
            });
            definition.FactionFlags.Add(new FactionFlagContentDefinition
            {
                FlagId = "flag:b", FactionId = Player, AnchorQ = 3, AnchorR = 3, EstablishedOrder = 5
            });
            var result = HexWorldContentLoader.Apply(world, definition);
            Assert.IsTrue(result.IsFailure);
            Assert.AreEqual("unchanged", world.HexWorld.MapId);
            Assert.AreEqual(12, world.HexWorld.Width);
            Assert.IsTrue(world.Strategic.FactionFlags.Flags.ContainsKey("flag:existing"));
        }
    }
}
