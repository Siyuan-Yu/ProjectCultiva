using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;

namespace XianXia.Tests
{
    /// <summary>MULTI-ENCOUNTER-01..11：Encounter-scoped spawn / registry / hostility。</summary>
    public sealed class MultiEncounterLifecycleTests
    {
        const string PlayerFaction = StrategicFactionCatalog.PlayerFactionId;
        const string NodeA = "base:node_huangcun";
        static readonly HexCoord H1 = Ch01HexPrototypeMapBuilder.QingyunLuHex;
        static readonly HexCoord H2 = new HexCoord(42, 20);
        static readonly HexCoord H3 = new HexCoord(45, 18);

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            world.Strategic.PlayerFactionId = PlayerFaction;
            world.WorldGraph.RegisterNode(new WorldNodeState
            {
                Id = NodeA,
                Name = "荒村",
                OwnerId = PlayerFaction,
                WorldX = 0f,
                WorldY = 0f
            });
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

        static FormalArmy SpawnEnemyAt(SimulationWorld world, HexCoord hex, bool weak)
        {
            Result<FormalArmy> result = weak
                ? ArmyStackAdapter.EnsureBanditWeakPatrolArmy(
                    world, NodeA, string.Empty, string.Empty, -1f)
                : ArmyStackAdapter.EnsureBanditPatrolArmy(
                    world, NodeA, string.Empty, string.Empty, -1f);
            Assert.IsTrue(result.IsSuccess);
            ArmyHexTravelService.InitializeArmyAtHex(result.Value, hex);
            return result.Value;
        }

        static bool TryGetEnemyStack(SimulationWorld world, FormalArmy enemy, out ArmyStack stack)
        {
            stack = null;
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
            Assert.IsTrue(TryGetEnemyStack(world, enemy, out var stack));
            var party = ArmyStackAdapter.CollectLivingMemberIds(world, player);
            Assert.IsTrue(BattleOfferService.TryBuildOfferForArmyVsArmy(
                world, player.ArmyId, party, stack, "MultiEncounter"));
            world.Strategic.BattleOffer.AutoWinPercent = 100;
            world.Random = new XianXia.Core.Random.DeterministicRandom(1);
            Assert.IsTrue(BattleOfferService.ResolveAuto(world, false, out var won, out _).IsSuccess);
            Assert.IsTrue(won);
            Assert.IsTrue(StrategicEncounterResolveService.ResolveAndEnd(world).IsSuccess);
            Assert.IsTrue(BattleOfferService.FinishOfferResolution(world).IsSuccess);
        }

        static LingeringBattlefieldState RequireBattlefieldAt(SimulationWorld world, HexCoord hex)
        {
            Assert.IsTrue(world.Strategic.LingeringBattlefields.TryGetAtHex(hex, out var state));
            Assert.IsNotNull(state);
            return state;
        }

        static List<ulong> CollectSpawnIds(LingeringBattlefieldState state)
        {
            var list = new List<ulong>(state.SpawnedEntityIds.Count);
            for (var i = 0; i < state.SpawnedEntityIds.Count; i++)
                list.Add(state.SpawnedEntityIds[i]);
            return list;
        }

        static int CountEnemyParticipants(LingeringBattlefieldState state)
        {
            var count = 0;
            for (var i = 0; i < state.Participants.Records.Count; i++)
            {
                var rec = state.Participants.Records[i];
                if (rec.Kind == BattleParticipantKind.EnemyPrimary ||
                    rec.Kind == BattleParticipantKind.EnemyReinforcement)
                    count++;
            }

            return count;
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

        static List<EntityId> CollectStoredEnemyIds(LingeringBattlefieldState state)
        {
            var list = new List<EntityId>(4);
            state?.Participants.CollectEnemyEntityIds(list);
            return list;
        }

        static void SimulateLingeringReentrySpawn(SimulationWorld world, LingeringBattlefieldState battlefield)
        {
            LingeringBattlefieldRegistry.BeginLocalMapSession(world, battlefield);
            world.Strategic.Encounter.SpawnOnNextMapLoad = true;
            Assert.IsTrue(StrategicEncounterSpawner.ApplyPending(world).IsSuccess);
        }

        [Test]
        public void LINGERING_PARTICIPANT_01_WeakBanditReentryUsesStoredParticipant()
        {
            var world = CreateWorld();
            var player = SpawnPlayerArmy(world, H2);
            AutoWinThenFinish(world, player, SpawnEnemyAt(world, H2, weak: true));
            var e2 = RequireBattlefieldAt(world, H2);
            var stored = CollectStoredEnemyIds(e2);
            Assert.AreEqual(1, stored.Count);

            SimulateLingeringReentrySpawn(world, e2);
            var scoped = BattlefieldSpawnScope.GetSpawnList(world);
            Assert.AreEqual(1, scoped.Count);
            Assert.AreEqual(stored[0].Value, scoped[0]);
            Assert.IsTrue(world.Entities.TryGet(stored[0], out var entity));
            Assert.IsTrue(entity.TryGet<LifecycleComponent>(out var life));
            Assert.IsTrue(life.IsIncapacitated);
        }

        [Test]
        public void LINGERING_PARTICIPANT_02_FourBanditsReentrySpawnsAllDowned()
        {
            var world = CreateWorld();
            var player = SpawnPlayerArmy(world, H1);
            AutoWinThenFinish(world, player, SpawnEnemyAt(world, H1, weak: false));
            var e1 = RequireBattlefieldAt(world, H1);
            Assert.AreEqual(4, CollectStoredEnemyIds(e1).Count);

            SimulateLingeringReentrySpawn(world, e1);
            var scoped = BattlefieldSpawnScope.GetSpawnList(world);
            Assert.AreEqual(4, scoped.Count);
            for (var i = 0; i < scoped.Count; i++)
            {
                var id = new EntityId(scoped[i]);
                Assert.IsTrue(world.Entities.TryGet(id, out var entity));
                Assert.IsTrue(entity.TryGet<LifecycleComponent>(out var life));
                Assert.IsTrue(life.IsIncapacitated);
            }
        }

        static bool SpawnListContains(IReadOnlyList<ulong> spawnList, ulong entityId)
        {
            if (spawnList == null)
                return false;
            for (var i = 0; i < spawnList.Count; i++)
            {
                if (spawnList[i] == entityId)
                    return true;
            }

            return false;
        }

        [Test]
        public void LINGERING_PARTICIPANT_03_TwoBattlefieldsDoNotCrossOnReentry()
        {
            var world = CreateWorld();
            var player = SpawnPlayerArmy(world, H1);
            AutoWinThenFinish(world, player, SpawnEnemyAt(world, H1, weak: false));
            ArmyHexTravelService.InitializeArmyAtHex(player, H2);
            AutoWinThenFinish(world, player, SpawnEnemyAt(world, H2, weak: true));

            var e1 = RequireBattlefieldAt(world, H1);
            var e2 = RequireBattlefieldAt(world, H2);
            var weakIds = CollectStoredEnemyIds(e2);
            var fourIds = CollectStoredEnemyIds(e1);

            SimulateLingeringReentrySpawn(world, e2);
            var h2Spawn = BattlefieldSpawnScope.GetSpawnList(world);
            Assert.AreEqual(1, h2Spawn.Count);
            Assert.AreEqual(weakIds[0].Value, h2Spawn[0]);
            for (var i = 0; i < fourIds.Count; i++)
                Assert.IsFalse(SpawnListContains(h2Spawn, fourIds[i].Value));

            SimulateLingeringReentrySpawn(world, e1);
            var h1Spawn = BattlefieldSpawnScope.GetSpawnList(world);
            Assert.AreEqual(4, h1Spawn.Count);
            Assert.IsFalse(SpawnListContains(h1Spawn, weakIds[0].Value));
        }

        [Test]
        public void MULTI_ENCOUNTER_01_ParticipantCounts()
        {
            var world = CreateWorld();
            var player = SpawnPlayerArmy(world, H1);
            AutoWinThenFinish(world, player, SpawnEnemyAt(world, H1, weak: false));
            var e1 = RequireBattlefieldAt(world, H1);
            Assert.AreEqual(4, CountEnemyParticipants(e1));

            ArmyHexTravelService.InitializeArmyAtHex(player, H2);
            AutoWinThenFinish(world, player, SpawnEnemyAt(world, H2, weak: true));
            var e2 = RequireBattlefieldAt(world, H2);
            Assert.AreEqual(1, CountEnemyParticipants(e2));
        }

        [Test]
        public void MULTI_ENCOUNTER_02_SpawnScopeIndependent()
        {
            var world = CreateWorld();
            var player = SpawnPlayerArmy(world, H1);
            AutoWinThenFinish(world, player, SpawnEnemyAt(world, H1, weak: false));
            ArmyHexTravelService.InitializeArmyAtHex(player, H2);
            AutoWinThenFinish(world, player, SpawnEnemyAt(world, H2, weak: true));

            var e1 = RequireBattlefieldAt(world, H1);
            var e2 = RequireBattlefieldAt(world, H2);
            Assert.AreNotEqual(e1.BattlefieldId, e2.BattlefieldId);
            Assert.AreEqual(4, e1.SpawnedEntityIds.Count);
            Assert.AreEqual(1, e2.SpawnedEntityIds.Count);
            Assert.AreNotEqual(e1.SpawnedEntityIds, e2.SpawnedEntityIds);
        }

        [Test]
        public void MULTI_ENCOUNTER_03_Battle2CleanupDoesNotRemoveBattle1()
        {
            var world = CreateWorld();
            var player = SpawnPlayerArmy(world, H1);
            AutoWinThenFinish(world, player, SpawnEnemyAt(world, H1, weak: false));
            var e1Before = RequireBattlefieldAt(world, H1);
            var e1SpawnBefore = CollectSpawnIds(e1Before);

            ArmyHexTravelService.InitializeArmyAtHex(player, H2);
            AutoWinThenFinish(world, player, SpawnEnemyAt(world, H2, weak: true));

            var e1After = RequireBattlefieldAt(world, H1);
            Assert.AreEqual(e1SpawnBefore.Count, e1After.SpawnedEntityIds.Count);
            for (var i = 0; i < e1SpawnBefore.Count; i++)
            {
                Assert.IsTrue(world.Entities.TryGet(new EntityId(e1SpawnBefore[i]), out var ent) && ent != null);
                Assert.IsTrue(e1After.ContainsSpawn(e1SpawnBefore[i]));
            }
        }

        [Test]
        public void MULTI_ENCOUNTER_04_WorldMapResidualsAtH1AndH2()
        {
            var world = CreateWorld();
            var player = SpawnPlayerArmy(world, H1);
            AutoWinThenFinish(world, player, SpawnEnemyAt(world, H1, weak: false));
            ArmyHexTravelService.InitializeArmyAtHex(player, H2);
            AutoWinThenFinish(world, player, SpawnEnemyAt(world, H2, weak: true));

            Assert.Greater(CollectEnemyResidualsAt(world, H1).Count, 0);
            Assert.Greater(CollectEnemyResidualsAt(world, H2).Count, 0);
        }

        [Test]
        public void MULTI_ENCOUNTER_05_LingeringReentryH2_OnlyWeakBandit()
        {
            var world = CreateWorld();
            var player = SpawnPlayerArmy(world, H1);
            AutoWinThenFinish(world, player, SpawnEnemyAt(world, H1, weak: false));
            ArmyHexTravelService.InitializeArmyAtHex(player, H2);
            AutoWinThenFinish(world, player, SpawnEnemyAt(world, H2, weak: true));

            var e2 = RequireBattlefieldAt(world, H2);
            LingeringBattlefieldRegistry.BeginLocalMapSession(world, e2);
            Assert.AreEqual(e2.BattlefieldId, world.Strategic.Encounter.ActiveBattlefieldId);
            Assert.AreEqual(1, BattlefieldSpawnScope.GetSpawnList(world).Count);
        }

        [Test]
        public void MULTI_ENCOUNTER_06_LingeringReentryH1_StillFour()
        {
            var world = CreateWorld();
            var player = SpawnPlayerArmy(world, H1);
            AutoWinThenFinish(world, player, SpawnEnemyAt(world, H1, weak: false));
            ArmyHexTravelService.InitializeArmyAtHex(player, H2);
            AutoWinThenFinish(world, player, SpawnEnemyAt(world, H2, weak: true));

            var e1 = RequireBattlefieldAt(world, H1);
            LingeringBattlefieldRegistry.BeginLocalMapSession(world, e1);
            Assert.AreEqual(4, BattlefieldSpawnScope.GetSpawnList(world).Count);
        }

        [Test]
        public void MULTI_ENCOUNTER_07_LingeringReentryRespectsIncapacitatedDomain()
        {
            var world = CreateWorld();
            var player = SpawnPlayerArmy(world, H2);
            AutoWinThenFinish(world, player, SpawnEnemyAt(world, H2, weak: true));
            var e2 = RequireBattlefieldAt(world, H2);
            Assert.AreEqual(1, e2.SpawnedEntityIds.Count);
            var weakId = new EntityId(e2.SpawnedEntityIds[0]);
            Assert.IsTrue(world.Entities.TryGet(weakId, out var entity));
            Assert.IsTrue(entity.TryGet<LifecycleComponent>(out var life));
            Assert.IsTrue(life.IsIncapacitated);

            LingeringBattlefieldRegistry.BeginLocalMapSession(world, e2);
            Assert.AreEqual(1, BattlefieldSpawnScope.GetSpawnList(world).Count);
            Assert.IsTrue(BattlefieldSpawnScope.IsTrackedInCurrentLocalMapScope(world, weakId));
        }

        [Test]
        public void MULTI_ENCOUNTER_09_StrategicHostilityAtWar()
        {
            var world = CreateWorld();
            var player = SpawnPlayerArmy(world, H2);
            AutoWinThenFinish(world, player, SpawnEnemyAt(world, H2, weak: true));
            var e2 = RequireBattlefieldAt(world, H2);
            LingeringBattlefieldRegistry.BeginLocalMapSession(world, e2);
            Assert.AreEqual(1, e2.SpawnedEntityIds.Count);
            var weakId = new EntityId(e2.SpawnedEntityIds[0]);
            Assert.IsTrue(world.Entities.TryGet(weakId, out var entity));
            Assert.IsTrue(StrategicEncounterHostilityService.IsHostileStrategicNpc(world, entity));
        }

        [Test]
        public void MULTI_ENCOUNTER_11_ThreeSequentialBattles()
        {
            var world = CreateWorld();
            var player = SpawnPlayerArmy(world, H1);
            AutoWinThenFinish(world, player, SpawnEnemyAt(world, H1, weak: false));
            ArmyHexTravelService.InitializeArmyAtHex(player, H2);
            AutoWinThenFinish(world, player, SpawnEnemyAt(world, H2, weak: true));
            ArmyHexTravelService.InitializeArmyAtHex(player, H3);
            AutoWinThenFinish(world, player, SpawnEnemyAt(world, H3, weak: false));

            Assert.IsTrue(world.Strategic.LingeringBattlefields.HasAtHex(H1));
            Assert.IsTrue(world.Strategic.LingeringBattlefields.HasAtHex(H2));
            Assert.IsTrue(world.Strategic.LingeringBattlefields.HasAtHex(H3));
            Assert.AreEqual(3, world.Strategic.LingeringBattlefields.Count);
        }
    }
}
