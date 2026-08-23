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
  public sealed class ArmyPursuitMovingTargetTests
  {
    const string FactionA = "test:faction_a";
    const string FactionB = "test:faction_b";
    const string NodeA = "base:node_huangcun";
    const string NodeB = "base:node_qingyun_lu";
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
      world.WorldGraph.RegisterNode(new WorldNodeState
      {
        Id = NodeC,
        Name = "C",
        OwnerId = FactionA,
        WorldX = 20f,
        WorldY = 0f
      });
      if (world.WorldGraph.TryGetNode(NodeC, out var node) && node != null)
        WorldSiteRegistrationService.LinkLegacyNodeToHex(node, HexC);
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

    static void AdvanceWorldTicks(SimulationWorld world, int ticks)
    {
      for (var i = 0; i < ticks; i++)
      {
        ArmyHexTravelService.AdvanceAll(world, 1);
        ArmyStackAdapter.SyncAllLinkedStacksFromFormalArmies(world);
        ArmyHexPursuitService.AfterTravelTick(world);
      }
    }

    static void BeginPursuitAndStartTravel(SimulationWorld world, FormalArmy pursuer, ArmyStack targetStack)
    {
      Assert.IsTrue(ArmyHexCommandService.AttackStack(world, pursuer.ArmyId, targetStack).IsSuccess);
    }

    [Test]
    public void PUR01_StaticTargetSameRoute_ReachesBattleOffer()
    {
      var world = CreateWorld();
      var leader = SpawnCharacter(world, "Pursuer", NodeA);
      var pursuer = CreateArmyAtHex(world, FactionA, NodeA, HexA, leader);
      var enemyLeader = SpawnCharacter(world, "Enemy", NodeA, FactionB);
      var targetHex = HexAlongPath(world, HexA, HexB, 0.62f);
      var (targetArmy, targetStack) = RegisterLinkedEnemy(world, "army:enemy_static", NodeA, targetHex, enemyLeader);
      FormalArmyTestSupport.SetHexMidTravel(world, targetArmy, targetHex, HexB, 0.10f);
      ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, targetStack);

      BeginPursuitAndStartTravel(world, pursuer, targetStack);
      AdvanceWorldTicks(world, 200);
      Assert.IsTrue(world.Strategic.HasBattleOffer, "PUR-01: static hex target should produce BattleOffer");
    }

    [Test]
    public void PUR02_MovingTargetSameRoute_FasterPursuer_ContinuesAndContacts()
    {
      var world = CreateWorld();
      var leader = SpawnCharacter(world, "Pursuer", NodeA);
      var pursuer = CreateArmyAtHex(world, FactionA, NodeA, HexA, leader);
      var enemyLeader = SpawnCharacter(world, "Enemy", NodeA, FactionB);
      var (targetArmy, targetStack) = RegisterLinkedEnemy(world, "army:enemy_move", NodeA, HexA, enemyLeader);
      Assert.IsTrue(ArmyHexCommandService.MoveArmy(world, targetArmy.ArmyId, HexB).IsSuccess);
      ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, targetStack);

      BeginPursuitAndStartTravel(world, pursuer, targetStack);
      Assert.IsTrue(world.Strategic.FormalArmies.TryGet(pursuer.ArmyId, out var formal));
      FormalArmyTestSupport.ScaleHexStepTicks(formal, 3);

      var originDest = formal.DestinationHex;
      var maxProgress = 0f;
      for (var i = 0; i < 120 && !world.Strategic.HasBattleOffer; i++)
      {
        AdvanceWorldTicks(world, 1);
        Assert.AreEqual(FormalArmyState.Moving, formal.State, "PUR-02: pursuer must keep traveling while chasing moving target");
        var progress = formal.StepProgress;
        Assert.GreaterOrEqual(progress, maxProgress - 0.001f, "PUR-02: progress must not backjump");
        maxProgress = System.Math.Max(maxProgress, progress);
        if (i > 0)
          Assert.AreEqual(originDest, formal.DestinationHex, "PUR-02: must not restart destination each tick");
      }

      Assert.IsTrue(world.Strategic.HasBattleOffer, "PUR-02: faster pursuer should eventually contact moving target");
    }

    [Test]
    public void PUR03_MovingTargetSameSpeed_PursuitStaysActive_NoFalseBattleOffer()
    {
      var world = CreateWorld();
      var leader = SpawnCharacter(world, "Pursuer", NodeA);
      var pursuer = CreateArmyAtHex(world, FactionA, NodeA, HexA, leader);
      var enemyLeader = SpawnCharacter(world, "Enemy", NodeA, FactionB);
      var (targetArmy, targetStack) = RegisterLinkedEnemy(world, "army:enemy_pace", NodeA, HexA, enemyLeader);
      Assert.IsTrue(ArmyHexCommandService.MoveArmy(world, targetArmy.ArmyId, HexB).IsSuccess);
      ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, targetStack);

      BeginPursuitAndStartTravel(world, pursuer, targetStack);
      Assert.IsTrue(world.Strategic.FormalArmies.TryGet(pursuer.ArmyId, out var formal));
      Assert.IsTrue(world.Strategic.FormalArmies.TryGet(targetArmy.ArmyId, out var target));

      for (var i = 0; i < 30; i++)
      {
        AdvanceWorldTicks(world, 1);
        Assert.IsFalse(world.Strategic.HasBattleOffer, "PUR-03: same-speed pursuit must not false-contact early");
        Assert.AreEqual(FormalArmyState.Moving, formal.State, "PUR-03: pursuit should remain active");
        Assert.IsTrue(
          target.State == FormalArmyState.Moving || !target.CurrentHex.Equals(HexA),
          "PUR-03: target should advance via real ticks");
      }
    }

    [Test]
    public void PUR04_TargetChangesRoute_PursuerRepaths()
    {
      var world = CreateWorld();
      var leader = SpawnCharacter(world, "Pursuer", NodeA);
      var pursuer = CreateArmyAtHex(world, FactionA, NodeA, HexA, leader);
      var enemyLeader = SpawnCharacter(world, "Enemy", NodeA, FactionB);
      var (targetArmy, targetStack) = RegisterLinkedEnemy(world, "army:enemy_reroute", NodeA, HexA, enemyLeader);
      Assert.IsTrue(ArmyHexCommandService.MoveArmy(world, targetArmy.ArmyId, HexB).IsSuccess);

      BeginPursuitAndStartTravel(world, pursuer, targetStack);
      AdvanceWorldTicks(world, 40);
      Assert.IsTrue(ArmyHexCommandService.MoveArmy(world, targetArmy.ArmyId, HexC).IsSuccess);
      ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, targetStack);

      Assert.IsTrue(world.Strategic.FormalArmies.TryGet(pursuer.ArmyId, out var formal));
      var beforeDest = formal.DestinationHex;
      AdvanceWorldTicks(world, 5);
      Assert.IsTrue(
        formal.State == FormalArmyState.Moving || !formal.DestinationHex.Equals(beforeDest),
        "PUR-04: pursuer should continue after target topology change");
      Assert.IsTrue(
        formal.DestinationHex.Equals(HexC) ||
        !formal.DestinationHex.Equals(beforeDest),
        "PUR-04: pursuer should repath toward new target topology");
    }

    [Test]
    public void PUR05_CrossNodePursuit_DoesNotUseStaleTargetProgress()
    {
      var world = CreateWorld();
      var leader = SpawnCharacter(world, "Pursuer", NodeA);
      var pursuer = CreateArmyAtHex(world, FactionA, NodeA, HexA, leader);
      var enemyLeader = SpawnCharacter(world, "Enemy", NodeB, FactionB);
      var targetHex = HexAlongPath(world, HexB, HexC, 0.15f);
      var (targetArmy, targetStack) = RegisterLinkedEnemy(world, "army:enemy_far", NodeB, targetHex, enemyLeader);
      FormalArmyTestSupport.SetHexMidTravel(world, targetArmy, targetHex, HexC, 0.10f);
      ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, targetStack);

      BeginPursuitAndStartTravel(world, pursuer, targetStack);
      Assert.IsTrue(
        pursuer.State == FormalArmyState.Moving ||
        !pursuer.CurrentHex.Equals(HexA));
      AdvanceWorldTicks(world, 200);
      Assert.IsFalse(
        world.Strategic.FormalArmies.TryGet(pursuer.ArmyId, out var done) &&
        done.State == FormalArmyState.Idle &&
        done.CurrentHex.Equals(HexAlongPath(world, HexB, HexC, 0.15f)) &&
        !world.Strategic.HasBattleOffer,
        "PUR-05: pursuer must not stop at stale command-time enemy progress");
    }

    [Test]
    public void PUR06_OppositeDirection_SweptContact_BattleOffer()
    {
      Assert.IsTrue(
        ArmyPursuitTargetService.DetectSweptRouteContact(0.45f, 0.55f, 0.56f, 0.44f),
        "PUR-06: swept crossing detection");

      var world = CreateWorld();
      var leader = SpawnCharacter(world, "Pursuer", NodeA);
      var pursuer = CreateArmyAtHex(world, FactionA, NodeA, HexA, leader);
      var enemyLeader = SpawnCharacter(world, "Enemy", NodeA, FactionB);
      var (targetArmy, targetStack) = RegisterLinkedEnemy(world, "army:enemy_opp", NodeA, HexB, enemyLeader);

      var pursuerHex = HexAlongPath(world, HexA, HexB, 0.54f);
      var targetHex = HexAlongPath(world, HexA, HexB, 0.46f);
      FormalArmyTestSupport.SetHexMidTravel(world, pursuer, pursuerHex, HexB, 0.10f);
      FormalArmyTestSupport.SetHexMidTravel(world, targetArmy, targetHex, HexA, 0.10f);
      ArmyPresenceAdapter.SyncFromArmy(world, pursuer);
      ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, targetStack);

      Assert.IsTrue(ArmyHexCommandService.AttackStack(world, pursuer.ArmyId, targetStack).IsSuccess);
      AdvanceWorldTicks(world, 40);
      Assert.IsTrue(world.Strategic.HasBattleOffer, "PUR-06: pursuit pair swept contact should open BattleOffer");
    }

    [Test]
    public void PUR07_TargetStopsOnRoute_PursuerCatches()
    {
      var world = CreateWorld();
      var leader = SpawnCharacter(world, "Pursuer", NodeA);
      var pursuer = CreateArmyAtHex(world, FactionA, NodeA, HexA, leader);
      var enemyLeader = SpawnCharacter(world, "Enemy", NodeA, FactionB);
      var targetHex = HexAlongPath(world, HexA, HexB, 0.7f);
      var (targetArmy, targetStack) = RegisterLinkedEnemy(world, "army:enemy_stop", NodeA, targetHex, enemyLeader);
      FormalArmyTestSupport.SetHexMidTravel(world, targetArmy, targetHex, HexB, 0.05f);
      ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, targetStack);

      BeginPursuitAndStartTravel(world, pursuer, targetStack);
      formalSpeedBoost(world, pursuer.ArmyId);
      AdvanceWorldTicks(world, 120);
      Assert.IsTrue(world.Strategic.HasBattleOffer, "PUR-07: pursuer should catch stopped target");
    }

    [Test]
    public void PUR08_NewMoveOrder_ClearsPursuit()
    {
      var world = CreateWorld();
      var leader = SpawnCharacter(world, "Pursuer", NodeA);
      var pursuer = CreateArmyAtHex(world, FactionA, NodeA, HexA, leader);
      var enemyLeader = SpawnCharacter(world, "Enemy", NodeA, FactionB);
      var (_, targetStack) = RegisterLinkedEnemy(world, "army:enemy_cancel", NodeA, HexA, enemyLeader);
      BeginPursuitAndStartTravel(world, pursuer, targetStack);
      Assert.IsFalse(string.IsNullOrEmpty(world.Strategic.Encounter.PursueStackId));

      Assert.IsTrue(ArmyHexCommandService.MoveArmy(world, pursuer.ArmyId, HexB).IsSuccess);
      Assert.IsTrue(string.IsNullOrEmpty(world.Strategic.Encounter.PursueStackId), "PUR-08: new MoveOrder must clear pursuit");
    }

    [Test]
    public void PUR09_TargetRemoved_ClearsPursuitSafely()
    {
      var world = CreateWorld();
      var leader = SpawnCharacter(world, "Pursuer", NodeA);
      var pursuer = CreateArmyAtHex(world, FactionA, NodeA, HexA, leader);
      var enemyLeader = SpawnCharacter(world, "Enemy", NodeA, FactionB);
      var (targetArmy, targetStack) = RegisterLinkedEnemy(world, "army:enemy_gone", NodeA, HexA, enemyLeader);
      BeginPursuitAndStartTravel(world, pursuer, targetStack);
      Assert.IsTrue(ArmyService.DisbandArmy(world, targetArmy.ArmyId).IsSuccess);
      targetStack.FormalArmyId = string.Empty;
      Assert.DoesNotThrow(() => AdvanceWorldTicks(world, 3));
      Assert.IsTrue(string.IsNullOrEmpty(world.Strategic.Encounter.PursueStackId), "PUR-09: invalid target must clear pursuit");
    }

    [Test]
    public void PUR10_StaleStackPresentation_FollowsFormalArmyLivePosition()
    {
      var world = CreateWorld();
      var leader = SpawnCharacter(world, "Pursuer", NodeA);
      var pursuer = CreateArmyAtHex(world, FactionA, NodeA, HexA, leader);
      var enemyLeader = SpawnCharacter(world, "Enemy", NodeA, FactionB);
      var (targetArmy, targetStack) = RegisterLinkedEnemy(world, "army:enemy_stale", NodeA, HexA, enemyLeader);
      Assert.IsTrue(ArmyHexCommandService.MoveArmy(world, targetArmy.ArmyId, HexB).IsSuccess);
      targetStack.NodeId = NodeA;

      BeginPursuitAndStartTravel(world, pursuer, targetStack);
      AdvanceWorldTicks(world, 10);
      ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, targetStack);
      Assert.IsFalse(
        targetStack.NodeId.Equals(NodeA) && targetArmy.CurrentHex.Equals(HexA),
        "PUR-10: stack must follow FormalArmy live position after sync");
      Assert.IsTrue(world.Strategic.FormalArmies.TryGet(pursuer.ArmyId, out var formal) && formal.State == FormalArmyState.Moving);
    }

    [Test]
    public void PUR11_TargetProgressChanges_NoPrepareTravelEveryTick()
    {
      var world = CreateWorld();
      var leader = SpawnCharacter(world, "Pursuer", NodeA);
      var pursuer = CreateArmyAtHex(world, FactionA, NodeA, HexA, leader);
      var enemyLeader = SpawnCharacter(world, "Enemy", NodeA, FactionB);
      var (targetArmy, targetStack) = RegisterLinkedEnemy(world, "army:enemy_tick", NodeA, HexA, enemyLeader);
      Assert.IsTrue(ArmyHexCommandService.MoveArmy(world, targetArmy.ArmyId, HexB).IsSuccess);
      BeginPursuitAndStartTravel(world, pursuer, targetStack);
      Assert.IsTrue(world.Strategic.FormalArmies.TryGet(pursuer.ArmyId, out var formal));

      var pathCount = formal.HexPathCount;
      var destinationHex = formal.DestinationHex;
      var stepTotal = formal.StepTotalTicks;
      for (var i = 0; i < 20; i++)
      {
        AdvanceWorldTicks(world, 1);
        Assert.AreEqual(destinationHex, formal.DestinationHex, "PUR-11: destination hex must stay stable");
        Assert.AreEqual(pathCount, formal.HexPathCount, "PUR-11: must not restart travel plan each tick");
        Assert.AreEqual(stepTotal, formal.StepTotalTicks, "PUR-11: must not restart travel plan each tick");
      }
    }

    static void formalSpeedBoost(SimulationWorld world, string armyId)
    {
      if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
        return;
      FormalArmyTestSupport.ScaleHexStepTicks(army, 4);
    }
  }
}
