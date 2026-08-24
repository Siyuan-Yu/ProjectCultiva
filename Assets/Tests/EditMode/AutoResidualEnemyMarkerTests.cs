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
    /// <summary>AUTO-RES：敌军 Auto 战后 Residual Marker 数据链回归。</summary>
    public sealed class AutoResidualEnemyMarkerTests
    {
        const string PlayerFaction = StrategicFactionCatalog.PlayerFactionId;
        const string NodeA = "base:node_huangcun";

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            world.Strategic.PlayerFactionId = PlayerFaction;
            HexTestWorldBootstrap.EnsureMinimalHexMap(world);
            WarGateService.DeclareWar(world, PlayerFaction, StrategicFactionCatalog.BanditId);
            return world;
        }

        static EntityId SpawnPlayer(SimulationWorld world)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", "hero"), "Hero");
            Assert.IsTrue(created.IsSuccess);
            created.Value.Get<FactionMembershipComponent>().Assign(PlayerFaction, FactionRoleKind.Member);
            world.WorldPresence.SetAtNode(created.Value.Id, NodeA);
            return created.Value.Id;
        }

        static (FormalArmy army, ArmyStack stack, List<EntityId> members) SeedBandits(
            SimulationWorld world,
            HexCoord hex)
        {
            var armyResult = ArmyStackAdapter.EnsureBanditPatrolArmy(
                world, NodeA, string.Empty, string.Empty, -1f);
            Assert.IsTrue(armyResult.IsSuccess);
            var army = armyResult.Value;
            ArmyHexTravelService.InitializeArmyAtHex(army, hex);
            Assert.IsTrue(world.Strategic.Armies.TryGet(ArmyStackAdapter.BanditPatrolStackId, out var stack));
            ArmyStackAdapter.SyncStackTravelFromFormalArmy(world, stack);

            var members = new List<EntityId>(army.MemberCharacterIds.Count);
            for (var i = 0; i < army.MemberCharacterIds.Count; i++)
                members.Add(new EntityId(army.MemberCharacterIds[i]));
            Assert.AreEqual(4, members.Count);
            return (army, stack, members);
        }

        static BattleParticipantSnapshot BuildSnap(FormalArmy enemyArmy, HexCoord hex)
        {
            var snap = new BattleParticipantSnapshot
            {
                PrimaryEnemyStackId = ArmyStackAdapter.BanditPatrolStackId,
                BattleAnchorHexQ = hex.Q,
                BattleAnchorHexR = hex.R,
                BattleAnchorNodeId = NodeA
            };
            ArmyHexBattleAnchorService.SetBattleAnchorHex(snap, hex);
            return snap;
        }

        [Test]
        public void AUTO_RES_01_FourEnemyDowned_ProducesEnemyDownedMarker()
        {
            var world = CreateWorld();
            var player = SpawnPlayer(world);
            var hex = Ch01HexPrototypeMapBuilder.HuangcunHex;
            var (army, stack, members) = SeedBandits(world, hex);

            var report = AutoBattleCasualtyService.ApplyPlayerVictory(
                world,
                new[] { player },
                stack,
                playerPower: 20,
                enemyPower: 10,
                executeOnWin: false);

            Assert.AreEqual(4, report.EnemyMembersSpared);
            Assert.IsTrue(report.Summary.Contains("全部弥留"));

            for (var i = 0; i < members.Count; i++)
                Assert.IsTrue(LingeringBattlefieldPartyService.IsIncapacitated(world, members[i]));

            var snap = BuildSnap(army, hex);
            world.Strategic.Encounter.ArmyStackId = ArmyStackAdapter.BanditPatrolStackId;
            world.Strategic.Encounter.BattlefieldLingering = true;
            StrategicEncounterSpawner.EnsureMacroRemnantSpawns(world, snap);
            ArmyPostBattleSyncService.SyncEnemyArmyAfterBattle(world, snap);

            for (var i = 0; i < members.Count; i++)
            {
                Assert.IsFalse(ArmyService.TryGetArmyForCharacter(world, members[i], out _));
                Assert.IsTrue(StrategicResidualPresenceService.TryGetResidualHex(world, members[i], out var rh));
                Assert.AreEqual(hex, rh);
            }

            var groups = StrategicResidualPresentationQuery.Query(world);
            ResidualMarkerGroupView enemyDowned = null;
            for (var i = 0; i < groups.Count; i++)
            {
                if (groups[i].State == ResidualStateBucket.Downed &&
                    (groups[i].Relation == StrategicRelationBucket.Enemy ||
                     groups[i].Relation == StrategicRelationBucket.Other))
                    enemyDowned = groups[i];
            }

            Assert.IsNotNull(enemyDowned, "Expected ENEMY/OTHER DOWNED group");
            Assert.AreEqual(4, enemyDowned.Count);
            Assert.AreEqual(hex, enemyDowned.Hex);
            Assert.AreEqual(StrategicRelationBucket.Enemy, enemyDowned.Relation);
        }

        [Test]
        public void AUTO_RES_01b_WithoutEnemyDetach_QueryExcludesFormalArmyMembers()
        {
            var world = CreateWorld();
            var player = SpawnPlayer(world);
            var hex = Ch01HexPrototypeMapBuilder.HuangcunHex;
            var (army, stack, members) = SeedBandits(world, hex);

            AutoBattleCasualtyService.ApplyPlayerVictory(
                world, new[] { player }, stack, 20, 10, executeOnWin: false);
            var snap = BuildSnap(army, hex);
            world.Strategic.Encounter.ArmyStackId = ArmyStackAdapter.BanditPatrolStackId;
            StrategicEncounterSpawner.EnsureMacroRemnantSpawns(world, snap);

            // 故意不 SyncEnemyArmyAfterBattle：模拟修复前断点
            for (var i = 0; i < members.Count; i++)
                Assert.IsTrue(ArmyService.TryGetArmyForCharacter(world, members[i], out _));

            var groups = StrategicResidualPresentationQuery.Query(world);
            var enemyCount = 0;
            for (var i = 0; i < groups.Count; i++)
            {
                if (groups[i].Relation == StrategicRelationBucket.Enemy ||
                    groups[i].Relation == StrategicRelationBucket.Other)
                    enemyCount += groups[i].Count;
            }

            Assert.AreEqual(0, enemyCount, "Pre-fix breakpoint: FormalArmy membership blocks Query");
        }

        [Test]
        public void AUTO_RES_02_MixedDownedAndDead_TwoMarkers()
        {
            var world = CreateWorld();
            var player = SpawnPlayer(world);
            var hex = Ch01HexPrototypeMapBuilder.HuangcunHex;
            var (army, stack, members) = SeedBandits(world, hex);

            for (var i = 0; i < 2; i++)
            {
                Assert.IsTrue(world.Entities.TryGet(members[i], out var ent));
                CombatDamageRules.EnsureVitals(ent);
                Assert.IsTrue(CombatLifeStateService.TryEnterIncapacitated(world, ent));
            }

            for (var i = 2; i < 4; i++)
            {
                Assert.IsTrue(world.Entities.TryGet(members[i], out var ent));
                CombatDamageRules.EnsureVitals(ent);
                Assert.IsTrue(CombatLifeStateService.TryEnterIncapacitated(world, ent));
                Assert.IsTrue(CombatLifeStateService.TryConfirmDeath(world, EntityId.None, ent, out _));
            }

            ArmyStackAdapter.SyncDownedCountsFromMembers(world, stack);
            stack.IsBattlefieldRemnant = true;
            var snap = BuildSnap(army, hex);
            world.Strategic.Encounter.ArmyStackId = ArmyStackAdapter.BanditPatrolStackId;
            StrategicEncounterSpawner.EnsureMacroRemnantSpawns(world, snap);
            ArmyPostBattleSyncService.SyncEnemyArmyAfterBattle(world, snap);

            var groups = StrategicResidualPresentationQuery.Query(world);
            var downed = 0;
            var dead = 0;
            for (var i = 0; i < groups.Count; i++)
            {
                if (groups[i].Relation != StrategicRelationBucket.Enemy &&
                    groups[i].Relation != StrategicRelationBucket.Other)
                    continue;
                if (groups[i].State == ResidualStateBucket.Downed)
                    downed += groups[i].Count;
                if (groups[i].State == ResidualStateBucket.Dead)
                    dead += groups[i].Count;
            }

            Assert.AreEqual(2, downed);
            Assert.AreEqual(2, dead);
        }

        [Test]
        public void AUTO_RES_03_FriendlyDowned_StillSelfMarker()
        {
            var world = CreateWorld();
            var player = SpawnPlayer(world);
            var hex = Ch01HexPrototypeMapBuilder.HuangcunHex;
            Assert.IsTrue(world.Entities.TryGet(player, out var ent));
            CombatDamageRules.EnsureVitals(ent);
            Assert.IsTrue(CombatLifeStateService.TryEnterIncapacitated(world, ent));
            StrategicResidualPresenceService.PlaceCharacterAtResidualHex(world, player, hex);

            var groups = StrategicResidualPresentationQuery.Query(world);
            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(StrategicRelationBucket.Self, groups[0].Relation);
            Assert.AreEqual(ResidualStateBucket.Downed, groups[0].State);
        }

        [Test]
        public void AUTO_RES_04_AfterFinishOffer_ResidualStillPresent()
        {
            var world = CreateWorld();
            var player = SpawnPlayer(world);
            var hex = Ch01HexPrototypeMapBuilder.HuangcunHex;
            var (army, stack, members) = SeedBandits(world, hex);

            AutoBattleCasualtyService.ApplyPlayerVictory(
                world, new[] { player }, stack, 20, 10, executeOnWin: false);
            var snap = BuildSnap(army, hex);
            world.Strategic.Participants.PrimaryEnemyStackId = ArmyStackAdapter.BanditPatrolStackId;
            world.Strategic.Participants.BattleAnchorHexQ = hex.Q;
            world.Strategic.Participants.BattleAnchorHexR = hex.R;
            world.Strategic.Participants.BattleAnchorNodeId = NodeA;
            ArmyHexBattleAnchorService.SetBattleAnchorHex(world.Strategic.Participants, hex);
            world.Strategic.Encounter.ArmyStackId = ArmyStackAdapter.BanditPatrolStackId;
            StrategicEncounterSpawner.EnsureMacroRemnantSpawns(world, snap);
            ArmyPostBattleSyncService.SyncEnemyArmyAfterBattle(world, snap);

            Assert.AreEqual(4, CountEnemyDownedCandidates(world));
            BattleOfferService.FinishOfferResolution(world);
            Assert.AreEqual(4, CountEnemyDownedCandidates(world));

            var groups = StrategicResidualPresentationQuery.Query(world);
            var n = 0;
            for (var i = 0; i < groups.Count; i++)
            {
                if (groups[i].State == ResidualStateBucket.Downed &&
                    groups[i].Relation == StrategicRelationBucket.Enemy)
                    n += groups[i].Count;
            }

            Assert.AreEqual(4, n);
        }

        [Test]
        public void AUTO_RES_05_EnemyDownedAtSiteHex_KeepsResidualAfterDetach()
        {
            var world = CreateWorld();
            var player = SpawnPlayer(world);
            var hex = Ch01HexPrototypeMapBuilder.HuangcunHex;
            Assert.IsTrue(world.Strategic.Sites.TryGetAtHex(hex, out _),
                "Regression guard: battle at Site hex used to promote downed to AtSite and hide markers.");

            var (army, stack, members) = SeedBandits(world, hex);
            AutoBattleCasualtyService.ApplyPlayerVictory(
                world, new[] { player }, stack, 20, 10, executeOnWin: false);

            var snap = BuildSnap(army, hex);
            world.Strategic.Encounter.ArmyStackId = ArmyStackAdapter.BanditPatrolStackId;
            StrategicEncounterSpawner.EnsureMacroRemnantSpawns(world, snap);
            ArmyPostBattleSyncService.SyncEnemyArmyAfterBattle(world, snap);

            for (var i = 0; i < members.Count; i++)
            {
                Assert.IsTrue(
                    StrategicResidualPresenceService.TryGetResidualHex(world, members[i], out var rh),
                    "Enemy downed must keep AtHex after DetachNonLivingMembersAtBattlefield.");
                Assert.AreEqual(hex, rh);
                Assert.IsTrue(world.WorldPresence.TryGet(members[i], out var wp) && wp != null);
                Assert.AreNotEqual(PartyWorldPresenceMode.AtSite, wp.Mode);
            }

            Assert.Greater(CountEnemyDownedCandidates(world), 0);
        }

        static int CountEnemyDownedCandidates(SimulationWorld world)
        {
            var n = 0;
            foreach (var ent in world.Entities.All)
            {
                if (ent == null)
                    continue;
                if (!StrategicResidualPresenceService.IsStrategicResidualCandidate(world, ent.Id))
                    continue;
                if (!LingeringBattlefieldPartyService.IsIncapacitated(world, ent.Id))
                    continue;
                var rel = StrategicRelationQuery.GetRelationToPlayer(
                    world, ArmyService.ResolveCharacterFactionId(world, ent.Id));
                if (rel == StrategicRelationBucket.Enemy || rel == StrategicRelationBucket.Other)
                    n++;
            }

            return n;
        }
    }
}
