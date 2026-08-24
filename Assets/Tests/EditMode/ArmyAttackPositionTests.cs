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
  public sealed class ArmyAttackPositionTests
  {
    const string FactionA = "test:faction_a";
    const string FactionB = "test:faction_b";
    const string NodeA = "base:node_huangcun";
    const string NodeB = "base:node_qingyun_lu";
    const string NodeC = "test:node_c";

    static readonly HexCoord HexA = Ch01HexPrototypeMapBuilder.HuangcunHex;
    static readonly HexCoord HexB = Ch01HexPrototypeMapBuilder.QingyunLuHex;
    static HexCoord HexC;

    public readonly struct FormalArmyStrategicPositionSnapshot
    {
      public readonly FormalArmyState State;
      public readonly string NodeId;
      public readonly HexCoord CurrentHex;
      public readonly HexCoord DestinationHex;
      public readonly float StepProgress;

      public FormalArmyStrategicPositionSnapshot(
        FormalArmyState state,
        string nodeId,
        HexCoord currentHex,
        HexCoord destinationHex,
        float stepProgress)
      {
        State = state;
        NodeId = nodeId ?? string.Empty;
        CurrentHex = currentHex;
        DestinationHex = destinationHex;
        StepProgress = stepProgress;
      }

      public static FormalArmyStrategicPositionSnapshot Capture(FormalArmy army)
      {
        if (army == null)
          return default;
        return new FormalArmyStrategicPositionSnapshot(
          army.State,
          army.NodeId,
          army.CurrentHex,
          army.DestinationHex,
          army.StepProgress);
      }
    }

    static void AssertNoStrategicTeleport(
      FormalArmyStrategicPositionSnapshot before,
      FormalArmyStrategicPositionSnapshot after,
      string context,
      float epsilon = 0.001f)
    {
      Assert.AreEqual(before.CurrentHex, after.CurrentHex, context + ": hex must not change before travel tick.");
      Assert.AreEqual(before.State, after.State, context + ": state must not change before travel tick.");
      if (before.State == FormalArmyState.Moving)
        Assert.AreEqual(before.StepProgress, after.StepProgress, epsilon, context + ": step progress must not jump before travel tick.");
    }

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
      world.WorldPresence.SetAtNode(entity.Id, nodeId);
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
        NodeId = nodeId
      };
      world.Strategic.Armies.Register(stack);
      ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, stack);
      return (army, stack);
    }

    static void ExecuteAttackCommand(SimulationWorld world, FormalArmy pursuer, ArmyStack targetStack)
    {
      // removed: ReconcileArmyWithLivingMembers(world, pursuer);
      Assert.IsTrue(ArmyHexCommandService.AttackStack(world, pursuer.ArmyId, targetStack).IsSuccess);
    }

    static void AdvanceWorldTicks(SimulationWorld world, int ticks)
    {
      for (var i = 0; i < ticks; i++)
      {
        ArmyHexTravelService.AdvanceAll(world, 1);
        ArmyStackAdapter.SyncAllLinkedStacksFromFormalArmies(world);
        ArmyHexPursuitService.AfterTravelTick(world);
      }
    }

    [Test]
    public void ATTACK_POS01_AttackCommand_DoesNotMoveArmyBeforeTravelTick()
    {
      var world = CreateWorld();
      var leader = SpawnCharacter(world, "Pursuer", NodeA);
      var pursuer = CreateArmyAtHex(world, FactionA, NodeA, HexA, leader);
      var pursuerHex = HexAlongPath(world, HexA, HexB, 0.37f);
      FormalArmyTestSupport.SetHexMidTravel(world, pursuer, pursuerHex, HexB, 0.35f);
      ArmyPresenceAdapter.SyncFromArmy(world, pursuer);

      var enemyLeader = SpawnCharacter(world, "Enemy", NodeB, FactionB);
      var targetHex = HexAlongPath(world, HexB, HexC, 0.70f);
      var (targetArmy, targetStack) = RegisterLinkedEnemy(world, "army:enemy_pos01", NodeB, targetHex, enemyLeader);
      FormalArmyTestSupport.SetHexMidTravel(world, targetArmy, targetHex, HexC, 0.20f);
      ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, targetStack);

      var before = FormalArmyStrategicPositionSnapshot.Capture(pursuer);
      ExecuteAttackCommand(world, pursuer, targetStack);
      var after = FormalArmyStrategicPositionSnapshot.Capture(pursuer);
      AssertNoStrategicTeleport(before, after, "ATTACK-POS-01");
    }

    [Test]
    public void ATTACK_POS02_AttackCommand_FromNode_DoesNotJumpToNextNode()
    {
      var world = CreateWorld();
      var leader = SpawnCharacter(world, "Pursuer", NodeA);
      var pursuer = CreateArmyAtHex(world, FactionA, NodeA, HexA, leader);
      var enemyLeader = SpawnCharacter(world, "Enemy", NodeC, FactionB);
      var (_, targetStack) = RegisterLinkedEnemy(world, "army:enemy_pos02", NodeC, HexC, enemyLeader);

      var before = FormalArmyStrategicPositionSnapshot.Capture(pursuer);
      ExecuteAttackCommand(world, pursuer, targetStack);
      var after = FormalArmyStrategicPositionSnapshot.Capture(pursuer);
      AssertNoStrategicTeleport(before, after, "ATTACK-POS-02");
      Assert.AreEqual(HexA, after.CurrentHex);
    }

    [Test]
    public void ATTACK_POS03_AttackFromMidRoute_CrossRoute_PreservesCurrentProgress()
    {
      var world = CreateWorld();
      var leader = SpawnCharacter(world, "Pursuer", NodeA);
      var pursuer = CreateArmyAtHex(world, FactionA, NodeA, HexA, leader);
      var pursuerHex = HexAlongPath(world, HexA, HexB, 0.40f);
      FormalArmyTestSupport.SetHexMidTravel(world, pursuer, pursuerHex, HexB, 0.40f);
      world.WorldPresence.SetAtNode(leader, NodeA);

      var enemyLeader = SpawnCharacter(world, "Enemy", NodeB, FactionB);
      var targetHex = HexAlongPath(world, HexB, HexC, 0.70f);
      var (_, targetStack) = RegisterLinkedEnemy(world, "army:enemy_pos03", NodeB, targetHex, enemyLeader);
      Assert.IsTrue(world.Strategic.FormalArmies.TryGet(targetStack.FormalArmyId, out var targetArmy));
      FormalArmyTestSupport.SetHexMidTravel(world, targetArmy, targetHex, HexC, 0.20f);
      ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, targetStack);

      var before = FormalArmyStrategicPositionSnapshot.Capture(pursuer);
      ExecuteAttackCommand(world, pursuer, targetStack);
      var after = FormalArmyStrategicPositionSnapshot.Capture(pursuer);
      AssertNoStrategicTeleport(before, after, "ATTACK-POS-03");
      Assert.AreEqual(0.40f, after.StepProgress, 0.001f);
      Assert.AreEqual(pursuerHex, after.CurrentHex);
    }

    [Test]
    public void ATTACK_POS04_AttackFromMidRoute_SameRoute_DoesNotResetProgress()
    {
      var world = CreateWorld();
      var leader = SpawnCharacter(world, "Pursuer", NodeA);
      var pursuer = CreateArmyAtHex(world, FactionA, NodeA, HexA, leader);
      var pursuerHex = HexAlongPath(world, HexA, HexB, 0.35f);
      FormalArmyTestSupport.SetHexMidTravel(world, pursuer, pursuerHex, HexB, 0.35f);

      var enemyLeader = SpawnCharacter(world, "Enemy", NodeA, FactionB);
      var enemyHex = HexAlongPath(world, HexA, HexB, 0.70f);
      var (_, targetStack) = RegisterLinkedEnemy(world, "army:enemy_pos04", NodeA, enemyHex, enemyLeader);
      Assert.IsTrue(world.Strategic.FormalArmies.TryGet(targetStack.FormalArmyId, out var targetArmy));
      FormalArmyTestSupport.SetHexMidTravel(world, targetArmy, enemyHex, HexB, 0.20f);
      ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, targetStack);

      var before = FormalArmyStrategicPositionSnapshot.Capture(pursuer);
      ExecuteAttackCommand(world, pursuer, targetStack);
      var after = FormalArmyStrategicPositionSnapshot.Capture(pursuer);
      AssertNoStrategicTeleport(before, after, "ATTACK-POS-04");
      Assert.AreEqual(0.35f, after.StepProgress, 0.001f);
    }

    [Test]
    public void ATTACK_POS05_PursuitRepath_DoesNotTeleportPursuer()
    {
      var world = CreateWorld();
      var leader = SpawnCharacter(world, "Pursuer", NodeA);
      var pursuer = CreateArmyAtHex(world, FactionA, NodeA, HexA, leader);
      var pursuerHex = HexAlongPath(world, HexA, HexB, 0.55f);
      FormalArmyTestSupport.SetHexMidTravel(world, pursuer, pursuerHex, HexB, 0.55f);

      var enemyLeader = SpawnCharacter(world, "Enemy", NodeA, FactionB);
      var enemyHex = HexAlongPath(world, HexA, HexB, 0.80f);
      var (targetArmy, targetStack) = RegisterLinkedEnemy(world, "army:enemy_pos05", NodeA, enemyHex, enemyLeader);
      FormalArmyTestSupport.SetHexMidTravel(world, targetArmy, enemyHex, HexB, 0.20f);
      ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, targetStack);

      ExecuteAttackCommand(world, pursuer, targetStack);
      var beforeRepath = FormalArmyStrategicPositionSnapshot.Capture(pursuer);

      Assert.IsTrue(ArmyHexCommandService.MoveArmy(world, targetArmy.ArmyId, HexC).IsSuccess);
      ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, targetStack);
      ArmyHexPursuitService.AfterTravelTick(world);

      var afterRepath = FormalArmyStrategicPositionSnapshot.Capture(pursuer);
      AssertNoStrategicTeleport(beforeRepath, afterRepath, "ATTACK-POS-05");
      Assert.AreEqual(0.55f, afterRepath.StepProgress, 0.001f);
      Assert.AreEqual(pursuerHex, afterRepath.CurrentHex);
    }

    [Test]
    public void ATTACK_POS06_AttackTravel_FirstTickMovesContinuouslyFromPreviousPosition()
    {
      var world = CreateWorld();
      var leader = SpawnCharacter(world, "Pursuer", NodeA);
      var pursuer = CreateArmyAtHex(world, FactionA, NodeA, HexA, leader);
      var pursuerHex = HexAlongPath(world, HexA, HexB, 0.40f);
      FormalArmyTestSupport.SetHexMidTravel(world, pursuer, pursuerHex, HexB, 0.40f);

      var enemyLeader = SpawnCharacter(world, "Enemy", NodeA, FactionB);
      var enemyHex = HexAlongPath(world, HexA, HexB, 0.80f);
      var (_, targetStack) = RegisterLinkedEnemy(world, "army:enemy_pos06", NodeA, enemyHex, enemyLeader);
      Assert.IsTrue(world.Strategic.FormalArmies.TryGet(targetStack.FormalArmyId, out var targetArmy));
      FormalArmyTestSupport.SetHexMidTravel(world, targetArmy, enemyHex, HexB, 0.20f);
      ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, targetStack);

      var before = FormalArmyStrategicPositionSnapshot.Capture(pursuer);
      ExecuteAttackCommand(world, pursuer, targetStack);
      var afterCommand = FormalArmyStrategicPositionSnapshot.Capture(pursuer);
      AssertNoStrategicTeleport(before, afterCommand, "ATTACK-POS-06 command frame");

      AdvanceWorldTicks(world, 1);
      var afterTick = FormalArmyStrategicPositionSnapshot.Capture(pursuer);
      Assert.GreaterOrEqual(afterTick.StepProgress, before.StepProgress - 0.001f, "first tick must not backjump from step progress");
    }

    [Test]
    public void ATTACK_POS07_MovingTarget_Repath_RemainsContinuousAcrossRouteChange()
    {
      var world = CreateWorld();
      var leader = SpawnCharacter(world, "Pursuer", NodeA);
      var pursuer = CreateArmyAtHex(world, FactionA, NodeA, HexA, leader);
      var pursuerHex = HexAlongPath(world, HexA, HexB, 0.42f);
      FormalArmyTestSupport.SetHexMidTravel(world, pursuer, pursuerHex, HexB, 0.42f);

      var enemyLeader = SpawnCharacter(world, "Enemy", NodeA, FactionB);
      var (targetArmy, targetStack) = RegisterLinkedEnemy(world, "army:enemy_pos07", NodeA, HexA, enemyLeader);
      Assert.IsTrue(ArmyHexCommandService.MoveArmy(world, targetArmy.ArmyId, HexB).IsSuccess);
      ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, targetStack);

      ExecuteAttackCommand(world, pursuer, targetStack);
      var before = FormalArmyStrategicPositionSnapshot.Capture(pursuer);

      Assert.IsTrue(ArmyHexCommandService.MoveArmy(world, targetArmy.ArmyId, HexC).IsSuccess);
      ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, targetStack);
      ArmyHexPursuitService.AfterTravelTick(world);

      var afterRepath = FormalArmyStrategicPositionSnapshot.Capture(pursuer);
      AssertNoStrategicTeleport(before, afterRepath, "ATTACK-POS-07 repath frame");
      AdvanceWorldTicks(world, 3);
      Assert.IsTrue(pursuer.State == FormalArmyState.Moving || pursuer.DestinationHex != pursuer.CurrentHex);
    }
  }
}
