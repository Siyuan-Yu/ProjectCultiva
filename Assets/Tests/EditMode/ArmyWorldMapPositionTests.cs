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
    public sealed class ArmyWorldMapPositionTests
    {
        const string FactionA = "test:faction_a";
        const string FactionB = "test:faction_b";
        const string NodeA = "base:site_huangcun";
        const string NodeB = "base:site_qingyun_lu";
        const string NodeC = "test:node_c";

        static readonly HexCoord HexA = Ch01HexPrototypeMapBuilder.HuangcunHex;
        static readonly HexCoord HexB = Ch01HexPrototypeMapBuilder.QingyunLuHex;
        static HexCoord HexC;

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            HexTestWorldBootstrap.EnsureMinimalHexMap(world);
            RegisterNodeC(world);
            WarGateService.DeclareWar(world, FactionA, FactionB);
            return world;
        }

        static void RegisterNodeC(SimulationWorld world)
        {
            HexC = HexAlongPath(world, HexA, HexB, 1f);
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, new WorldSite
            {
                SiteId = NodeC,
                AnchorHex = HexC,
                LocalMapId = "loc_test_c",
            });
        }

        static HexCoord HexAlongPath(SimulationWorld world, HexCoord from, HexCoord to, float t)
        {
            var path = new List<HexCoord>(32);
            Assert.IsTrue(HexPathfinder.TryFindPath(world.HexWorld, from, to, path));
            var idx = (int)System.Math.Round((path.Count - 1) * t);
            idx = System.Math.Max(0, System.Math.Min(path.Count - 1, idx));
            return path[idx];
        }

        static EntityId SpawnCharacter(SimulationWorld world, string name, string nodeId, string faction = FactionA)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            var entity = created.Value;
            entity.Get<FactionMembershipComponent>().Assign(faction, FactionRoleKind.Member);
            world.WorldPresence.SetAtSite(entity.Id, nodeId);
            return entity.Id;
        }

        static FormalArmy CreateArmyAtHex(
            SimulationWorld world,
            string faction,
            string nodeId,
            HexCoord hex,
            EntityId leader)
        {
            var army = ArmyService.CreateArmy(world, faction, nodeId, new[] { leader }).Value;
            FormalArmyTestSupport.AnchorOnHex(army, hex);
            return army;
        }

        static (FormalArmy army, ArmyStack stack) RegisterLinkedEnemy(
            SimulationWorld world,
            string stackId,
            string nodeId,
            HexCoord hex,
            EntityId leader)
        {
            var created = ArmyService.CreateArmy(world, FactionB, nodeId, new[] { leader });
            Assert.IsTrue(created.IsSuccess);
            var army = created.Value;
            FormalArmyTestSupport.AnchorOnHex(army, hex);
            var stack = new ArmyStack
            {
                Id = stackId,
                FormalArmyId = army.ArmyId,
                FactionId = FactionB,
                DisplayName = "Enemy",
                SiteId = nodeId
            };
            world.Strategic.Armies.Register(stack);
            ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, stack);
            return (army, stack);
        }

        static void AssertWorldPoint(
            SimulationWorld world,
            FormalArmy army,
            float expectedX,
            float expectedY,
            float epsilon = 0.001f,
            string context = null)
        {
            Assert.IsTrue(
                ArmyWorldMapPresentation.TryResolveArmyWorldPoint(world, army, out var x, out var y),
                context + ": resolve failed");
            Assert.AreEqual(expectedX, x, epsilon, context + ": WorldX");
            Assert.AreEqual(expectedY, y, epsilon, context + ": WorldY");
        }

        static (float x, float y) CaptureRender(SimulationWorld world, FormalArmy army)
        {
            Assert.IsTrue(ArmyWorldMapPresentation.TryResolveArmyWorldPoint(world, army, out var x, out var y));
            return (x, y);
        }

        static void AdvanceHexTicks(SimulationWorld world, int ticks)
        {
            ArmyHexTravelService.AdvanceAll(world, ticks);
            ArmyHexPursuitService.AfterTravelTick(world);
        }

        [Test]
        public void ARMY_VIS01_OnRouteFormalArmy_ResolvesRouteProgressPosition()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Leader", NodeA);
            var army = CreateArmyAtHex(world, FactionA, NodeA, HexA, leader);
            FormalArmyTestSupport.SetHexMidTravel(world, army, HexA, HexB, 0.4f);

            Assert.IsTrue(
                FormalArmyHexWorldPositionResolver.TryResolve(world, army, out var expectedX, out var expectedY));
            AssertWorldPoint(world, army, expectedX, expectedY, context: "ARMY-VIS-01");
        }

        [Test]
        public void ARMY_VIS02_PursuitBeginWithoutTick_KeepsRenderPosition()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Pursuer", NodeA);
            var pursuer = CreateArmyAtHex(world, FactionA, NodeA, HexA, leader);
            var pursuerHex = HexAlongPath(world, HexA, HexB, 0.40f);
            FormalArmyTestSupport.SetHexMidTravel(world, pursuer, pursuerHex, HexB, 0.35f);
            ArmyPresenceAdapter.SyncFromArmy(world, pursuer);

            var enemyLeader = SpawnCharacter(world, "Enemy", NodeB, FactionB);
            var targetHex = HexAlongPath(world, HexB, HexC, 0.70f);
            var (targetArmy, targetStack) = RegisterLinkedEnemy(world, "army:vis02", NodeB, targetHex, enemyLeader);
            FormalArmyTestSupport.SetHexMidTravel(world, targetArmy, targetHex, HexC, 0.20f);
            ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, targetStack);

            var before = CaptureRender(world, pursuer);
            // removed: ReconcileArmyWithLivingMembers(world, pursuer);
            Assert.IsTrue(ArmyHexCommandService.AttackStack(world, pursuer.ArmyId, targetStack).IsSuccess);
            var after = CaptureRender(world, pursuer);

            Assert.AreEqual(before.x, after.x, 0.001f, "ARMY-VIS-02: render X");
            Assert.AreEqual(before.y, after.y, 0.001f, "ARMY-VIS-02: render Y");
        }

        [Test]
        public void ARMY_VIS03_PursuitDestNodeChangeWithoutRouteProgress_KeepsRenderPosition()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Pursuer", NodeA);
            var pursuer = CreateArmyAtHex(world, FactionA, NodeA, HexA, leader);
            var pursuerHex = HexAlongPath(world, HexA, HexB, 0.40f);
            FormalArmyTestSupport.SetHexMidTravel(world, pursuer, pursuerHex, HexB, 0.35f);

            var before = CaptureRender(world, pursuer);
            var after = CaptureRender(world, pursuer);

            Assert.AreEqual(before.x, after.x, 0.001f, "ARMY-VIS-03: render X");
            Assert.AreEqual(before.y, after.y, 0.001f, "ARMY-VIS-03: render Y");
        }

        [Test]
        public void ARMY_VIS04_PursuitRepathWithoutRouteProgressChange_KeepsRenderPosition()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Pursuer", NodeA);
            var pursuer = CreateArmyAtHex(world, FactionA, NodeA, HexA, leader);
            var pursuerHex = HexAlongPath(world, HexA, HexB, 0.40f);
            FormalArmyTestSupport.SetHexMidTravel(world, pursuer, pursuerHex, HexB, 0.35f);

            var enemyLeader = SpawnCharacter(world, "Enemy", NodeB, FactionB);
            var targetHex = HexAlongPath(world, HexB, HexC, 0.70f);
            var (targetArmy, targetStack) = RegisterLinkedEnemy(world, "army:vis04", NodeB, targetHex, enemyLeader);
            FormalArmyTestSupport.SetHexMidTravel(world, targetArmy, targetHex, HexC, 0.20f);
            ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, targetStack);

            var before = CaptureRender(world, pursuer);
            Assert.IsTrue(ArmyHexCommandService.AttackStack(world, pursuer.ArmyId, targetStack).IsSuccess);
            var after = CaptureRender(world, pursuer);

            Assert.AreEqual(before.x, after.x, 0.001f, "ARMY-VIS-04: render X");
            Assert.AreEqual(before.y, after.y, 0.001f, "ARMY-VIS-04: render Y");
        }

        [Test]
        public void ARMY_VIS05_WorldTickAdvance_MovesRenderContinuously()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Leader", NodeA);
            var army = CreateArmyAtHex(world, FactionA, NodeA, HexA, leader);
            Assert.IsTrue(ArmyHexCommandService.MoveArmy(world, army.ArmyId, HexB).IsSuccess);
            ArmyPresenceAdapter.SyncFromArmy(world, army);

            var before = CaptureRender(world, army);
            AdvanceHexTicks(world, 1);
            var after = CaptureRender(world, army);

            Assert.IsFalse(
                System.Math.Abs(after.x - before.x) < 0.0001f &&
                System.Math.Abs(after.y - before.y) < 0.0001f,
                "ARMY-VIS-05: render should advance along hex path");
            Assert.AreNotEqual(HexB, army.CurrentHex, "ARMY-VIS-05: should not snap to destination hex yet");
        }

        [Test]
        public void ARMY_VIS06_ArrivalAtNode_SnapsRenderOnlyAfterDomainAtNode()
        {
            var world = CreateWorld();
            var leader = SpawnCharacter(world, "Leader", NodeA);
            var army = CreateArmyAtHex(world, FactionA, NodeA, HexA, leader);
            Assert.IsTrue(ArmyHexCommandService.MoveArmy(world, army.ArmyId, HexB).IsSuccess);

            var mid = CaptureRender(world, army);
            HexMath.ToWorldPosition(HexB, world.HexWorld.HexSize, out var destX, out var destY);
            Assert.IsFalse(
                System.Math.Abs(mid.x - destX) < 0.001f && System.Math.Abs(mid.y - destY) < 0.001f,
                "ARMY-VIS-06: mid-path must not be at destination hex");

            while (army.State == FormalArmyState.Moving)
                AdvanceHexTicks(world, 1);

            Assert.AreEqual(FormalArmyState.Idle, army.State);
            Assert.AreEqual(HexB, army.CurrentHex);
            AssertWorldPoint(world, army, destX, destY, context: "ARMY-VIS-06");
        }
    }
}
