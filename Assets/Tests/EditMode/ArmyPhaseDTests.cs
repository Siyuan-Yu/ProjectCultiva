using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    public sealed class ArmyPhaseDTests
    {
        const string TestFactionA = "test:faction_a";
        const string TestNodeA = "base:node_huangcun";
        const string TestNodeB = "base:node_qingyun_lu";
        const string TestRoute = "test:route_ab";

        static readonly HexCoord HexA = Ch01HexPrototypeMapBuilder.HuangcunHex;
        static readonly HexCoord HexB = Ch01HexPrototypeMapBuilder.QingyunLuHex;

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            Ch01HexPrototypeMapBuilder.Build(world);return world;
        }

        static SimulationWorld CreateHexWorld()
        {
            var world = CreateWorld();
            HexTestWorldBootstrap.EnsureMinimalHexMap(world);
            WarGateService.DeclareWar(world, TestFactionA, "enemy:faction");
            return world;
        }

        static FormalArmy CreateArmyAtHex(
            SimulationWorld world,
            string nodeId,
            HexCoord hex,
            params EntityId[] members)
        {
            var army = ArmyService.CreateArmy(world, TestFactionA, nodeId, members).Value;
            FormalArmyTestSupport.AnchorOnHex(army, hex);
            return army;
        }

        static void AdvanceHexTravelTicks(SimulationWorld world, int ticks)
        {
            for (var i = 0; i < ticks; i++)
            {
                ArmyHexTravelService.AdvanceAll(world, 1);
                ArmyHexPursuitService.AfterTravelTick(world);
            }
        }

        static void AdvanceHexTicks(SimulationWorld world, int ticks) =>
            AdvanceHexTravelTicks(world, ticks);

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
            var world = CreateHexWorld();
            var a = SpawnCharacter(world, "A", TestNodeA);
            var b = SpawnCharacter(world, "B", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { a, b });
            Assert.IsTrue(armyResult.IsSuccess);
            var army = armyResult.Value;
            FormalArmyTestSupport.AnchorOnHex(army, HexA);

            var move = ArmyHexCommandService.MoveArmy(world, army.ArmyId, HexB);
            Assert.IsTrue(move.IsSuccess);
            Assert.AreEqual(FormalArmyState.Moving, army.State);
            Assert.Greater(army.StepRemainingTicks, 0);

            while (army.State == FormalArmyState.Moving)
                AdvanceHexTicks(world, 1);
            Assert.AreEqual(FormalArmyState.Idle, army.State);
            Assert.AreEqual(HexB, army.CurrentHex);
        }

        [Test]
        public void ArmyTravel_SyncsMemberPresence()
        {
            var world = CreateHexWorld();
            var a = SpawnCharacter(world, "A", TestNodeA);
            var b = SpawnCharacter(world, "B", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { a, b });
            Assert.IsTrue(armyResult.IsSuccess);
            var army = armyResult.Value;
            FormalArmyTestSupport.AnchorOnHex(army, HexA);

            Assert.IsTrue(ArmyHexCommandService.MoveArmy(world, army.ArmyId, HexB).IsSuccess);
            ArmyPresenceAdapter.SyncFromArmy(world, army);
            Assert.IsTrue(world.WorldPresence.TryGet(a, out var pa));
            Assert.IsTrue(world.WorldPresence.TryGet(b, out var pb));
            Assert.AreEqual(TestNodeA, pa.NodeId);
            Assert.AreEqual(TestNodeA, pb.NodeId);
        }

        [Test]
        public void LegacyExit_UngroupedCharacter_PlayerMoveOrderBlocked()
        {
            var world = CreateWorld();
            var solo = SpawnCharacter(world, "Solo", TestNodeA);
            Assert.IsFalse(WorldTravelService.CanReceivePlayerMacroTravelOrder(world, solo));
            Assert.IsFalse(WorldTravelService.BlocksFormalArmyMemberIndependentTravel(world, solo));
        }

        [Test]
        public void LegacyExit_ArmyMoveOrder_Succeeds()
        {
            var world = CreateWorld();
            var a = SpawnCharacter(world, "A", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { a });
            Assert.IsTrue(armyResult.IsSuccess);
            var move = ArmyHexCommandService.MoveArmyToSite(world, armyResult.Value.ArmyId, TestNodeB);
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
            var world = CreateHexWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var member = SpawnCharacter(world, "Member", TestNodeA);
            var armyResult = ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { leader, member });
            Assert.IsTrue(armyResult.IsSuccess);
            var army = armyResult.Value;
            var midHex = HexAlongPath(world, HexA, HexB, 0.4f);
            FormalArmyTestSupport.SetHexMidTravel(world, army, midHex, HexB, 0.4f);

            Assert.IsTrue(
                FormalArmyHexWorldPositionResolver.TryResolve(world, army, out var expectedX, out var expectedY));
            Assert.IsTrue(
                ArmyWorldMapPresentation.TryResolveArmyWorldPoint(world, army, out var wx, out var wy));
            Assert.AreEqual(expectedX, wx, 0.01f);
            Assert.AreEqual(expectedY, wy, 0.01f);
        }

        [Test]
        public void PrepareArmyMacroTravel_DoesNotSnapStaleRouteAnchorToFromNode()
        {
            var world = CreateHexWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var army = CreateArmyAtHex(world, TestNodeA, HexA, leader);
            Assert.IsTrue(ArmyHexCommandService.MoveArmy(world, army.ArmyId, HexB).IsSuccess);
            world.WorldPresence.SetAtNode(leader, TestNodeB);

            var enemyLeader = SpawnCharacter(world, "Enemy", TestNodeB);
            var enemyArmy = ArmyService.CreateArmy(world, "enemy:faction", TestNodeB, new[] { enemyLeader }).Value;
            FormalArmyTestSupport.AnchorOnHex(enemyArmy, HexB);
            var enemy = new ArmyStack
            {
                Id = "enemy:far",
                FormalArmyId = enemyArmy.ArmyId,
                FactionId = "enemy:faction",
                NodeId = TestNodeB
            };
            world.Strategic.Armies.Register(enemy);

            Assert.IsTrue(ArmyHexCommandService.AttackStack(world, army.ArmyId, enemy).IsSuccess);

            Assert.IsTrue(world.WorldPresence.TryGet(leader, out var leaderP));
            Assert.AreNotEqual(TestNodeA, leaderP.NodeId,
                "Pursuit must not snap living members back to stale departure node.");
        }

        [Test]
        public void ArmyPursuit_SameRoute_ClampDoesNotResetToFromNodeEachTick()
        {
            var world = CreateHexWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var army = CreateArmyAtHex(world, TestNodeA, HexA, leader);

            var enemyLeader = SpawnCharacter(world, "Enemy", TestNodeA);
            var enemyArmy = ArmyService.CreateArmy(world, "enemy:faction", TestNodeA, new[] { enemyLeader }).Value;
            var enemyHex = HexAlongPath(world, HexA, HexB, 0.42f);
            FormalArmyTestSupport.AnchorOnHex(enemyArmy, enemyHex);
            var stack = new ArmyStack
            {
                Id = "enemy:patrol",
                FormalArmyId = enemyArmy.ArmyId,
                FactionId = "enemy:faction",
                NodeId = TestNodeA
            };
            world.Strategic.Armies.Register(stack);
            ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, stack);

            Assert.IsTrue(ArmyHexCommandService.AttackStack(world, army.ArmyId, stack).IsSuccess);
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(army.ArmyId, out var formal));
            Assert.AreEqual(FormalArmyState.Moving, formal.State);

            var beforeProgress = formal.StepProgress;
            var beforeRemaining = formal.StepRemainingTicks;
            Assert.Greater(beforeRemaining, 0);

            for (var i = 0; i < 5; i++)
                AdvanceHexTicks(world, 1);

            Assert.AreEqual(FormalArmyState.Moving, formal.State, "Same-route pursuit must keep traveling.");
            Assert.Less(formal.StepRemainingTicks, beforeRemaining, "Ticks must decrease; clamp must not full-reset.");
            Assert.Greater(formal.StepProgress, beforeProgress - 0.001f,
                "Must advance along hex path, not snap back to origin each tick.");
        }

        static HexCoord HexAlongPath(SimulationWorld world, HexCoord from, HexCoord to, float t)
        {
            var path = new List<HexCoord>(32);
            Assert.IsTrue(HexPathfinder.TryFindPath(world.HexWorld, from, to, path));
            var idx = (int)System.Math.Round((path.Count - 1) * t);
            idx = System.Math.Max(0, System.Math.Min(path.Count - 1, idx));
            return path[idx];
        }

        [Test]
        public void ArmyPursuit_TravelTicksDecreaseAcrossAfterTravelTicks()
        {
            var world = CreateHexWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var army = CreateArmyAtHex(world, TestNodeA, HexA, leader);

            var enemyLeader = SpawnCharacter(world, "Enemy", TestNodeB);
            var enemyArmy = ArmyService.CreateArmy(world, "enemy:faction", TestNodeB, new[] { enemyLeader }).Value;
            FormalArmyTestSupport.AnchorOnHex(enemyArmy, HexB);
            var stack = new ArmyStack
            {
                Id = "enemy:stack",
                FormalArmyId = enemyArmy.ArmyId,
                FactionId = "enemy:faction",
                NodeId = TestNodeB
            };
            world.Strategic.Armies.Register(stack);
            ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, stack);

            Assert.IsTrue(ArmyHexCommandService.AttackStack(world, army.ArmyId, stack).IsSuccess);

            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(army.ArmyId, out var formal));
            Assert.AreEqual(FormalArmyState.Moving, formal.State);
            var before = formal.StepRemainingTicks;
            Assert.Greater(before, 0);

            AdvanceHexTicks(world, 1);

            Assert.AreEqual(FormalArmyState.Moving, formal.State, "Pursuit travel should not reset every tick.");
            Assert.Less(formal.StepRemainingTicks, before);
        }

        [Test]
        public void ArmyPursuit_CrossNode_StartsFormalArmyTravel()
        {
            var world = CreateHexWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var army = CreateArmyAtHex(world, TestNodeA, HexA, leader);

            var enemyLeader = SpawnCharacter(world, "Enemy", TestNodeB);
            var enemyArmy = ArmyService.CreateArmy(world, "enemy:faction", TestNodeB, new[] { enemyLeader }).Value;
            FormalArmyTestSupport.AnchorOnHex(enemyArmy, HexB);
            var stack = new ArmyStack
            {
                Id = "enemy:stack",
                FormalArmyId = enemyArmy.ArmyId,
                FactionId = "enemy:faction",
                NodeId = TestNodeB
            };
            world.Strategic.Armies.Register(stack);
            ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, stack);

            Assert.IsTrue(ArmyHexCommandService.AttackStack(world, army.ArmyId, stack).IsSuccess);

            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(army.ArmyId, out var formal));
            Assert.AreEqual(FormalArmyState.Moving, formal.State, "Cross-node pursuit must start FormalArmy macro travel.");
            Assert.AreEqual(HexB, formal.DestinationHex);

            Assert.IsTrue(world.WorldPresence.TryGet(leader, out var leaderP));
            Assert.AreEqual(stack.Id, leaderP.CombatPursuitStackId);
        }

        [Test]
        public void ArmyPursuit_OnlyLeaderStartsTravel_MembersMirrorLeader()
        {
            var world = CreateHexWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var member = SpawnCharacter(world, "Member", TestNodeA);
            var army = CreateArmyAtHex(world, TestNodeA, HexA, leader, member);

            var enemyLeader = SpawnCharacter(world, "Enemy", TestNodeB);
            var enemyArmy = ArmyService.CreateArmy(world, "enemy:faction", TestNodeB, new[] { enemyLeader }).Value;
            FormalArmyTestSupport.AnchorOnHex(enemyArmy, HexB);
            var stack = new ArmyStack
            {
                Id = "enemy:stack",
                FormalArmyId = enemyArmy.ArmyId,
                FactionId = "enemy:faction",
                NodeId = TestNodeB
            };
            world.Strategic.Armies.Register(stack);
            ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, stack);

            Assert.IsTrue(ArmyHexCommandService.AttackStack(world, army.ArmyId, stack).IsSuccess);
            ArmyPresenceAdapter.SyncFromArmy(world, army);

            Assert.IsTrue(world.WorldPresence.TryGet(leader, out var leaderP));
            Assert.IsTrue(world.WorldPresence.TryGet(member, out var memberP));
            Assert.AreEqual(leaderP.NodeId, memberP.NodeId);
            Assert.AreEqual(leaderP.CombatPursuitStackId, memberP.CombatPursuitStackId);
        }

        [Test]
        public void FormalArmyMember_CannotStartIndependentTravel()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Leader", TestNodeA);
            var member = SpawnCharacter(world, "Member", TestNodeA);
            Assert.IsTrue(ArmyService.CreateArmy(world, TestFactionA, TestNodeA, new[] { leader, member }).IsSuccess);

            Assert.IsFalse(WorldTravelService.CanReceivePlayerMacroTravelOrder(world, member));
            Assert.IsTrue(WorldTravelService.BlocksFormalArmyMemberIndependentTravel(world, member));
        }
    }
}
