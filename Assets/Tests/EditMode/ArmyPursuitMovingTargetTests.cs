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
  public sealed class ArmyPursuitMovingTargetTests
  {
    const string FactionA = "test:faction_a";
    const string FactionB = "test:faction_b";
    const string NodeA = "test:node_a";
    const string NodeB = "test:node_b";
    const string NodeC = "test:node_c";
    const string RouteAb = "test:route_ab";
    const string RouteBc = "test:route_bc";

    static SimulationWorld CreateWorld()
    {
      var world = new SimulationWorld();
      world.WorldGraph.RegisterNode(new WorldNodeState { Id = NodeA, Name = "A", OwnerId = FactionA, WorldX = 0f, WorldY = 0f });
      world.WorldGraph.RegisterNode(new WorldNodeState { Id = NodeB, Name = "B", OwnerId = FactionA, WorldX = 10f, WorldY = 0f });
      world.WorldGraph.RegisterNode(new WorldNodeState { Id = NodeC, Name = "C", OwnerId = FactionA, WorldX = 20f, WorldY = 0f });
      world.WorldGraph.RegisterRoute(new WorldRouteState { Id = RouteAb, FromNodeId = NodeA, ToNodeId = NodeB, TravelCost = 1 });
      world.WorldGraph.RegisterRoute(new WorldRouteState { Id = RouteBc, FromNodeId = NodeB, ToNodeId = NodeC, TravelCost = 1 });
      return world;
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

    static (FormalArmy army, ArmyStack stack) RegisterLinkedEnemy(
      SimulationWorld world,
      string stackId,
      string nodeId,
      EntityId leader)
    {
      var created = ArmyService.CreateArmy(world, FactionB, nodeId, new[] { leader });
      Assert.IsTrue(created.IsSuccess);
      var army = created.Value;
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
        WorldTravelService.AdvanceTravel(world, 1, StrategicTravelDriver.BeginArrivalCapture());
        StrategicTravelDriver.AfterTravelTick(world, 1);
      }
    }

    static void BeginPursuitAndStartTravel(SimulationWorld world, FormalArmy pursuer, ArmyStack targetStack)
    {
      StrategicPursuitService.BeginPursuitArmy(world, pursuer.ArmyId, targetStack);
      Assert.IsTrue(ArmyTravelCommandService.MoveArmyToTargetArmy(world, pursuer.ArmyId, targetStack.FormalArmyId).IsSuccess);
    }

    [Test]
    public void PUR01_StaticTargetSameRoute_ReachesBattleOffer()
    {
      var world = CreateWorld();
      var leader = SpawnCharacter(world, "Pursuer", NodeA);
      var pursuer = ArmyService.CreateArmy(world, FactionA, NodeA, new[] { leader }).Value;
      var enemyLeader = SpawnCharacter(world, "Enemy", NodeA, FactionB);
      var (targetArmy, targetStack) = RegisterLinkedEnemy(world, "army:enemy_static", NodeA, enemyLeader);
      targetArmy.RouteId = RouteAb;
      targetArmy.DestNodeId = NodeB;
      targetArmy.RouteAnchorProgress = 0.62f;
      targetArmy.State = FormalArmyState.AtNode;
      ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, targetStack);

      BeginPursuitAndStartTravel(world, pursuer, targetStack);
      AdvanceWorldTicks(world, 200);
      Assert.IsTrue(world.Strategic.HasBattleOffer, "PUR-01: static route target should produce BattleOffer");
    }

    [Test]
    public void PUR02_MovingTargetSameRoute_FasterPursuer_ContinuesAndContacts()
    {
      var world = CreateWorld();
      var leader = SpawnCharacter(world, "Pursuer", NodeA);
      var pursuer = ArmyService.CreateArmy(world, FactionA, NodeA, new[] { leader }).Value;
      var enemyLeader = SpawnCharacter(world, "Enemy", NodeA, FactionB);
      var (targetArmy, targetStack) = RegisterLinkedEnemy(world, "army:enemy_move", NodeA, enemyLeader);
      Assert.IsTrue(ArmyTravelCommandService.MoveArmyToNode(world, targetArmy.ArmyId, NodeB).IsSuccess);
      ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, targetStack);

      BeginPursuitAndStartTravel(world, pursuer, targetStack);
      Assert.IsTrue(world.Strategic.FormalArmies.TryGet(pursuer.ArmyId, out var formal));
      formal.TravelTotalTicks = System.Math.Max(8, formal.TravelTotalTicks / 3);
      formal.RemainingTravelTicks = formal.TravelTotalTicks;

      var origin = formal.RouteSegmentOriginProgress;
      var maxProgress = 0f;
      for (var i = 0; i < 120 && !world.Strategic.HasBattleOffer; i++)
      {
        AdvanceWorldTicks(world, 1);
        Assert.IsTrue(formal.IsTraveling, "PUR-02: pursuer must keep traveling while chasing moving target");
        var progress = formal.GetRouteDisplayProgress();
        Assert.GreaterOrEqual(progress, maxProgress - 0.001f, "PUR-02: progress must not backjump");
        maxProgress = System.Math.Max(maxProgress, progress);
        if (i > 0)
          Assert.AreEqual(origin, formal.RouteSegmentOriginProgress, "PUR-02: must not restart segment origin each tick");
      }

      Assert.IsTrue(world.Strategic.HasBattleOffer, "PUR-02: faster pursuer should eventually contact moving target");
    }

    [Test]
    public void PUR03_MovingTargetSameSpeed_PursuitStaysActive_NoFalseBattleOffer()
    {
      var world = CreateWorld();
      var leader = SpawnCharacter(world, "Pursuer", NodeA);
      var pursuer = ArmyService.CreateArmy(world, FactionA, NodeA, new[] { leader }).Value;
      var enemyLeader = SpawnCharacter(world, "Enemy", NodeA, FactionB);
      var (targetArmy, targetStack) = RegisterLinkedEnemy(world, "army:enemy_pace", NodeA, enemyLeader);
      Assert.IsTrue(ArmyTravelCommandService.MoveArmyToNode(world, targetArmy.ArmyId, NodeB).IsSuccess);
      ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, targetStack);

      BeginPursuitAndStartTravel(world, pursuer, targetStack);
      Assert.IsTrue(world.Strategic.FormalArmies.TryGet(pursuer.ArmyId, out var formal));
      Assert.IsTrue(world.Strategic.FormalArmies.TryGet(targetArmy.ArmyId, out var target));

      for (var i = 0; i < 30; i++)
      {
        AdvanceWorldTicks(world, 1);
        Assert.IsFalse(world.Strategic.HasBattleOffer, "PUR-03: same-speed pursuit must not false-contact early");
        Assert.IsTrue(formal.IsTraveling, "PUR-03: pursuit should remain active");
        Assert.Greater(target.GetRouteDisplayProgress(), 0.05f, "PUR-03: target should advance via real ticks");
      }
    }

    [Test]
    public void PUR04_TargetChangesRoute_PursuerRepaths()
    {
      var world = CreateWorld();
      var leader = SpawnCharacter(world, "Pursuer", NodeA);
      var pursuer = ArmyService.CreateArmy(world, FactionA, NodeA, new[] { leader }).Value;
      var enemyLeader = SpawnCharacter(world, "Enemy", NodeA, FactionB);
      var (targetArmy, targetStack) = RegisterLinkedEnemy(world, "army:enemy_reroute", NodeA, enemyLeader);
      Assert.IsTrue(ArmyTravelCommandService.MoveArmyToNode(world, targetArmy.ArmyId, NodeB).IsSuccess);

      BeginPursuitAndStartTravel(world, pursuer, targetStack);
      AdvanceWorldTicks(world, 40);
      Assert.IsTrue(ArmyTravelCommandService.MoveArmyToNode(world, targetArmy.ArmyId, NodeC).IsSuccess);
      ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, targetStack);

      Assert.IsTrue(world.Strategic.FormalArmies.TryGet(pursuer.ArmyId, out var formal));
      var beforeDest = formal.DestNodeId;
      AdvanceWorldTicks(world, 5);
      Assert.IsTrue(formal.IsTraveling || ArmyTravelCommandService.HasPendingLegs(formal.ArmyId),
        "PUR-04: pursuer should continue after target topology change");
      Assert.IsTrue(
        string.Equals(formal.DestNodeId, NodeC, System.StringComparison.Ordinal) ||
        ArmyTravelCommandService.HasPendingLegs(formal.ArmyId) ||
        !string.Equals(beforeDest, formal.DestNodeId, System.StringComparison.Ordinal),
        "PUR-04: pursuer should repath toward new target topology");
    }

    [Test]
    public void PUR05_CrossNodePursuit_DoesNotUseStaleTargetProgress()
    {
      var world = CreateWorld();
      var leader = SpawnCharacter(world, "Pursuer", NodeA);
      var pursuer = ArmyService.CreateArmy(world, FactionA, NodeA, new[] { leader }).Value;
      var enemyLeader = SpawnCharacter(world, "Enemy", NodeB, FactionB);
      var (targetArmy, targetStack) = RegisterLinkedEnemy(world, "army:enemy_far", NodeB, enemyLeader);
      targetArmy.RouteId = RouteBc;
      targetArmy.DestNodeId = NodeC;
      targetArmy.RouteAnchorProgress = 0.15f;
      targetArmy.State = FormalArmyState.AtNode;
      ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, targetStack);

      BeginPursuitAndStartTravel(world, pursuer, targetStack);
      Assert.IsTrue(ArmyTravelCommandService.HasPendingLegs(pursuer.ArmyId) || world.Strategic.FormalArmies.TryGet(pursuer.ArmyId, out var formal) && formal.IsTraveling);
      AdvanceWorldTicks(world, 200);
      Assert.IsFalse(
        world.Strategic.FormalArmies.TryGet(pursuer.ArmyId, out var done) &&
        done.IsRouteAnchored &&
        done.RouteAnchorProgress > 0.1f &&
        done.RouteAnchorProgress < 0.2f &&
        string.Equals(done.RouteId, RouteBc, System.StringComparison.Ordinal) &&
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
      var pursuer = ArmyService.CreateArmy(world, FactionA, NodeA, new[] { leader }).Value;
      var enemyLeader = SpawnCharacter(world, "Enemy", NodeA, FactionB);
      var (targetArmy, targetStack) = RegisterLinkedEnemy(world, "army:enemy_opp", NodeA, enemyLeader);

      pursuer.State = FormalArmyState.OnRoute;
      pursuer.RouteId = RouteAb;
      pursuer.NodeId = NodeA;
      pursuer.DestNodeId = NodeB;
      pursuer.RouteSegmentOriginProgress = 0.54f;
      pursuer.RouteSegmentEndProgress = 1f;
      pursuer.TravelTotalTicks = 10;
      pursuer.RemainingTravelTicks = 10;
      targetArmy.State = FormalArmyState.OnRoute;
      targetArmy.RouteId = RouteAb;
      targetArmy.NodeId = NodeB;
      targetArmy.DestNodeId = NodeA;
      targetArmy.RouteSegmentOriginProgress = 0.46f;
      targetArmy.RouteSegmentEndProgress = 0f;
      targetArmy.TravelTotalTicks = 10;
      targetArmy.RemainingTravelTicks = 10;
      ArmyPresenceAdapter.SyncFromArmy(world, pursuer);
      ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, targetStack);

      StrategicPursuitService.BeginPursuitArmy(world, pursuer.ArmyId, targetStack);
      ArmyPursuitTargetService.CaptureTickSnapshot(pursuer, targetArmy);
      AdvanceWorldTicks(world, 1);
      Assert.IsTrue(world.Strategic.HasBattleOffer, "PUR-06: pursuit pair swept contact should open BattleOffer");
    }

    [Test]
    public void PUR07_TargetStopsOnRoute_PursuerCatches()
    {
      var world = CreateWorld();
      var leader = SpawnCharacter(world, "Pursuer", NodeA);
      var pursuer = ArmyService.CreateArmy(world, FactionA, NodeA, new[] { leader }).Value;
      var enemyLeader = SpawnCharacter(world, "Enemy", NodeA, FactionB);
      var (targetArmy, targetStack) = RegisterLinkedEnemy(world, "army:enemy_stop", NodeA, enemyLeader);
      Assert.IsTrue(ArmyTravelCommandService.MoveArmyToRouteProgress(world, targetArmy.ArmyId, RouteAb, 0.7f).IsSuccess);
      AdvanceWorldTicks(world, 50);
      targetArmy.ClearTravel();
      targetArmy.State = FormalArmyState.AtNode;
      targetArmy.RouteId = RouteAb;
      targetArmy.RouteAnchorProgress = 0.7f;
      targetArmy.DestNodeId = NodeB;
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
      var pursuer = ArmyService.CreateArmy(world, FactionA, NodeA, new[] { leader }).Value;
      var enemyLeader = SpawnCharacter(world, "Enemy", NodeA, FactionB);
      var (_, targetStack) = RegisterLinkedEnemy(world, "army:enemy_cancel", NodeA, enemyLeader);
      BeginPursuitAndStartTravel(world, pursuer, targetStack);
      Assert.IsFalse(string.IsNullOrEmpty(world.Strategic.Encounter.PursueStackId));

      Assert.IsTrue(ArmyTravelCommandService.MoveArmyToNode(world, pursuer.ArmyId, NodeB).IsSuccess);
      Assert.IsTrue(string.IsNullOrEmpty(world.Strategic.Encounter.PursueStackId), "PUR-08: new MoveOrder must clear pursuit");
    }

    [Test]
    public void PUR09_TargetRemoved_ClearsPursuitSafely()
    {
      var world = CreateWorld();
      var leader = SpawnCharacter(world, "Pursuer", NodeA);
      var pursuer = ArmyService.CreateArmy(world, FactionA, NodeA, new[] { leader }).Value;
      var enemyLeader = SpawnCharacter(world, "Enemy", NodeA, FactionB);
      var (targetArmy, targetStack) = RegisterLinkedEnemy(world, "army:enemy_gone", NodeA, enemyLeader);
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
      var pursuer = ArmyService.CreateArmy(world, FactionA, NodeA, new[] { leader }).Value;
      var enemyLeader = SpawnCharacter(world, "Enemy", NodeA, FactionB);
      var (targetArmy, targetStack) = RegisterLinkedEnemy(world, "army:enemy_stale", NodeA, enemyLeader);
      Assert.IsTrue(ArmyTravelCommandService.MoveArmyToNode(world, targetArmy.ArmyId, NodeB).IsSuccess);
      targetStack.RouteAnchorProgress = 0.05f;
      targetStack.RouteId = RouteAb;

      BeginPursuitAndStartTravel(world, pursuer, targetStack);
      AdvanceWorldTicks(world, 10);
      ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, targetStack);
      Assert.Greater(targetStack.RouteAnchorProgress, 0.1f, "PUR-10: stack must follow FormalArmy live position after sync");
      Assert.IsTrue(world.Strategic.FormalArmies.TryGet(pursuer.ArmyId, out var formal) && formal.IsTraveling);
    }

    [Test]
    public void PUR11_TargetProgressChanges_NoPrepareTravelEveryTick()
    {
      var world = CreateWorld();
      var leader = SpawnCharacter(world, "Pursuer", NodeA);
      var pursuer = ArmyService.CreateArmy(world, FactionA, NodeA, new[] { leader }).Value;
      var enemyLeader = SpawnCharacter(world, "Enemy", NodeA, FactionB);
      var (targetArmy, targetStack) = RegisterLinkedEnemy(world, "army:enemy_tick", NodeA, enemyLeader);
      Assert.IsTrue(ArmyTravelCommandService.MoveArmyToNode(world, targetArmy.ArmyId, NodeB).IsSuccess);
      BeginPursuitAndStartTravel(world, pursuer, targetStack);
      Assert.IsTrue(world.Strategic.FormalArmies.TryGet(pursuer.ArmyId, out var formal));

      var segmentOrigin = formal.RouteSegmentOriginProgress;
      var segmentEnd = formal.RouteSegmentEndProgress;
      var totalTicks = formal.TravelTotalTicks;
      for (var i = 0; i < 20; i++)
      {
        var ticksBefore = formal.RemainingTravelTicks;
        AdvanceWorldTicks(world, 1);
        Assert.AreEqual(segmentOrigin, formal.RouteSegmentOriginProgress, "PUR-11: segment origin must stay stable");
        Assert.AreEqual(segmentEnd, formal.RouteSegmentEndProgress, "PUR-11: segment end must stay stable");
        Assert.AreEqual(totalTicks, formal.TravelTotalTicks, "PUR-11: must not restart travel plan each tick");
        Assert.LessOrEqual(formal.RemainingTravelTicks, ticksBefore, "PUR-11: remaining ticks should decrease monotonically");
      }
    }

    static void formalSpeedBoost(SimulationWorld world, string armyId)
    {
      if (!world.Strategic.FormalArmies.TryGet(armyId, out var army) || army == null)
        return;
      army.TravelTotalTicks = System.Math.Max(8, army.TravelTotalTicks / 4);
      army.RemainingTravelTicks = army.TravelTotalTicks;
    }
  }
}
