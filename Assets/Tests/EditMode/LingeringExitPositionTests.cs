using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    /// <summary>LINGER-POS-01..：残留战场再�?/ 退出战�?Hex 回归�?/summary>
    public sealed class LingeringExitPositionTests
    {
        const string PlayerFaction = StrategicFactionCatalog.PlayerFactionId;
        const string NodeA = "base:site_huangcun";
        static readonly HexCoord HuangcunHex = Ch01HexPrototypeMapBuilder.HuangcunHex;
        static readonly HexCoord BattleHex = Ch01HexPrototypeMapBuilder.QingyunLuHex;

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            world.Strategic.PlayerFactionId = PlayerFaction;
            HexTestWorldBootstrap.EnsureMinimalHexMap(world);
            WarGateService.DeclareWar(world, PlayerFaction, StrategicFactionCatalog.BanditId);
            return world;
        }

        static EntityId SpawnFriendly(SimulationWorld world, string name)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            created.Value.Get<FactionMembershipComponent>().Assign(PlayerFaction, FactionRoleKind.Member);
            world.WorldPresence.SetAtSite(created.Value.Id, NodeA);
            return created.Value.Id;
        }

        static FormalArmy SpawnPlayerArmy(SimulationWorld world, HexCoord hex)
        {
            var leader = SpawnFriendly(world, "Leader");
            var created = ArmyService.CreateArmy(
                world,
                PlayerFaction,
                NodeA,
                new[] { leader });
            Assert.IsTrue(created.IsSuccess);
            ArmyHexTravelService.InitializeArmyAtHex(created.Value, hex);
            return created.Value;
        }

        static void ConfirmDeath(SimulationWorld world, EntityId id)
        {
            Assert.IsTrue(world.Entities.TryGet(id, out var entity));
            Assert.IsTrue(CombatLifeStateService.TryConfirmDeath(world, EntityId.None, entity, out _));
        }

        static void SeedAutoBattleEnemyDownedThenFinish(
            SimulationWorld world,
            HexCoord hex,
            bool executeOnWin)
        {
            var result = ArmyStackAdapter.EnsureBanditPatrolArmy(world, NodeA);
            Assert.IsTrue(result.IsSuccess);
            ArmyHexTravelService.InitializeArmyAtHex(result.Value, hex);
            Assert.IsTrue(world.Strategic.Armies.TryGet(ArmyStackAdapter.BanditPatrolStackId, out var stack));

            var player = SpawnFriendly(world, "AutoLeader");
            var report = AutoBattleCasualtyService.ApplyPlayerVictory(
                world,
                new[] { player },
                stack,
                playerPower: 40,
                enemyPower: 5,
                executeOnWin: executeOnWin);
            Assert.IsNotNull(report);

            var snap = world.Strategic.Participants;
            snap.PrimaryEnemyStackId = ArmyStackAdapter.BanditPatrolStackId;
            ArmyHexBattleAnchorService.SetBattleAnchorHex(snap, hex);
            world.Strategic.Encounter.ArmyStackId = ArmyStackAdapter.BanditPatrolStackId;

            StrategicEncounterSpawner.EnsureMacroRemnantSpawns(world, snap);
            ArmyPostBattleSyncService.SyncEnemyArmyAfterBattle(world, snap);

            Assert.IsTrue(StrategicEncounterResolveService.HasLingeringBattlefieldRemnants(world));
            Assert.IsTrue(StrategicEncounterResolveService.ResolveAndEnd(world).IsSuccess);
            Assert.IsTrue(BattleOfferService.FinishOfferResolution(world).IsSuccess);

            Assert.IsTrue(world.Strategic.Encounter.BattlefieldLingering);
            Assert.IsTrue(StrategicEncounterResolveService.TryGetLingeringBattleAnchorHex(
                world, out var anchor));
            Assert.AreEqual(hex, anchor);
        }

        static List<EntityId> CollectEnemyResidualIds(SimulationWorld world, HexCoord hex)
        {
            var list = new List<EntityId>(4);
            foreach (var kv in world.WorldPresence.All)
            {
                var id = new EntityId(kv.Key);
                if (id.IsNone ||
                    !StrategicResidualPresenceService.IsStrategicResidualCandidate(world, id))
                    continue;
                if (!StrategicResidualPresenceService.TryGetResidualHex(world, id, out var residualHex) ||
                    !residualHex.Equals(hex))
                    continue;
                list.Add(id);
            }

            return list;
        }

        static void SimulateLingeringReentryAndExit(
            SimulationWorld world,
            FormalArmy playerArmy,
            string enemyStackId)
        {
            var party = ArmyStackAdapter.CollectLivingMemberIds(world, playerArmy);
            Assert.Greater(party.Count, 0);
            Assert.IsTrue(BattleOfferService.TryBuildOfferForEnemyRemnantReentry(
                world, party, enemyStackId, "残留战场"));

            Assert.IsTrue(ArmyHexBattleAnchorService.TryGetBattleAnchorHex(
                world.Strategic.Participants, out var offerAnchor));
            Assert.AreEqual(
                BattleHex,
                offerAnchor,
                "再进 Offer 必须�?BattleAnchorHex 钉在 canonical lingering hex，而非 spawn Node");

            StrategicClockFreezeService.BeginOrPromote(
                world, StrategicClockFreezeReason.ManualEncounter);
            var engaged = world.Strategic.Participants.CollectSelectedFriendly();
            StrategicEncounterSpawner.PlanManualEncounter(
                world,
                enemyStackId,
                "linger-reentry",
                engaged,
                3,
                2);
            world.PartyWorld.LocalMapId = BattleOfferService.ResolveActiveEncounterLocalMapId(world);
            Assert.IsTrue(StrategicEncounterSpawner.ApplyPending(world).IsSuccess);

            Assert.IsTrue(StrategicEncounterResolveService.ResolveAndEnd(world).IsSuccess);
            Assert.IsTrue(BattleOfferService.FinishOfferResolution(world).IsSuccess);
        }

        static void AssertArmyAtHex(FormalArmy army, HexCoord expected, string message)
        {
            Assert.IsNotNull(army);
            Assert.IsTrue(army.UsesHexStrategicPosition, message);
            Assert.AreEqual(expected, army.CurrentHex, message);
            Assert.AreNotEqual(HuangcunHex, army.CurrentHex, message + " (must not snap to 青石荒村)");
        }

        static void AssertResidualsAtHex(
            SimulationWorld world,
            IReadOnlyList<EntityId> ids,
            HexCoord expected,
            string message)
        {
            for (var i = 0; i < ids.Count; i++)
            {
                Assert.IsTrue(
                    StrategicResidualPresenceService.TryGetResidualHex(world, ids[i], out var hex),
                    message);
                Assert.AreEqual(expected, hex, message);
                Assert.AreNotEqual(HuangcunHex, hex, message + " (must not snap to 青石荒村)");
            }
        }

        [Test]
        public void LINGER_POS_01_EnemyLingeringReentryExit_ArmyAndResidualStayAtBattleHex()
        {
            var world = CreateWorld();
            SeedAutoBattleEnemyDownedThenFinish(world, BattleHex, executeOnWin: false);
            var playerArmy = SpawnPlayerArmy(world, BattleHex);
            var residualIds = CollectEnemyResidualIds(world, BattleHex);
            Assert.Greater(residualIds.Count, 0);

            SimulateLingeringReentryAndExit(
                world, playerArmy, ArmyStackAdapter.BanditPatrolStackId);

            AssertArmyAtHex(playerArmy, BattleHex, "Army after lingering exit");
            AssertResidualsAtHex(world, residualIds, BattleHex, "Enemy residual after lingering exit");
            Assert.IsTrue(StrategicEncounterResolveService.TryGetLingeringBattleAnchorHex(
                world, out var anchor));
            Assert.AreEqual(BattleHex, anchor);
        }

        [Test]
        public void LINGER_POS_02_DownedResidual_StaysAtBattleHexAfterExit()
        {
            var world = CreateWorld();
            SeedAutoBattleEnemyDownedThenFinish(world, BattleHex, executeOnWin: false);
            var playerArmy = SpawnPlayerArmy(world, BattleHex);
            var residualIds = CollectEnemyResidualIds(world, BattleHex);
            Assert.IsTrue(residualIds.Count > 0);
            Assert.IsTrue(LingeringBattlefieldPartyService.IsIncapacitated(world, residualIds[0]));

            SimulateLingeringReentryAndExit(
                world, playerArmy, ArmyStackAdapter.BanditPatrolStackId);

            Assert.IsTrue(LingeringBattlefieldPartyService.IsIncapacitated(world, residualIds[0]));
            AssertResidualsAtHex(world, residualIds, BattleHex, "Downed residual");
        }

        [Test]
        public void LINGER_POS_03_DeadCorpse_StaysAtBattleHexAfterExit()
        {
            var world = CreateWorld();
            SeedAutoBattleEnemyDownedThenFinish(world, BattleHex, executeOnWin: true);
            var playerArmy = SpawnPlayerArmy(world, BattleHex);
            var residualIds = CollectEnemyResidualIds(world, BattleHex);
            Assert.IsTrue(residualIds.Count > 0);
            ConfirmDeath(world, residualIds[0]);
            Assert.IsTrue(LingeringBattlefieldPartyService.IsVisibleCorpse(world, residualIds[0]));

            SimulateLingeringReentryAndExit(
                world, playerArmy, ArmyStackAdapter.BanditPatrolStackId);

            Assert.IsTrue(LingeringBattlefieldPartyService.IsVisibleCorpse(world, residualIds[0]));
            AssertResidualsAtHex(world, residualIds, BattleHex, "Dead corpse");
        }

        [Test]
        public void LINGER_POS_04_ArmyMembersStayAtBattleHexAfterExit()
        {
            var world = CreateWorld();
            SeedAutoBattleEnemyDownedThenFinish(world, BattleHex, executeOnWin: false);
            var leader = SpawnFriendly(world, "A");
            var mate = SpawnFriendly(world, "B");
            var created = ArmyService.CreateArmy(world, PlayerFaction, NodeA, new[] { leader, mate });
            Assert.IsTrue(created.IsSuccess);
            ArmyHexTravelService.InitializeArmyAtHex(created.Value, BattleHex);

            SimulateLingeringReentryAndExit(
                world, created.Value, ArmyStackAdapter.BanditPatrolStackId);

            AssertArmyAtHex(created.Value, BattleHex, "Multi-member army");
            Assert.AreEqual(2, created.Value.MemberCharacterIds.Count);
        }

        [Test]
        public void LINGER_POS_05_SecondReentry_StillAtBattleHex()
        {
            var world = CreateWorld();
            SeedAutoBattleEnemyDownedThenFinish(world, BattleHex, executeOnWin: false);
            var playerArmy = SpawnPlayerArmy(world, BattleHex);

            SimulateLingeringReentryAndExit(
                world, playerArmy, ArmyStackAdapter.BanditPatrolStackId);
            AssertArmyAtHex(playerArmy, BattleHex, "After first exit");

            Assert.IsTrue(LingeringBattlefieldQueryService.TryGetLingeringBattlefieldAtHex(
                world, BattleHex, out _));

            SimulateLingeringReentryAndExit(
                world, playerArmy, ArmyStackAdapter.BanditPatrolStackId);
            AssertArmyAtHex(playerArmy, BattleHex, "After second exit");
            Assert.IsTrue(LingeringBattlefieldQueryService.TryGetLingeringBattlefieldAtHex(
                world, BattleHex, out _));
        }

        [Test]
        public void LINGER_POS_06_ReentryOffer_DoesNotUseSpawnNodeAsBattleAnchor()
        {
            var world = CreateWorld();
            SeedAutoBattleEnemyDownedThenFinish(world, BattleHex, executeOnWin: false);
            var playerArmy = SpawnPlayerArmy(world, BattleHex);
            var party = ArmyStackAdapter.CollectLivingMemberIds(world, playerArmy);

            Assert.IsTrue(BattleOfferService.TryBuildOfferForEnemyRemnantReentry(
                world, party, ArmyStackAdapter.BanditPatrolStackId, "残留战场"));
            Assert.IsTrue(ArmyHexBattleAnchorService.TryGetBattleAnchorHex(
                world.Strategic.Participants, out var anchor));
            Assert.AreEqual(BattleHex, anchor);
            Assert.AreNotEqual(HuangcunHex, anchor);
        }
    }
}
