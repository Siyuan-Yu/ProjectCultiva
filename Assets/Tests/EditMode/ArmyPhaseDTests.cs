using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    public sealed class ArmyPhaseDTests
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

        [Test]
        public void ArmyTravel_MovesViaWorldTravelAdapter()
        {
            var world = CreateWorld();
            var a = SpawnCharacter(world, "A", TestNodeA);
            var b = SpawnCharacter(world, "B", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { a, b });
            Assert.IsTrue(armyResult.IsSuccess);
            var armyId = armyResult.Value.ArmyId;

            var move = ArmyTravelCommandService.MoveArmyToNode(world, armyId, TestNodeB);
            Assert.IsTrue(move.IsSuccess);
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(armyId, out var army));
            Assert.AreEqual(FormalArmyState.OnRoute, army.State);
            Assert.Greater(army.RemainingTravelTicks, 0);

            ArmyTravelService.AdvanceAll(world, army.RemainingTravelTicks, null);
            Assert.AreEqual(FormalArmyState.AtNode, army.State);
            Assert.AreEqual(TestNodeB, army.NodeId);
        }

        [Test]
        public void ArmyTravel_SyncsMemberPresence()
        {
            var world = CreateWorld();
            var a = SpawnCharacter(world, "A", TestNodeA);
            var b = SpawnCharacter(world, "B", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { a, b });
            Assert.IsTrue(armyResult.IsSuccess);
            var armyId = armyResult.Value.ArmyId;

            Assert.IsTrue(ArmyTravelCommandService.MoveArmyToNode(world, armyId, TestNodeB).IsSuccess);
            Assert.IsTrue(world.WorldPresence.TryGet(a, out var pa));
            Assert.IsTrue(world.WorldPresence.TryGet(b, out var pb));
            Assert.AreEqual(PartyWorldPresenceMode.Traveling, pa.Mode);
            Assert.AreEqual(PartyWorldPresenceMode.Traveling, pb.Mode);
            Assert.AreEqual(TestRoute, pa.RouteId);
        }

        [Test]
        public void LegacyExit_UngroupedCharacter_PlayerMoveOrderBlocked()
        {
            var world = CreateWorld();
            var solo = SpawnCharacter(world, "Solo", TestNodeA);
            Assert.IsFalse(WorldTravelService.CanReceivePlayerMacroTravelOrder(world, solo));
            var move = WorldTravelService.StartTravel(world, solo, TestNodeB);
            Assert.IsTrue(move.IsSuccess, "Internal StartTravel API still allowed for compatibility.");
        }

        [Test]
        public void LegacyExit_ArmyMoveOrder_Succeeds()
        {
            var world = CreateWorld();
            var a = SpawnCharacter(world, "A", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { a });
            Assert.IsTrue(armyResult.IsSuccess);
            var move = ArmyTravelCommandService.MoveArmyToNode(world, armyResult.Value.ArmyId, TestNodeB);
            Assert.IsTrue(move.IsSuccess);
        }

        static void SetMemberPursuitTravel(
            SimulationWorld world,
            EntityId memberId,
            string fromNodeId,
            float routeProgress)
        {
            Assert.IsTrue(world.WorldPresence.TryGet(memberId, out var presence));
            presence.NodeId = fromNodeId;
            presence.Mode = PartyWorldPresenceMode.Traveling;
            presence.RouteId = TestRoute;
            presence.DestNodeId = TestNodeB;
            presence.CombatPursuitStackId = "army:test_enemy";
            presence.TravelTotalTicks = 100;
            presence.RemainingTravelTicks = (int)(100 * (1f - routeProgress));
        }

        [Test]
        public void ArmyWorldMap_PursuitTravel_SuppressesMemberIndependentPortraits()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var member = SpawnCharacter(world, "Member", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { leader, member });
            Assert.IsTrue(armyResult.IsSuccess);
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(armyResult.Value.ArmyId, out var army));
            Assert.AreEqual(FormalArmyState.AtNode, army.State);

            SetMemberPursuitTravel(world, leader, TestNodeA, 0.35f);
            SetMemberPursuitTravel(world, member, TestNodeA, 0.55f);

            Assert.IsFalse(ArmyWorldMapPresentation.ShouldDrawIndependentCharacterPortrait(world, leader));
            Assert.IsFalse(ArmyWorldMapPresentation.ShouldDrawIndependentCharacterPortrait(world, member));
            Assert.IsTrue(ArmyWorldMapPresentation.ShouldDrawFormalArmyPortrait(world, army));
        }

        [Test]
        public void ArmyWorldMap_PursuitTravel_ResolvesLeaderMacroPositionOnRoute()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var member = SpawnCharacter(world, "Member", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { leader, member });
            Assert.IsTrue(armyResult.IsSuccess);

            SetMemberPursuitTravel(world, leader, TestNodeA, 0.4f);
            SetMemberPursuitTravel(world, member, TestNodeA, 0.8f);

            Assert.IsTrue(
                ArmyWorldMapPresentation.TryResolveArmyWorldPoint(world, armyResult.Value, out var wx, out var wy));
            Assert.AreEqual(4f, wx, 0.01f);
            Assert.AreEqual(0f, wy, 0.01f);
        }

        [Test]
        public void PrepareArmyMacroTravel_DoesNotSnapStaleRouteAnchorToFromNode()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var army = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { leader }).Value;

            Assert.IsTrue(ArmyTravelCommandService.MoveArmyToRouteProgress(
                world,
                army.ArmyId,
                TestRoute,
                0f).IsSuccess);
            world.WorldPresence.SetAtNode(leader, TestNodeB);

            var enemy = new ArmyStack
            {
                Id = "enemy:far",
                FactionId = "enemy:faction",
                NodeId = TestNodeB
            };
            world.Strategic.Armies.Register(enemy);

            StrategicPursuitService.BeginPursuitArmy(world, army.ArmyId, enemy);
            var pursue = StrategicPursuitService.CollectPursueParty(world, world.Strategic.Encounter);
            Assert.IsTrue(ArmyTravelCommandService.MoveArmyToStackAnchor(world, army.ArmyId, enemy).IsSuccess);

            Assert.IsTrue(world.WorldPresence.TryGet(leader, out var leaderP));
            Assert.AreNotEqual(TestNodeA, leaderP.NodeId,
                "Pursuit must not snap living members back to stale route FromNode.");
        }

        [Test]
        public void ArmyPursuit_SameRoute_ClampDoesNotResetToFromNodeEachTick()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var army = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { leader }).Value;

            // 原型匪军：停在荒村↔关隘路上 42%（From=NodeA）
            var stack = new ArmyStack
            {
                Id = "enemy:patrol",
                FactionId = "enemy:faction",
                NodeId = TestNodeA,
                RouteId = TestRoute,
                DestNodeId = TestNodeB,
                RouteAnchorProgress = 0.42f,
                RemainingTravelTicks = 0
            };
            world.Strategic.Armies.Register(stack);

            StrategicPursuitService.BeginPursuitArmy(world, army.ArmyId, stack);
            Assert.IsTrue(ArmyTravelCommandService.MoveArmyToStackAnchor(world, army.ArmyId, stack).IsSuccess);
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(army.ArmyId, out var formal));
            Assert.IsTrue(formal.IsTraveling);
            Assert.AreEqual(TestRoute, formal.RouteId);

            var pursue = StrategicPursuitService.CollectPursueParty(world, world.Strategic.Encounter);
            // 开拔当下立刻 Sync（旧 bug：StartArmyTravelToRouteProgress 把起点写成 0 并每 tick 重置）
            StrategicPursuitService.SyncPursuersToStack(world, pursue, stack);

            var beforeTicks = formal.RemainingTravelTicks;
            var beforeProgress = formal.GetRouteDisplayProgress();
            Assert.Greater(beforeTicks, 0);

            for (var i = 0; i < 5; i++)
            {
                WorldTravelService.AdvanceTravel(world, 1, StrategicTravelDriver.BeginArrivalCapture());
                StrategicTravelDriver.AfterTravelTick(world, 1);
            }

            Assert.IsTrue(formal.IsTraveling, "Same-route pursuit must keep traveling.");
            Assert.Less(formal.RemainingTravelTicks, beforeTicks, "Ticks must decrease; Clamp must not full-reset.");
            Assert.Greater(formal.GetRouteDisplayProgress(), beforeProgress + 0.001f,
                "Must advance along route, not snap back to FromNode each tick.");
            Assert.AreEqual(TestNodeA, formal.NodeId, "On-route NodeId is graph FromNode; progress carries position.");
            Assert.Greater(formal.GetRouteDisplayProgress(), 0.02f);
        }

        [Test]
        public void ArmyPursuit_TravelTicksDecreaseAcrossAfterTravelTicks()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var army = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { leader }).Value;

            var stack = new ArmyStack
            {
                Id = "enemy:stack",
                FactionId = "enemy:faction",
                NodeId = TestNodeB
            };
            world.Strategic.Armies.Register(stack);

            StrategicPursuitService.BeginPursuitArmy(world, army.ArmyId, stack);
            var pursue = StrategicPursuitService.CollectPursueParty(world, world.Strategic.Encounter);
            Assert.IsTrue(ArmyTravelCommandService.MoveArmyToStackAnchor(world, army.ArmyId, stack).IsSuccess);
            StrategicPursuitService.SyncPursuersToStack(world, pursue, stack);

            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(army.ArmyId, out var formal));
            Assert.IsTrue(formal.IsTraveling);
            var before = formal.RemainingTravelTicks;
            Assert.Greater(before, 0);

            WorldTravelService.AdvanceTravel(world, 1, StrategicTravelDriver.BeginArrivalCapture());
            StrategicTravelDriver.AfterTravelTick(world, 1);

            Assert.IsTrue(formal.IsTraveling, "Pursuit travel should not reset every tick.");
            Assert.Less(formal.RemainingTravelTicks, before);
        }

        [Test]
        public void ArmyPursuit_CrossNode_StartsFormalArmyTravel()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var army = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { leader }).Value;

            var stack = new ArmyStack
            {
                Id = "enemy:stack",
                FactionId = "enemy:faction",
                NodeId = TestNodeB
            };
            world.Strategic.Armies.Register(stack);

            StrategicPursuitService.BeginPursuitArmy(world, army.ArmyId, stack);
            var pursue = StrategicPursuitService.CollectPursueParty(world, world.Strategic.Encounter);
            Assert.IsTrue(ArmyTravelCommandService.MoveArmyToStackAnchor(world, army.ArmyId, stack).IsSuccess);
            StrategicPursuitService.SyncPursuersToStack(world, pursue, stack);

            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(army.ArmyId, out var formal));
            Assert.IsTrue(formal.IsTraveling, "Cross-node pursuit must start FormalArmy macro travel.");
            Assert.AreEqual(TestNodeB, formal.DestNodeId);

            Assert.IsTrue(world.WorldPresence.TryGet(leader, out var leaderP));
            Assert.AreEqual(PartyWorldPresenceMode.Traveling, leaderP.Mode);
            Assert.AreEqual(stack.Id, leaderP.CombatPursuitStackId);
        }

        [Test]
        public void ArmyPursuit_OnlyLeaderStartsTravel_MembersMirrorLeader()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var member = SpawnCharacter(world, "Member", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { leader, member });
            Assert.IsTrue(armyResult.IsSuccess);
            var army = armyResult.Value;

            var stack = new ArmyStack
            {
                Id = "enemy:stack",
                FactionId = "enemy:faction",
                NodeId = TestNodeB
            };
            world.Strategic.Armies.Register(stack);

            StrategicPursuitService.BeginPursuitArmy(world, army.ArmyId, stack);
            var pursue = StrategicPursuitService.CollectPursueParty(world, world.Strategic.Encounter);
            Assert.IsTrue(ArmyTravelCommandService.MoveArmyToStackAnchor(world, army.ArmyId, stack).IsSuccess);
            StrategicPursuitService.SyncPursuersToStack(world, pursue, stack);

            Assert.IsTrue(world.WorldPresence.TryGet(leader, out var leaderP));
            Assert.IsTrue(world.WorldPresence.TryGet(member, out var memberP));
            Assert.IsTrue(leaderP.HasRoutePresentation || leaderP.Mode == PartyWorldPresenceMode.Traveling);
            Assert.AreEqual(leaderP.Mode, memberP.Mode);
            Assert.AreEqual(leaderP.RouteId, memberP.RouteId);
            Assert.AreEqual(leaderP.DestNodeId, memberP.DestNodeId);
            Assert.AreEqual(leaderP.RemainingTravelTicks, memberP.RemainingTravelTicks);
            Assert.AreEqual(leaderP.TravelTotalTicks, memberP.TravelTotalTicks);
        }

        [Test]
        public void FormalArmyMember_CannotStartIndependentTravel()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var member = SpawnCharacter(world, "Member", TestNodeA);
            Assert.IsTrue(ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { leader, member }).IsSuccess);

            var move = WorldTravelService.StartTravel(world, member, TestNodeB);
            Assert.IsTrue(move.IsFailure);
            StringAssert.Contains("cannot travel independently", move.Error.Message);
        }
    }
}
