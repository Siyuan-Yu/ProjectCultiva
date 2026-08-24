using System.Collections.Generic;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Unity.Host;

namespace XianXia.Tests
{
    /// <summary>SUPPORT-01..04 + WeakBandit 首战 Participant / LocalMap 一致性�?/summary>
    public sealed class EncounterAssemblyTests
    {
        const string NodeA = "base:site_huangcun";

        static SimulationWorld CreateBanditWorld(
            out FormalArmy weakArmy,
            out FormalArmy strongArmy,
            out HexCoord weakHex,
            out HexCoord strongHex)
        {
            var world = new SimulationWorld();
            world.Strategic.PlayerFactionId = StrategicFactionCatalog.PlayerFactionId;
            HexTestWorldBootstrap.EnsureMinimalHexMap(world);
            Assert.IsTrue(ArmyStackAdapter.EnsureBanditPatrolArmy(world, NodeA).IsSuccess);
            Assert.IsTrue(ArmyStackAdapter.EnsureBanditWeakPatrolArmy(world, NodeA).IsSuccess);
            Ch01ScenarioStrategicSetup.PositionPrototypeTestBanditArmies(world);
            WarGateService.DeclareWar(
                world,
                StrategicFactionCatalog.PlayerFactionId,
                StrategicFactionCatalog.BanditId);

            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(
                ArmyStackAdapter.BanditWeakPatrolFormalArmyId, out weakArmy));
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(
                ArmyStackAdapter.BanditPatrolFormalArmyId, out strongArmy));
            Ch01HexPrototypeMapBuilder.ResolvePrototypeTestBanditHexesBelowHuangcun(
                world, out strongHex, out weakHex);
            return world;
        }

        static FormalArmy SpawnPlayerAt(SimulationWorld world, HexCoord hex)
        {
            var created = world.Entities.CreateCharacter(
                new DefinitionId("test", "player_leader"),
                "PlayerLeader");
            Assert.IsTrue(created.IsSuccess);
            created.Value.Get<FactionMembershipComponent>()
                .Assign(StrategicFactionCatalog.PlayerFactionId, FactionRoleKind.Member);
            world.WorldPresence.SetAtSite(created.Value.Id, NodeA);
            var armyCreated = ArmyService.CreateArmy(
                world,
                StrategicFactionCatalog.PlayerFactionId,
                NodeA,
                new[] { created.Value.Id });
            Assert.IsTrue(armyCreated.IsSuccess);
            ArmyHexTravelService.InitializeArmyAtHex(armyCreated.Value, hex);
            return armyCreated.Value;
        }

        static bool TryGetWeakStack(SimulationWorld world, out ArmyStack stack) =>
            world.Strategic.Armies.TryGet(ArmyStackAdapter.BanditWeakPatrolStackId, out stack) &&
            stack != null;

        static void BuildWeakBanditOffer(SimulationWorld world, HexCoord battleHex)
        {
            var player = SpawnPlayerAt(world, battleHex);
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(
                ArmyStackAdapter.BanditWeakPatrolFormalArmyId,
                out var weakFormal));
            ArmyHexTravelService.InitializeArmyAtHex(weakFormal, battleHex);
            Assert.IsTrue(TryGetWeakStack(world, out var weakStack));
            var party = ArmyStackAdapter.CollectLivingMemberIds(world, player);
            Assert.IsTrue(BattleOfferService.TryBuildOfferForArmyVsArmy(
                world, player.ArmyId, party, weakStack, "EncounterAssemblyTest"));
        }

        static EntityId FindNpcByName(SimulationWorld world, string name)
        {
            foreach (var entity in world.Entities.All)
            {
                if (string.Equals(entity.DisplayName, name, System.StringComparison.Ordinal))
                    return entity.Id;
            }

            Assert.Fail("Missing NPC " + name);
            return EntityId.None;
        }

        static int CountEnemyReinforcementRecords(SimulationWorld world)
        {
            var snap = world.Strategic.Participants;
            var count = 0;
            for (var i = 0; i < snap.Records.Count; i++)
            {
                if (snap.Records[i].Kind == BattleParticipantKind.EnemyReinforcement)
                    count++;
            }

            return count;
        }

        static void EnterEncounterLocalMap(SimulationWorld world, IReadOnlyList<EntityId> engaged)
        {
            world.PartyWorld.SiteId = NodeA;
            world.PartyWorld.LocalMapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;
            world.LocalMap.ActiveMapLayoutId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;
            StrategicEncounterSpawner.PlanManualEncounter(
                world,
                world.Strategic.BattleOffer.ArmyStackId,
                "test",
                engaged);
            Assert.IsTrue(StrategicEncounterSpawner.ApplyPending(world).IsSuccess);
        }

        static void ApplyEncounterPresentationOverrides(
            SimulationWorld world,
            IReadOnlyList<EntityId> engaged)
        {
            if (engaged == null || engaged.Count == 0)
                return;
            var startId = world.WorldRegion.StartLocationId;
            WorldLocationState startLoc = null;
            var hasStart = !string.IsNullOrEmpty(startId) &&
                           world.WorldRegion.TryGet(startId, out startLoc);
            for (var i = 0; i < engaged.Count; i++)
            {
                var id = engaged[i];
                if (id.IsNone || !world.Entities.TryGet(id, out var ent))
                    continue;
                if (!ent.TryGet<XianXia.Core.Exploration.EntityLocationComponent>(out var loc))
                {
                    loc = new XianXia.Core.Exploration.EntityLocationComponent();
                    ent.AddComponent(loc);
                }

                if (hasStart)
                {
                    loc.LocationId = startId;
                    loc.SetPresentationOverride(startLoc.PresentationX, startLoc.PresentationZ);
                }
                else
                {
                    loc.SetPresentationOverride(0f, 0f);
                }
            }
        }

        [Test]
        public void SUPPORT_01_WeakBandit_NoReinforcementWhenDistanceGreaterThanOne()
        {
            var world = CreateBanditWorld(out _, out var strongArmy, out var weakHex, out var strongHex);
            ArmyHexTravelService.InitializeArmyAtHex(strongArmy, strongHex);
            BuildWeakBanditOffer(world, weakHex);

            var enemyIds = new List<EntityId>(8);
            world.Strategic.Participants.CollectEnemyEntityIds(enemyIds);
            Assert.AreEqual(1, enemyIds.Count);
            Assert.AreEqual(0, CountEnemyReinforcementRecords(world));
            Assert.Greater(HexMath.Distance(weakHex, strongHex), 1);
        }

        static HexCoord StepNeighbor(SimulationWorld world, HexCoord from, int directionIndex)
        {
            var next = HexMath.Neighbor(from, directionIndex);
            if (world?.HexWorld != null && world.HexWorld.Contains(next))
                return next;
            return from;
        }

        [Test]
        public void SUPPORT_04_NoReinforcementAtHexDistanceTwo()
        {
            var world = CreateBanditWorld(out _, out var strongArmy, out var weakHex, out _);
            var distant = StepNeighbor(world, StepNeighbor(world, weakHex, 0), 0);
            ArmyHexTravelService.InitializeArmyAtHex(strongArmy, distant);
            Assert.AreEqual(2, HexMath.Distance(weakHex, distant));
            BuildWeakBanditOffer(world, weakHex);

            var enemyIds = new List<EntityId>(8);
            world.Strategic.Participants.CollectEnemyEntityIds(enemyIds);
            Assert.AreEqual(1, enemyIds.Count);
            Assert.AreEqual(0, CountEnemyReinforcementRecords(world));
        }

        [Test]
        public void SUPPORT_03_AdjacentSameFactionReinforcementJoinsSnapshot()
        {
            var world = CreateBanditWorld(out _, out var strongArmy, out var weakHex, out _);
            var adjacent = StepNeighbor(world, weakHex, 1);
            ArmyHexTravelService.InitializeArmyAtHex(strongArmy, adjacent);
            Assert.AreEqual(1, HexMath.Distance(weakHex, adjacent));
            BuildWeakBanditOffer(world, weakHex);

            var enemyIds = new List<EntityId>(8);
            world.Strategic.Participants.CollectEnemyEntityIds(enemyIds);
            Assert.AreEqual(5, enemyIds.Count);
            Assert.AreEqual(4, CountEnemyReinforcementRecords(world));
        }

        [Test]
        public void ENCOUNTER_ASSEMBLY_01_WeakBanditManualMapShowsOnlyParticipant()
        {
            var world = CreateBanditWorld(out _, out var strongArmy, out var weakHex, out var strongHex);
            ArmyHexTravelService.InitializeArmyAtHex(strongArmy, strongHex);
            BuildWeakBanditOffer(world, weakHex);

            var engaged = world.Strategic.Participants.CollectSelectedFriendly();
            EnterEncounterLocalMap(world, engaged);

            var weakId = FindNpcByName(world, "WeakBandit");
            var leaderId = FindNpcByName(world, "BanditLeader");
            var aId = FindNpcByName(world, "BanditA");
            var bId = FindNpcByName(world, "BanditB");
            var cId = FindNpcByName(world, "BanditC");

            Assert.IsTrue(LocalMapVisibility.IsEntityVisible(world, weakId));
            Assert.IsFalse(LocalMapVisibility.IsEntityVisible(world, leaderId));
            Assert.IsFalse(LocalMapVisibility.IsEntityVisible(world, aId));
            Assert.IsFalse(LocalMapVisibility.IsEntityVisible(world, bId));
            Assert.IsFalse(LocalMapVisibility.IsEntityVisible(world, cId));

            var scoped = BattlefieldSpawnScope.GetSpawnList(world);
            Assert.AreEqual(1, scoped.Count);
            Assert.AreEqual(weakId.Value, scoped[0]);
        }

        [Test]
        public void ENCOUNTER_ASSEMBLY_03_EngagedFriendlyVisible_WithEmptyPartyNodeIdAndSiteFocus()
        {
            var world = CreateBanditWorld(out _, out var strongArmy, out var weakHex, out var strongHex);
            ArmyHexTravelService.InitializeArmyAtHex(strongArmy, strongHex);
            BuildWeakBanditOffer(world, weakHex);

            var engaged = world.Strategic.Participants.CollectSelectedFriendly();
            Assert.Greater(engaged.Count, 0);

            world.PartyWorld.SiteId = string.Empty;
            world.PartyWorld.SiteId = Ch01HexPrototypeMapBuilder.SiteHuangcun;
            world.PartyWorld.LocalMapId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;
            world.LocalMap.ActiveMapLayoutId = StrategicEncounterCatalog.DefaultEncounterLocalMapId;
            StrategicEncounterSpawner.PlanManualEncounter(
                world,
                world.Strategic.BattleOffer.ArmyStackId,
                "test",
                engaged);
            Assert.IsTrue(StrategicEncounterSpawner.ApplyPending(world).IsSuccess);
            ApplyEncounterPresentationOverrides(world, engaged);

            var leaderId = engaged[0];
            Assert.IsTrue(LocalMapVisibility.IsEntityVisible(world, leaderId));
            world.WorldPresence.TryGet(leaderId, out var wp);
            Assert.AreEqual(PartyWorldPresenceMode.InEncounter, wp.Mode);
        }

        [Test]
        public void ENCOUNTER_ASSEMBLY_02_AutoAndManualShareSnapshotEnemyCount()
        {
            var world = CreateBanditWorld(out _, out var strongArmy, out var weakHex, out var strongHex);
            ArmyHexTravelService.InitializeArmyAtHex(strongArmy, strongHex);
            BuildWeakBanditOffer(world, weakHex);

            var snapIds = new List<EntityId>(8);
            world.Strategic.Participants.CollectEnemyEntityIds(snapIds);
            var autoStacks = world.Strategic.Participants.CollectEnemyStackIds();
            Assert.AreEqual(1, snapIds.Count);
            Assert.AreEqual(1, autoStacks.Count);

            EnterEncounterLocalMap(world, world.Strategic.Participants.CollectSelectedFriendly());
            Assert.AreEqual(1, BattlefieldSpawnScope.GetSpawnList(world).Count);
        }
    }
}
