using System.IO;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Random;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Data.Bootstrap;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    public sealed class StrategicPhaseTests
    {
        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        static PlayableDayBootstrapResult StartCh01()
        {
            var started = new PlayableDayBootstrap().Start(
                BaseGamePath,
                new PlayableDayOptions { OpeningScenarioId = "base:scenario_ch01_reference" });
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");
            return started.Value;
        }

        [Test]
        public void Phase1_Ch01_DefaultOwners_AreSeeded()
        {
            var world = StartCh01().World;
            Assert.IsTrue(world.WorldGraph.TryGetNode("base:node_huangcun", out var huangcun));
            Assert.AreEqual(StrategicFactionCatalog.HuangcunLaborId, huangcun.OwnerId);
            Assert.IsTrue(world.WorldGraph.TryGetNode("base:node_linjian", out var linjian));
            Assert.AreEqual(StrategicFactionCatalog.BanditId, linjian.OwnerId);
        }

        [Test]
        public void Phase2_DefaultDiplomacy_BanditsAtWar()
        {
            var world = StartCh01().World;
            var player = world.Strategic.PlayerFactionId;
            Assert.IsTrue(world.Strategic.Diplomacy.IsHostile(player, StrategicFactionCatalog.BanditId));
            Assert.IsFalse(world.Strategic.Diplomacy.IsHostile(player, StrategicFactionCatalog.FisherVillageId));
        }

        [Test]
        public void Phase2_SetStance_UpdatesHostileCheck()
        {
            var world = StartCh01().World;
            var player = world.Strategic.PlayerFactionId;
            var fisher = StrategicFactionCatalog.FisherVillageId;
            world.Strategic.Diplomacy.SetStance(player, fisher, FactionStance.War);
            Assert.IsTrue(world.Strategic.Diplomacy.IsHostile(player, fisher));
            world.Strategic.Diplomacy.SetStance(player, fisher, FactionStance.Friendly);
            Assert.IsFalse(world.Strategic.Diplomacy.IsHostile(player, fisher));
        }

        [Test]
        public void Phase3_BattleOffer_AutoResolve_CanRemoveEnemyStack()
        {
            var session = StartCh01();
            var world = session.World;
            var party = session.CharacterIds;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var enemy));
            Assert.IsNotNull(enemy);

            world.Random = new DeterministicRandom(7);
            Assert.IsTrue(BattleOfferService.TryBuildOfferForArmy(world, party, enemy, "测试接战"));
            Assert.IsTrue(world.Strategic.HasBlockingInterrupt);
            var stacksBefore = world.Strategic.Armies.Stacks.Count;

            var resolved = BattleOfferService.ResolveAuto(world, out _);
            Assert.IsTrue(resolved.IsSuccess, resolved.IsFailure ? resolved.Error.ToString() : "");
            Assert.IsFalse(world.Strategic.HasBlockingInterrupt);
            Assert.LessOrEqual(world.Strategic.Armies.Stacks.Count, stacksBefore);
        }

        [Test]
        public void Phase3_CombatPower_EstimateIsBounded()
        {
            var pct = CombatPowerCalculator.EstimateAutoWinPercent(10, 1);
            Assert.GreaterOrEqual(pct, 5);
            Assert.LessOrEqual(pct, 95);
            pct = CombatPowerCalculator.EstimateAutoWinPercent(1, 100);
            Assert.GreaterOrEqual(pct, 5);
            Assert.LessOrEqual(pct, 95);
        }

        [Test]
        public void Phase4_DayHandler_SpawnsBanditPatrolOnce()
        {
            var world = StartCh01().World;
            world.Strategic.Armies.Remove("army:bandit_patrol_auto");
            var handler = new StrategicDayHandler();
            handler.OnDayStarted(world, 1);
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_auto", out var stack));
            Assert.IsNotNull(stack);
            Assert.AreEqual(StrategicFactionCatalog.BanditId, stack.FactionId);
            Assert.IsTrue(stack.IsTraveling || !string.IsNullOrEmpty(stack.NodeId));
        }

        [Test]
        public void NodeAccess_NoPartyAtNode_Blocked()
        {
            var world = StartCh01().World;
            Assert.IsFalse(StrategicNodeAccessService.HasPartyMemberAtNode(world, "base:node_linjian"));
            var access = StrategicNodeAccessService.CanEnterNodeLocalMap(world, "base:node_linjian");
            Assert.IsTrue(access.IsFailure);
            StringAssert.Contains("无己方角色", access.Error.Message);
        }

        [Test]
        public void NodeAccess_HostileNode_BlockedBeforeArrival()
        {
            var session = StartCh01();
            var world = session.World;
            Assert.AreEqual("base:node_huangcun", world.PartyWorld.NodeId);
            var access = StrategicNodeAccessService.CanEnterNodeLocalMap(world, "base:node_linjian");
            Assert.IsTrue(access.IsFailure, access.IsFailure ? access.Error.ToString() : "expected blocked");
        }

        [Test]
        public void NodeAccess_HostileOwner_BlockedEvenIfPartyPresent()
        {
            var session = StartCh01();
            var world = session.World;
            var party = session.CharacterIds;
            var travel = WorldTravelService.StartTravel(world, party, "base:node_linjian");
            Assert.IsTrue(travel.IsSuccess, travel.IsFailure ? travel.Error.ToString() : "");
            WorldTravelService.AdvanceTravel(world, 500);
            var access = StrategicNodeAccessService.CanEnterNodeLocalMap(world, "base:node_linjian");
            Assert.IsTrue(access.IsFailure, "bandit-owned node should stay blocked without battle");
        }

        [Test]
        public void TravelDriver_DoesNotOpenRouteRandomEncounter()
        {
            var session = StartCh01();
            var world = session.World;
            var party = session.CharacterIds;
            var travel = WorldTravelService.StartTravel(world, party, "base:node_linjian");
            Assert.IsTrue(travel.IsSuccess, travel.IsFailure ? travel.Error.ToString() : "");

            Assert.IsTrue(world.WorldPresence.TryGet(party[0], out var presence));
            Assert.IsTrue(world.WorldGraph.TryGetRoute(presence.RouteId, out var route));
            route.Danger = 1f;

            for (var i = 0; i < 4000; i++)
            {
                WorldTravelService.AdvanceTravel(world, 1);
                StrategicTravelDriver.AfterTravelTick(world, 1);
            }

            Assert.IsFalse(world.Strategic.HasBlockingInterrupt);
        }

        [Test]
        public void EncounterSpawner_PlacesBandits_OnStubMap()
        {
            var session = StartCh01();
            var world = session.World;
            StrategicEncounterSpawner.PlanManualEncounter(
                world,
                string.Empty,
                string.Empty,
                null,
                2,
                2);
            world.PartyWorld.LocalMapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;
            var applied = StrategicEncounterSpawner.ApplyPending(world);
            Assert.IsTrue(applied.IsSuccess, applied.IsFailure ? applied.Error.ToString() : "");
            Assert.AreEqual(2, world.Strategic.Encounter.SpawnedEntityIds.Count);
        }

        [Test]
        public void ManualEncounter_WithEngagedParty_OnlyMarksSelectedMembers()
        {
            var session = StartCh01();
            var world = session.World;
            var solo = new System.Collections.Generic.List<EntityId> { session.CharacterIds[0] };
            StrategicEncounterSpawner.PlanManualEncounter(
                world,
                string.Empty,
                string.Empty,
                solo,
                2,
                2);

            Assert.IsTrue(world.Strategic.Encounter.HasEngagedParty);
            Assert.AreEqual(1, world.Strategic.Encounter.EngagedPartyIds.Count);
            Assert.IsTrue(world.Strategic.Encounter.IsEngaged(session.CharacterIds[0]));
            Assert.IsFalse(world.Strategic.Encounter.IsEngaged(session.CharacterIds[1]));
        }

        [Test]
        public void BanditPatrol_IsAnchoredOnHuangcunLinjianRoute()
        {
            var world = StartCh01().World;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var stack));
            Assert.IsTrue(stack.IsRouteAnchored);
            Assert.AreEqual(0.5f, stack.RouteAnchorProgress, 0.001f);
            Assert.AreEqual("base:node_huangcun", stack.NodeId);
            Assert.AreEqual("base:node_linjian", stack.DestNodeId);
        }

        [Test]
        public void Pursuit_OpensBattleOffer_WhenPartyReachesStackNode()
        {
            var session = StartCh01();
            var world = session.World;
            var party = new System.Collections.Generic.List<EntityId> { session.CharacterIds[0] };
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var stack));
            Assert.NotNull(stack);

            StrategicPursuitService.BeginPursuit(world, party, stack);
            WorldTravelService.PlaceAgentsAtNode(world, party, stack.DestNodeId);
            StrategicPursuitService.AfterTravelTick(world);
            Assert.IsTrue(world.Strategic.HasBlockingInterrupt, "Expected battle offer after pursuit arrival.");
            Assert.AreEqual(1, world.Strategic.BattleOffer.PlayerPartyIds.Count);
        }

        [Test]
        public void EncounterSpawner_SpawnsAreVisible_OnStubMap()
        {
            var hostSession = new PlayableHostSession();
            Assert.IsTrue(hostSession.Initialize(BaseGamePath).IsSuccess, hostSession.LastError);
            var world = hostSession.World;
            StrategicEncounterSpawner.PlanManualEncounter(
                world,
                string.Empty,
                string.Empty,
                null,
                2,
                2);
            world.PartyWorld.LocalMapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;
            world.LocalMap.ActiveMapLayoutId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;
            var applied = StrategicEncounterSpawner.ApplyPending(world);
            Assert.IsTrue(applied.IsSuccess, applied.IsFailure ? applied.Error.ToString() : "");

            hostSession.RefreshViewableEntityIds();
            Assert.AreEqual(2, world.Strategic.Encounter.SpawnedEntityIds.Count);
            for (var i = 0; i < world.Strategic.Encounter.SpawnedEntityIds.Count; i++)
            {
                var id = new EntityId(world.Strategic.Encounter.SpawnedEntityIds[i]);
                Assert.IsTrue(
                    LocalMapVisibility.IsEntityVisible(world, id),
                    "Encounter spawn " + id.Value + " should be visible on stub map.");
                var listed = false;
                for (var j = 0; j < hostSession.ViewableEntityIds.Count; j++)
                {
                    if (hostSession.ViewableEntityIds[j] == id)
                    {
                        listed = true;
                        break;
                    }
                }

                Assert.IsTrue(listed, "Encounter spawn " + id.Value + " should be in ViewableEntityIds.");
            }
        }
    }
}
