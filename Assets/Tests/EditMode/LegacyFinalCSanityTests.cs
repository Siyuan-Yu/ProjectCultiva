using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using XianXia.Core.Persistence;
using XianXia.Core.Exploration;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Data.Bootstrap;
using XianXia.Data.Serialization;

namespace XianXia.Tests
{
    /// <summary>Narrow offline proof for LEGACY-FINAL-C authority and one-way migration.</summary>
    public sealed class LegacyFinalCSanityTests
    {
        public static string HeadlessContentRoot;

        static string BaseGamePath => !string.IsNullOrEmpty(HeadlessContentRoot)
            ? HeadlessContentRoot
            : Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));

        [Test]
        public void ModernNewGameAndSnapshotContainNoRetiredRuntimeAuthority()
        {
            var started = new PlayableDayBootstrap().Start(BaseGamePath);
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : string.Empty);
            var world = started.Value.World;
            var party = BindPlayerParty(world, started.Value.CharacterIds[0]);

            Assert.IsFalse(world.WorldPresence.All.Values.Any(p =>
                p != null && p.Mode == PartyWorldPresenceMode.AtHex));

            var dto = StrategicSnapshotHelper.Capture(world, party);
            AssertModernSnapshot(dto);
            var json = new JsonSnapshotSerializer().Serialize(new WorldSnapshot { Strategic = dto });
            Assert.IsTrue(json.IsSuccess, json.IsFailure ? json.Error.ToString() : string.Empty);
            StringAssert.DoesNotContain("\"territoryRegionControllers\"", json.Value);
            StringAssert.DoesNotContain("\"residualCharacterPresences\"", json.Value);
            StringAssert.DoesNotContain("\"retreatingArmies\"", json.Value);
            StringAssert.DoesNotContain("\"pendingEngagement\"", json.Value);
            StringAssert.DoesNotContain("\"ch01FormationScenarioCompat\"", json.Value);
        }

        [Test]
        public void CombinedLegacyInputsMigrateOneWayAndDoNotReemit()
        {
            var started = new PlayableDayBootstrap().Start(BaseGamePath);
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : string.Empty);
            var world = started.Value.World;
            var party = BindPlayerParty(world, started.Value.CharacterIds[0]);
            var regionSite = new WorldSite
            {
                SiteId = "legacy:site", DisplayName = "Legacy Site", SiteType = "Legacy",
                TerritoryRegionId = "legacy:region", OwnerFactionId = string.Empty
            };
            world.Strategic.Sites.Register(regionSite);
            var legacy = StrategicSnapshotHelper.Capture(world, party);

            var precise = legacy.CharacterWorldPresences.First(p =>
                p != null && p.HasWorldPosition && !string.IsNullOrEmpty(p.PersonalSurfaceId));
            var preciseHex = HexMath.WorldToHex(precise.WorldX, precise.WorldY,
                world.LegacyHexWorld.HexSize > 0f ? world.LegacyHexWorld.HexSize : 1f);
            precise.Mode = (int)PartyWorldPresenceMode.AtHex;
            precise.HexQ = preciseHex.Q;
            precise.HexR = preciseHex.R;

            var residualSource = legacy.CharacterWorldPresences.First(p =>
                p != null && p.CharacterId != precise.CharacterId &&
                p.HasWorldPosition && !string.IsNullOrEmpty(p.PersonalSurfaceId));
            var residualHex = HexMath.WorldToHex(residualSource.WorldX, residualSource.WorldY,
                world.LegacyHexWorld.HexSize > 0f ? world.LegacyHexWorld.HexSize : 1f);
            legacy.CharacterWorldPresences.Remove(residualSource);
            legacy.ResidualCharacterPresences.Add(new ResidualCharacterPresenceDto
            {
                CharacterId = residualSource.CharacterId,
                HexQ = residualHex.Q,
                HexR = residualHex.R
            });
            legacy.RetreatingArmies.Add(new RetreatingArmySnapshotDto
            {
                RetreatingArmyId = "legacy:retreat", SourceArmyId = "legacy:army",
                MemberCharacterIds = { residualSource.CharacterId }
            });

            legacy.WorldSiteOwners.RemoveAll(s => s != null && s.SiteId == regionSite.SiteId);
            legacy.TerritoryRegionControllers.Add(new TerritoryRegionControllerSnapshotDto
            {
                RegionId = regionSite.TerritoryRegionId,
                ControlFactionId = "legacy:faction"
            });

            var snapshot = new WorldSnapshot { Strategic = legacy };
            var restored = StrategicSnapshotHelper.Restore(world, legacy);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : string.Empty);
            Assert.IsTrue(world.WorldPresence.TryGet(new XianXia.Core.Domain.Ids.EntityId(precise.CharacterId), out var migrated));
            Assert.AreEqual(PartyWorldPresenceMode.AtWorldPosition, migrated.Mode);
            Assert.AreEqual(precise.PersonalSurfaceId, migrated.PersonalSurfaceId);
            Assert.AreEqual(precise.WorldX, migrated.WorldPosX, .0001f);
            Assert.AreEqual(precise.WorldY, migrated.WorldPosY, .0001f);
            Assert.IsTrue(world.WorldPresence.TryGet(new XianXia.Core.Domain.Ids.EntityId(residualSource.CharacterId), out var residual));
            Assert.AreEqual(PartyWorldPresenceMode.AtWorldPosition, residual.Mode);
            Assert.IsFalse(string.IsNullOrEmpty(residual.PersonalSurfaceId));
            Assert.AreEqual("legacy:faction", regionSite.OwnerFactionId);

            var squads = world.Strategic.Squads.Squads.Values.Where(s =>
                s != null && s.MemberCharacterIds.Count > 0).Take(2).ToArray();
            Assert.AreEqual(2, squads.Length, "Combined legacy battle sanity requires two real restored Squads.");
            legacy.PendingEngagement = new PendingEngagementSnapshotDto
            {
                EngagementId = "legacy:active",
                ParticipantBattleAnchorSurfaceId = precise.PersonalSurfaceId
            };
            for (var i = 0; i < squads.Length; i++)
            {
                var characterId = squads[i].LeaderCharacterId;
                Assert.IsTrue(world.WorldPresence.TryGet(characterId, out var presence));
                Assert.IsTrue(presence.HasContinuousWorldPosition);
                Assert.AreEqual(precise.PersonalSurfaceId, presence.PersonalSurfaceId);
                legacy.PendingEngagement.ParticipantRecords.Add(new PendingEngagementParticipantRecordDto
                {
                    Kind = (int)(i == 0 ? BattleParticipantKind.MandatoryFriendly : BattleParticipantKind.EnemyPrimary),
                    EntityId = characterId.Value,
                    SquadId = squads[i].SquadId,
                    Selected = true,
                    HasPreBattle = true,
                    PreBattleMode = (int)PartyWorldPresenceMode.AtWorldPosition,
                    PreBattleHasWorldPosition = true,
                    PreBattleWorldX = presence.WorldPosX,
                    PreBattleWorldY = presence.WorldPosY,
                    PreBattleSurfaceId = presence.PersonalSurfaceId
                });
            }

            var battle = LegacyPendingEngagementSnapshotMigration.Migrate(world, snapshot);
            Assert.IsTrue(battle.IsSuccess, battle.IsFailure ? battle.Error.ToString() : string.Empty);
            Assert.IsNotNull(snapshot.CharacterEncounter);
            Assert.IsNull(legacy.PendingEngagement);

            var modern = StrategicSnapshotHelper.Capture(world, party);
            AssertModernSnapshot(modern);
        }

        [Test]
        public void SeparateSpaceSnapshotAuthorityRemainsIndependentFromOutdoorRetirement()
        {
            var started = new PlayableDayBootstrap().Start(BaseGamePath);
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : string.Empty);
            var world = started.Value.World;
            var active = started.Value.CharacterIds[0];
            var party = BindPlayerParty(world, active);
            var outdoor = StrategicSnapshotHelper.Capture(world, party).CharacterWorldPresences.First(p =>
                p != null && p.CharacterId == active.Value && p.HasWorldPosition &&
                !string.IsNullOrEmpty(p.PersonalSurfaceId));
            var dto = new StrategicSnapshotDto
            {
                SeparateSpace = new SeparateSpaceSessionSnapshotDto
                {
                    IsInSeparateSpace = true,
                    SpaceKind = (int)SeparateSpaceKind.Cave,
                    ActiveMapLayoutId = "base:map_cave_acceptance",
                    ActiveLocalPlaceSetId = "base:places_cave_acceptance",
                    EntryLocationId = "base:loc_cave_entry",
                    ReturnLocationId = "base:loc_cave_entry",
                    HasOutdoorReturn = true,
                    ReturnSurfaceId = outdoor.PersonalSurfaceId,
                    ReturnWorldX = outdoor.WorldX,
                    ReturnWorldY = outdoor.WorldY,
                    ActiveCharacterId = active.Value,
                    EntryReason = "legacy-final-c-sanity"
                }
            };
            dto.SeparateSpace.OccupantIds.Add(active.Value);

            var restored = SeparateSpaceSessionSnapshotRestore.Restore(world, dto);
            Assert.IsTrue(restored.IsSuccess, restored.IsFailure ? restored.Error.ToString() : string.Empty);
            Assert.IsTrue(world.LocalMap.IsActive,
                $"Separate Space inactive: map={world.LocalMap.ActiveMapLayoutId}, kind={world.LocalMap.SpaceKind}, return={world.LocalMap.ReturnSurfaceId}");
            Assert.AreEqual(SeparateSpaceKind.Cave, world.LocalMap.SpaceKind);
            Assert.IsTrue(world.LocalMap.ContainsOccupant(active), "Restored Separate Space lost its explicit occupant.");
            Assert.AreEqual(PartyWorldPresenceMode.InSeparateSpace, world.PartyWorld.Mode);
            Assert.IsFalse(world.WorldPresence.TryGet(active, out _),
                "Separate Space-owned Character must not retain duplicate outdoor world presence.");

            var recaptured = StrategicSnapshotHelper.Capture(world, party);
            Assert.IsNotNull(recaptured.SeparateSpace);
            Assert.AreEqual(dto.SeparateSpace.ReturnSurfaceId, recaptured.SeparateSpace.ReturnSurfaceId);
            Assert.Contains(active.Value, recaptured.SeparateSpace.OccupantIds);
        }

        static PlayerPartyRuntime BindPlayerParty(
            XianXia.Core.Simulation.SimulationWorld world,
            XianXia.Core.Domain.Ids.EntityId active)
        {
            Assert.IsTrue(world.Strategic.Squads.TryGetForCharacter(active, out var squad));
            var party = new PlayerPartyRuntime();
            party.BindWorld(world);
            Assert.IsTrue(party.TryBindControlledSquad(squad.SquadId, active, out var error), error);
            return party;
        }

        static void AssertModernSnapshot(StrategicSnapshotDto dto)
        {
            Assert.IsEmpty(dto.TerritoryRegionControllers);
            Assert.IsEmpty(dto.ResidualCharacterPresences);
            Assert.IsEmpty(dto.RetreatingArmies);
            Assert.IsNull(dto.PendingEngagement);
            Assert.IsFalse(dto.Ch01FormationScenarioCompat);
            Assert.IsFalse(dto.CharacterWorldPresences.Any(p =>
                p != null && p.Mode == (int)PartyWorldPresenceMode.AtHex));
        }
    }
}
