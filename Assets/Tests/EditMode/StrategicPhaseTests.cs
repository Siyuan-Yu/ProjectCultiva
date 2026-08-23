using System;
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
            HexTestWorldBootstrap.EnsureCh01HexMap(started.Value.World);
            return started.Value;
        }

        [Test]
        public void Phase1_Ch01_DefaultOwners_ClearedWhileDiplomacyOff()
        {
            var world = StartCh01().World;
            Assert.IsTrue(world.WorldGraph.TryGetNode("base:node_huangcun", out var huangcun));
            Assert.IsTrue(string.IsNullOrEmpty(huangcun.OwnerId), "暂不做节点势力归属");
            Assert.IsTrue(world.WorldGraph.TryGetNode("base:node_linjian", out var linjian));
            Assert.IsTrue(string.IsNullOrEmpty(linjian.OwnerId));
        }

        [Test]
        public void Phase2_DefaultDiplomacy_NeutralNotHostile()
        {
            var world = StartCh01().World;
            var player = world.Strategic.PlayerFactionId;
            Assert.IsFalse(world.Strategic.Diplomacy.IsHostile(player, StrategicFactionCatalog.BanditId));
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

            var resolved = BattleOfferService.ResolveAuto(world, false, out _, out _);
            Assert.IsTrue(resolved.IsSuccess, resolved.IsFailure ? resolved.Error.ToString() : "");
            Assert.IsTrue(world.Strategic.Participants.IsAutoSettlement);
            Assert.IsTrue(world.Strategic.HasBlockingInterrupt, "自动战结算弹窗仍为打断");
            Assert.IsTrue(StrategicEncounterResolveService.ResolveAndEnd(world).IsSuccess);
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
            world.Strategic.Armies.Remove(ArmyStackAdapter.BanditScoutStackId);
            var handler = new StrategicDayHandler();
            handler.OnDayStarted(world, 1);
            Assert.IsTrue(world.Strategic.Armies.TryGet(ArmyStackAdapter.BanditScoutStackId, out var stack));
            Assert.IsNotNull(stack);
            Assert.IsTrue(ArmyStackAdapter.HasFormalArmyLink(stack));
            Assert.AreEqual(StrategicFactionCatalog.BanditId, stack.FactionId);
            Assert.AreEqual(4, ArmyStackAdapter.GetMemberCount(world, stack));
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
        public void NodeAccess_PartyPresent_CanEnterEvenIfHostileOwner()
        {
            var session = StartCh01();
            var world = session.World;
            var party = session.CharacterIds;
            var travel = WorldTravelService.StartTravel(world, party, "base:node_linjian");
            Assert.IsTrue(travel.IsSuccess, travel.IsFailure ? travel.Error.ToString() : "");
            WorldTravelService.AdvanceTravel(world, 500);
            var access = StrategicNodeAccessService.CanEnterNodeLocalMap(world, "base:node_linjian");
            Assert.IsTrue(access.IsSuccess, "我方在场即可进入，含敌占节点");
        }

        [Test]
        public void NodeAccess_Huangcun_PartyPresent_CanEnterDespiteHostileLabor()
        {
            var session = StartCh01();
            var world = session.World;
            var access = StrategicNodeAccessService.CanEnterNodeLocalMap(world, "base:node_huangcun");
            Assert.IsTrue(access.IsSuccess, "荒村有我方时应可进入场景");
        }

        [Test]
        public void PursuitArrival_SkipsArrivalNotice_OpensBattleOffer()
        {
            var session = StartCh01();
            var world = session.World;
            var party = new System.Collections.Generic.List<EntityId> { session.CharacterIds[0] };
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var stack));

            StrategicPursuitService.BeginPursuit(world, party, stack);
            Assert.IsTrue(world.WorldPresence.TryGet(party[0], out var p));
            Assert.IsTrue(p.IsCombatPursuing);

            var travel = WorldTravelService.StartTravelPartyToStackAnchor(world, party, stack);
            Assert.IsTrue(travel.IsSuccess, travel.IsFailure ? travel.Error.ToString() : "");

            for (var i = 0; i < 5000; i++)
            {
                WorldTravelService.AdvanceTravel(world, 1, StrategicTravelDriver.BeginArrivalCapture());
                StrategicTravelDriver.AfterTravelTick(world, 1);
                if (world.Strategic.HasBattleOffer)
                    break;
            }

            Assert.IsTrue(world.Strategic.HasBattleOffer, "追击到位应弹接战");
            Assert.IsFalse(world.Strategic.HasArrivalNotice, "追击到位不应弹到站查看");
        }

        [Test]
        public void ManualEnter_KeepsEnRoutePursuerMark_SecondJoinsWithoutArrivalNotice()
        {
            var session = StartCh01();
            var world = session.World;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var stack));
            var first = new System.Collections.Generic.List<EntityId> { session.CharacterIds[0] };
            var second = new System.Collections.Generic.List<EntityId> { session.CharacterIds[1] };
            var both = new System.Collections.Generic.List<EntityId>
            {
                session.CharacterIds[0],
                session.CharacterIds[1]
            };

            StrategicPursuitService.BeginPursuit(world, both, stack);
            Assert.IsTrue(WorldTravelService.StartTravelPartyToStackAnchor(world, both, stack).IsSuccess);

            // 先到者赶到并手动进战
            for (var i = 0; i < 5000; i++)
            {
                WorldTravelService.AdvanceTravel(world, 1, StrategicTravelDriver.BeginArrivalCapture());
                StrategicTravelDriver.AfterTravelTick(world, 1);
                if (world.Strategic.HasBattleOffer)
                    break;
            }

            Assert.IsTrue(world.Strategic.HasBattleOffer);
            world.Strategic.ClearBattleOffer();

            StrategicEncounterSpawner.PlanManualEncounter(
                world,
                stack.Id,
                string.Empty,
                first,
                3,
                2);
            world.PartyWorld.LocalMapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;
            Assert.IsTrue(StrategicEncounterSpawner.ApplyPending(world).IsSuccess);

            // 模拟旧 bug：开战 ClearPursuit 会清掉第二人 → 现改为只清进场者
            StrategicPursuitService.ClearPursuitForEngagedKeepEnRoute(world, first);

            Assert.IsTrue(world.WorldPresence.TryGet(session.CharacterIds[1], out var secondP));
            Assert.IsTrue(secondP.IsCombatPursuing, "路上增援应保留追击标记");
            Assert.IsTrue(world.Strategic.Encounter.IsPursue(session.CharacterIds[1]));

            world.Strategic.ClearArrivalNotice();
            for (var i = 0; i < 5000; i++)
            {
                WorldTravelService.AdvanceTravel(world, 1, StrategicTravelDriver.BeginArrivalCapture());
                StrategicTravelDriver.AfterTravelTick(world, 1);
                if (world.Strategic.HasBattleOffer)
                    break;
            }

            Assert.IsTrue(world.Strategic.InterruptQueue.Count >= 1, "后到应入接战队列（不再战中 JoinOngoing）");
            Assert.IsFalse(world.Strategic.HasArrivalNotice, "后到追击不应弹到站查看");
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
                WorldTravelService.AdvanceTravel(world, 1, StrategicTravelDriver.BeginArrivalCapture());
                StrategicTravelDriver.AfterTravelTick(world, 1);
                if (!world.Strategic.ArrivalNotice.Resolved &&
                    !string.IsNullOrEmpty(world.Strategic.ArrivalNotice.NoticeId))
                    world.Strategic.ClearArrivalNotice();
            }

            Assert.IsTrue(
                world.Strategic.BattleOffer.Resolved ||
                string.IsNullOrEmpty(world.Strategic.BattleOffer.OfferId),
                "Route danger must not open battle offer");
        }

        [Test]
        public void TravelSameRouteAsAnchoredHostile_DoesNotAutoOffer()
        {
            var session = StartCh01();
            var world = session.World;
            var party = session.CharacterIds;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var stack));
            Assert.IsTrue(stack.IsRouteAnchored);

            var travel = WorldTravelService.StartTravel(world, party, "base:node_linjian");
            Assert.IsTrue(travel.IsSuccess, travel.IsFailure ? travel.Error.ToString() : "");

            for (var i = 0; i < 4000; i++)
            {
                WorldTravelService.AdvanceTravel(world, 1, StrategicTravelDriver.BeginArrivalCapture());
                StrategicTravelDriver.AfterTravelTick(world, 1);
                // 到站提示会挡后续 tick；清掉以便继续验证「不过路接战」
                if (!world.Strategic.ArrivalNotice.Resolved &&
                    !string.IsNullOrEmpty(world.Strategic.ArrivalNotice.NoticeId))
                    world.Strategic.ClearArrivalNotice();
            }

            Assert.IsTrue(
                world.Strategic.BattleOffer.Resolved ||
                string.IsNullOrEmpty(world.Strategic.BattleOffer.OfferId),
                "过路同路敌军栈不应自动弹接战");
        }

        [Test]
        public void FinalArrival_OpensArrivalNotice()
        {
            var session = StartCh01();
            var world = session.World;
            var party = new System.Collections.Generic.List<EntityId> { session.CharacterIds[0] };
            var travel = WorldTravelService.StartTravel(world, party, "base:node_kuangshan");
            Assert.IsTrue(travel.IsSuccess, travel.IsFailure ? travel.Error.ToString() : "");

            for (var i = 0; i < 5000; i++)
            {
                WorldTravelService.AdvanceTravel(world, 1, StrategicTravelDriver.BeginArrivalCapture());
                StrategicTravelDriver.AfterTravelTick(world, 1);
                if (!world.Strategic.ArrivalNotice.Resolved &&
                    !string.IsNullOrEmpty(world.Strategic.ArrivalNotice.NoticeId))
                    break;
            }

            Assert.IsFalse(world.Strategic.ArrivalNotice.Resolved);
            Assert.IsFalse(string.IsNullOrEmpty(world.Strategic.ArrivalNotice.NoticeId));
            Assert.IsTrue(world.Strategic.ArrivalNotice.Summary.Contains("抵达"));
        }

        [Test]
        public void TravelSameRouteAsTravelingHostile_DoesNotAutoOffer()
        {
            var session = StartCh01();
            var world = session.World;
            var party = new System.Collections.Generic.List<EntityId> { session.CharacterIds[0] };
            Assert.IsTrue(world.WorldGraph.TryFindRoute(
                "base:node_huangcun", "base:node_linjian", out var route) ||
                world.WorldGraph.TryFindRoute(
                    "base:node_linjian", "base:node_huangcun", out route));

            world.Strategic.Armies.Register(new ArmyStack
            {
                Id = "army:test_marching",
                FactionId = StrategicFactionCatalog.BanditId,
                DisplayName = "测试行军匪",
                NodeId = "base:node_linjian",
                DestNodeId = "base:node_huangcun",
                RouteId = route.Id,
                TravelTotalTicks = 200,
                RemainingTravelTicks = 200,
                MemberCount = 2,
                CombatPower = 2
            });

            var travel = WorldTravelService.StartTravel(world, party, "base:node_linjian");
            Assert.IsTrue(travel.IsSuccess, travel.IsFailure ? travel.Error.ToString() : "");

            for (var i = 0; i < 50; i++)
            {
                WorldTravelService.AdvanceTravel(world, 1, StrategicTravelDriver.BeginArrivalCapture());
                StrategicTravelDriver.AfterTravelTick(world, 1);
                if (!world.Strategic.ArrivalNotice.Resolved &&
                    !string.IsNullOrEmpty(world.Strategic.ArrivalNotice.NoticeId))
                    world.Strategic.ClearArrivalNotice();
            }

            Assert.IsTrue(
                world.Strategic.BattleOffer.Resolved ||
                string.IsNullOrEmpty(world.Strategic.BattleOffer.OfferId),
                "同路行军敌军也不应自动弹接战（须主动攻击／追击）");
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
        public void EncounterSpawner_CasualtySyncsArmyStackMemberCount()
        {
            var session = StartCh01();
            var world = session.World;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var stack));
            StrategicEncounterSpawner.PlanManualEncounter(
                world,
                stack.Id,
                string.Empty,
                null,
                3,
                2);
            world.PartyWorld.LocalMapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;
            Assert.IsTrue(StrategicEncounterSpawner.ApplyPending(world).IsSuccess);
            var expectedLiving = ArmyStackAdapter.GetMemberCount(world, stack);
            Assert.AreEqual(expectedLiving, stack.MemberCount);
            Assert.AreEqual(expectedLiving, world.Strategic.Encounter.SpawnedEntityIds.Count);

            var first = new EntityId(world.Strategic.Encounter.SpawnedEntityIds[0]);
            if (world.Entities.TryGet(first, out var entity) &&
                entity.TryGet<XianXia.Core.Entities.LifecycleComponent>(out var life))
            {
                life.State = XianXia.Core.Entities.LifecycleState.Dead;
            }

            StrategicEncounterSpawner.OnCombatantDefeated(world, first);
            Assert.AreEqual(2, stack.MemberCount);
            Assert.AreEqual(2, world.Strategic.Encounter.SpawnedEntityIds.Count);

            StrategicEncounterSpawner.PlanManualEncounter(world, stack.Id, string.Empty, null, 3, 2);
            Assert.IsTrue(StrategicEncounterSpawner.ApplyPending(world).IsSuccess);
            Assert.AreEqual(2, world.Strategic.Encounter.SpawnedEntityIds.Count);
        }

        [Test]
        public void BanditPatrol_IsAnchoredOnHuangcunLinjianRoute()
        {
            var world = StartCh01().World;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var stack));
            if (ArmyHexCommandService.IsHexStrategicActive(world))
            {
                Assert.IsTrue(world.Strategic.FormalArmies.TryGet(
                    ArmyStackAdapter.BanditPatrolFormalArmyId,
                    out var army));
                Assert.IsTrue(army.UsesHexStrategicPosition);
                Assert.AreEqual(FormalArmyState.Idle, army.State);
                Assert.IsFalse(stack.IsTraveling);
                return;
            }

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
            var travel = WorldTravelService.StartTravelPartyToStackAnchor(world, party, stack);
            Assert.IsTrue(travel.IsSuccess, travel.IsFailure ? travel.Error.ToString() : "");
            WorldTravelService.AdvanceTravel(world, 500);
            StrategicPursuitService.AfterTravelTick(world);
            Assert.IsTrue(world.Strategic.HasBlockingInterrupt, "Expected battle offer after pursuit arrival.");
            Assert.AreEqual(1, world.Strategic.BattleOffer.PlayerPartyIds.Count);
        }

        [Test]
        public void EncounterSpawner_SpawnsAreVisible_OnStubMap()
        {
            var hostSession = new PlayableHostSession();
            Assert.IsTrue(hostSession.Initialize(BaseGamePath).IsSuccess, hostSession.LastError);
            HexTestWorldBootstrap.EnsureCh01HexMap(hostSession.World);
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

        [Test]
        public void NodeAccess_InEncounterAtDepartureNode_AllowsEnter()
        {
            var session = StartCh01();
            var world = session.World;
            var fighter = session.CharacterIds[0];
            var huangcun = "base:node_huangcun";
            WorldTravelService.PlaceAgentsAtNode(world, new[] { fighter }, huangcun);
            Assert.IsTrue(world.WorldPresence.TryGet(fighter, out var presence));
            presence.Mode = PartyWorldPresenceMode.InEncounter;

            Assert.IsTrue(StrategicNodeAccessService.HasPartyMemberAtNode(world, huangcun));
            var access = StrategicNodeAccessService.CanEnterNodeLocalMap(world, huangcun);
            Assert.IsTrue(access.IsSuccess, access.IsFailure ? access.Error.ToString() : "");
        }

        [Test]
        public void BattleOffer_SecondPartyWhileManualFightActive_GoesToQueue()
        {
            var session = StartCh01();
            var world = session.World;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var stack));
            var first = new System.Collections.Generic.List<EntityId> { session.CharacterIds[0] };
            var second = new System.Collections.Generic.List<EntityId> { session.CharacterIds[1] };

            StrategicEncounterSpawner.PlanManualEncounter(
                world,
                stack.Id,
                string.Empty,
                first,
                3,
                2);
            world.PartyWorld.LocalMapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;
            Assert.IsTrue(StrategicEncounterSpawner.ApplyPending(world).IsSuccess);
            Assert.IsTrue(BattleOfferService.HasActiveEncounterForStack(world, stack.Id));

            Assert.IsTrue(BattleOfferService.TryBuildOfferForArmy(world, second, stack, "增援接战"));
            Assert.IsTrue(world.Strategic.InterruptQueue.Count >= 1, "手动战进行中应排队，不做战中加入");
            Assert.IsTrue(world.Strategic.Encounter.IsEngaged(session.CharacterIds[0]));
            Assert.IsFalse(world.Strategic.Encounter.IsEngaged(session.CharacterIds[1]));
        }

        [Test]
        public void BattleOffer_QueuedPromote_WhenPartyNotColocated_StartsPursuitNotOffer()
        {
            var session = StartCh01();
            var world = session.World;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var stack));
            var farParty = new System.Collections.Generic.List<EntityId> { session.CharacterIds[1] };
            WorldTravelService.PlaceAgentsAtNode(world, farParty, "base:node_huangcun");

            world.Strategic.InterruptQueue.Enqueue("排队测试", stack.Id, farParty);
            Assert.IsFalse(BattleOfferService.TryPromoteNextQueuedOffer(world));
            Assert.IsFalse(world.Strategic.HasBattleOffer, "人未到不应弹 Offer");
            Assert.AreEqual(stack.Id, world.Strategic.Encounter.PursueStackId);
        }

        [Test]
        public void FieldCleared_UnlocksMacroTravel_WithoutSettlement()
        {
            var session = StartCh01();
            var world = session.World;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var stack));
            var party = new System.Collections.Generic.List<EntityId> { session.CharacterIds[0] };

            StrategicEncounterSpawner.PlanManualEncounter(world, stack.Id, string.Empty, party, 1, 2);
            world.PartyWorld.LocalMapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;
            world.LocalMap.ActiveMapLayoutId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;
            Assert.IsTrue(StrategicEncounterSpawner.ApplyPending(world).IsSuccess);
            Assert.AreEqual(1, world.Strategic.Encounter.SpawnedEntityIds.Count);
            Assert.IsTrue(world.WorldPresence.TryGet(party[0], out var before));
            before.Mode = PartyWorldPresenceMode.InEncounter;

            Assert.IsFalse(WorldTravelService.CanReceiveTravelOrder(world, party[0]));

            var enemyId = new EntityId(world.Strategic.Encounter.SpawnedEntityIds[0]);
            Assert.IsTrue(world.Entities.TryGet(enemyId, out var enemy));
            if (!enemy.TryGet<XianXia.Core.Entities.LifecycleComponent>(out var life))
            {
                life = new XianXia.Core.Entities.LifecycleComponent();
                enemy.AddComponent(life);
            }

            life.State = XianXia.Core.Entities.LifecycleState.Dead;
            Assert.IsTrue(StrategicEncounterSpawner.OnCombatantDefeated(world, enemyId));
            Assert.IsTrue(StrategicEncounterSpawner.IsFieldCleared(world));
            Assert.IsTrue(WorldTravelService.CanReceiveTravelOrder(world, party[0]));
            Assert.AreEqual(
                StrategicEncounterCatalog.DefaultEncounterLocalMapId,
                world.LocalMap.ActiveMapLayoutId,
                "Field clear must keep encounter LocalMap");
        }

        [Test]
        public void FieldCleared_StillInEncounter_CanOrderTravelBackToHuangcun()
        {
            var session = StartCh01();
            var world = session.World;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var stack));
            var party = new System.Collections.Generic.List<EntityId> { session.CharacterIds[0] };

            StrategicEncounterSpawner.PlanManualEncounter(world, stack.Id, string.Empty, party, 1, 2);
            world.PartyWorld.LocalMapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;
            Assert.IsTrue(StrategicEncounterSpawner.ApplyPending(world).IsSuccess);
            Assert.IsTrue(world.WorldPresence.TryGet(party[0], out var wp));
            wp.Mode = PartyWorldPresenceMode.InEncounter;
            Assert.That(wp.RouteAnchorProgress, Is.EqualTo(0.5f).Within(0.05f));

            var enemyId = new EntityId(world.Strategic.Encounter.SpawnedEntityIds[0]);
            Assert.IsTrue(world.Entities.TryGet(enemyId, out var enemy));
            if (!enemy.TryGet<XianXia.Core.Entities.LifecycleComponent>(out var life))
            {
                life = new XianXia.Core.Entities.LifecycleComponent();
                enemy.AddComponent(life);
            }

            life.State = XianXia.Core.Entities.LifecycleState.Dead;
            Assert.IsTrue(StrategicEncounterSpawner.OnCombatantDefeated(world, enemyId));
            Assert.IsTrue(StrategicEncounterSpawner.IsFieldCleared(world));
            Assert.IsTrue(world.WorldPresence.TryGet(party[0], out wp));
            Assert.AreEqual(PartyWorldPresenceMode.InEncounter, wp.Mode);

            var target = WorldTravelTarget.AtNode("base:node_huangcun");
            Assert.IsTrue(
                WorldTravelPathService.CanAgentReachTarget(world, wp, target),
                "清场后未 Release 前也应能点回青石荒村");

            var started = WorldTravelPathService.StartAgentTravelToTarget(world, party[0], target);
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");
            Assert.IsTrue(world.WorldPresence.TryGet(party[0], out wp));
            Assert.AreEqual(PartyWorldPresenceMode.Traveling, wp.Mode);
        }

        [Test]
        public void ReleaseAfterRoadFight_KeepsProgress_CanReturnToOrigin()
        {
            var session = StartCh01();
            var world = session.World;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var stack));
            var party = new System.Collections.Generic.List<EntityId> { session.CharacterIds[0] };

            StrategicEncounterSpawner.PlanManualEncounter(world, stack.Id, string.Empty, party, 1, 2);
            Assert.IsTrue(world.WorldPresence.TryGet(party[0], out var wp));
            Assert.AreEqual(0.5f, wp.RouteAnchorProgress, 0.05f);

            // 模拟进图时曾清掉 TravelTicks（旧 bug 会把进度弄丢）
            wp.TravelTotalTicks = 0;
            wp.RemainingTravelTicks = 0;
            wp.Mode = PartyWorldPresenceMode.InEncounter;

            world.Strategic.Encounter.FieldCleared = true;
            StrategicEncounterSpawner.ReleaseEngagedForMacroTravel(world, party[0]);
            Assert.IsTrue(world.WorldPresence.TryGet(party[0], out wp));
            Assert.AreEqual(PartyWorldPresenceMode.RouteAnchored, wp.Mode);
            Assert.That(wp.RouteAnchorProgress, Is.EqualTo(0.5f).Within(0.05f));

            var back = WorldTravelService.StartTravel(world, party[0], "base:node_huangcun");
            Assert.IsTrue(back.IsSuccess, back.IsFailure ? back.Error.ToString() : "");
            Assert.IsTrue(world.WorldPresence.TryGet(party[0], out wp));
            Assert.AreEqual(PartyWorldPresenceMode.Traveling, wp.Mode);
            Assert.Greater(wp.RemainingTravelTicks, 4);
        }

        [Test]
        public void RouteAnchor_TravelFromMidRoute_ReachesOrigin()
        {
            var session = StartCh01();
            var world = session.World;
            var agent = session.CharacterIds[0];
            WorldTravelService.StartTravel(world, agent, "base:node_linjian");
            WorldTravelService.AdvanceTravel(world, 12);
            Assert.IsTrue(world.WorldPresence.TryGet(agent, out var p));
            Assert.AreEqual(PartyWorldPresenceMode.Traveling, p.Mode);
            p.AnchorOnRoute(0.5f);
            Assert.AreEqual(PartyWorldPresenceMode.RouteAnchored, p.Mode);

            var travel = WorldTravelService.StartTravel(world, agent, p.NodeId);
            Assert.IsTrue(travel.IsSuccess, travel.IsFailure ? travel.Error.ToString() : "");
            WorldTravelService.AdvanceTravel(world, 500);
            Assert.IsTrue(world.WorldPresence.TryGet(agent, out p));
            Assert.AreEqual(PartyWorldPresenceMode.AtNode, p.Mode);
            Assert.AreEqual("base:node_huangcun", p.NodeId);
        }

        [Test]
        public void RouteAnchor_TravelToMidProgress_StopsOnRoute()
        {
            var session = StartCh01();
            var world = session.World;
            var agent = session.CharacterIds[0];
            WorldTravelService.StartTravel(world, agent, "base:node_linjian");
            WorldTravelService.AdvanceTravel(world, 12);
            Assert.IsTrue(world.WorldPresence.TryGet(agent, out var p));
            p.AnchorOnRoute(0.2f);

            var travel = WorldTravelService.StartTravelToRouteProgress(world, agent, 0.65f);
            Assert.IsTrue(travel.IsSuccess, travel.IsFailure ? travel.Error.ToString() : "");
            WorldTravelService.AdvanceTravel(world, 500);
            Assert.IsTrue(world.WorldPresence.TryGet(agent, out p));
            Assert.AreEqual(PartyWorldPresenceMode.RouteAnchored, p.Mode);
            Assert.That(p.RouteAnchorProgress, Is.EqualTo(0.65f).Within(0.06f));
        }

        [Test]
        public void Pursuit_FirstRetreatFromOffer_SecondStillGetsBattleOffer()
        {
            var session = StartCh01();
            var world = session.World;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var stack));
            var first = new System.Collections.Generic.List<EntityId> { session.CharacterIds[0] };
            var second = new System.Collections.Generic.List<EntityId> { session.CharacterIds[1] };
            var both = new System.Collections.Generic.List<EntityId>
            {
                session.CharacterIds[0],
                session.CharacterIds[1]
            };

            StrategicPursuitService.BeginPursuit(world, both, stack);
            Assert.IsTrue(WorldTravelService.StartTravelPartyToStackAnchor(world, both, stack).IsSuccess);

            for (var i = 0; i < 5000; i++)
            {
                WorldTravelService.AdvanceTravel(world, 1, StrategicTravelDriver.BeginArrivalCapture());
                StrategicTravelDriver.AfterTravelTick(world, 1);
                if (world.Strategic.HasBattleOffer)
                    break;
            }

            Assert.IsTrue(world.Strategic.HasBattleOffer, "先到者应弹接战");
            StrategicPursuitService.ClearPursuitForAgents(world, first);
            ArrivalNoticeService.SuppressForParty(world, first);
            BattleOfferService.FinishOfferResolution(world);
            world.Strategic.ClearBattleOffer();

            Assert.IsTrue(world.WorldPresence.TryGet(session.CharacterIds[1], out var secondP));
            Assert.IsTrue(secondP.IsCombatPursuing, "第一人撤退不应清掉第二人追击标记");

            world.Strategic.ClearArrivalNotice();
            for (var i = 0; i < 5000; i++)
            {
                WorldTravelService.AdvanceTravel(world, 1, StrategicTravelDriver.BeginArrivalCapture());
                StrategicTravelDriver.AfterTravelTick(world, 1);
                if (world.Strategic.HasBattleOffer || world.Strategic.InterruptQueue.Count > 0)
                    break;
            }

            Assert.IsTrue(
                world.Strategic.HasBattleOffer || world.Strategic.InterruptQueue.Count > 0,
                "第二人到位应弹接战而非到站查看");
            Assert.IsFalse(world.Strategic.HasArrivalNotice, "追击到位不应弹到站查看");
        }

        [Test]
        public void LingeringOffer_ActingArmyLivingMembersAreMandatory_NotPerCharacterOptional()
        {
            var session = StartCh01();
            var world = session.World;
            var downed = session.CharacterIds[0];
            var living = session.CharacterIds[1];
            var roster = session.CharacterIds;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var enemy));

            var prevDebug = AutoBattleCasualtyService.DebugForceSoloAutoBattleIncapacitated;
            AutoBattleCasualtyService.DebugForceSoloAutoBattleIncapacitated = true;
            try
            {
                world.Random = new DeterministicRandom(7);
                Assert.IsTrue(BattleOfferService.TryBuildOfferForArmy(
                    world, new System.Collections.Generic.List<EntityId> { downed }, enemy, "测试"));
                world.Strategic.BattleOffer.AutoWinPercent = 0;
                Assert.IsTrue(BattleOfferService.ResolveAuto(world, false, out _, out _).IsSuccess);
                Assert.IsTrue(StrategicEncounterResolveService.ResolveAndEnd(world).IsSuccess);
            }
            finally
            {
                AutoBattleCasualtyService.DebugForceSoloAutoBattleIncapacitated = prevDebug;
            }

            WorldTravelService.PlaceAgentsAtNode(world, new[] { living }, "base:node_huangcun");
            world.Strategic.ReinforcementWorldRadius = 1f;

            var decisionMakers = new System.Collections.Generic.List<EntityId> { living };
            Assert.IsTrue(BattleOfferService.TryBuildOfferForLingeringBattlefield(
                world, roster, downed, "残留战场", decisionMakers));

            var snap = world.Strategic.Participants;
            Assert.AreEqual(
                BattleParticipantKind.MandatoryFriendly,
                snap.FindByEntity(living).Kind);
            Assert.AreEqual(
                BattleParticipantKind.MandatoryFriendly,
                snap.FindByEntity(downed).Kind);
            for (var i = 0; i < roster.Count; i++)
            {
                var id = roster[i];
                if (id == living || id == downed)
                    continue;
                if (!LingeringBattlefieldPartyService.IsLivingForMacroOrder(world, id))
                    continue;
                var rec = snap.FindByEntity(id);
                if (rec == null)
                    continue;
                Assert.AreNotEqual(
                    BattleParticipantKind.OptionalFriendly,
                    rec.Kind,
                    "散装角色不应再逐人出现在可选支援名单");
            }
        }

        [Test]
        public void Pursuit_SecondArrival_OffersJoinBattle()
        {
            var session = StartCh01();
            var world = session.World;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var stack));
            var first = new System.Collections.Generic.List<EntityId> { session.CharacterIds[0] };
            var second = new System.Collections.Generic.List<EntityId> { session.CharacterIds[1] };
            var pursue = new System.Collections.Generic.List<EntityId> { session.CharacterIds[0], session.CharacterIds[1] };

            Assert.IsTrue(WorldTravelService.StartTravelPartyToStackAnchor(world, first, stack).IsSuccess);
            WorldTravelService.AdvanceTravel(world, 500);
            StrategicEncounterSpawner.PlanManualEncounter(
                world,
                stack.Id,
                string.Empty,
                first,
                3,
                2);
            world.PartyWorld.LocalMapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;
            Assert.IsTrue(StrategicEncounterSpawner.ApplyPending(world).IsSuccess);

            StrategicPursuitService.BeginPursuit(world, pursue, stack);
            Assert.IsTrue(WorldTravelService.StartTravelPartyToStackAnchor(world, second, stack).IsSuccess);
            WorldTravelService.AdvanceTravel(world, 500);
            StrategicPursuitService.AfterTravelTick(world);

            Assert.IsTrue(world.Strategic.InterruptQueue.Count >= 1 || world.Strategic.HasBlockingInterrupt);
            Assert.IsTrue(world.Strategic.Encounter.IsEngaged(session.CharacterIds[0]));
            Assert.IsFalse(world.Strategic.Encounter.IsEngaged(session.CharacterIds[1]));
        }

        [Test]
        public void EncounterKill_DoesNotAutoResolveVictoryOrClearEngagement()
        {
            var session = StartCh01();
            var world = session.World;
            var agent = session.CharacterIds[0];
            WorldTravelService.StartTravel(world, agent, "base:node_linjian");
            WorldTravelService.AdvanceTravel(world, 20);
            Assert.IsTrue(world.WorldPresence.TryGet(agent, out var p));
            p.Mode = PartyWorldPresenceMode.InEncounter;

            StrategicEncounterSpawner.PlanManualEncounter(
                world,
                "army:bandit_patrol_1",
                string.Empty,
                new System.Collections.Generic.List<EntityId> { agent },
                1,
                2);
            world.PartyWorld.LocalMapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;
            Assert.IsTrue(StrategicEncounterSpawner.ApplyPending(world).IsSuccess);

            var spawnId = new EntityId(world.Strategic.Encounter.SpawnedEntityIds[0]);
            for (var i = 0; i < world.Strategic.Encounter.SpawnedEntityIds.Count; i++)
            {
                var id = new EntityId(world.Strategic.Encounter.SpawnedEntityIds[i]);
                if (!world.Entities.TryGet(id, out var spawn) ||
                    !spawn.TryGet<XianXia.Core.Entities.LifecycleComponent>(out var life))
                    continue;
                life.State = XianXia.Core.Entities.LifecycleState.Dead;
            }

            Assert.IsTrue(StrategicEncounterSpawner.OnCombatantDefeated(world, spawnId));
            Assert.IsTrue(world.WorldPresence.TryGet(agent, out p));
            Assert.AreEqual(PartyWorldPresenceMode.InEncounter, p.Mode);
            Assert.IsTrue(world.Strategic.Encounter.HasEngagedParty);
            Assert.IsTrue(BattleOfferService.HasActiveManualEncounter(world));
            Assert.IsTrue(StrategicEncounterSpawner.IsFieldCleared(world));
        }

        [Test]
        public void PursuitTravel_StopsAtRouteAnchoredStack_NotDestination()
        {
            var session = StartCh01();
            var world = session.World;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var stack));
            var party = session.CharacterIds;

            StrategicPursuitService.BeginPursuit(world, party, stack);
            var travel = WorldTravelService.StartTravelPartyToStackAnchor(world, party, stack);
            Assert.IsTrue(travel.IsSuccess, travel.IsFailure ? travel.Error.ToString() : "");

            WorldTravelService.AdvanceTravel(world, 500);
            for (var i = 0; i < party.Count; i++)
            {
                Assert.IsTrue(world.WorldPresence.TryGet(party[i], out var p), "Missing presence " + i);
                Assert.AreEqual(
                    PartyWorldPresenceMode.RouteAnchored,
                    p.Mode,
                    "Traveler " + i + " should stop at stack anchor.");
                Assert.AreEqual(stack.RouteId, p.RouteId);
                Assert.That(p.RouteAnchorProgress, Is.EqualTo(stack.RouteAnchorProgress).Within(0.06f));
            }
        }

        [Test]
        public void Pursuit_RetargetsWhenStackMovesAlongRoute_ThenOffersBattle()
        {
            var session = StartCh01();
            var world = session.World;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var stack));
            var party = new System.Collections.Generic.List<EntityId> { session.CharacterIds[0] };

            StrategicPursuitService.BeginPursuit(world, party, stack);
            Assert.IsTrue(WorldTravelService.StartTravelPartyToStackAnchor(world, party, stack).IsSuccess);

            // 追上原 0.5 锚点
            for (var i = 0; i < 800; i++)
            {
                WorldTravelService.AdvanceTravel(world, 1, StrategicTravelDriver.BeginArrivalCapture());
                StrategicTravelDriver.AfterTravelTick(world, 1);
                if (world.Strategic.HasBattleOffer)
                    break;
            }

            Assert.IsTrue(world.Strategic.HasBattleOffer, "追上原锚点应弹接战");
            world.Strategic.ClearBattleOffer();

            // 敌军沿路挪到更远处；追击应改道再贴
            stack.RouteAnchorProgress = 0.85f;
            Assert.IsTrue(world.WorldPresence.TryGet(party[0], out var p));
            Assert.IsFalse(
                StrategicEngageRules.IsAgentColocatedWithStack(world, p, stack),
                "挪位后应暂时不重合");

            for (var i = 0; i < 800; i++)
            {
                WorldTravelService.AdvanceTravel(world, 1, StrategicTravelDriver.BeginArrivalCapture());
                StrategicTravelDriver.AfterTravelTick(world, 1);
                if (world.Strategic.HasBattleOffer)
                    break;
            }

            Assert.IsTrue(world.Strategic.HasBattleOffer, "贴上挪位后的敌军应再弹接战");
            Assert.IsTrue(world.WorldPresence.TryGet(party[0], out p));
            Assert.That(p.TravelProgress, Is.EqualTo(0.85f).Within(0.08f));
        }

        [Test]
        public void AutoBattle_Defeat_AppliesIncapacitatedOrKillToParty()
        {
            var session = StartCh01();
            var world = session.World;
            var party = session.CharacterIds;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var enemy));

            world.Random = new XianXia.Core.Random.DeterministicRandom(99);
            Assert.IsTrue(BattleOfferService.TryBuildOfferForArmy(world, party, enemy, "测试失利"));

            // 强制败北：把胜率压到 0
            world.Strategic.BattleOffer.AutoWinPercent = 0;
            var resolved = BattleOfferService.ResolveAuto(world, false, out var won, out var report);
            Assert.IsTrue(resolved.IsSuccess);
            Assert.IsFalse(won);
            Assert.IsNotNull(report);
            Assert.Greater(report.PlayerKilled + report.PlayerIncapacitated + report.PlayerWounded, 0);
        }

        [Test]
        public void AutoBattle_ExecuteOnWin_LeavesCorpseRemnant()
        {
            var session = StartCh01();
            var world = session.World;
            var party = session.CharacterIds;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var enemy));
            var stackId = enemy.Id;
            var beforeMembers = enemy.MemberCount;

            world.Random = new XianXia.Core.Random.DeterministicRandom(1);
            Assert.IsTrue(BattleOfferService.TryBuildOfferForArmy(world, party, enemy, "测试处决"));
            world.Strategic.BattleOffer.AutoWinPercent = 100;

            var resolved = BattleOfferService.ResolveAuto(world, true, out var won, out var report);
            Assert.IsTrue(resolved.IsSuccess);
            Assert.IsTrue(won);
            Assert.IsTrue(world.Strategic.Armies.TryGet(stackId, out var after));
            Assert.IsNotNull(after);
            Assert.IsTrue(after.HasCorpseRemnant);
            Assert.AreEqual(beforeMembers, after.CorpseMemberCount);
            Assert.AreEqual(0, after.IncapacitatedMemberCount);
            Assert.AreEqual(beforeMembers, CountTrackedEnemyDownedSpawns(world));
            Assert.IsFalse(world.Strategic.Encounter.SpawnOnNextMapLoad);
            Assert.IsNotNull(report);
            Assert.Greater(report.EnemyMembersEliminated, 0);

            Assert.IsTrue(StrategicEncounterResolveService.ResolveAndEnd(world).IsSuccess);
            Assert.IsTrue(BattleOfferService.HasLingeringBattlefield(world));
        }

        [Test]
        public void AutoBattle_SpareOnWin_AllIncapacitatedRemnant_NoKills()
        {
            var session = StartCh01();
            var world = session.World;
            var party = session.CharacterIds;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var enemy));
            var beforeMembers = enemy.MemberCount;

            world.Random = new XianXia.Core.Random.DeterministicRandom(2);
            Assert.IsTrue(BattleOfferService.TryBuildOfferForArmy(world, party, enemy, "测试弥留"));
            world.Strategic.BattleOffer.AutoWinPercent = 100;

            var resolved = BattleOfferService.ResolveAuto(world, false, out var won, out var report);
            Assert.IsTrue(resolved.IsSuccess);
            Assert.IsTrue(won);
            Assert.IsTrue(world.Strategic.Armies.TryGet(enemy.Id, out var after));
            Assert.IsNotNull(after);
            Assert.AreEqual(beforeMembers, after.MemberCount, "未处决应保留全员为弥留人数");
            Assert.IsTrue(after.HasIncapacitatedRemnant);
            Assert.AreEqual(beforeMembers, after.IncapacitatedMemberCount);
            Assert.AreEqual(beforeMembers, CountTrackedEnemyDownedSpawns(world));
            Assert.IsFalse(world.Strategic.Encounter.SpawnOnNextMapLoad);
            Assert.IsNotNull(report);
            Assert.AreEqual(0, report.EnemyMembersEliminated);
            Assert.AreEqual(beforeMembers, report.EnemyMembersSpared);

            Assert.IsTrue(StrategicEncounterResolveService.ResolveAndEnd(world).IsSuccess);
            Assert.IsTrue(BattleOfferService.HasLingeringBattlefield(world));
            Assert.IsTrue(world.Strategic.Armies.TryGet(enemy.Id, out var parked));
            Assert.IsTrue(parked.HasIncapacitatedRemnant);
        }

        [Test]
        public void AutoBattle_Defeat_SoloIncap_LingersAfterResolveEnd()
        {
            var session = StartCh01();
            var world = session.World;
            var solo = new System.Collections.Generic.List<EntityId> { session.CharacterIds[0] };
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var enemy));

            var prevDebug = AutoBattleCasualtyService.DebugForceSoloAutoBattleIncapacitated;
            AutoBattleCasualtyService.DebugForceSoloAutoBattleIncapacitated = true;
            try
            {
                world.Random = new XianXia.Core.Random.DeterministicRandom(7);
                Assert.IsTrue(BattleOfferService.TryBuildOfferForArmy(world, solo, enemy, "测试失利弥留"));
                world.Strategic.BattleOffer.AutoWinPercent = 0;

                Assert.IsTrue(BattleOfferService.ResolveAuto(world, false, out var won, out var report).IsSuccess);
                Assert.IsFalse(won);
                Assert.IsNotNull(report);
                Assert.AreEqual(1, report.PlayerIncapacitated);

                var id = solo[0];
                Assert.IsTrue(LingeringBattlefieldPartyService.IsIncapacitated(world, id));

                Assert.IsTrue(StrategicEncounterResolveService.ResolveAndEnd(world).IsSuccess);
                Assert.IsTrue(BattleOfferService.HasLingeringBattlefield(world));
                Assert.IsTrue(LingeringBattlefieldPartyService.IsIncapacitated(world, id));
                Assert.IsFalse(ArmyService.TryGetArmyForCharacter(world, id, out _));
                Assert.IsTrue(world.WorldPresence.TryGet(id, out var wp));
                Assert.IsFalse(string.IsNullOrEmpty(wp.NodeId));
            }
            finally
            {
                AutoBattleCasualtyService.DebugForceSoloAutoBattleIncapacitated = prevDebug;
            }
        }

        [Test]
        public void AutoBattle_SpareOnWin_SpawnsMacroRemnantsImmediately()
        {
            var session = StartCh01();
            var world = session.World;
            var party = session.CharacterIds;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var enemy));
            var beforeMembers = enemy.MemberCount;

            world.Random = new XianXia.Core.Random.DeterministicRandom(2);
            Assert.IsTrue(BattleOfferService.TryBuildOfferForArmy(world, party, enemy, "测试弥留刷怪"));
            world.Strategic.BattleOffer.AutoWinPercent = 100;

            Assert.IsTrue(BattleOfferService.ResolveAuto(world, false, out var won, out _).IsSuccess);
            Assert.IsTrue(won);
            Assert.AreEqual(beforeMembers, CountTrackedEnemyDownedSpawns(world));
            Assert.IsFalse(world.Strategic.Encounter.SpawnOnNextMapLoad);

            Assert.IsTrue(StrategicEncounterResolveService.ResolveAndEnd(world).IsSuccess);
            Assert.IsTrue(BattleOfferService.HasLingeringBattlefield(world));
            Assert.AreEqual(beforeMembers, CountTrackedEnemyDownedSpawns(world));
            Assert.IsFalse(world.Strategic.Encounter.SpawnOnNextMapLoad);
        }

        [Test]
        public void LingeringReentry_OpensBattleOffer_WithLingeringLocalMap()
        {
            var session = StartCh01();
            var world = session.World;
            var party = session.CharacterIds;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var enemy));

            world.Random = new DeterministicRandom(2);
            Assert.IsTrue(BattleOfferService.TryBuildOfferForArmy(world, party, enemy, "测试弥留"));
            world.Strategic.BattleOffer.AutoWinPercent = 100;
            Assert.IsTrue(BattleOfferService.ResolveAuto(world, false, out _, out _).IsSuccess);
            Assert.IsTrue(StrategicEncounterResolveService.ResolveAndEnd(world).IsSuccess);
            Assert.IsTrue(BattleOfferService.HasLingeringBattlefield(world));

            const string lingerMap = "base:map_world_node_stub";
            world.Strategic.Encounter.LingeringLocalMapId = lingerMap;
            var anchorNode = world.Strategic.Participants.BattleAnchorNodeId;
            if (string.IsNullOrEmpty(anchorNode))
                anchorNode = "base:node_huangcun";
            WorldTravelService.PlaceAgentsAtNode(world, party, anchorNode);

            var focus = party[0];
            Assert.IsTrue(
                BattleOfferService.TryBuildOfferForLingeringBattlefield(world, party, focus, "残留战场"));
            Assert.IsTrue(world.Strategic.HasBattleOffer);
            Assert.AreEqual("残留战场", world.Strategic.BattleOffer.Title);
            Assert.AreEqual(lingerMap, world.Strategic.BattleOffer.EncounterLocalMapId);
        }

        [Test]
        public void RemnantStackAttack_OpensBattleOffer_WithLingeringLocalMap()
        {
            var session = StartCh01();
            var world = session.World;
            var party = new System.Collections.Generic.List<EntityId> { session.CharacterIds[0] };
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var enemy));

            enemy.IsBattlefieldRemnant = true;
            enemy.IncapacitatedMemberCount = Math.Max(1, enemy.MemberCount);
            const string lingerMap = "base:map_world_node_stub";
            world.Strategic.Encounter.LingeringLocalMapId = lingerMap;
            world.Strategic.Encounter.BattlefieldLingering = true;
            world.Strategic.Encounter.ArmyStackId = enemy.Id;

            StrategicPursuitService.BeginPursuit(world, party, enemy);
            var travel = WorldTravelService.StartTravelPartyToStackAnchor(world, party, enemy);
            Assert.IsTrue(travel.IsSuccess, travel.IsFailure ? travel.Error.ToString() : "");
            WorldTravelService.AdvanceTravel(world, 500);
            StrategicPursuitService.AfterTravelTick(world);

            Assert.IsTrue(world.Strategic.HasBattleOffer);
            Assert.AreEqual("残留战场", world.Strategic.BattleOffer.Title);
            Assert.AreEqual(lingerMap, world.Strategic.BattleOffer.EncounterLocalMapId);
        }

        [Test]
        public void Adr0023_BattleOffer_FreezesWorldTick()
        {
            var session = StartCh01();
            var world = session.World;
            var party = session.CharacterIds;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var enemy));

            var tickBefore = world.Tick.Value;
            Assert.IsTrue(BattleOfferService.TryBuildOfferForArmy(world, party, enemy, "冻结测试"));
            Assert.IsTrue(world.Strategic.IsWorldTickFrozen);
            Assert.AreEqual(StrategicClockFreezeReason.BattleOffer, world.Strategic.ClockFreeze.Reason);

            var loop = new XianXia.Core.Simulation.SimulationLoop(world);
            Assert.IsTrue(loop.TickOnce().IsSuccess);
            Assert.AreEqual(tickBefore, world.Tick.Value, "冻结期间 Tick 不得推进");
        }

        [Test]
        public void Adr0023_AutoResolve_EndsFreeze_WithoutAdvancingTick()
        {
            var session = StartCh01();
            var world = session.World;
            var party = session.CharacterIds;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var enemy));

            var tickBefore = world.Tick.Value;
            Assert.IsTrue(BattleOfferService.TryBuildOfferForArmy(world, party, enemy, "自动解冻"));
            world.Strategic.BattleOffer.AutoWinPercent = 100;
            world.Random = new DeterministicRandom(1);

            var resolved = BattleOfferService.ResolveAuto(world, false, out _, out _);
            Assert.IsTrue(resolved.IsSuccess);
            Assert.IsTrue(world.Strategic.Participants.IsAutoSettlement);
            Assert.IsTrue(world.Strategic.IsWorldTickFrozen, "结算确认前仍冻结");
            Assert.IsTrue(StrategicEncounterResolveService.ResolveAndEnd(world).IsSuccess);
            Assert.IsFalse(world.Strategic.IsWorldTickFrozen);
            Assert.AreEqual(tickBefore, world.Tick.Value, "AutoResolve 不得额外推进 Tick");
        }

        [Test]
        public void Adr0023_ModalEncounter_BlocksStrategicTravel()
        {
            var session = StartCh01();
            var world = session.World;
            var id = session.CharacterIds[0];
            world.WorldPresence.SetAtNode(id, "base:node_huangcun");
            StrategicClockFreezeService.BeginOrPromote(world, StrategicClockFreezeReason.ManualEncounter);

            var started = WorldTravelPathService.StartAgentTravelToTarget(
                world, id, WorldTravelTarget.AtNode("base:node_linjian"));
            Assert.IsTrue(started.IsFailure, "Modal 下禁止战略出行");
        }

        [Test]
        public void LingeringReenter_PreservesEnemyIncapAndCorpseTimers()
        {
            var session = StartCh01();
            var world = session.World;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var stack));
            stack.MemberCount = 2;
            stack.IncapacitatedMemberCount = 2;
            stack.IsBattlefieldRemnant = true;

            StrategicEncounterSpawner.PlanManualEncounter(
                world, stack.Id, string.Empty, session.CharacterIds, 2, 2);
            world.PartyWorld.LocalMapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;
            Assert.IsTrue(StrategicEncounterSpawner.ApplyPending(world).IsSuccess);
            Assert.AreEqual(2, world.Strategic.Encounter.SpawnedEntityIds.Count);

            var incapId = new EntityId(world.Strategic.Encounter.SpawnedEntityIds[0]);
            var corpseId = new EntityId(world.Strategic.Encounter.SpawnedEntityIds[1]);
            Assert.IsTrue(world.Entities.TryGet(incapId, out var incapEnt));
            Assert.IsTrue(world.Entities.TryGet(corpseId, out var corpseEnt));
            Assert.IsTrue(incapEnt.TryGet<XianXia.Core.Entities.LifecycleComponent>(out var incapLife));
            Assert.IsTrue(incapLife.IsIncapacitated);

            // 我方弥留：再进残留也不该被刷怪逻辑改倒计时
            var allyId = session.CharacterIds[0];
            Assert.IsTrue(world.Entities.TryGet(allyId, out var allyEnt));
            Assert.IsTrue(
                XianXia.Core.Combat.CombatLifeStateService.TryEnterIncapacitated(world, allyEnt));
            Assert.IsTrue(allyEnt.TryGet<XianXia.Core.Entities.LifecycleComponent>(out var allyLife));
            var allyBleedSaved = allyLife.BleedOutAfterTick;

            // 推进时间，让倒计时不再是满值
            world.Tick = new XianXia.Core.Domain.Time.WorldTick(world.Tick.Value + 17);
            var bleedSaved = incapLife.BleedOutAfterTick;
            Assert.Less(
                bleedSaved - world.Tick.Value,
                XianXia.Core.Combat.CombatLifeStateService.BleedOutDurationTicks);

            Assert.IsTrue(
                XianXia.Core.Combat.CombatLifeStateService.TryConfirmDeath(
                    world, EntityId.None, corpseEnt, out _));
            Assert.IsTrue(corpseEnt.TryGet<XianXia.Core.Combat.CorpseComponent>(out var corpse));
            var corpseSaved = corpse.RemoveAfterTick;

            // 模拟出图残留后再进
            world.Strategic.Encounter.BattlefieldLingering = true;
            world.Strategic.Encounter.ArmyStackId = stack.Id;
            StrategicEncounterSpawner.PlanManualEncounter(
                world, stack.Id, string.Empty, session.CharacterIds, 2, 2);
            Assert.IsFalse(
                world.Strategic.Encounter.SpawnOnNextMapLoad,
                "已有弥留／尸体时不应再计划刷怪");
            Assert.IsTrue(StrategicEncounterSpawner.ApplyPending(world).IsSuccess);

            Assert.AreEqual(2, world.Strategic.Encounter.SpawnedEntityIds.Count);
            Assert.IsTrue(world.Entities.TryGet(incapId, out incapEnt));
            Assert.IsTrue(incapEnt.TryGet<XianXia.Core.Entities.LifecycleComponent>(out incapLife));
            Assert.AreEqual(bleedSaved, incapLife.BleedOutAfterTick, "再进不得刷新敌军弥留倒计时");
            Assert.IsTrue(world.Entities.TryGet(corpseId, out corpseEnt));
            Assert.IsTrue(corpseEnt.TryGet<XianXia.Core.Combat.CorpseComponent>(out corpse));
            Assert.AreEqual(corpseSaved, corpse.RemoveAfterTick, "再进不得刷新尸体腐烂倒计时");
            Assert.IsTrue(world.Entities.TryGet(allyId, out allyEnt));
            Assert.IsTrue(allyEnt.TryGet<XianXia.Core.Entities.LifecycleComponent>(out allyLife));
            Assert.AreEqual(allyBleedSaved, allyLife.BleedOutAfterTick, "再进不得刷新我方弥留倒计时");
        }

        [Test]
        public void FormalArmy_AutoBattleRemnant_DoesNotDuplicateGenericGrunts()
        {
            var session = StartCh01();
            var world = session.World;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var stack));
            Assert.IsTrue(ArmyStackAdapter.TryGetFormalArmy(world, stack, out var army));

            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
            {
                var id = new EntityId(army.MemberCharacterIds[i]);
                Assert.IsTrue(world.Entities.TryGet(id, out var entity));
                Assert.IsTrue(XianXia.Core.Combat.CombatLifeStateService.TryEnterIncapacitated(world, entity));
            }

            ArmyStackAdapter.SyncDownedCountsFromMembers(world, stack);
            stack.IsBattlefieldRemnant = true;
            world.Strategic.Encounter.ArmyStackId = stack.Id;

            var snap = BattleParticipantSnapshotBuilder.BuildArmyVsArmy(
                world,
                string.Empty,
                stack,
                "test:auto-remnant");
            StrategicEncounterSpawner.EnsureMacroRemnantSpawns(world, snap);

            Assert.AreEqual(
                army.MemberCharacterIds.Count,
                world.Strategic.Encounter.SpawnedEntityIds.Count,
                "残留应只跟踪 FormalArmy 成员，不应额外刷 generic 山贼");

            for (var i = 0; i < world.Strategic.Encounter.SpawnedEntityIds.Count; i++)
            {
                var tracked = new EntityId(world.Strategic.Encounter.SpawnedEntityIds[i]);
                Assert.IsTrue(army.ContainsMember(tracked));
                Assert.IsTrue(world.Entities.TryGet(tracked, out var ent));
                Assert.IsTrue(ent.TryGet<XianXia.Core.Entities.LifecycleComponent>(out var life));
                Assert.IsTrue(life.IsIncapacitated);
            }
        }

        static int CountTrackedEnemyDownedSpawns(XianXia.Core.Simulation.SimulationWorld world)
        {
            var rt = world?.Strategic?.Encounter;
            if (rt == null)
                return 0;
            var n = 0;
            for (var i = 0; i < rt.SpawnedEntityIds.Count; i++)
            {
                var id = new EntityId(rt.SpawnedEntityIds[i]);
                if (LingeringBattlefieldPartyService.IsLingeringDowned(world, id))
                    n++;
            }

            return n;
        }
    }
}
