using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Tests
{
    public sealed class BattleAuthorityTests
    {
        const string FactionPlayer = StrategicFactionCatalog.PlayerFactionId;
        const string FactionBandit = StrategicFactionCatalog.BanditId;
        const string FactionThirdParty = StrategicFactionCatalog.DongLinGuildId;
        const string TravelWorldId = "base:hex_world_travel_mvp_30x15";
        static readonly HexCoord HexA = Ch01HexPrototypeMapBuilder.HuangcunHex;
        static readonly HexCoord HexB = Ch01HexPrototypeMapBuilder.QingyunLuHex;
        static readonly HexCoord HexNeighbor = HexMath.Neighbor(HexA, 0);

        static string BaseGamePath =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        static SimulationWorld CreateWorld()
        {
            var world = new SimulationWorld();
            Ch01HexPrototypeMapBuilder.Build(world);
            world.Strategic.PlayerFactionId = FactionPlayer;
            WarGateService.DeclareWar(world, FactionPlayer, FactionBandit);
            return world;
        }

        static EntityId SpawnCharacter(SimulationWorld world, string name, string factionId, HexCoord hex)
        {
            var created = world.Entities.CreateCharacter(new DefinitionId("test", name), name);
            Assert.IsTrue(created.IsSuccess);
            var entity = created.Value;
            entity.Get<FactionMembershipComponent>().Assign(factionId, FactionRoleKind.Member);
            var hexSize = world.HexWorld.HexSize;
            HexMath.ToWorldPosition(hex, hexSize, out var x, out var y);
            world.WorldPresence.SetAtWorldPosition(entity.Id, new WorldVec2(x, y), hex);
            return entity.Id;
        }

        static FormalArmy CreateArmyAt(SimulationWorld world, string factionId, HexCoord hex, params EntityId[] members)
        {
            var siteId = Ch01HexPrototypeMapBuilder.SiteHuangcun;
            var created = ArmyService.CreateArmy(world, factionId, siteId, members);
            Assert.IsTrue(created.IsSuccess);
            var army = created.Value;
            ArmyHexTravelService.InitializeArmyAtHex(world, army, hex);
            ArmyStackAdapter.SyncAllLinkedStacksFromFormalArmies(world);
            return army;
        }

        static ArmyStack RequireStack(SimulationWorld world, FormalArmy army)
        {
            foreach (var kv in world.Strategic.Armies.Stacks)
            {
                if (kv.Value != null &&
                    string.Equals(kv.Value.FormalArmyId, army.ArmyId, System.StringComparison.Ordinal))
                    return kv.Value;
            }

            Assert.Fail("Missing stack for army " + army.ArmyId);
            return null;
        }

        static PlayerPartyRuntime CreateParty(SimulationWorld world, params EntityId[] members)
        {
            var party = new PlayerPartyRuntime();
            if (members.Length < 1)
                return party;
            Assert.IsTrue(party.TryInitialize(members[0], out _));
            for (var i = 1; i < members.Length; i++)
                Assert.IsTrue(party.TryAddMember(world, members, members[i], out var err), err);
            return party;
        }

        static void PlacePlayerParty(SimulationWorld world, HexCoord hex)
        {
            var hexSize = world.HexWorld.HexSize;
            HexMath.ToWorldPosition(hex, hexSize, out var x, out var y);
            world.PlayerPartyTravel.SetAtWorldPosition(new WorldVec2(x, y), hex);
        }

        static void PlacePlayerPartyAtHexEdgeToward(
            SimulationWorld world,
            HexCoord hex,
            HexCoord towardHex)
        {
            var hexSize = world.HexWorld.HexSize;
            HexMath.ToWorldPosition(hex, hexSize, out var cx, out var cy);
            HexMath.ToWorldPosition(towardHex, hexSize, out var tx, out var ty);
            var x = cx + (tx - cx) * 0.49f;
            var y = cy + (ty - cy) * 0.49f;
            world.PlayerPartyTravel.SetAtWorldPosition(new WorldVec2(x, y), hex);
        }

        static void BeginEngagement(
            SimulationWorld world,
            PlayerPartyRuntime party,
            FormalArmy attacker,
            FormalArmy defender)
        {
            Assert.IsTrue(BattleEngagementAuthorityService.TryBeginEngagement(
                world, party, attacker.ArmyId, defender.ArmyId, RequireStack(world, defender),
                new List<EntityId>(), "test-offer", out _));
        }

        [Test]
        public void T1_InitiatorAndDefender_AlwaysIncluded()
        {
            var world = CreateWorld();
            var h0 = HexA;
            var h1 = HexMath.Neighbor(h0, 1);
            var initiator = CreateArmyAt(world, FactionPlayer, h0, SpawnCharacter(world, "A", FactionPlayer, h0));
            var defender = CreateArmyAt(world, FactionBandit, h1, SpawnCharacter(world, "X", FactionBandit, h1));

            BeginEngagement(world, null, initiator, defender);

            var engagement = world.Strategic.PendingEngagement;
            Assert.AreEqual(h1, engagement.BattleLocation);
            Assert.IsTrue(engagement.HasSupportArea);
            Assert.IsTrue(engagement.SupportArea.Contains(h1));
            Assert.IsTrue(BattleEngagementHexDistance.TryResolveDefenderEngagementHex(
                world, defender.ArmyId, out var defenderPresenceHex));
            Assert.AreEqual(h1, defenderPresenceHex);
            Assert.IsTrue(engagement.ContainsFormalArmy(initiator.ArmyId));
            Assert.IsTrue(engagement.ContainsFormalArmy(defender.ArmyId));
        }

        [Test]
        public void T2_SameFactionArmyWithinSupportArea_Included()
        {
            var world = CreateWorld();
            var battleHex = HexA;
            var initiator = CreateArmyAt(world, FactionPlayer, battleHex, SpawnCharacter(world, "A", FactionPlayer, battleHex));
            var defender = CreateArmyAt(world, FactionBandit, battleHex, SpawnCharacter(world, "X", FactionBandit, battleHex));
            var reinforcement = CreateArmyAt(world, FactionPlayer, HexNeighbor,
                SpawnCharacter(world, "B", FactionPlayer, HexNeighbor));

            BeginEngagement(world, null, initiator, defender);

            var engagement = world.Strategic.PendingEngagement;
            Assert.IsTrue(engagement.HasSupportArea);
            Assert.IsTrue(engagement.SupportArea.Contains(HexNeighbor));
            Assert.IsTrue(engagement.ContainsFormalArmy(reinforcement.ArmyId));
        }

        [Test]
        public void T3_ArmyOutsideSupportArea_NotIncluded()
        {
            var world = CreateWorld();
            var battleHex = HexA;
            var initiator = CreateArmyAt(world, FactionPlayer, battleHex, SpawnCharacter(world, "A", FactionPlayer, battleHex));
            var defender = CreateArmyAt(world, FactionBandit, battleHex, SpawnCharacter(world, "X", FactionBandit, battleHex));
            var farArmy = CreateArmyAt(world, FactionPlayer, HexB,
                SpawnCharacter(world, "C", FactionPlayer, HexB));

            BeginEngagement(world, null, initiator, defender);

            var engagement = world.Strategic.PendingEngagement;
            Assert.IsFalse(engagement.ContainsFormalArmy(farArmy.ArmyId));
        }

        [Test]
        public void T4_SupportAreaUsesHexAdjacency_NotWorldPositionOffset()
        {
            var world = CreateWorld();
            var battleHex = HexA;
            var initiator = CreateArmyAt(world, FactionPlayer, battleHex, SpawnCharacter(world, "A", FactionPlayer, battleHex));
            var defender = CreateArmyAt(world, FactionBandit, battleHex, SpawnCharacter(world, "X", FactionBandit, battleHex));
            var hero = SpawnCharacter(world, "Hero", FactionPlayer, HexNeighbor);
            PlacePlayerPartyAtHexEdgeToward(world, HexNeighbor, HexA);
            var party = CreateParty(world, hero);

            Assert.IsTrue(CharacterWorldPresenceQuery.TryGetPartyWorldHex(world, party, out var partyPresenceHex));
            BeginEngagement(world, party, initiator, defender);

            var engagement = world.Strategic.PendingEngagement;
            Assert.IsTrue(engagement.SupportArea.Contains(partyPresenceHex));
            Assert.IsTrue(engagement.PlayerPartyIncluded);
            Assert.IsTrue(BattleDecisionPolicy.CanPlayerManuallyParticipate(engagement));
        }

        [Test]
        public void T5_PlayerPartyWithinSupportArea_ForcedIncluded_ManualEligible()
        {
            var world = CreateWorld();
            var battleHex = HexA;
            var initiator = CreateArmyAt(world, FactionPlayer, battleHex, SpawnCharacter(world, "A", FactionPlayer, battleHex));
            var defender = CreateArmyAt(world, FactionBandit, battleHex, SpawnCharacter(world, "X", FactionBandit, battleHex));
            var hero = SpawnCharacter(world, "Hero", FactionPlayer, HexNeighbor);
            PlacePlayerParty(world, HexNeighbor);
            var party = CreateParty(world, hero);

            BeginEngagement(world, party, initiator, defender);

            var engagement = world.Strategic.PendingEngagement;
            Assert.IsTrue(engagement.PlayerPartyIncluded);
            Assert.IsTrue(BattleDecisionPolicy.CanPlayerManuallyParticipate(engagement));
        }

        [Test]
        public void T6_PlayerPartyNearInitiatorButOutsideSupportArea_NotIncluded()
        {
            var world = CreateWorld();
            var h0 = HexA;
            var h1 = HexMath.Neighbor(h0, 1);
            var partyHex = HexMath.Neighbor(h0, 0);
            Assert.AreEqual(1, HexMath.Distance(partyHex, h0));
            Assert.Greater(HexMath.Distance(partyHex, h1), 1);

            var initiator = CreateArmyAt(world, FactionPlayer, h0, SpawnCharacter(world, "A", FactionPlayer, h0));
            var defender = CreateArmyAt(world, FactionBandit, h1, SpawnCharacter(world, "X", FactionBandit, h1));
            PlacePlayerParty(world, partyHex);
            var party = CreateParty(world, SpawnCharacter(world, "Hero", FactionPlayer, partyHex));

            BeginEngagement(world, party, initiator, defender);

            var engagement = world.Strategic.PendingEngagement;
            Assert.AreEqual(h1, engagement.BattleLocation);
            Assert.IsFalse(engagement.SupportArea.Contains(partyHex));
            Assert.IsFalse(engagement.PlayerPartyIncluded);
            Assert.IsFalse(BattleDecisionPolicy.CanPlayerManuallyParticipate(engagement));
        }

        [Test]
        public void T7_ThirdPartyFactionWithinSupportArea_NotIncluded()
        {
            var world = CreateWorld();
            var battleHex = HexA;
            var initiator = CreateArmyAt(world, FactionPlayer, battleHex, SpawnCharacter(world, "A", FactionPlayer, battleHex));
            var defender = CreateArmyAt(world, FactionBandit, battleHex, SpawnCharacter(world, "X", FactionBandit, battleHex));
            var neutral = CreateArmyAt(world, FactionThirdParty, HexNeighbor,
                SpawnCharacter(world, "N", FactionThirdParty, HexNeighbor));

            BeginEngagement(world, null, initiator, defender);

            var engagement = world.Strategic.PendingEngagement;
            Assert.IsFalse(engagement.ContainsFormalArmy(neutral.ArmyId));
        }

        [Test]
        public void T8_NoChainReinforcement_FromSupportAreaOnly()
        {
            var world = CreateWorld();
            var battleHex = HexA;
            var initiator = CreateArmyAt(world, FactionPlayer, battleHex, SpawnCharacter(world, "A", FactionPlayer, battleHex));
            var defender = CreateArmyAt(world, FactionBandit, battleHex, SpawnCharacter(world, "X", FactionBandit, battleHex));
            var armyB = CreateArmyAt(world, FactionPlayer, HexNeighbor,
                SpawnCharacter(world, "B", FactionPlayer, HexNeighbor));
            var farHex = HexMath.Neighbor(HexNeighbor, 1);
            var armyC = CreateArmyAt(world, FactionPlayer, farHex, SpawnCharacter(world, "C", FactionPlayer, farHex));
            Assert.LessOrEqual(HexMath.Distance(armyC.CurrentHex, armyB.CurrentHex), 1);

            BeginEngagement(world, null, initiator, defender);

            var engagement = world.Strategic.PendingEngagement;
            Assert.IsTrue(engagement.SupportArea.Contains(armyB.CurrentHex));
            Assert.IsFalse(engagement.SupportArea.Contains(armyC.CurrentHex));
            Assert.IsTrue(engagement.ContainsFormalArmy(armyB.ArmyId));
            Assert.IsFalse(engagement.ContainsFormalArmy(armyC.ArmyId));
        }

        [Test]
        public void T9_PendingEngagementSnapshot_RestoresWithoutRescan()
        {
            var world = CreateWorld();
            var battleHex = HexA;
            var initiator = CreateArmyAt(world, FactionPlayer, battleHex, SpawnCharacter(world, "A", FactionPlayer, battleHex));
            var defender = CreateArmyAt(world, FactionBandit, battleHex, SpawnCharacter(world, "X", FactionBandit, battleHex));
            PlacePlayerParty(world, HexNeighbor);
            var party = CreateParty(world, SpawnCharacter(world, "Hero", FactionPlayer, HexNeighbor));

            BeginEngagement(world, party, initiator, defender);

            var before = world.Strategic.PendingEngagement;
            var lockedPlayer = new List<string>(before.LockedPlayerFormalArmyIds);
            var lockedEnemy = new List<string>(before.LockedEnemyFormalArmyIds);
            var partyIncluded = before.PlayerPartyIncluded;
            var battleLocation = before.BattleLocation;

            world.Strategic.BattleOffer.Resolved = false;
            world.Strategic.BattleOffer.OfferId = "test-offer";
            world.Strategic.BattleOffer.ArmyStackId = RequireStack(world, defender).Id;

            var dto = StrategicSnapshotHelper.Capture(world, party);
            var world2 = CreateWorld();
            var lateReinforcement = CreateArmyAt(world2, FactionPlayer, HexNeighbor,
                SpawnCharacter(world2, "Late", FactionPlayer, HexNeighbor));
            StrategicSnapshotHelper.Restore(world2, dto);

            var restored = world2.Strategic.PendingEngagement;
            Assert.IsTrue(restored.IsActive);
            Assert.AreEqual(battleLocation, restored.BattleLocation);
            Assert.AreEqual(partyIncluded, restored.PlayerPartyIncluded);
            Assert.AreEqual(lockedPlayer.Count, restored.LockedPlayerFormalArmyIds.Count);
            Assert.AreEqual(lockedEnemy.Count, restored.LockedEnemyFormalArmyIds.Count);
            for (var i = 0; i < lockedPlayer.Count; i++)
                Assert.AreEqual(lockedPlayer[i], restored.LockedPlayerFormalArmyIds[i]);
            Assert.IsFalse(restored.ContainsFormalArmy(lateReinforcement.ArmyId));
            Assert.IsTrue(world2.Strategic.HasBattleOffer);
        }

        [Test]
        public void LevelTester_PlayerAttacksDefender_BattleLocationIsDefenderHex_ReinforcementsAndManualGate()
        {
            var world = CreateWorld();
            var defenderHex = HexA;
            var initiatorHex = HexMath.Neighbor(defenderHex, 0);
            var reinfHex1 = HexMath.Neighbor(defenderHex, 1);
            var reinfHex2 = HexMath.Neighbor(defenderHex, 2);
            Assert.AreEqual(1, HexMath.Distance(reinfHex1, defenderHex));
            Assert.AreEqual(1, HexMath.Distance(reinfHex2, defenderHex));
            Assert.AreNotEqual(reinfHex1, reinfHex2);

            HexCoord yellowHex = default;
            var foundYellow = false;
            for (var d = 0; d < 6 && !foundYellow; d++)
            {
                var mid = HexMath.Neighbor(defenderHex, d);
                for (var e = 0; e < 6; e++)
                {
                    var candidate = HexMath.Neighbor(mid, e);
                    if (HexMath.Distance(candidate, defenderHex) != 2)
                        continue;
                    yellowHex = candidate;
                    foundYellow = true;
                    break;
                }
            }

            Assert.IsTrue(foundYellow, "Test setup requires a hex at distance 2 from defender.");
            Assert.AreEqual(1, HexMath.Distance(yellowHex, initiatorHex));

            var initiator = CreateArmyAt(world, FactionPlayer, initiatorHex,
                SpawnCharacter(world, "BlueInitiator", FactionPlayer, initiatorHex));
            var defender = CreateArmyAt(world, FactionBandit, defenderHex,
                SpawnCharacter(world, "PurpleDefender", FactionBandit, defenderHex));
            var reinf1 = CreateArmyAt(world, FactionPlayer, reinfHex1,
                SpawnCharacter(world, "BlueReinf1", FactionPlayer, reinfHex1));
            var reinf2 = CreateArmyAt(world, FactionPlayer, reinfHex2,
                SpawnCharacter(world, "BlueReinf2", FactionPlayer, reinfHex2));

            PlacePlayerParty(world, yellowHex);
            var hero = SpawnCharacter(world, "YellowActive", FactionPlayer, yellowHex);
            var party = CreateParty(world, hero);
            Assert.IsTrue(CharacterWorldPresenceQuery.TryGetPartyWorldHex(world, party, out var partyHex));
            Assert.AreEqual(yellowHex, partyHex);

            BeginEngagement(world, party, initiator, defender);

            var engagement = world.Strategic.PendingEngagement;
            Assert.AreEqual(defenderHex, engagement.BattleLocation);
            Assert.IsTrue(engagement.HasSupportArea);
            Assert.AreEqual(1, engagement.SupportArea.BattleAreaHexes.Count);
            Assert.AreEqual(defenderHex, engagement.SupportArea.BattleAreaHexes[0]);
            Assert.IsTrue(engagement.SupportArea.Contains(reinfHex1));
            Assert.IsTrue(engagement.SupportArea.Contains(reinfHex2));
            Assert.IsFalse(engagement.SupportArea.Contains(yellowHex));
            Assert.IsTrue(BattleEngagementHexDistance.TryResolveDefenderEngagementHex(
                world, defender.ArmyId, out var resolvedDefenderHex));
            Assert.AreEqual(defenderHex, resolvedDefenderHex);
            Assert.AreNotEqual(initiatorHex, engagement.BattleLocation);

            Assert.IsTrue(engagement.ContainsFormalArmy(initiator.ArmyId));
            Assert.IsTrue(engagement.ContainsFormalArmy(defender.ArmyId));
            Assert.IsTrue(engagement.ContainsFormalArmy(reinf1.ArmyId));
            Assert.IsTrue(engagement.ContainsFormalArmy(reinf2.ArmyId));
            Assert.IsFalse(engagement.PlayerPartyIncluded);
            Assert.IsFalse(BattleDecisionPolicy.CanPlayerManuallyParticipate(engagement));
        }

        [Test]
        public void T10_MultiHexWorldSite_DefenderFootprintIsBattleArea_NotAnchorOnly()
        {
            var world = CreateWorld();
            var anchor = HexA;
            var footprintB = HexMath.Neighbor(anchor, 1);
            var site = new WorldSite
            {
                SiteId = "test:multi_hex_battle",
                AnchorHex = anchor,
                PresenceHex = anchor,
                OwnerFactionId = FactionBandit,
            };
            site.SetFootprint(new[] { anchor, footprintB });
            WorldSiteRegistrationService.RegisterSiteOnGrid(world, site);

            var initiatorHex = HexMath.Neighbor(anchor, 2);
            var initiator = CreateArmyAt(world, FactionPlayer, initiatorHex,
                SpawnCharacter(world, "Attacker", FactionPlayer, initiatorHex));
            var defender = CreateArmyAt(world, FactionBandit, anchor,
                SpawnCharacter(world, "Defender", FactionBandit, anchor));
            defender.WorldMotion.SetAtWorldSite(site.SiteId, site.AnchorHex, world.HexWorld.HexSize);

            HexCoord supportHex = default;
            for (var d = 0; d < 6; d++)
            {
                var candidate = HexMath.Neighbor(footprintB, d);
                if (candidate == anchor || candidate == footprintB)
                    continue;
                supportHex = candidate;
                break;
            }

            Assert.AreNotEqual(default, supportHex);
            var reinforcement = CreateArmyAt(world, FactionBandit, supportHex,
                SpawnCharacter(world, "SiteReinf", FactionBandit, supportHex));

            BeginEngagement(world, null, initiator, defender);

            var engagement = world.Strategic.PendingEngagement;
            Assert.IsTrue(engagement.HasSupportArea);
            Assert.AreEqual(2, engagement.SupportArea.BattleAreaHexes.Count);
            Assert.IsTrue(engagement.SupportArea.Contains(anchor));
            Assert.IsTrue(engagement.SupportArea.Contains(footprintB));
            Assert.IsTrue(engagement.SupportArea.Contains(supportHex));
            Assert.IsTrue(engagement.ContainsFormalArmy(reinforcement.ArmyId));
        }

        [Test]
        public void SeedMandatoryAttackers_OutsideSupportArea_NotAddedToParticipantSnapshot()
        {
            var world = CreateWorld();
            var defenderHex = HexA;
            var initiatorHex = HexMath.Neighbor(defenderHex, 0);
            var playerHex = HexB;
            Assert.Greater(HexMath.Distance(playerHex, defenderHex), 1);

            var initiator = CreateArmyAt(world, FactionPlayer, initiatorHex,
                SpawnCharacter(world, "A", FactionPlayer, initiatorHex));
            var defender = CreateArmyAt(world, FactionBandit, defenderHex,
                SpawnCharacter(world, "X", FactionBandit, defenderHex));
            PlacePlayerParty(world, playerHex);
            var hero = SpawnCharacter(world, "Hero", FactionPlayer, playerHex);
            var party = CreateParty(world, hero);

            BeginEngagement(world, party, initiator, defender);

            var engagement = world.Strategic.PendingEngagement;
            Assert.IsFalse(engagement.PlayerPartyIncluded);

            BattleEngagementAuthorityService.BuildSnapshotFromEngagement(
                world,
                engagement,
                RequireStack(world, defender),
                new List<EntityId> { hero },
                "test-offer");

            Assert.IsNull(world.Strategic.Participants.FindByEntity(hero));
        }

        [Test]
        public void EnemyInitiated_RetreatSubjectIsPlayerArmy()
        {
            var world = CreateWorld();
            var enemy = CreateArmyAt(world, FactionBandit, HexA, SpawnCharacter(world, "E1", FactionBandit, HexA));
            var playerArmy = CreateArmyAt(world, FactionPlayer, HexA, SpawnCharacter(world, "P1", FactionPlayer, HexA));

            BeginEngagement(world, null, enemy, playerArmy);

            var engagement = world.Strategic.PendingEngagement;
            Assert.AreEqual(enemy.ArmyId, engagement.InitiatorFormalArmyId);
            Assert.AreEqual(BattleDecisionSubjectKind.FormalArmy, engagement.DecisionSubjectKind);
            Assert.AreEqual(playerArmy.ArmyId, engagement.DecisionSubjectFormalArmyId);

            var beforeHex = playerArmy.CurrentHex;
            var retreat = BattleRetreatService.ExecuteRetreat(world, null);
            Assert.IsTrue(retreat.IsSuccess);
            Assert.AreEqual(beforeHex, playerArmy.CurrentHex);
        }

        [Test]
        public void RemoteFormalArmy_ManualEntryRejectedWhenPartyNotIncluded()
        {
            var world = CreateWorld();
            var initiator = CreateArmyAt(world, FactionPlayer, HexA, SpawnCharacter(world, "P1", FactionPlayer, HexA));
            var defender = CreateArmyAt(world, FactionBandit, HexA, SpawnCharacter(world, "E1", FactionBandit, HexA));
            PlacePlayerParty(world, HexB);

            BeginEngagement(world, CreateParty(world, SpawnCharacter(world, "Hero", FactionPlayer, HexB)),
                initiator, defender);

            var gate = BattleManualEntryPolicy.ValidateManualEntry(world);
            Assert.IsTrue(gate.IsFailure);
        }

        [Test]
        public void Trigger_CommittedHexTwoAway_DoesNotBeginEngagement()
        {
            var world = CreateWorld();
            var defenderHex = HexA;
            var initiatorHex = HexB;
            Assert.Greater(HexMath.Distance(initiatorHex, defenderHex), 1);

            var initiator = CreateArmyAt(world, FactionPlayer, initiatorHex,
                SpawnCharacter(world, "Attacker", FactionPlayer, initiatorHex));
            var defender = CreateArmyAt(world, FactionBandit, defenderHex,
                SpawnCharacter(world, "Defender", FactionBandit, defenderHex));

            Assert.IsFalse(BattleEngagementTriggerService.CanTriggerEngagement(
                world, initiator.ArmyId, defender.ArmyId, out var reason));
            Assert.AreEqual(
                BattleEngagementTriggerService.ReasonInitiatorNotAdjacentToBattleArea,
                reason);
            Assert.IsFalse(BattleEngagementAuthorityService.TryBeginEngagement(
                world, null, initiator.ArmyId, defender.ArmyId, RequireStack(world, defender),
                new System.Collections.Generic.List<EntityId>(), "blocked-offer", out _));
        }

        [Test]
        public void Trigger_CommittedHexAdjacent_BeginsEngagement()
        {
            var world = CreateWorld();
            var defenderHex = HexA;
            var initiatorHex = HexMath.Neighbor(defenderHex, 0);
            var initiator = CreateArmyAt(world, FactionPlayer, initiatorHex,
                SpawnCharacter(world, "Attacker", FactionPlayer, initiatorHex));
            var defender = CreateArmyAt(world, FactionBandit, defenderHex,
                SpawnCharacter(world, "Defender", FactionBandit, defenderHex));

            Assert.IsTrue(BattleEngagementTriggerService.CanTriggerEngagement(
                world, initiator.ArmyId, defender.ArmyId, out var reason));
            Assert.AreEqual(BattleEngagementTriggerService.ReasonAdjacentToBattleArea, reason);
            BeginEngagement(world, null, initiator, defender);
            Assert.IsTrue(world.Strategic.PendingEngagement.IsActive);
        }

        [Test]
        public void Trigger_DerivedHexAdjacentButCommittedTwoAway_DoesNotOpenBattleOffer()
        {
            var world = CreateWorld();
            var defenderHex = HexA;
            var midHex = HexNeighbor;
            var farHex = HexB;
            Assert.AreEqual(1, HexMath.Distance(midHex, defenderHex));
            Assert.Greater(HexMath.Distance(farHex, defenderHex), 1);

            var pursuer = CreateArmyAt(world, FactionPlayer, farHex,
                SpawnCharacter(world, "Pursuer", FactionPlayer, farHex));
            var defender = CreateArmyAt(world, FactionBandit, defenderHex,
                SpawnCharacter(world, "Defender", FactionBandit, defenderHex));

            Assert.IsTrue(ArmyHexTravelService.MoveArmyToHex(world, pursuer.ArmyId, midHex).IsSuccess);

            var hexSize = world.HexWorld.HexSize;
            var budget = PlayerPartyHexTravelService.WorldUnitsPerTick(hexSize) * 0.45f;
            FormalArmyContinuousTravelService.AdvanceDistanceBudget(world, pursuer, budget);

            Assert.IsTrue(pursuer.WorldMotion.IsMoving);
            Assert.IsTrue(pursuer.WorldMotion.TryGetActiveStepHexes(out var committedFrom, out _));
            Assert.AreEqual(farHex, committedFrom);
            Assert.AreEqual(1, HexMath.Distance(pursuer.WorldMotion.CurrentHex, defenderHex));
            Assert.IsFalse(BattleEngagementTriggerService.CanTriggerEngagement(
                world, pursuer.ArmyId, defender.ArmyId, out _));

            world.Strategic.Encounter.PursueAttackerArmyId = pursuer.ArmyId;
            world.Strategic.Encounter.PursueDefenderArmyId = defender.ArmyId;
            world.Strategic.Encounter.PursueStackId = RequireStack(world, defender).Id;
            ArmyHexPursuitService.AfterTravelTick(world);
            Assert.IsFalse(world.Strategic.HasBattleOffer);
        }

        [Test]
        public void Gathering_PlayerTwoHexFromDefenderNearReinforcement_NotIncluded()
        {
            var world = CreateWorld();
            var defenderHex = HexA;
            var reinfHex = HexNeighbor;
            HexCoord playerHex = default;
            for (var d = 0; d < 6; d++)
            {
                var mid = HexMath.Neighbor(defenderHex, d);
                for (var e = 0; e < 6; e++)
                {
                    var candidate = HexMath.Neighbor(mid, e);
                    if (HexMath.Distance(candidate, defenderHex) == 2 &&
                        HexMath.Distance(candidate, reinfHex) == 1)
                    {
                        playerHex = candidate;
                        break;
                    }
                }

                if (!playerHex.Equals(default))
                    break;
            }

            Assert.IsFalse(playerHex.Equals(default));

            var initiator = CreateArmyAt(world, FactionPlayer, HexMath.Neighbor(defenderHex, 1),
                SpawnCharacter(world, "Init", FactionPlayer, HexMath.Neighbor(defenderHex, 1)));
            var defender = CreateArmyAt(world, FactionBandit, defenderHex,
                SpawnCharacter(world, "Def", FactionBandit, defenderHex));
            CreateArmyAt(world, FactionPlayer, reinfHex,
                SpawnCharacter(world, "Reinf", FactionPlayer, reinfHex));

            PlacePlayerParty(world, playerHex);
            var party = CreateParty(world, SpawnCharacter(world, "Hero", FactionPlayer, playerHex));

            BeginEngagement(world, party, initiator, defender);

            var engagement = world.Strategic.PendingEngagement;
            Assert.IsFalse(engagement.SupportArea.Contains(playerHex));
            Assert.IsFalse(engagement.PlayerPartyIncluded);
        }

        [Test]
        public void Gathering_BelligerentReinforcementInSupportArea_Included_ThirdPartyExcluded()
        {
            var world = CreateWorld();
            var battleHex = HexA;
            var reinfHex = HexNeighbor;
            var thirdPartyHex = HexMath.Neighbor(reinfHex, 1);

            var initiator = CreateArmyAt(world, FactionPlayer, battleHex,
                SpawnCharacter(world, "A", FactionPlayer, battleHex));
            var defender = CreateArmyAt(world, FactionBandit, battleHex,
                SpawnCharacter(world, "X", FactionBandit, battleHex));
            var reinf = CreateArmyAt(world, FactionPlayer, reinfHex,
                SpawnCharacter(world, "B", FactionPlayer, reinfHex));
            var neutral = CreateArmyAt(world, FactionThirdParty, thirdPartyHex,
                SpawnCharacter(world, "N", FactionThirdParty, thirdPartyHex));

            BeginEngagement(world, null, initiator, defender);

            var engagement = world.Strategic.PendingEngagement;
            Assert.IsTrue(engagement.SupportArea.Contains(reinfHex));
            Assert.IsTrue(engagement.ContainsFormalArmy(reinf.ArmyId));
            Assert.IsFalse(engagement.ContainsFormalArmy(neutral.ArmyId));
        }

        [Test]
        public void Gathering_PlayerAdjacentToInitiatorButTwoFromDefender_NotIncluded_ManualIneligible()
        {
            var world = CreateWorld();
            var defenderHex = HexA;
            var initiatorHex = HexNeighbor;
            var friendlyHex = HexMath.Neighbor(defenderHex, 1);
            HexCoord playerHex = default;
            for (var d = 0; d < 6; d++)
            {
                var mid = HexMath.Neighbor(defenderHex, d);
                for (var e = 0; e < 6; e++)
                {
                    var candidate = HexMath.Neighbor(mid, e);
                    if (HexMath.Distance(candidate, defenderHex) == 2 &&
                        (HexMath.Distance(candidate, initiatorHex) == 1 ||
                         HexMath.Distance(candidate, friendlyHex) == 1))
                    {
                        playerHex = candidate;
                        break;
                    }
                }

                if (!playerHex.Equals(default))
                    break;
            }

            Assert.IsFalse(playerHex.Equals(default));
            Assert.AreEqual(2, HexMath.Distance(playerHex, defenderHex));

            var initiator = CreateArmyAt(world, FactionPlayer, initiatorHex,
                SpawnCharacter(world, "Init", FactionPlayer, initiatorHex));
            var defender = CreateArmyAt(world, FactionBandit, defenderHex,
                SpawnCharacter(world, "Def", FactionBandit, defenderHex));
            CreateArmyAt(world, FactionPlayer, friendlyHex,
                SpawnCharacter(world, "Friendly", FactionPlayer, friendlyHex));

            var hero = SpawnCharacter(world, "Hero", FactionPlayer, playerHex);
            PlacePlayerParty(world, playerHex);
            var party = CreateParty(world, hero);

            var staleSupportHex = initiatorHex;
            Assert.AreEqual(1, HexMath.Distance(staleSupportHex, defenderHex));
            Assert.AreNotEqual(playerHex, staleSupportHex);
            var hexSize = world.HexWorld.HexSize;
            HexMath.ToWorldPosition(staleSupportHex, hexSize, out var sx, out var sy);
            world.WorldPresence.SetAtWorldPosition(hero, new WorldVec2(sx, sy), staleSupportHex);

            BeginEngagement(world, party, initiator, defender);

            var engagement = world.Strategic.PendingEngagement;
            var battleArea = engagement.SupportArea.BattleAreaHexes;
            Assert.AreEqual(1, battleArea.Count);
            Assert.AreEqual(defenderHex, battleArea[0]);
            Assert.IsTrue(engagement.ContainsFormalArmy(initiator.ArmyId));
            Assert.IsTrue(engagement.ContainsFormalArmy(defender.ArmyId));
            Assert.IsFalse(engagement.PlayerPartyIncluded);
            Assert.IsFalse(BattleDecisionPolicy.ResolveDecisionOptions(engagement).Manual);

            BattleEngagementSpatialQuery.TryGetCommittedPartyHex(
                world, party, out var authorityHex, out var source);
            Assert.AreEqual(playerHex, authorityHex);
            Assert.AreEqual(PartyHexAuthoritySource.PartyTravel, source);
            Assert.IsFalse(engagement.SupportArea.Contains(authorityHex));
        }

        [Test]
        public void OfferPath_PlayerTwoHexFromDefender_NotInParticipants_ManualIneligible()
        {
            var world = CreateWorld();
            var defenderHex = HexA;
            var initiatorHex = HexNeighbor;
            var friendlyHex = HexMath.Neighbor(defenderHex, 1);
            HexCoord playerHex = default;
            for (var d = 0; d < 6; d++)
            {
                var mid = HexMath.Neighbor(defenderHex, d);
                for (var e = 0; e < 6; e++)
                {
                    var candidate = HexMath.Neighbor(mid, e);
                    if (HexMath.Distance(candidate, defenderHex) == 2 &&
                        HexMath.Distance(candidate, initiatorHex) == 1)
                    {
                        playerHex = candidate;
                        break;
                    }
                }

                if (!playerHex.Equals(default))
                    break;
            }

            Assert.IsFalse(playerHex.Equals(default));

            var initiator = CreateArmyAt(world, FactionPlayer, initiatorHex,
                SpawnCharacter(world, "InitA", FactionPlayer, initiatorHex),
                SpawnCharacter(world, "InitB", FactionPlayer, initiatorHex));
            var defender = CreateArmyAt(world, FactionBandit, defenderHex,
                SpawnCharacter(world, "Def", FactionBandit, defenderHex));
            CreateArmyAt(world, FactionPlayer, friendlyHex,
                SpawnCharacter(world, "Friendly", FactionPlayer, friendlyHex));

            PlacePlayerParty(world, playerHex);
            var hero = SpawnCharacter(world, "Hero", FactionPlayer, playerHex);
            var party = CreateParty(world, hero);
            world.Strategic.PlayerPartyContext = party;

            var staleSupportHex = initiatorHex;
            var hexSize = world.HexWorld.HexSize;
            HexMath.ToWorldPosition(staleSupportHex, hexSize, out var sx, out var sy);
            world.WorldPresence.SetAtWorldPosition(hero, new WorldVec2(sx, sy), staleSupportHex);

            var armyParty = new List<EntityId>();
            for (var i = 0; i < initiator.MemberCharacterIds.Count; i++)
                armyParty.Add(new EntityId(initiator.MemberCharacterIds[i]));

            Assert.IsTrue(BattleOfferService.TryBuildOfferForArmyVsArmy(
                world,
                initiator.ArmyId,
                armyParty,
                RequireStack(world, defender),
                "integration-test"));

            var engagement = world.Strategic.PendingEngagement;
            var snap = world.Strategic.Participants;
            Assert.AreEqual(1, engagement.SupportArea.BattleAreaHexes.Count);
            Assert.AreEqual(defenderHex, engagement.SupportArea.BattleAreaHexes[0]);
            Assert.IsFalse(engagement.SupportArea.Contains(playerHex));
            Assert.IsTrue(engagement.ContainsFormalArmy(initiator.ArmyId));
            Assert.IsTrue(engagement.ContainsFormalArmy(defender.ArmyId));
            Assert.IsFalse(engagement.PlayerPartyIncluded);
            Assert.IsFalse(BattleDecisionPolicy.ResolveDecisionOptions(engagement).Manual);
            Assert.IsNull(snap.FindByEntity(hero));
            Assert.AreEqual(BattleParticipantInclusionReason.None, engagement.PlayerInclusionReason);
            Assert.IsFalse(engagement.PlayerInclusionTrace.PlayerInSnapshotRecords);
        }

        [Test]
        public void SupportArea_AtWorldSiteOutsideFootprint_UsesCommittedDefenderHexOnly()
        {
            var world = CreateWorld();
            var anchor = HexA;
            var fieldHex = HexMath.Neighbor(anchor, 4);
            Assert.IsFalse(world.Strategic.Sites.TryGetAtHex(fieldHex, out _));
            Assert.IsTrue(world.Strategic.Sites.TryGet(Ch01HexPrototypeMapBuilder.SiteHuangcun, out var huangcun));
            Assert.IsFalse(huangcun.OccupiesHex(fieldHex));

            var defender = CreateArmyAt(world, FactionBandit, fieldHex,
                SpawnCharacter(world, "FieldDefender", FactionBandit, fieldHex));
            defender.WorldMotion.SetAtWorldSite(
                Ch01HexPrototypeMapBuilder.SiteHuangcun,
                fieldHex,
                world.HexWorld.HexSize);

            var area = BattleEngagementSupportArea.ResolveAndFreeze(world, defender.ArmyId);
            Assert.AreEqual(1, area.BattleAreaHexes.Count);
            Assert.AreEqual(fieldHex, area.BattleAreaHexes[0]);
            Assert.IsTrue(area.Contains(fieldHex));
            Assert.IsFalse(area.Contains(anchor));
        }

        [Test]
        public void FromFrozenLists_RebuildsSupportFromBattleArea_IgnoresStaleSupportList()
        {
            var defenderHex = HexA;
            var initiatorHex = HexNeighbor;
            HexCoord playerHex = default;
            for (var d = 0; d < 6; d++)
            {
                var mid = HexMath.Neighbor(defenderHex, d);
                for (var e = 0; e < 6; e++)
                {
                    var candidate = HexMath.Neighbor(mid, e);
                    if (HexMath.Distance(candidate, defenderHex) == 2)
                    {
                        playerHex = candidate;
                        break;
                    }
                }

                if (!playerHex.Equals(default))
                    break;
            }

            Assert.IsFalse(playerHex.Equals(default));
            var staleSupport = new List<HexCoord>
            {
                defenderHex,
                initiatorHex,
                playerHex,
            };

            var area = BattleEngagementSupportArea.FromFrozenLists(
                new[] { defenderHex },
                staleSupport,
                defenderHex);

            Assert.AreEqual(1, area.BattleAreaHexes.Count);
            Assert.IsTrue(area.Contains(defenderHex));
            Assert.IsTrue(area.Contains(initiatorHex));
            Assert.IsFalse(area.Contains(playerHex));
        }

        static SimulationWorld CreateTravelMvpWorld()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            Assert.IsTrue(
                loaded.Value.Registry.TryGetHexWorldContent(
                    DefinitionId.Parse(TravelWorldId).Value,
                    out var definition));

            var world = new SimulationWorld();
            Assert.IsTrue(HexWorldContentLoader.Apply(world, definition).IsSuccess);
            Ch01ScenarioStrategicSetup.Apply(world);
            Ch01ScenarioStrategicSetup.EnsureLevelTesterFixtures(world);
            Ch01ScenarioStrategicSetup.PositionPrototypeTestBanditArmies(world);
            return world;
        }

        [Test]
        public void TravelMvp_WeakBanditFieldBattle_SupportAreaExcludesDist2Player()
        {
            var world = CreateTravelMvpWorld();
            Assert.IsTrue(world.Strategic.FormalArmies.TryGet(
                ArmyStackAdapter.BanditWeakPatrolFormalArmyId,
                out var defender));
            Assert.IsTrue(world.Strategic.Sites.TryGet(
                Ch01HexPrototypeMapBuilder.SiteHuangcun,
                out var huangcun));
            Assert.IsFalse(huangcun.OccupiesHex(defender.CurrentHex));

            var defenderHex = defender.CurrentHex;
            var initiatorHex = HexMath.Neighbor(defenderHex, 0);
            HexCoord playerHex = default;
            for (var d = 0; d < 6; d++)
            {
                var mid = HexMath.Neighbor(defenderHex, d);
                for (var e = 0; e < 6; e++)
                {
                    var candidate = HexMath.Neighbor(mid, e);
                    if (HexMath.Distance(candidate, defenderHex) != 2)
                        continue;
                    if (HexMath.Distance(candidate, initiatorHex) != 1)
                        continue;
                    playerHex = candidate;
                    break;
                }

                if (!playerHex.Equals(default))
                    break;
            }

            Assert.IsFalse(playerHex.Equals(default));

            var initiator = CreateArmyAt(world, FactionPlayer, initiatorHex,
                SpawnCharacter(world, "InitA", FactionPlayer, initiatorHex),
                SpawnCharacter(world, "InitB", FactionPlayer, initiatorHex));
            PlacePlayerParty(world, playerHex);
            var hero = SpawnCharacter(world, "Hero", FactionPlayer, playerHex);
            var party = CreateParty(world, hero);
            world.Strategic.PlayerPartyContext = party;

            BeginEngagement(world, party, initiator, defender);

            var engagement = world.Strategic.PendingEngagement;
            Assert.AreEqual(1, engagement.SupportArea.BattleAreaHexes.Count);
            Assert.AreEqual(defenderHex, engagement.SupportArea.BattleAreaHexes[0]);
            Assert.AreNotEqual(initiatorHex, engagement.SupportArea.BattleAreaHexes[0]);
            Assert.IsFalse(engagement.SupportArea.Contains(playerHex));
            Assert.IsFalse(engagement.PlayerPartyIncluded);
        }
    }
}
