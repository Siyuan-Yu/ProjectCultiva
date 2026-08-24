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
    /// <summary>MULTI-BATTLE-01..06：连续多场 Encounter Anchor 生命周期。</summary>
    public sealed class MultiBattleAnchorLifecycleTests
    {
        const string PlayerFaction = StrategicFactionCatalog.PlayerFactionId;
        const string NodeA = "base:node_huangcun";
        static readonly HexCoord H1 = Ch01HexPrototypeMapBuilder.QingyunLuHex;
        static readonly HexCoord H2 = new HexCoord(42, 20);
        static readonly HexCoord H3 = new HexCoord(45, 18);
        static readonly HexCoord Huangcun = Ch01HexPrototypeMapBuilder.HuangcunHex;

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            world.Strategic.PlayerFactionId = PlayerFaction;
            HexTestWorldBootstrap.EnsureMinimalHexMap(world);
            EnsurePassable(world, H1);
            EnsurePassable(world, H2);
            EnsurePassable(world, H3);
            WarGateService.DeclareWar(world, PlayerFaction, StrategicFactionCatalog.BanditId);
            return world;
        }

        static void EnsurePassable(SimulationWorld world, HexCoord hex)
        {
            if (world?.HexWorld == null || !world.HexWorld.Contains(hex))
                return;
            if (!world.HexWorld.TryGetCell(hex, out var cell) || cell == null)
                return;
            cell.IsPassable = true;
        }

        static EntityId SpawnFriendly(SimulationWorld world, string name)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            created.Value.Get<FactionMembershipComponent>().Assign(PlayerFaction, FactionRoleKind.Member);
            world.WorldPresence.SetAtNode(created.Value.Id, NodeA);
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

        static FormalArmy SpawnEnemyAt(SimulationWorld world, HexCoord hex, string stackId, string formalId)
        {
            FormalArmy army;
            if (string.Equals(stackId, ArmyStackAdapter.BanditPatrolStackId, System.StringComparison.Ordinal))
            {
                var result = ArmyStackAdapter.EnsureBanditPatrolArmy(
                    world, NodeA, string.Empty, string.Empty, -1f);
                Assert.IsTrue(result.IsSuccess);
                army = result.Value;
            }
            else
            {
                var result = ArmyStackAdapter.EnsureBanditWeakPatrolArmy(
                    world, NodeA, string.Empty, string.Empty, -1f);
                Assert.IsTrue(result.IsSuccess);
                army = result.Value;
            }

            ArmyHexTravelService.InitializeArmyAtHex(army, hex);
            return army;
        }

        static FormalArmy SpawnCustomEnemyAt(SimulationWorld world, HexCoord hex, string name)
        {
            var result = ArmyStackAdapter.EnsureBanditScoutArmy(
                world, NodeA, string.Empty, string.Empty, -1f, 0);
            Assert.IsTrue(result.IsSuccess, name);
            ArmyHexTravelService.InitializeArmyAtHex(result.Value, hex);
            return result.Value;
        }

        static bool TryGetEnemyStack(SimulationWorld world, FormalArmy enemy, out ArmyStack stack)
        {
            stack = null;
            if (world?.Strategic?.Armies == null || enemy == null)
                return false;
            foreach (var kv in world.Strategic.Armies.Stacks)
            {
                var candidate = kv.Value;
                if (candidate == null)
                    continue;
                if (ArmyStackAdapter.TryGetFormalArmy(world, candidate, out var formal) &&
                    formal != null &&
                    string.Equals(formal.ArmyId, enemy.ArmyId, System.StringComparison.Ordinal))
                {
                    stack = candidate;
                    return true;
                }
            }

            return false;
        }

        static void AutoWinThenFinish(SimulationWorld world, FormalArmy player, FormalArmy enemy)
        {
            Assert.IsTrue(player.UsesHexStrategicPosition);
            Assert.IsTrue(enemy.UsesHexStrategicPosition);
            Assert.AreEqual(player.CurrentHex, enemy.CurrentHex, "Contact must be same hex before offer");

            var contact = player.CurrentHex;
            Assert.IsTrue(TryGetEnemyStack(world, enemy, out var stack), "Enemy stack missing");

            var party = ArmyStackAdapter.CollectLivingMemberIds(world, player);
            Assert.IsTrue(BattleOfferService.TryBuildOfferForArmyVsArmy(
                world, player.ArmyId, party, stack, "MultiBattle"));

            Assert.IsTrue(ArmyHexBattleAnchorService.TryGetBattleAnchorHex(
                world.Strategic.Participants, out var offerAnchor));
            Assert.AreEqual(contact, offerAnchor, "BattleOffer must use Contact Hex, not travel origin");

            world.Strategic.BattleOffer.AutoWinPercent = 100;
            world.Random = new XianXia.Core.Random.DeterministicRandom(1);
            Assert.IsTrue(BattleOfferService.ResolveAuto(world, false, out var won, out _).IsSuccess);
            Assert.IsTrue(won);

            Assert.IsTrue(ArmyHexBattleAnchorService.TryGetBattleAnchorHex(
                world.Strategic.Participants, out var afterAuto));
            Assert.AreEqual(contact, afterAuto, "After AutoResolve anchor must stay Contact Hex");

            Assert.IsTrue(StrategicEncounterResolveService.ResolveAndEnd(world).IsSuccess);
            Assert.IsTrue(BattleOfferService.FinishOfferResolution(world).IsSuccess);

            Assert.AreEqual(contact, player.CurrentHex, "Settlement Army Hex must equal Contact Hex");
            Assert.AreNotEqual(Huangcun, player.CurrentHex);
        }

        static List<EntityId> CollectEnemyResidualsAt(SimulationWorld world, HexCoord hex)
        {
            var list = new List<EntityId>(4);
            foreach (var kv in world.WorldPresence.All)
            {
                var id = new EntityId(kv.Key);
                if (!StrategicResidualPresenceService.IsStrategicResidualCandidate(world, id))
                    continue;
                if (!StrategicResidualPresenceService.TryGetResidualHex(world, id, out var rh) ||
                    !rh.Equals(hex))
                    continue;
                list.Add(id);
            }

            return list;
        }

        [Test]
        public void MULTI_BATTLE_01_SameHexBattle_SettlesAtContact()
        {
            Assert.AreNotEqual(H1, H2);
            var world = CreateWorld();
            var player = SpawnPlayerArmy(world, H1);
            var enemy = SpawnEnemyAt(
                world, H1, ArmyStackAdapter.BanditPatrolStackId, ArmyStackAdapter.BanditPatrolFormalArmyId);
            AutoWinThenFinish(world, player, enemy);
            Assert.AreEqual(H1, player.CurrentHex);
            Assert.IsTrue(world.Strategic.LingeringBattlefields.HasAtHex(H1));
        }

        [Test]
        public void MULTI_BATTLE_02_TravelThenBattle_SettlesAtH2NotH1()
        {
            var world = CreateWorld();
            var player = SpawnPlayerArmy(world, H1);
            var enemy1 = SpawnEnemyAt(
                world, H1, ArmyStackAdapter.BanditPatrolStackId, ArmyStackAdapter.BanditPatrolFormalArmyId);
            AutoWinThenFinish(world, player, enemy1);
            Assert.AreEqual(H1, player.CurrentHex);

            var enemy2 = SpawnEnemyAt(
                world, H2, ArmyStackAdapter.BanditWeakPatrolStackId, ArmyStackAdapter.BanditWeakPatrolFormalArmyId);
            ArmyHexTravelService.InitializeArmyAtHex(player, H2);
            Assert.AreEqual(H2, player.CurrentHex);
            Assert.AreEqual(H2, enemy2.CurrentHex);

            AutoWinThenFinish(world, player, enemy2);
            Assert.AreEqual(H2, player.CurrentHex, "Battle2 must settle at H2, not roll back to H1");
            Assert.AreNotEqual(H1, player.CurrentHex);
        }

        [Test]
        public void MULTI_BATTLE_03_Battle2Residual_AtH2()
        {
            var world = CreateWorld();
            var player = SpawnPlayerArmy(world, H1);
            var enemy1 = SpawnEnemyAt(
                world, H1, ArmyStackAdapter.BanditPatrolStackId, ArmyStackAdapter.BanditPatrolFormalArmyId);
            AutoWinThenFinish(world, player, enemy1);

            var enemy2 = SpawnEnemyAt(
                world, H2, ArmyStackAdapter.BanditWeakPatrolStackId, ArmyStackAdapter.BanditWeakPatrolFormalArmyId);
            ArmyHexTravelService.InitializeArmyAtHex(player, H2);
            AutoWinThenFinish(world, player, enemy2);

            var residuals = CollectEnemyResidualsAt(world, H2);
            Assert.Greater(residuals.Count, 0, "Battle2 residual must exist at H2");
            for (var i = 0; i < residuals.Count; i++)
            {
                Assert.IsTrue(StrategicResidualPresenceService.TryGetResidualHex(
                    world, residuals[i], out var hex));
                Assert.AreEqual(H2, hex);
                Assert.AreNotEqual(H1, hex);
            }
        }

        [Test]
        public void MULTI_BATTLE_04_BothLingeringAnchors_H1AndH2()
        {
            var world = CreateWorld();
            var player = SpawnPlayerArmy(world, H1);
            var enemy1 = SpawnEnemyAt(
                world, H1, ArmyStackAdapter.BanditPatrolStackId, ArmyStackAdapter.BanditPatrolFormalArmyId);
            AutoWinThenFinish(world, player, enemy1);

            var enemy2 = SpawnEnemyAt(
                world, H2, ArmyStackAdapter.BanditWeakPatrolStackId, ArmyStackAdapter.BanditWeakPatrolFormalArmyId);
            ArmyHexTravelService.InitializeArmyAtHex(player, H2);
            AutoWinThenFinish(world, player, enemy2);

            Assert.IsTrue(world.Strategic.LingeringBattlefields.HasAtHex(H1));
            Assert.IsTrue(world.Strategic.LingeringBattlefields.HasAtHex(H2));
            Assert.IsTrue(LingeringBattlefieldQueryService.TryGetLingeringBattlefieldAtHex(
                world, H1, out _));
            Assert.IsTrue(LingeringBattlefieldQueryService.TryGetLingeringBattlefieldAtHex(
                world, H2, out _));
        }

        [Test]
        public void MULTI_BATTLE_05_EnterExitBattle1Lingering_DoesNotBindArmyToH1()
        {
            var world = CreateWorld();
            var player = SpawnPlayerArmy(world, H1);
            var enemy1 = SpawnEnemyAt(
                world, H1, ArmyStackAdapter.BanditPatrolStackId, ArmyStackAdapter.BanditPatrolFormalArmyId);
            AutoWinThenFinish(world, player, enemy1);

            var party = ArmyStackAdapter.CollectLivingMemberIds(world, player);
            Assert.IsTrue(BattleOfferService.TryBuildOfferForEnemyRemnantReentry(
                world, party, ArmyStackAdapter.BanditPatrolStackId, "残留战场", H1));
            Assert.IsTrue(ArmyHexBattleAnchorService.TryGetBattleAnchorHex(
                world.Strategic.Participants, out var lingerAnchor));
            Assert.AreEqual(H1, lingerAnchor);

            Assert.IsTrue(StrategicEncounterResolveService.ResolveAndEnd(world).IsSuccess);
            Assert.IsTrue(BattleOfferService.FinishOfferResolution(world).IsSuccess);

            ArmyHexTravelService.InitializeArmyAtHex(player, H2);
            var enemy2 = SpawnEnemyAt(
                world, H2, ArmyStackAdapter.BanditWeakPatrolStackId, ArmyStackAdapter.BanditWeakPatrolFormalArmyId);
            AutoWinThenFinish(world, player, enemy2);
            Assert.AreEqual(H2, player.CurrentHex);
        }

        [Test]
        public void MULTI_BATTLE_07_Battle1Residual_SurvivesBattle2AutoSettle()
        {
            var world = CreateWorld();
            var player = SpawnPlayerArmy(world, H1);
            var enemy1 = SpawnEnemyAt(
                world, H1, ArmyStackAdapter.BanditPatrolStackId, ArmyStackAdapter.BanditPatrolFormalArmyId);
            AutoWinThenFinish(world, player, enemy1);

            var h1Before = CollectEnemyResidualsAt(world, H1);
            Assert.Greater(h1Before.Count, 0, "Battle1 must leave residuals at H1");

            var enemy2 = SpawnEnemyAt(
                world, H2, ArmyStackAdapter.BanditWeakPatrolStackId, ArmyStackAdapter.BanditWeakPatrolFormalArmyId);
            ArmyHexTravelService.InitializeArmyAtHex(player, H2);
            AutoWinThenFinish(world, player, enemy2);

            var h1After = CollectEnemyResidualsAt(world, H1);
            Assert.AreEqual(
                h1Before.Count,
                h1After.Count,
                "Battle2 cleanup must not remove Battle1 residuals at H1");
            for (var i = 0; i < h1After.Count; i++)
            {
                Assert.IsTrue(StrategicResidualPresenceService.TryGetResidualHex(
                    world, h1After[i], out var hex));
                Assert.AreEqual(H1, hex);
            }
        }

        [Test]
        public void MULTI_BATTLE_08_Battle2LingeringLookup_UsesWeakStackNotPatrol()
        {
            var world = CreateWorld();
            var player = SpawnPlayerArmy(world, H1);
            var enemy1 = SpawnEnemyAt(
                world, H1, ArmyStackAdapter.BanditPatrolStackId, ArmyStackAdapter.BanditPatrolFormalArmyId);
            AutoWinThenFinish(world, player, enemy1);

            var enemy2 = SpawnEnemyAt(
                world, H2, ArmyStackAdapter.BanditWeakPatrolStackId, ArmyStackAdapter.BanditWeakPatrolFormalArmyId);
            ArmyHexTravelService.InitializeArmyAtHex(player, H2);
            AutoWinThenFinish(world, player, enemy2);

            Assert.IsTrue(LingeringBattlefieldQueryService.TryGetLingeringBattlefieldAtHex(
                world, H2, out var ctx));
            Assert.AreEqual(ArmyStackAdapter.BanditWeakPatrolStackId, ctx.EnemyStackId);
            Assert.AreNotEqual(ArmyStackAdapter.BanditPatrolStackId, ctx.EnemyStackId);

            Assert.IsTrue(world.Strategic.LingeringBattlefields.TryGetAtHex(
                H2, out var record));
            Assert.AreEqual(ArmyStackAdapter.BanditWeakPatrolStackId, record.EnemyStackId);

            var h2Residuals = CollectEnemyResidualsAt(world, H2);
            Assert.AreEqual(1, h2Residuals.Count, "Battle2 weak patrol must leave exactly 1 residual");
        }

        static int CountTrackedForHex(SimulationWorld world, HexCoord hex, string stackId)
        {
            var rt = world.Strategic.Encounter;
            FormalArmy army = null;
            if (world.Strategic.Armies.TryGet(stackId, out var stack) &&
                stack != null)
                ArmyStackAdapter.TryGetFormalArmy(world, stack, out army);

            var count = 0;
            for (var i = 0; i < rt.SpawnedEntityIds.Count; i++)
            {
                var id = new EntityId(rt.SpawnedEntityIds[i]);
                if (StrategicResidualPresenceService.TryGetResidualHex(world, id, out var rh) &&
                    rh.Equals(hex))
                    count++;
                else if (army != null && army.ContainsMember(id))
                    count++;
            }

            return count;
        }

        [Test]
        public void MULTI_BATTLE_09_Battle2MacroRemnant_DoesNotFinalizeBattle1()
        {
            var world = CreateWorld();
            var player = SpawnPlayerArmy(world, H1);
            var enemy1 = SpawnEnemyAt(
                world, H1, ArmyStackAdapter.BanditPatrolStackId, ArmyStackAdapter.BanditPatrolFormalArmyId);
            AutoWinThenFinish(world, player, enemy1);

            var h1Ids = CollectEnemyResidualsAt(world, H1);
            Assert.GreaterOrEqual(h1Ids.Count, 1);

            var enemy2 = SpawnEnemyAt(
                world, H2, ArmyStackAdapter.BanditWeakPatrolStackId, ArmyStackAdapter.BanditWeakPatrolFormalArmyId);
            ArmyHexTravelService.InitializeArmyAtHex(player, H2);
            AutoWinThenFinish(world, player, enemy2);

            for (var i = 0; i < h1Ids.Count; i++)
            {
                Assert.IsTrue(
                    world.Entities.TryGet(h1Ids[i], out var ent) && ent != null,
                    "Battle1 residual entity must still exist after Battle2 EnsureMacroRemnantSpawns");
                Assert.IsTrue(
                    StrategicResidualPresenceService.IsStrategicResidualCandidate(world, h1Ids[i]),
                    "Battle1 residual must remain strategic candidate");
            }
        }

        [Test]
        public void MULTI_BATTLE_06_Battle3_SettlesAtH3_NoStaleH1H2()
        {
            var world = CreateWorld();
            var player = SpawnPlayerArmy(world, H1);

            var e1 = SpawnEnemyAt(
                world, H1, ArmyStackAdapter.BanditPatrolStackId, ArmyStackAdapter.BanditPatrolFormalArmyId);
            AutoWinThenFinish(world, player, e1);

            var e2 = SpawnEnemyAt(
                world, H2, ArmyStackAdapter.BanditWeakPatrolStackId, ArmyStackAdapter.BanditWeakPatrolFormalArmyId);
            ArmyHexTravelService.InitializeArmyAtHex(player, H2);
            AutoWinThenFinish(world, player, e2);

            var e3 = SpawnCustomEnemyAt(world, H3, "BanditWave3");
            ArmyHexTravelService.InitializeArmyAtHex(player, H3);
            AutoWinThenFinish(world, player, e3);
            Assert.AreEqual(H3, player.CurrentHex);
            Assert.AreNotEqual(H1, player.CurrentHex);
            Assert.AreNotEqual(H2, player.CurrentHex);
        }
    }
}
