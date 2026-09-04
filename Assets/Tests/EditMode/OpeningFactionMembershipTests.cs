using System;
using System.IO;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;
using XianXia.Core.Social;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;
using XianXia.Data.Bootstrap;

namespace XianXia.Tests
{
    /// <summary>
    /// Character default faction + Spawn FactionMode（三模式）数据模型与 resolver 收口测试。
    /// 运行时成员归属真源 = FactionMembershipComponent；本组验证 New Game 时的 default/override/unaffiliated 解析。
    /// </summary>
    public sealed class OpeningFactionMembershipTests
    {
        static OpeningSpawnEntry Entry(
            OpeningFactionMode mode,
            string factionId,
            string factionRole,
            bool legacyAssign = false) =>
            new OpeningSpawnEntry
            {
                DefinitionId = "test:char",
                FactionMode = mode,
                FactionModeExplicit = mode != OpeningFactionMode.CharacterDefault,
                FactionId = factionId ?? string.Empty,
                FactionRole = factionRole ?? string.Empty,
                AssignOpeningFaction = legacyAssign
            };

        static CharacterDefinition Char(string fid, string role) => new CharacterDefinition
        {
            Id = new DefinitionId("test", "char"),
            DefaultFactionId = fid ?? string.Empty,
            DefaultFactionRole = role ?? string.Empty
        };

        static ResolvedFactionAssignment Resolve(OpeningSpawnEntry e, CharacterDefinition c, string scenarioFid = null) =>
            OpeningFactionAssignmentResolver.Resolve(e, c, scenarioFid);

        [Test]
        public void A_CharacterDefault_Faction_Inherited()
        {
            var r = Resolve(Entry(OpeningFactionMode.CharacterDefault, null, null), Char("base:faction_player", "Member"));
            Assert.IsTrue(r.IsAffiliated);
            Assert.AreEqual("base:faction_player", r.FactionId);
            Assert.AreEqual(FactionRoleKind.Member, r.Role);
            Assert.AreEqual(FactionAssignmentSource.CharacterDefault, r.Source);
        }

        [Test]
        public void B_CharacterNoDefault_StaysUnaffiliated()
        {
            var r = Resolve(Entry(OpeningFactionMode.CharacterDefault, null, null), Char(null, null));
            Assert.IsFalse(r.IsAffiliated);
            Assert.AreEqual(FactionAssignmentSource.CharacterDefault, r.Source);
        }

        [Test]
        public void C_ScenarioOverride_Wins_Over_CharacterDefault()
        {
            var r = Resolve(
                Entry(OpeningFactionMode.Override, "base:faction_player", "Member"),
                Char("base:faction_shuofeng", "Supervisor"));
            Assert.IsTrue(r.IsAffiliated);
            Assert.AreEqual("base:faction_player", r.FactionId);
            Assert.AreEqual(FactionRoleKind.Member, r.Role);
            Assert.AreEqual(FactionAssignmentSource.ScenarioOverride, r.Source);
        }

        [Test]
        public void D_ScenarioUnaffiliated_Clears_Default()
        {
            var r = Resolve(Entry(OpeningFactionMode.Unaffiliated, null, null), Char("base:faction_shuofeng", "Supervisor"));
            Assert.IsFalse(r.IsAffiliated);
            Assert.AreEqual(FactionAssignmentSource.ExplicitUnaffiliated, r.Source);
        }

        [Test]
        public void L_LegacyExplicitOverride_TreatedAsOverride_WithLegacySource()
        {
            var e = Entry(OpeningFactionMode.CharacterDefault, "base:sect_huangcun_labor", "Member");
            e.FactionModeExplicit = false;
            var r = Resolve(e, null);
            Assert.IsTrue(r.IsAffiliated);
            Assert.AreEqual("base:sect_huangcun_labor", r.FactionId);
            Assert.AreEqual(FactionAssignmentSource.Legacy, r.Source);
        }

        [Test]
        public void M_OldestAssignOpeningFaction_StillCompatible()
        {
            var e = Entry(OpeningFactionMode.CharacterDefault, null, "LaborDisciple", legacyAssign: true);
            e.FactionModeExplicit = false;
            var r = Resolve(e, null, "base:sect_huangcun_labor");
            Assert.IsTrue(r.IsAffiliated);
            Assert.AreEqual("base:sect_huangcun_labor", r.FactionId);
            Assert.AreEqual(FactionRoleKind.LaborDisciple, r.Role);
            Assert.AreEqual(FactionAssignmentSource.Legacy, r.Source);
        }

        [Test]
        public void F_CharacterInvalidDefaultFaction_ValidationFails()
        {
            var registry = BuildRegistryWithFaction("base:faction_player");
            var character = Char("base:faction_does_not_exist", "Member");
            character.Id = new DefinitionId("base", "character_bad");
            AssertOk(registry.RegisterCharacter(character));
            var report = new ContentReferenceValidator().Validate(registry);
            Assert.IsFalse(report.IsValid);
        }

        [Test]
        public void G_CharacterFactionWithoutRole_ValidationFails()
        {
            var registry = BuildRegistryWithFaction("base:faction_player");
            var character = Char("base:faction_player", "");
            character.Id = new DefinitionId("base", "character_bad2");
            AssertOk(registry.RegisterCharacter(character));
            var report = new ContentReferenceValidator().Validate(registry);
            Assert.IsFalse(report.IsValid);
        }

        [Test]
        public void H_CharacterRoleWithoutFaction_ValidationFails()
        {
            var registry = BuildRegistryWithFaction("base:faction_player");
            var character = Char("", "Member");
            character.Id = new DefinitionId("base", "character_bad3");
            AssertOk(registry.RegisterCharacter(character));
            var report = new ContentReferenceValidator().Validate(registry);
            Assert.IsFalse(report.IsValid);
        }

        [Test]
        public void I_SpawnOverrideUnknownFaction_ValidationFails()
        {
            var registry = BuildRegistryWithFaction("base:faction_player");
            var character = Char("base:faction_player", "Member");
            character.Id = new DefinitionId("base", "character_ok");
            AssertOk(registry.RegisterCharacter(character));
            var scenario = new OpeningScenarioDefinition
            {
                Id = new DefinitionId("base", "scenario_test"),
                Spawns =
                {
                    Entry(OpeningFactionMode.Override, "base:faction_unknown", "Member")
                }
            };
            AssertOk(registry.RegisterOpeningScenario(scenario));
            var report = new ContentReferenceValidator().Validate(registry);
            Assert.IsFalse(report.IsValid);
        }

        [Test]
        public void J_SpawnExplicitCharacterDefault_WithFaction_ValidationFails()
        {
            var registry = BuildRegistryWithFaction("base:faction_player");
            var character = Char("base:faction_player", "Member");
            character.Id = new DefinitionId("base", "character_ok");
            AssertOk(registry.RegisterCharacter(character));
            var scenario = new OpeningScenarioDefinition
            {
                Id = new DefinitionId("base", "scenario_j"),
                Spawns =
                {
                    Entry(OpeningFactionMode.CharacterDefault, "base:faction_player", "Member")
                }
            };
            scenario.Spawns[0].FactionModeExplicit = true; // 显式 CharacterDefault + factionId = 新格式非法
            AssertOk(registry.RegisterOpeningScenario(scenario));
            var report = new ContentReferenceValidator().Validate(registry);
            Assert.IsFalse(report.IsValid);
        }

        [Test]
        public void K_SpawnUnaffiliated_WithFaction_ValidationFails()
        {
            var registry = BuildRegistryWithFaction("base:faction_player");
            var scenario = new OpeningScenarioDefinition
            {
                Id = new DefinitionId("base", "scenario_k"),
                Spawns =
                {
                    Entry(OpeningFactionMode.Unaffiliated, "base:faction_player", "Member")
                }
            };
            AssertOk(registry.RegisterOpeningScenario(scenario));
            var report = new ContentReferenceValidator().Validate(registry);
            Assert.IsFalse(report.IsValid);
        }

        [Test]
        public void Z_BaseGame_LevelTesterRoster_InheritsGuardCharacterDefaults()
        {
            var baseGame = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));
            var loaded = new ContentPackageLoader().Load(new[] { baseGame });
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : "");
            var scenarioId = DefinitionId.Parse("base:scenario_ch01_reference").Value;
            var rosterId = DefinitionId.Parse("base:roster_level_tester").Value;
            Assert.IsTrue(loaded.Value.Registry.TryGetOpeningScenario(scenarioId, out var scenario));
            Assert.IsTrue(loaded.Value.Registry.TryGetCharacterRoster(rosterId, out var roster));
            var started = new ContentGameStart().StartFromScenario(
                loaded.Value, scenarioId, characterRosterId: "base:roster_level_tester");
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : "");
            var applied = OpeningScenarioApplier.Apply(
                started.Value.World, loaded.Value.Registry, scenario,
                new GameStartLookup(started.Value.SpawnedByDefinitionId), 1, roster.Entries);
            Assert.IsTrue(applied.IsSuccess, applied.IsFailure ? applied.Error.ToString() : "");

            AssertMembership(started.Value.World, started.Value.SpawnedByDefinitionId,
                "base:character_ch01_ref_guard_a", "base:sect_huangcun_labor", FactionRoleKind.Member);
            AssertMembership(started.Value.World, started.Value.SpawnedByDefinitionId,
                "base:character_ch01_ref_guard_b", "base:sect_huangcun_labor", FactionRoleKind.Member);
            AssertMembership(started.Value.World, started.Value.SpawnedByDefinitionId,
                "base:character_ch01_ref_guard_c", "base:faction_fisher_village", FactionRoleKind.Supervisor);
        }

        static void AssertMembership(
            XianXia.Core.Simulation.SimulationWorld world,
            System.Collections.Generic.IReadOnlyDictionary<string, EntityId> ids,
            string definitionId,
            string factionId,
            FactionRoleKind role)
        {
            Assert.IsTrue(ids.TryGetValue(definitionId, out var entityId), definitionId + " 未出生。");
            Assert.IsTrue(world.Entities.TryGet(entityId, out var entity), definitionId + " 实体缺失。");
            Assert.IsTrue(entity.TryGet<FactionMembershipComponent>(out var membership), definitionId + " 没有势力组件。");
            Assert.AreEqual(factionId, membership.FactionId, definitionId);
            Assert.AreEqual(role, membership.Role, definitionId);
        }

        static DefinitionRegistry BuildRegistryWithFaction(string factionId)
        {
            var registry = new DefinitionRegistry();
            AssertOk(registry.RegisterStrategicFaction(new StrategicFactionDefinition
            {
                Id = DefinitionId.Parse(factionId).Value,
                Name = "Test",
                MapColor = "#000000"
            }));
            return registry;
        }

        static void AssertOk(Result result) => Assert.IsTrue(result.IsSuccess, result.IsFailure ? result.Error.ToString() : "");
    }
}
