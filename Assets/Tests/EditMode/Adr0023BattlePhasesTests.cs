using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Random;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Data.Bootstrap;

namespace XianXia.Tests
{
    /// <summary>ADR-0023 Phase A～E 核心断言�?/summary>
    public sealed class Adr0023BattlePhasesTests
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
        public void PhaseB_ParticipantSnapshot_IncludesOptionalAndEnemyWithinRange()
        {
            var session = StartCh01();
            var world = session.World;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var enemy));

            var a = session.CharacterIds[0];
            var b = session.CharacterIds[1];
            world.WorldPresence.SetAtSite(a, "base:site_huangcun");
            world.WorldPresence.SetAtSite(b, "base:site_huangcun");
            if (ArmyStackAdapter.TryGetFormalArmy(world, enemy, out var enemyArmy) && enemyArmy != null)
                ArmyHexTravelService.InitializeArmyAtHex(enemyArmy, Ch01HexPrototypeMapBuilder.HuangcunHex);

            world.Strategic.ReinforcementWorldRadius = 1f;
            Assert.IsTrue(BattleOfferService.TryBuildOfferForArmy(
                world, new List<EntityId> { a }, enemy, "快照"));

            var snap = world.Strategic.Participants;
            Assert.AreEqual(1, CountKind(snap, BattleParticipantKind.MandatoryFriendly));
            Assert.GreaterOrEqual(CountKind(snap, BattleParticipantKind.OptionalFriendly), 1);
            Assert.AreEqual(1, CountKind(snap, BattleParticipantKind.EnemyPrimary));
            Assert.IsTrue(ArmyHexBattleAnchorService.HasBattleAnchorHex(snap));
        }

        [Test]
        public void PhaseB_ReinforcementRange_UsesWorldProximity_NotAdjacentNode()
        {
            var session = StartCh01();
            var world = session.World;
            var a = session.CharacterIds[0];
            world.WorldPresence.SetAtSite(a, "base:site_huangcun");
            Assert.IsTrue(world.WorldPresence.TryGet(a, out var p));

            world.Strategic.ReinforcementWorldRadius = 1f;
            ArmyHexBattleAnchorService.TryResolveHexForSite(world, "base:site_huangcun", out var huangcunHex);
            Assert.IsTrue(ReinforcementRangeService.IsWithinReinforcementRange(
                world, p, huangcunHex));
            ArmyHexBattleAnchorService.TryResolveHexForSite(world, "base:site_qingyun_lu", out var remoteHex);
            Assert.IsTrue(ReinforcementRangeService.TryGetWorldDistance(
                world, p, remoteHex, out var dist));
            Assert.Greater(dist, 1f);
            Assert.IsFalse(ReinforcementRangeService.IsWithinReinforcementRange(
                world, p, remoteHex));
        }

        [Test]
        public void PhaseC_OptionalToggle_ChangesOfferPower()
        {
            var session = StartCh01();
            var world = session.World;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var enemy));
            var a = session.CharacterIds[0];
            var b = session.CharacterIds[1];
            world.WorldPresence.SetAtSite(a, "base:site_huangcun");
            world.WorldPresence.SetAtSite(b, "base:site_huangcun");
            if (ArmyStackAdapter.TryGetFormalArmy(world, enemy, out var enemyArmy) && enemyArmy != null)
                ArmyHexTravelService.InitializeArmyAtHex(enemyArmy, Ch01HexPrototypeMapBuilder.HuangcunHex);
            world.Strategic.ReinforcementWorldRadius = 1f;

            Assert.IsTrue(BattleOfferService.TryBuildOfferForArmy(
                world, new List<EntityId> { a }, enemy, "勾选"));
            var before = world.Strategic.BattleOffer.PlayerPower;
            Assert.IsTrue(BattleOfferService.SetOptionalSelected(world, b, true));
            Assert.Greater(world.Strategic.BattleOffer.PlayerPower, before);
            Assert.IsTrue(BattleOfferService.SetOptionalSelected(world, b, false));
            Assert.AreEqual(before, world.Strategic.BattleOffer.PlayerPower);
        }

        [Test]
        public void PhaseD_OptionalRestore_EngagedStaysAtBattleAnchor_NoTeleportHome()
        {
            var session = StartCh01();
            var world = session.World;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var enemy));
            var a = session.CharacterIds[0];
            var b = session.CharacterIds[1];
            world.WorldPresence.SetAtSite(a, "base:site_huangcun");
            world.WorldPresence.SetAtSite(b, "base:site_huangcun");
            if (ArmyStackAdapter.TryGetFormalArmy(world, enemy, out var enemyArmy) && enemyArmy != null)
                ArmyHexTravelService.InitializeArmyAtHex(enemyArmy, Ch01HexPrototypeMapBuilder.HuangcunHex);
            world.Strategic.ReinforcementWorldRadius = 1f;

            Assert.IsTrue(BattleOfferService.TryBuildOfferForArmy(
                world, new List<EntityId> { a }, enemy, "还原"));
            Assert.IsTrue(BattleOfferService.SetOptionalSelected(world, b, true));

            Assert.IsTrue(world.WorldPresence.TryGet(b, out var bp));
            bp.Mode = PartyWorldPresenceMode.InEncounter;
            bp.SiteId = "base:site_huangcun";
            world.Strategic.Encounter.AddEngagedPartyMember(b);

            world.Strategic.Participants.PlayerWon = true;
            world.Strategic.Participants.LastBattleSummary = "测试清场";
            StrategicClockFreezeService.BeginOrPromote(world, StrategicClockFreezeReason.PostBattle);
            Assert.IsTrue(StrategicEncounterResolveService.ResolveAndEnd(world).IsSuccess);

            Assert.IsTrue(world.WorldPresence.TryGet(b, out var after));
            Assert.AreEqual("base:site_huangcun", after.SiteId, "上场支援留在接战锚点，禁止瞬移回�");
        }

        [Test]
        public void PhaseE_InterruptQueue_SerialOffers()
        {
            var session = StartCh01();
            var world = session.World;
            Assert.IsTrue(world.Strategic.Armies.TryGet("army:bandit_patrol_1", out var e1));

            // 注册第二敌军�?
            var e2 = world.Strategic.Armies.Register(new ArmyStack
            {
                Id = "army:test_bandit_2",
                FactionId = e1.FactionId,
                DisplayName = "测试匪",
                SiteId = "base:site_huangcun",
                MemberCount = 2,
                CombatPower = 4
            });

            var a = session.CharacterIds[0];
            world.WorldPresence.SetAtSite(a, "base:site_huangcun");
            if (ArmyStackAdapter.TryGetFormalArmy(world, e1, out var e1Army) && e1Army != null)
                ArmyHexTravelService.InitializeArmyAtHex(e1Army, Ch01HexPrototypeMapBuilder.HuangcunHex);

            var tick = world.Tick.Value;
            Assert.IsTrue(BattleOfferService.TryBuildOfferForArmy(
                world, new List<EntityId> { a }, e1, "A"));
            Assert.IsTrue(world.Strategic.HasBattleOffer);
            Assert.IsTrue(BattleOfferService.TryBuildOfferForArmy(
                world, new List<EntityId> { a }, e2, "B"));
            Assert.AreEqual(1, world.Strategic.InterruptQueue.Count);

            world.Random = new DeterministicRandom(1);
            world.Strategic.BattleOffer.AutoWinPercent = 100;
            Assert.IsTrue(BattleOfferService.ResolveAuto(world, false, out _, out _).IsSuccess);
            Assert.AreEqual(tick, world.Tick.Value);
            Assert.IsTrue(world.Strategic.Participants.IsAutoSettlement, "自动战应先出结算�");
            Assert.IsTrue(StrategicEncounterResolveService.ResolveAndEnd(world).IsSuccess);
            Assert.IsTrue(world.Strategic.HasBattleOffer, "确认结算后应弹出队列中的 B");
            Assert.AreEqual(e2.Id, world.Strategic.BattleOffer.ArmyStackId);
            Assert.IsTrue(world.Strategic.IsWorldTickFrozen);

            world.Strategic.BattleOffer.AutoWinPercent = 100;
            Assert.IsTrue(BattleOfferService.ResolveAuto(world, false, out _, out _).IsSuccess);
            Assert.IsTrue(StrategicEncounterResolveService.ResolveAndEnd(world).IsSuccess);
            Assert.IsFalse(world.Strategic.IsWorldTickFrozen, "队列清空后解�");
            Assert.AreEqual(tick, world.Tick.Value);
        }

        [Test]
        public void PhaseA_Modal_BlocksStrategicOrders()
        {
            var session = StartCh01();
            var world = session.World;
            var id = session.CharacterIds[0];
            StrategicClockFreezeService.BeginOrPromote(world, StrategicClockFreezeReason.ManualEncounter);
            Assert.IsFalse(WorldTravelService.CanReceiveTravelOrder(world, id));
        }

        static int CountKind(BattleParticipantSnapshot snap, BattleParticipantKind kind)
        {
            var n = 0;
            for (var i = 0; i < snap.Records.Count; i++)
            {
                if (snap.Records[i].Kind == kind)
                    n++;
            }

            return n;
        }
    }
}
