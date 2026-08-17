using System.IO;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Random;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Data.Bootstrap;

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
        public void Phase0_RouteEncounter_ResolveSuccess_ClearsPending()
        {
            var session = StartCh01();
            var world = session.World;
            world.Strategic.RouteEncounter.Resolved = false;
            world.Strategic.RouteEncounter.EncounterId = RouteEncounterService.DefaultEncounterId;
            world.Strategic.RouteEncounter.LocalMapId = RouteEncounterService.DefaultEncounterLocalMapId;
            Assert.IsTrue(world.Strategic.HasBlockingInterrupt);

            RouteEncounterService.ResolveSuccess(world);
            Assert.IsFalse(world.Strategic.HasBlockingInterrupt);
            Assert.IsTrue(string.IsNullOrEmpty(world.PartyWorld.EncounterId));
        }

        [Test]
        public void Phase0_RouteEncounter_RollsDuringTravel_WhenDangerHigh()
        {
            var session = StartCh01();
            var world = session.World;
            var party = session.CharacterIds;
            var travel = WorldTravelService.StartTravel(world, party, "base:node_linjian");
            Assert.IsTrue(travel.IsSuccess, travel.IsFailure ? travel.Error.ToString() : "");

            Assert.IsTrue(world.WorldPresence.TryGet(party[0], out var presence));
            Assert.IsTrue(world.WorldGraph.TryGetRoute(presence.RouteId, out var route));
            route.Danger = 1f;

            var hit = false;
            for (var i = 0; i < 4000 && !hit; i++)
            {
                WorldTravelService.AdvanceTravel(world, 1);
                StrategicTravelDriver.AfterTravelTick(world, 1);
                hit = world.Strategic.HasBlockingInterrupt;
            }

            Assert.IsTrue(hit, "Expected route encounter within 4000 travel ticks at danger=1.");
            Assert.IsFalse(string.IsNullOrEmpty(world.Strategic.RouteEncounter.EncounterId));
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

            var resolved = BattleOfferService.ResolveAuto(world);
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
    }
}
