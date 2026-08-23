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
    public sealed class ResidualGroupingPresentationTests
    {
        const string PlayerFaction = StrategicFactionCatalog.PlayerFactionId;
        const string AllyFactionA = "test:ally_a";
        const string AllyFactionB = "test:ally_b";
        const string EnemyFactionA = "test:enemy_a";
        const string EnemyFactionB = "test:enemy_b";
        const string OtherFaction = "test:other";
        const string TestNode = "test:node_a";

        static SimulationWorld CreateHexWorld()
        {
            var world = new SimulationWorld();
            world.Strategic.PlayerFactionId = PlayerFaction;
            world.WorldGraph.RegisterNode(new WorldNodeState
            {
                Id = TestNode,
                Name = "A",
                OwnerId = PlayerFaction,
                WorldX = 0f,
                WorldY = 0f
            });
            HexTestWorldBootstrap.EnsureMinimalHexMap(world);
            return world;
        }

        static EntityId Spawn(SimulationWorld world, string name, string factionId)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            var entity = created.Value;
            entity.Get<FactionMembershipComponent>().Assign(factionId, FactionRoleKind.Member);
            return entity.Id;
        }

        static void Down(SimulationWorld world, EntityId id)
        {
            Assert.IsTrue(world.Entities.TryGet(id, out var entity));
            CombatDamageRules.EnsureVitals(entity);
            if (entity.TryGet<CombatVitalsComponent>(out var vitals))
                vitals.CurrentHp = 0;
            Assert.IsTrue(CombatLifeStateService.TryEnterIncapacitated(world, entity));
        }

        static void KillVisible(SimulationWorld world, EntityId id)
        {
            Assert.IsTrue(world.Entities.TryGet(id, out var entity));
            CombatDamageRules.EnsureVitals(entity);
            Assert.IsTrue(CombatLifeStateService.TryEnterIncapacitated(world, entity));
            Assert.IsTrue(CombatLifeStateService.TryConfirmDeath(world, EntityId.None, entity, out _));
        }

        static void PlaceResidual(SimulationWorld world, EntityId id, HexCoord hex)
        {
            StrategicResidualPresenceService.PlaceCharacterAtResidualHex(world, id, hex);
        }

        [Test]
        public void RESIDUAL_ARMY_01_DetachDownedKeepLiving()
        {
            var world = CreateHexWorld();
            var a = Spawn(world, "A", PlayerFaction);
            var b = Spawn(world, "B", PlayerFaction);
            var c = Spawn(world, "C", PlayerFaction);
            var d = Spawn(world, "D", PlayerFaction);
            var e = Spawn(world, "E", PlayerFaction);
            var army = ArmyService.CreateArmy(world, PlayerFaction, TestNode, new[] { a, b, c, d, e });
            Assert.IsTrue(army.IsSuccess);
            Down(world, a);
            Down(world, b);
            Down(world, c);
            ArmyService.DetachNonLivingMembersAtBattlefield(world, army.Value);

            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(army.Value.ArmyId, out var kept));
            Assert.AreEqual(2, kept.MemberCharacterIds.Count);
            Assert.IsTrue(kept.ContainsMember(d));
            Assert.IsTrue(kept.ContainsMember(e));
            Assert.IsFalse(ArmyService.TryGetArmyForCharacter(world, a, out _));
        }

        [Test]
        public void RESIDUAL_ARMY_02_LeaderDowned_RefreshLeader()
        {
            var world = CreateHexWorld();
            var leader = Spawn(world, "Leader", PlayerFaction);
            var m2 = Spawn(world, "M2", PlayerFaction);
            var army = ArmyService.CreateArmy(world, PlayerFaction, TestNode, new[] { leader, m2 });
            Assert.IsTrue(army.IsSuccess);
            Assert.AreEqual(leader, army.Value.LeaderCharacterId);
            Down(world, leader);
            ArmyService.DetachNonLivingMembersAtBattlefield(world, army.Value);
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(army.Value.ArmyId, out var kept));
            Assert.AreEqual(m2, kept.LeaderCharacterId);
            Assert.IsFalse(kept.ContainsMember(leader));
        }

        [Test]
        public void RESIDUAL_ARMY_03_AllDowned_ForceRemoveArmy()
        {
            var world = CreateHexWorld();
            var a = Spawn(world, "A", PlayerFaction);
            var b = Spawn(world, "B", PlayerFaction);
            var army = ArmyService.CreateArmy(world, PlayerFaction, TestNode, new[] { a, b });
            Assert.IsTrue(army.IsSuccess);
            var armyId = army.Value.ArmyId;
            Down(world, a);
            Down(world, b);
            ArmyService.DetachNonLivingMembersAtBattlefield(world, army.Value);
            Assert.IsFalse(world.Strategic.FormalArmies.TryGet(armyId, out _));
        }

        [Test]
        public void RESIDUAL_GROUP_01_SameHexSelfDowned_OneMarker()
        {
            var world = CreateHexWorld();
            var hex = new HexCoord(2, 2);
            var ids = new[]
            {
                Spawn(world, "A", PlayerFaction),
                Spawn(world, "B", PlayerFaction),
                Spawn(world, "C", PlayerFaction)
            };
            for (var i = 0; i < ids.Length; i++)
            {
                Down(world, ids[i]);
                PlaceResidual(world, ids[i], hex);
            }

            var groups = StrategicResidualPresentationQuery.Query(world);
            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(3, groups[0].Count);
            Assert.AreEqual(StrategicRelationBucket.Self, groups[0].Relation);
            Assert.AreEqual(ResidualStateBucket.Downed, groups[0].State);
        }

        [Test]
        public void RESIDUAL_GROUP_02_SameHex_DeadAndDowned_TwoMarkers()
        {
            var world = CreateHexWorld();
            var hex = new HexCoord(3, 3);
            var d1 = Spawn(world, "D1", PlayerFaction);
            var d2 = Spawn(world, "D2", PlayerFaction);
            var n1 = Spawn(world, "N1", PlayerFaction);
            var n2 = Spawn(world, "N2", PlayerFaction);
            var n3 = Spawn(world, "N3", PlayerFaction);
            KillVisible(world, d1);
            KillVisible(world, d2);
            Down(world, n1);
            Down(world, n2);
            Down(world, n3);
            PlaceResidual(world, d1, hex);
            PlaceResidual(world, d2, hex);
            PlaceResidual(world, n1, hex);
            PlaceResidual(world, n2, hex);
            PlaceResidual(world, n3, hex);

            var groups = StrategicResidualPresentationQuery.Query(world);
            Assert.AreEqual(2, groups.Count);
            ResidualMarkerGroupView dead = null;
            ResidualMarkerGroupView downed = null;
            for (var i = 0; i < groups.Count; i++)
            {
                if (groups[i].State == ResidualStateBucket.Dead)
                    dead = groups[i];
                else
                    downed = groups[i];
            }

            Assert.IsNotNull(dead);
            Assert.IsNotNull(downed);
            Assert.AreEqual(2, dead.Count);
            Assert.AreEqual(3, downed.Count);
        }

        [Test]
        public void RESIDUAL_GROUP_03_DifferentHex_SeparateMarkers()
        {
            var world = CreateHexWorld();
            var a = Spawn(world, "A", PlayerFaction);
            var b = Spawn(world, "B", PlayerFaction);
            KillVisible(world, a);
            KillVisible(world, b);
            PlaceResidual(world, a, new HexCoord(1, 1));
            PlaceResidual(world, b, new HexCoord(4, 4));
            var groups = StrategicResidualPresentationQuery.Query(world);
            Assert.AreEqual(2, groups.Count);
        }

        [Test]
        public void RESIDUAL_GROUP_04_TwoBattlesSameHex_Merge()
        {
            var world = CreateHexWorld();
            var hex = new HexCoord(5, 5);
            var a = Spawn(world, "A", PlayerFaction);
            var b = Spawn(world, "B", PlayerFaction);
            var c = Spawn(world, "C", PlayerFaction);
            KillVisible(world, a);
            KillVisible(world, b);
            KillVisible(world, c);
            PlaceResidual(world, a, hex);
            PlaceResidual(world, b, hex);
            PlaceResidual(world, c, hex);
            var groups = StrategicResidualPresentationQuery.Query(world);
            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(3, groups[0].Count);
        }

        [Test]
        public void RESIDUAL_REL_01_TwoAllyFactions_Merge()
        {
            var world = CreateHexWorld();
            world.Strategic.Alliances.Clear();
            world.Strategic.Alliances.RestoreAlliance(
                "alliance:test",
                new List<string> { PlayerFaction, AllyFactionA, AllyFactionB });

            var hex = new HexCoord(2, 4);
            var a = Spawn(world, "AllyA1", AllyFactionA);
            var b = Spawn(world, "AllyA2", AllyFactionA);
            var c = Spawn(world, "AllyB1", AllyFactionB);
            var d = Spawn(world, "AllyB2", AllyFactionB);
            var e = Spawn(world, "AllyB3", AllyFactionB);
            KillVisible(world, a);
            KillVisible(world, b);
            KillVisible(world, c);
            KillVisible(world, d);
            KillVisible(world, e);
            PlaceResidual(world, a, hex);
            PlaceResidual(world, b, hex);
            PlaceResidual(world, c, hex);
            PlaceResidual(world, d, hex);
            PlaceResidual(world, e, hex);

            var groups = StrategicResidualPresentationQuery.Query(world);
            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(StrategicRelationBucket.Ally, groups[0].Relation);
            Assert.AreEqual(5, groups[0].Count);
        }

        [Test]
        public void RESIDUAL_REL_02_TwoEnemyFactions_Merge()
        {
            var world = CreateHexWorld();
            Assert.IsTrue(WarGateService.DeclareWar(world, PlayerFaction, EnemyFactionA).IsSuccess);
            Assert.IsTrue(WarGateService.DeclareWar(world, PlayerFaction, EnemyFactionB).IsSuccess);
            var hex = new HexCoord(6, 1);
            var a = Spawn(world, "E1", EnemyFactionA);
            var b = Spawn(world, "E2", EnemyFactionB);
            KillVisible(world, a);
            KillVisible(world, b);
            PlaceResidual(world, a, hex);
            PlaceResidual(world, b, hex);
            var groups = StrategicResidualPresentationQuery.Query(world);
            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(StrategicRelationBucket.Enemy, groups[0].Relation);
            Assert.AreEqual(2, groups[0].Count);
        }

        [Test]
        public void RESIDUAL_REL_03_PeaceMakesEnemyBecomeOther()
        {
            var world = CreateHexWorld();
            Assert.IsTrue(WarGateService.DeclareWar(world, PlayerFaction, EnemyFactionA).IsSuccess);
            var hex = new HexCoord(7, 2);
            var a = Spawn(world, "E1", EnemyFactionA);
            KillVisible(world, a);
            PlaceResidual(world, a, hex);
            var before = StrategicResidualPresentationQuery.Query(world);
            Assert.AreEqual(StrategicRelationBucket.Enemy, before[0].Relation);

            world.Strategic.Wars.Clear();
            world.Strategic.Diplomacy.SetStance(PlayerFaction, EnemyFactionA, FactionStance.Neutral);
            var after = StrategicResidualPresentationQuery.Query(world);
            Assert.AreEqual(1, after.Count);
            Assert.AreEqual(StrategicRelationBucket.Other, after[0].Relation);
        }

        [Test]
        public void RESIDUAL_LIFE_01_DownedToDead_Regroup()
        {
            var world = CreateHexWorld();
            var hex = new HexCoord(1, 5);
            var id = Spawn(world, "Hero", PlayerFaction);
            Down(world, id);
            PlaceResidual(world, id, hex);
            var before = StrategicResidualPresentationQuery.Query(world);
            Assert.AreEqual(ResidualStateBucket.Downed, before[0].State);

            Assert.IsTrue(world.Entities.TryGet(id, out var ent));
            Assert.IsTrue(CombatLifeStateService.TryConfirmDeath(world, EntityId.None, ent, out _));
            var after = StrategicResidualPresentationQuery.Query(world);
            Assert.AreEqual(1, after.Count);
            Assert.AreEqual(ResidualStateBucket.Dead, after[0].State);
        }

        [Test]
        public void RESIDUAL_LIFE_02_DownedToAlive_LeavesResidual()
        {
            var world = CreateHexWorld();
            var hex = new HexCoord(2, 5);
            var id = Spawn(world, "Hero", PlayerFaction);
            Down(world, id);
            PlaceResidual(world, id, hex);
            Assert.AreEqual(1, StrategicResidualPresentationQuery.Query(world).Count);

            Assert.IsTrue(world.Entities.TryGet(id, out var ent));
            Assert.IsTrue(CombatLifeStateService.TryRecoverFromIncapacitated(world, ent));
            Assert.AreEqual(0, StrategicResidualPresentationQuery.Query(world).Count);
        }

        [Test]
        public void RESIDUAL_LIFE_03_CorpseRemoved_LeavesResidual()
        {
            var world = CreateHexWorld();
            var hex = new HexCoord(3, 5);
            var id = Spawn(world, "Dead", PlayerFaction);
            KillVisible(world, id);
            PlaceResidual(world, id, hex);
            Assert.AreEqual(1, StrategicResidualPresentationQuery.Query(world).Count);

            Assert.IsTrue(world.Entities.TryGet(id, out var ent));
            CombatLifeStateService.FinalizeRemoval(world, ent);
            Assert.AreEqual(0, StrategicResidualPresentationQuery.Query(world).Count);
        }

        [Test]
        public void RESIDUAL_SAVE_01_DownedHexRoundTrip()
        {
            var world = CreateHexWorld();
            var hex = new HexCoord(4, 4);
            var id = Spawn(world, "SaveDown", PlayerFaction);
            Down(world, id);
            PlaceResidual(world, id, hex);

            var service = new SnapshotService(new XianXia.Data.Serialization.JsonSnapshotSerializer());
            var snap = service.Capture(world, new SimulationLoop(world));
            Assert.AreEqual(WorldSnapshot.CurrentSchemaVersion, snap.SchemaVersion);
            Assert.IsNotNull(snap.Strategic.ResidualCharacterPresences);
            Assert.GreaterOrEqual(snap.Strategic.ResidualCharacterPresences.Count, 1);

            var restored = service.Restore(snap);
            Assert.IsTrue(restored.IsSuccess);
            var world2 = restored.Value.world;
            Assert.IsTrue(StrategicResidualPresenceService.TryGetResidualHex(world2, id, out var loaded));
            Assert.AreEqual(hex, loaded);
            var groups = StrategicResidualPresentationQuery.Query(world2);
            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(ResidualStateBucket.Downed, groups[0].State);
        }

        [Test]
        public void RESIDUAL_SAVE_02_CorpseHexRoundTrip()
        {
            var world = CreateHexWorld();
            var hex = new HexCoord(5, 3);
            var id = Spawn(world, "SaveDead", PlayerFaction);
            KillVisible(world, id);
            PlaceResidual(world, id, hex);
            var service = new SnapshotService(new XianXia.Data.Serialization.JsonSnapshotSerializer());
            var snap = service.Capture(world, new SimulationLoop(world));
            var restored = service.Restore(snap);
            Assert.IsTrue(restored.IsSuccess);
            var world2 = restored.Value.world;
            Assert.IsTrue(StrategicResidualPresenceService.TryGetResidualHex(world2, id, out var loaded));
            Assert.AreEqual(hex, loaded);
            Assert.AreEqual(ResidualStateBucket.Dead, StrategicResidualPresentationQuery.Query(world2)[0].State);
        }

        [Test]
        public void RESIDUAL_SAVE_03_SnapshotDoesNotPersistRelationOrGroup()
        {
            var world = CreateHexWorld();
            var id = Spawn(world, "X", PlayerFaction);
            Down(world, id);
            PlaceResidual(world, id, new HexCoord(1, 1));
            var dto = StrategicSnapshotHelper.Capture(world);
            Assert.AreEqual(1, dto.ResidualCharacterPresences.Count);
            var row = dto.ResidualCharacterPresences[0];
            Assert.AreEqual(id.Value, row.CharacterId);
            Assert.AreEqual(1, row.HexQ);
            Assert.AreEqual(1, row.HexR);
            // DTO surface: only CharacterId + Hex — no Relation / Group / Count fields.
            Assert.IsNotNull(typeof(ResidualCharacterPresenceDto).GetProperty("CharacterId"));
            Assert.IsNotNull(typeof(ResidualCharacterPresenceDto).GetProperty("HexQ"));
            Assert.IsNull(typeof(ResidualCharacterPresenceDto).GetProperty("RelationBucket"));
            Assert.IsNull(typeof(ResidualCharacterPresenceDto).GetProperty("Count"));
        }

        [Test]
        public void RESIDUAL_SAVE_04_LoadDoesNotRejoinFormalArmy()
        {
            var world = CreateHexWorld();
            var a = Spawn(world, "A", PlayerFaction);
            var b = Spawn(world, "B", PlayerFaction);
            var army = ArmyService.CreateArmy(world, PlayerFaction, TestNode, new[] { a, b });
            Assert.IsTrue(army.IsSuccess);
            Down(world, a);
            ArmyService.DetachNonLivingMembersAtBattlefield(world, army.Value);
            PlaceResidual(world, a, new HexCoord(2, 2));
            Assert.IsFalse(ArmyService.TryGetArmyForCharacter(world, a, out _));

            var service = new SnapshotService(new XianXia.Data.Serialization.JsonSnapshotSerializer());
            var snap = service.Capture(world, new SimulationLoop(world));
            var restored = service.Restore(snap);
            Assert.IsTrue(restored.IsSuccess);
            Assert.IsFalse(ArmyService.TryGetArmyForCharacter(restored.Value.world, a, out _));
        }

        [Test]
        public void RESIDUAL_HEX_PlaceUsesAtHexNotNodeRoute()
        {
            var world = CreateHexWorld();
            var id = Spawn(world, "HexOnly", PlayerFaction);
            Down(world, id);
            var hex = Ch01HexPrototypeMapBuilder.HuangcunHex;
            var snap = new BattleParticipantSnapshot
            {
                BattleAnchorHexQ = hex.Q,
                BattleAnchorHexR = hex.R,
                BattleAnchorNodeId = TestNode,
                BattleAnchorRouteId = "should_not_use",
                BattleAnchorProgress = 0.5f
            };
            StrategicEncounterResolveService.PlaceAtBattleAnchor(
                world, world.WorldPresence.GetOrCreate(id), snap);
            Assert.IsTrue(world.WorldPresence.TryGet(id, out var wp));
            Assert.AreEqual(PartyWorldPresenceMode.AtHex, wp.Mode);
            Assert.IsTrue(string.IsNullOrEmpty(wp.RouteId));
            Assert.IsFalse(ArmyWorldMapPresentation.ShouldDrawIndependentCharacterPortrait(world, id));
        }

        [Test]
        public void RESIDUAL_UI_05_VisualPriorityOrder()
        {
            Assert.Greater(
                ResidualMarkerGroupView.ComputeVisualPriority(
                    StrategicRelationBucket.Self, ResidualStateBucket.Dead),
                ResidualMarkerGroupView.ComputeVisualPriority(
                    StrategicRelationBucket.Self, ResidualStateBucket.Downed));
            Assert.Greater(
                ResidualMarkerGroupView.ComputeVisualPriority(
                    StrategicRelationBucket.Ally, ResidualStateBucket.Dead),
                ResidualMarkerGroupView.ComputeVisualPriority(
                    StrategicRelationBucket.Other, ResidualStateBucket.Dead));
            Assert.Greater(
                ResidualMarkerGroupView.ComputeVisualPriority(
                    StrategicRelationBucket.Other, ResidualStateBucket.Downed),
                ResidualMarkerGroupView.ComputeVisualPriority(
                    StrategicRelationBucket.Enemy, ResidualStateBucket.Dead));
        }
    }
}
