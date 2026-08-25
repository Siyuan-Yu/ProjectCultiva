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
using XianXia.Data.Serialization;

namespace XianXia.Tests
{
    /// <summary>Guards Strategic Snapshot v6 JSON against DTO drift in JsonSnapshotSerializer.</summary>
    public sealed class StrategicSnapshotJsonV6RoundtripTests
    {
        const string FactionA = "test:faction_a";
        const string FactionB = "test:faction_b";
        const string FactionC = "test:faction_c";
        static readonly HexCoord HexA = Ch01HexPrototypeMapBuilder.HuangcunHex;
        static readonly HexCoord HexB = Ch01HexPrototypeMapBuilder.QingyunLuHex;
        static readonly string SiteA = Ch01HexPrototypeMapBuilder.SiteHuangcun;

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            world.Strategic.PlayerFactionId = FactionA;
            world.Strategic.Ch01FormationScenarioCompat = true;
            HexTestWorldBootstrap.EnsureMinimalHexMap(world);
            return world;
        }

        static EntityId SpawnCharacter(SimulationWorld world, string name, string factionId)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            var entity = created.Value;
            entity.Get<FactionMembershipComponent>().Assign(factionId, FactionRoleKind.Member);
            return entity.Id;
        }

        static SimulationWorld BuildPopulatedWorld(out FormalArmy movingArmy, out EntityId residualId)
        {
            var world = CreateWorld();
            WorldSiteOwnershipService.SetOwner(world, SiteA, FactionB);

            var leader = SpawnCharacter(world, "Leader", FactionA);
            var member = SpawnCharacter(world, "Member", FactionA);
            world.WorldPresence.SetAtSite(leader, SiteA);
            world.WorldPresence.SetAtSite(member, SiteA);

            movingArmy = ArmyService.CreateArmy(world, FactionA, SiteA, new[] { leader, member }).Value;
            FormalArmyTestSupport.SetHexMidTravel(world, movingArmy, HexA, HexB, 0.42f);

            residualId = SpawnCharacter(world, "Downed", FactionA);
            Assert.IsTrue(world.Entities.TryGet(residualId, out var downedEntity));
            CombatDamageRules.EnsureVitals(downedEntity);
            Assert.IsTrue(CombatLifeStateService.TryEnterIncapacitated(world, downedEntity));
            StrategicResidualPresenceService.PlaceCharacterAtResidualHex(
                world, residualId, new HexCoord(HexA.Q + 1, HexA.R));

            WarGateService.DeclareWar(world, FactionA, FactionB);
            world.Strategic.Alliances.RestoreAlliance("alliance:test", new List<string> { FactionA, FactionC });
            world.Strategic.Vassalages.TryBindVassalage(FactionC, FactionA);

            var retreatMember = SpawnCharacter(world, "Retreat", FactionA);
            var retreat = new RetreatingArmy
            {
                RetreatingArmyId = "retreat:test_01",
                SourceArmyId = movingArmy.ArmyId,
                FactionId = FactionA,
                HexQ = HexA.Q,
                HexR = HexA.R
            };
            retreat.SetMembers(new[] { retreatMember });
            world.Strategic.RetreatingArmies.Register(retreat);

            world.Strategic.CaptureObjectives.Register(new CaptureObjectiveState
            {
                ObjectiveId = "capture:test_core",
                SiteId = SiteA,
                WorkAreaId = "work:test_core",
                CurrentHp = 3,
                MaxHp = 10,
                Completed = false
            });

            return world;
        }

        static void BootstrapSitesAndReapplyStrategic(SimulationWorld world, StrategicSnapshotDto strategic)
        {
            HexTestWorldBootstrap.EnsureMinimalHexMap(world);
            StrategicSnapshotHelper.Restore(world, strategic);
        }

        [Test]
        public void SNAP_JSON_V6_01_DeserializeMatchesCaptureStrategic()
        {
            var world = BuildPopulatedWorld(out _, out _);
            var service = new SnapshotService(new JsonSnapshotSerializer());
            var captured = service.Capture(world, new SimulationLoop(world));
            var json = service.CaptureJson(world, new SimulationLoop(world));
            Assert.IsTrue(json.IsSuccess);

            var parsed = new JsonSnapshotSerializer().Deserialize(json.Value);
            Assert.IsTrue(parsed.IsSuccess);
            AssertStrategicEqual(captured.Strategic, parsed.Value.Strategic);
            StringAssert.DoesNotContain("\"nodeId\"", json.Value);
            StringAssert.DoesNotContain("\"routeId\"", json.Value);
            StringAssert.Contains("\"worldSiteOwners\"", json.Value);
            StringAssert.Contains("\"hexPath\"", json.Value);
            StringAssert.Contains("\"residualCharacterPresences\"", json.Value);
            StringAssert.Contains("\"characterWorldPresences\"", json.Value);
        }

        [Test]
        public void SNAP_JSON_V6_02_WorldSiteOwnerRoundtrip()
        {
            var world = CreateWorld();
            const string newOwner = "test:faction_captured";
            WorldSiteOwnershipService.SetOwner(world, SiteA, newOwner);

            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, new SimulationLoop(world));
            Assert.IsTrue(json.IsSuccess);

            var dto = new JsonSnapshotSerializer().Deserialize(json.Value).Value.Strategic;
            Assert.AreEqual(1, dto.WorldSiteOwners.Count);
            Assert.AreEqual(newOwner, dto.WorldSiteOwners[0].OwnerFactionId);

            var restored = service.RestoreJson(json.Value);
            Assert.IsTrue(restored.IsSuccess);
            BootstrapSitesAndReapplyStrategic(restored.Value.world, dto);
            Assert.AreEqual(newOwner, WorldSiteOwnershipService.GetOwner(restored.Value.world, SiteA));
        }

        [Test]
        public void SNAP_JSON_V6_03_ArmyMembershipRoundtrip()
        {
            var world = CreateWorld();
            var a = SpawnCharacter(world, "A", FactionA);
            var b = SpawnCharacter(world, "B", FactionA);
            world.WorldPresence.SetAtSite(a, SiteA);
            world.WorldPresence.SetAtSite(b, SiteA);
            var army = ArmyService.CreateArmy(world, FactionA, SiteA, new[] { a, b }).Value;

            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, new SimulationLoop(world));
            var restored = service.RestoreJson(json.Value);
            Assert.IsTrue(restored.IsSuccess);
            var world2 = restored.Value.world;

            Assert.IsTrue(world2.Strategic.FormalArmies.TryGet(army.ArmyId, out var restoredArmy));
            Assert.AreEqual(a.Value, restoredArmy.LeaderCharacterId.Value);
            Assert.AreEqual(2, restoredArmy.MemberCharacterIds.Count);
            Assert.IsTrue(ArmyService.TryGetArmyForCharacter(world2, a, out var armyForA));
            Assert.IsTrue(ArmyService.TryGetArmyForCharacter(world2, b, out var armyForB));
            Assert.AreEqual(army.ArmyId, armyForA.ArmyId);
            Assert.AreEqual(army.ArmyId, armyForB.ArmyId);
        }

        [Test]
        public void SNAP_JSON_V6_04_HexMovementPathRoundtrip()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "MoveLeader", FactionA);
            world.WorldPresence.SetAtSite(leader, SiteA);
            var army = ArmyService.CreateArmy(world, FactionA, SiteA, new[] { leader }).Value;
            FormalArmyTestSupport.SetHexMidTravel(world, army, HexA, HexB, 0.37f);

            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, new SimulationLoop(world));
            var restored = service.RestoreJson(json.Value);
            Assert.IsTrue(restored.IsSuccess);
            Assert.IsTrue(restored.Value.world.Strategic.FormalArmies.TryGet(army.ArmyId, out var loaded));

            Assert.AreEqual(FormalArmyState.Moving, loaded.State);
            Assert.AreEqual(HexA, loaded.CurrentHex);
            Assert.AreEqual(HexB, loaded.DestinationHex);
            Assert.Greater(loaded.HexPathCount, 1);
            Assert.AreEqual(0.37f, loaded.StepProgress, 0.001f);
        }

        [Test]
        public void SNAP_JSON_V6_05_ResidualRoundtrip()
        {
            var world = CreateWorld();
            var hex = new HexCoord(HexA.Q + 2, HexA.R + 1);
            var id = SpawnCharacter(world, "Residual", FactionA);
            Assert.IsTrue(world.Entities.TryGet(id, out var residualEntity));
            Assert.IsTrue(CombatLifeStateService.TryEnterIncapacitated(world, residualEntity));
            StrategicResidualPresenceService.PlaceCharacterAtResidualHex(world, id, hex);

            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, new SimulationLoop(world));
            var restored = service.RestoreJson(json.Value);
            Assert.IsTrue(restored.IsSuccess);
            var world2 = restored.Value.world;

            Assert.IsTrue(StrategicResidualPresenceService.TryGetResidualHex(world2, id, out var loadedHex));
            Assert.AreEqual(hex, loadedHex);
            Assert.AreEqual(1, StrategicResidualPresentationQuery.Query(world2).Count);
        }

        [Test]
        public void SNAP_JSON_V6_06_CaptureAndDiplomacyRoundtrip()
        {
            var world = BuildPopulatedWorld(out _, out _);
            var service = new SnapshotService(new JsonSnapshotSerializer());
            var json = service.CaptureJson(world, new SimulationLoop(world));
            var dto = new JsonSnapshotSerializer().Deserialize(json.Value).Value.Strategic;

            var restored = service.RestoreJson(json.Value);
            Assert.IsTrue(restored.IsSuccess);
            BootstrapSitesAndReapplyStrategic(restored.Value.world, dto);
            var world2 = restored.Value.world;

            Assert.IsTrue(WarGateService.IsAtWar(world2, FactionA, FactionB));
            Assert.IsTrue(world2.Strategic.Alliances.TryGetAllianceId(FactionA, out _));
            Assert.IsTrue(world2.Strategic.Vassalages.TryGetOverlord(FactionC, out var overlord));
            Assert.AreEqual(FactionA, overlord);
            Assert.IsTrue(world2.Strategic.CaptureObjectives.TryGet("capture:test_core", out var objective));
            Assert.AreEqual(SiteA, objective.SiteId);
            Assert.AreEqual(3, objective.CurrentHp);
            Assert.IsFalse(objective.Completed);
            Assert.IsTrue(world2.Strategic.RetreatingArmies.TryGet("retreat:test_01", out var retreat));
            Assert.AreEqual(HexA.Q, retreat.HexQ);
        }

        static void AssertStrategicEqual(StrategicSnapshotDto expected, StrategicSnapshotDto actual)
        {
            Assert.NotNull(actual);
            Assert.AreEqual(expected.PlayerFactionId, actual.PlayerFactionId);
            Assert.AreEqual(expected.Ch01FormationScenarioCompat, actual.Ch01FormationScenarioCompat);
            Assert.AreEqual(expected.FormalArmies.Count, actual.FormalArmies.Count);
            Assert.AreEqual(expected.ArmyMemberships.Count, actual.ArmyMemberships.Count);
            Assert.AreEqual(expected.ResidualCharacterPresences.Count, actual.ResidualCharacterPresences.Count);
            Assert.AreEqual(expected.CharacterWorldPresences.Count, actual.CharacterWorldPresences.Count);
            Assert.AreEqual(expected.WorldSiteOwners.Count, actual.WorldSiteOwners.Count);
            Assert.AreEqual(expected.Wars.Count, actual.Wars.Count);
            Assert.AreEqual(expected.Alliances.Count, actual.Alliances.Count);
            Assert.AreEqual(expected.Vassalages.Count, actual.Vassalages.Count);
            Assert.AreEqual(expected.RetreatingArmies.Count, actual.RetreatingArmies.Count);
            Assert.AreEqual(expected.CaptureObjectives.Count, actual.CaptureObjectives.Count);

            for (var i = 0; i < expected.FormalArmies.Count; i++)
            {
                var e = expected.FormalArmies[i];
                var a = actual.FormalArmies[i];
                Assert.AreEqual(e.ArmyId, a.ArmyId);
                Assert.AreEqual(e.CurrentHexQ, a.CurrentHexQ);
                Assert.AreEqual(e.CurrentHexR, a.CurrentHexR);
                Assert.AreEqual(e.DestinationHexQ, a.DestinationHexQ);
                Assert.AreEqual(e.DestinationHexR, a.DestinationHexR);
                Assert.AreEqual(e.StepProgress, a.StepProgress, 0.001f);
                Assert.AreEqual(e.HexPath.Count, a.HexPath.Count);
            }
        }
    }
}
