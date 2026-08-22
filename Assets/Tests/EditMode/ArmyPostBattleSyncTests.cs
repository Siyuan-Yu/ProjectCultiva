using System;
using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    public sealed class ArmyPostBattleSyncTests
    {
        const string TestFactionA = "test:faction_a";
        const string TestNodeA = "test:node_a";
        const string TestNodeB = "test:node_b";
        const string TestRoute = "test:route_ab";

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            world.WorldGraph.RegisterNode(new WorldNodeState
            {
                Id = TestNodeA,
                Name = "A",
                OwnerId = TestFactionA,
                WorldX = 0f,
                WorldY = 0f
            });
            world.WorldGraph.RegisterNode(new WorldNodeState
            {
                Id = TestNodeB,
                Name = "B",
                OwnerId = TestFactionA,
                WorldX = 10f,
                WorldY = 0f
            });
            world.WorldGraph.RegisterRoute(new WorldRouteState
            {
                Id = TestRoute,
                FromNodeId = TestNodeA,
                ToNodeId = TestNodeB,
                TravelCost = 1
            });
            return world;
        }

        static EntityId SpawnCharacter(SimulationWorld world, string name, string nodeId)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            var entity = created.Value;
            entity.Get<FactionMembershipComponent>().Assign(TestFactionA, FactionRoleKind.Member);
            world.WorldPresence.SetAtNode(entity.Id, nodeId);
            return entity.Id;
        }

        static void EnterIncapacitated(SimulationWorld world, EntityId id)
        {
            Assert.IsTrue(world.Entities.TryGet(id, out var entity));
            CombatDamageRules.EnsureVitals(entity);
            if (entity.TryGet<CombatVitalsComponent>(out var vitals))
                vitals.CurrentHp = 0;
            Assert.IsTrue(CombatLifeStateService.TryEnterIncapacitated(world, entity));
        }

        static BattleParticipantSnapshot BuildSnap(string armyId, float anchorProgress)
        {
            return new BattleParticipantSnapshot
            {
                AttackerArmyId = armyId,
                BattleAnchorNodeId = TestNodeA,
                BattleAnchorRouteId = TestRoute,
                BattleAnchorDestNodeId = TestNodeB,
                BattleAnchorProgress = anchorProgress
            };
        }

        [Test]
        public void PostBattleSync_MoveNearRouteDest_StillRequiresTravelTicks()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { leader });
            Assert.IsTrue(armyResult.IsSuccess);
            var armyId = armyResult.Value.ArmyId;
            var snap = BuildSnap(armyId, 0.995f);

            ArmyPostBattleSyncService.SyncAttackerArmyAfterBattle(world, snap);
            Assert.IsTrue(ArmyTravelCommandService.MoveArmyToNode(world, armyId, TestNodeB).IsSuccess);
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(armyId, out var army));
            Assert.IsTrue(army.IsTraveling);
            Assert.GreaterOrEqual(army.RemainingTravelTicks, 8);
        }

        [Test]
        public void MoveArmyToRouteProgress_StartsTravelInsteadOfInstantAnchor()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { leader });
            Assert.IsTrue(armyResult.IsSuccess);
            var armyId = armyResult.Value.ArmyId;
            var snap = BuildSnap(armyId, 0.25f);

            ArmyPostBattleSyncService.SyncAttackerArmyAfterBattle(world, snap);
            Assert.IsTrue(
                ArmyTravelCommandService.MoveArmyToRouteProgress(world, armyId, TestRoute, 0.75f).IsSuccess);
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(armyId, out var army));
            Assert.IsTrue(army.IsTraveling);
            Assert.GreaterOrEqual(army.RemainingTravelTicks, 8);
            Assert.AreEqual(0.25f, army.RouteSegmentOriginProgress, 0.001f);
            Assert.AreEqual(0.75f, army.RouteSegmentEndProgress, 0.001f);
        }

        [Test]
        public void PostBattleSync_PreservesRouteAnchorWithoutReconcileDowngrade()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { leader });
            Assert.IsTrue(armyResult.IsSuccess);
            var armyId = armyResult.Value.ArmyId;
            var snap = BuildSnap(armyId, 0.48f);

            StrategicEncounterResolveService.PlaceAtBattleAnchor(
                world, world.WorldPresence.GetOrCreate(leader), snap);
            ArmyPostBattleSyncService.SyncAttackerArmyAfterBattle(world, snap);

            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(armyId, out var army));
            Assert.IsTrue(army.IsRouteAnchored);
            Assert.AreEqual(0.48f, army.RouteAnchorProgress, 0.001f);
        }

        [Test]
        public void RouteAnchor_MoveToDestEndpoint_FromNearDest_TravelForwardNotBacktrack()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { leader });
            Assert.IsTrue(armyResult.IsSuccess);
            var armyId = armyResult.Value.ArmyId;
            var snap = BuildSnap(armyId, 0.92f);

            ArmyPostBattleSyncService.SyncAttackerArmyAfterBattle(world, snap);
            Assert.IsTrue(ArmyTravelCommandService.MoveArmyToNode(world, armyId, TestNodeB).IsSuccess);
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(armyId, out var army));
            Assert.IsTrue(army.IsTraveling);
            Assert.AreEqual(1f, army.RouteSegmentEndProgress, 0.001f);
            Assert.GreaterOrEqual(army.RouteSegmentOriginProgress, 0.9f);
        }

        [Test]
        public void RouteAnchor_NormalizesSwappedBattleAnchorNodeIds()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { leader });
            Assert.IsTrue(armyResult.IsSuccess);
            var armyId = armyResult.Value.ArmyId;

            var snap = new BattleParticipantSnapshot
            {
                AttackerArmyId = armyId,
                BattleAnchorNodeId = TestNodeB,
                BattleAnchorRouteId = TestRoute,
                BattleAnchorDestNodeId = TestNodeA,
                BattleAnchorProgress = 0.88f
            };

            ArmyPostBattleSyncService.SyncAttackerArmyAfterBattle(world, snap);
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(armyId, out var army));
            Assert.AreEqual(TestNodeA, army.NodeId);
            Assert.AreEqual(TestNodeB, army.DestNodeId);
            Assert.AreEqual(0.12f, army.RouteAnchorProgress, 0.05f);

            Assert.IsTrue(ArmyTravelCommandService.MoveArmyToNode(world, armyId, TestNodeB).IsSuccess);
            Assert.IsTrue(army.IsTraveling);
            Assert.AreEqual(1f, army.RouteSegmentEndProgress, 0.001f);
            Assert.Less(army.RouteSegmentOriginProgress, army.RouteSegmentEndProgress);
        }

        [Test]
        public void DirectHop_ReverseGraphDirection_UsesSegmentOneToZero()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeB);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeB, new[] { leader });
            Assert.IsTrue(armyResult.IsSuccess);
            var armyId = armyResult.Value.ArmyId;

            Assert.IsTrue(ArmyTravelCommandService.MoveArmyToNode(world, armyId, TestNodeA).IsSuccess);
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(armyId, out var army));
            Assert.IsTrue(army.IsTraveling);
            Assert.AreEqual(1f, army.RouteSegmentOriginProgress, 0.001f);
            Assert.AreEqual(0f, army.RouteSegmentEndProgress, 0.001f);
            Assert.AreEqual(TestRoute, army.RouteId);
        }

        [Test]
        public void RefreshFromMembers_InEncounterReverseLabels_ConvertsGraphProgress()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeB);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeB, new[] { leader });
            Assert.IsTrue(armyResult.IsSuccess);
            var armyId = armyResult.Value.ArmyId;

            // FormalArmy 误落在路网 From 端；成员实际在 To 端（沿 Node→Dest 的反向 label）
            var snap = new BattleParticipantSnapshot
            {
                AttackerArmyId = armyId,
                BattleAnchorNodeId = TestNodeA,
                BattleAnchorRouteId = TestRoute,
                BattleAnchorDestNodeId = TestNodeB,
                BattleAnchorProgress = 0f
            };
            ArmyPostBattleSyncService.SyncAttackerArmyAfterBattle(world, snap);
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(armyId, out var army));
            Assert.AreEqual(0f, army.RouteAnchorProgress, 0.001f);

            var wp = world.WorldPresence.GetOrCreate(leader);
            wp.Mode = PartyWorldPresenceMode.InEncounter;
            wp.RouteId = TestRoute;
            wp.NodeId = TestNodeB;
            wp.DestNodeId = TestNodeA;
            wp.RouteAnchorProgress = 0f;

            world.Strategic.Participants.AttackerArmyId = armyId;
            ArmyPostBattleSyncService.RefreshAttackerArmyFromMembers(world);

            Assert.AreEqual(1f, army.RouteAnchorProgress, 0.001f);
            Assert.IsTrue(ArmyTravelCommandService.MoveArmyToNode(world, armyId, TestNodeA).IsSuccess);
            Assert.IsTrue(army.IsTraveling);
            Assert.AreEqual(1f, army.RouteSegmentOriginProgress, 0.001f);
            Assert.AreEqual(0f, army.RouteSegmentEndProgress, 0.001f);
        }

        [Test]
        public void MultiHop_ContinuesQueuedTravelAfterFirstLeg()
        {
            var world = CreateWorld();
            world.WorldGraph.RegisterNode(new WorldNodeState
            {
                Id = "test:node_c",
                Name = "C",
                OwnerId = TestFactionA,
                WorldX = 20f,
                WorldY = 0f
            });
            world.WorldGraph.RegisterRoute(new WorldRouteState
            {
                Id = "test:route_bc",
                FromNodeId = TestNodeB,
                ToNodeId = "test:node_c",
                TravelCost = 1
            });

            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { leader });
            Assert.IsTrue(armyResult.IsSuccess);
            var armyId = armyResult.Value.ArmyId;

            Assert.IsTrue(ArmyTravelCommandService.MoveArmyToNode(world, armyId, "test:node_c").IsSuccess);
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(armyId, out var army));
            Assert.IsTrue(army.IsTraveling);
            Assert.AreEqual(TestNodeB, army.DestNodeId);

            ArmyTravelService.AdvanceAll(world, army.TravelTotalTicks);
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(armyId, out army));
            Assert.IsTrue(army.IsTraveling, "Second hop should start automatically.");
            Assert.AreEqual("test:node_c", army.DestNodeId);
        }

        [Test]
        public void PostBattleSync_DetachesIncapacitatedMemberAndDisbandsSoloArmy()
        {
            var world = CreateWorld();
            var solo = SpawnCharacter(world, "Solo", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { solo });
            Assert.IsTrue(armyResult.IsSuccess);
            var armyId = armyResult.Value.ArmyId;

            EnterIncapacitated(world, solo);
            StrategicEncounterResolveService.PlaceAtBattleAnchor(
                world,
                world.WorldPresence.GetOrCreate(solo),
                BuildSnap(armyId, 0.42f));

            ArmyPostBattleSyncService.SyncAttackerArmyAfterBattle(world, BuildSnap(armyId, 0.42f));

            Assert.IsFalse(ArmyService.TryGetArmyForCharacter(world, solo, out _));
            Assert.IsFalse(world.Strategic.FormalArmies.TryGet(armyId, out _));
            Assert.IsTrue(LingeringBattlefieldPartyService.IsIncapacitated(world, solo));
            Assert.IsTrue(
                ArmyWorldMapPresentation.ShouldDrawIndependentCharacterPortrait(world, solo));
        }

        [Test]
        public void PostBattleSync_ParksSurvivorArmyAtBattleAnchor()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var downed = SpawnCharacter(world, "Downed", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { leader, downed });
            Assert.IsTrue(armyResult.IsSuccess);
            var armyId = armyResult.Value.ArmyId;

            EnterIncapacitated(world, downed);
            var snap = BuildSnap(armyId, 0.55f);
            StrategicEncounterResolveService.PlaceAtBattleAnchor(
                world, world.WorldPresence.GetOrCreate(leader), snap);
            StrategicEncounterResolveService.PlaceAtBattleAnchor(
                world, world.WorldPresence.GetOrCreate(downed), snap);

            ArmyPostBattleSyncService.SyncAttackerArmyAfterBattle(world, snap);

            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(armyId, out var army));
            Assert.AreEqual(TestRoute, army.RouteId);
            Assert.AreEqual(0.55f, army.RouteAnchorProgress, 0.001f);
            Assert.AreEqual(TestNodeA, army.NodeId);
            Assert.AreEqual(TestNodeB, army.DestNodeId);
            Assert.IsFalse(ArmyService.TryGetArmyForCharacter(world, downed, out _));
            Assert.IsTrue(ArmyService.TryGetArmyForCharacter(world, leader, out _));

            ArmyPresenceAdapter.SyncFromArmy(world, army);
            Assert.IsTrue(world.WorldPresence.TryGet(downed, out var downedWp));
            Assert.AreEqual(0.55f, downedWp.RouteAnchorProgress, 0.001f);
            Assert.IsTrue(world.WorldPresence.TryGet(leader, out var leaderWp));
            Assert.AreEqual(0.55f, leaderWp.RouteAnchorProgress, 0.001f);
        }

        [Test]
        public void PostBattleSync_MoveFromRouteAnchor_DoesNotTeleportToPreBattleNode()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { leader });
            Assert.IsTrue(armyResult.IsSuccess);
            var armyId = armyResult.Value.ArmyId;
            var snap = BuildSnap(armyId, 0.6f);

            ArmyPostBattleSyncService.SyncAttackerArmyAfterBattle(world, snap);
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(armyId, out var army));
            Assert.IsTrue(army.IsRouteAnchored);
            Assert.AreEqual(0.6f, army.RouteAnchorProgress, 0.001f);

            Assert.IsTrue(ArmyTravelCommandService.MoveArmyToNode(world, armyId, TestNodeB).IsSuccess);
            Assert.IsTrue(world.WorldPresence.TryGet(leader, out var wp));
            Assert.AreEqual(TestRoute, wp.RouteId);
            Assert.AreEqual(TestNodeA, wp.NodeId);
            Assert.AreEqual(TestNodeB, wp.DestNodeId);
            Assert.AreEqual(PartyWorldPresenceMode.Traveling, wp.Mode);
            Assert.GreaterOrEqual(wp.RouteSegmentOriginProgress, 0.55f);
            Assert.AreEqual(1f, wp.RouteSegmentEndProgress, 0.001f);
            Assert.AreEqual(0f, wp.TravelProgress, 0.001f);
            Assert.GreaterOrEqual(wp.TravelTotalTicks, 8);
        }

        [Test]
        public void PostBattleSync_ReconcileBeforeMove_WhenArmyNodeStale()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { leader });
            Assert.IsTrue(armyResult.IsSuccess);
            var armyId = armyResult.Value.ArmyId;
            var snap = BuildSnap(armyId, 0.45f);

            // 仅成员落在接战锚点，FormalArmy 仍停在战前 AtNode（模拟战后未同步）
            StrategicEncounterResolveService.PlaceAtBattleAnchor(
                world, world.WorldPresence.GetOrCreate(leader), snap);
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(armyId, out var army));
            Assert.IsFalse(army.IsRouteAnchored);

            Assert.IsTrue(ArmyTravelCommandService.MoveArmyToNode(world, armyId, TestNodeB).IsSuccess);
            Assert.IsTrue(world.WorldPresence.TryGet(leader, out var wp));
            Assert.AreEqual(PartyWorldPresenceMode.Traveling, wp.Mode);
            Assert.GreaterOrEqual(wp.RouteSegmentOriginProgress, 0.4f);
        }

        [Test]
        public void MidTravel_Retarget_DoesNotSnapToRouteOrigin()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { leader });
            Assert.IsTrue(armyResult.IsSuccess);
            var armyId = armyResult.Value.ArmyId;

            Assert.IsTrue(ArmyTravelCommandService.MoveArmyToNode(world, armyId, TestNodeB).IsSuccess);
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(armyId, out var army));
            var total = army.TravelTotalTicks;
            ArmyTravelService.AdvanceAll(world, Math.Max(1, total / 2));

            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(armyId, out army));
            Assert.IsTrue(army.IsTraveling);
            var midProgress = army.GetRouteDisplayProgress();
            Assert.Greater(midProgress, 0.15f);
            Assert.Less(midProgress, 0.85f);

            Assert.IsTrue(ArmyTravelCommandService.MoveArmyToNode(world, armyId, TestNodeA).IsSuccess);
            Assert.IsTrue(army.IsTraveling);
            Assert.AreEqual(0f, army.RouteSegmentEndProgress, 0.001f);
            Assert.AreEqual(midProgress, army.RouteSegmentOriginProgress, 0.08f);
            Assert.AreEqual(0f, army.TravelProgress, 0.001f);
        }

        [Test]
        public void MidTravel_RetargetToRouteProgress_AnchorsAtCurrentPosition()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { leader });
            Assert.IsTrue(armyResult.IsSuccess);
            var armyId = armyResult.Value.ArmyId;

            Assert.IsTrue(ArmyTravelCommandService.MoveArmyToNode(world, armyId, TestNodeB).IsSuccess);
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(armyId, out var army));
            ArmyTravelService.AdvanceAll(world, Math.Max(1, army.TravelTotalTicks / 3));
            var midProgress = army.GetRouteDisplayProgress();

            Assert.IsTrue(
                ArmyTravelCommandService.MoveArmyToRouteProgress(world, armyId, TestRoute, 0.8f).IsSuccess);
            Assert.IsTrue(army.IsTraveling);
            Assert.AreEqual(0.8f, army.RouteSegmentEndProgress, 0.001f);
            Assert.AreEqual(midProgress, army.RouteSegmentOriginProgress, 0.08f);
        }

        [Test]
        public void RouteProgressLeg_MountsAtArrivalEndpoint_NotTargetProgress()
        {
            var world = CreateWorld();
            world.WorldGraph.RegisterNode(new WorldNodeState
            {
                Id = "test:node_c",
                Name = "C",
                OwnerId = TestFactionA,
                WorldX = 20f,
                WorldY = 0f
            });
            const string routeBc = "test:route_bc";
            world.WorldGraph.RegisterRoute(new WorldRouteState
            {
                Id = routeBc,
                FromNodeId = TestNodeB,
                ToNodeId = "test:node_c",
                TravelCost = 1
            });

            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { leader });
            Assert.IsTrue(armyResult.IsSuccess);
            var armyId = armyResult.Value.ArmyId;

            Assert.IsTrue(ArmyTravelCommandService.MoveArmyToRouteProgress(world, armyId, routeBc, 0.75f).IsSuccess);
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(armyId, out var army));
            Assert.IsTrue(ArmyTravelCommandService.HasPendingLegs(armyId));
            Assert.AreEqual(TestNodeB, army.DestNodeId);

            ArmyTravelService.AdvanceAll(world, army.TravelTotalTicks);
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(armyId, out army));
            Assert.IsTrue(army.IsTraveling);
            Assert.AreEqual(routeBc, army.RouteId);
            Assert.Less(army.RouteSegmentOriginProgress, 0.1f);
            Assert.AreEqual(0.75f, army.RouteSegmentEndProgress, 0.05f);
        }

        [Test]
        public void FormalArmyTravel_NotAdvancedTwiceByWorldTravelService()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { leader });
            Assert.IsTrue(armyResult.IsSuccess);
            var armyId = armyResult.Value.ArmyId;

            Assert.IsTrue(ArmyTravelCommandService.MoveArmyToNode(world, armyId, TestNodeB).IsSuccess);
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(armyId, out var army));
            var ticksBefore = army.RemainingTravelTicks;
            Assert.IsTrue(world.WorldPresence.TryGet(leader, out var wp));
            Assert.AreEqual(ticksBefore, wp.RemainingTravelTicks);

            WorldTravelService.AdvanceTravel(world, 1);
            ArmyTravelService.AdvanceAll(world, 1);

            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(armyId, out army));
            Assert.AreEqual(ticksBefore - 1, army.RemainingTravelTicks);
            Assert.IsTrue(world.WorldPresence.TryGet(leader, out wp));
            Assert.AreEqual(army.RemainingTravelTicks, wp.RemainingTravelTicks);
        }

        [Test]
        public void PostBattleSync_DeadArmyCannotTravel()
        {
            var world = CreateWorld();
            var solo = SpawnCharacter(world, "Solo", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { solo });
            Assert.IsTrue(armyResult.IsSuccess);
            var armyId = armyResult.Value.ArmyId;

            EnterIncapacitated(world, solo);
            ArmyPostBattleSyncService.SyncAttackerArmyAfterBattle(world, BuildSnap(armyId, 0.5f));

            Assert.IsFalse(world.Strategic.FormalArmies.TryGet(armyId, out _));
            var move = ArmyTravelCommandService.MoveArmyToNode(world, armyId, TestNodeB);
            Assert.IsTrue(move.IsFailure);
        }
    }
}
