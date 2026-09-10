using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using XianXia.Core.Bootstrap;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;
using XianXia.Unity.Host;

namespace XianXia.Tests.EditMode
{
    /// <summary>
    /// NewGame Continuous Outdoor Opening Population Bootstrap（§19 A–F）。
    /// 只断言 domain／bootstrap 层事实；EntityView materialize 由 Host 层 FINAL BARRIER
    /// + startup invariant 负责（见 ContinuousOutdoorSurfaceRuntime）。
    /// </summary>
    public sealed class ContinuousOutdoorOpeningPopulationTests
    {
        const string ScenarioId = "base:scenario_ch01_reference";
        const string OpeningSiteId = "base:site_huangcun";
        const string ProtagonistDefinitionId = "base:character_protagonist";
        const string CompanionADefinitionId = "base:character_companion_a";
        const string CompanionBDefinitionId = "base:character_companion_b";
        const string CaveShadeDefinitionId = "base:character_cave_shade";
        const string CaveChamberLocationId = "base:loc_cave_chamber";
        const string LaborYardLocationId = "base:loc_ref_labor_yard";
        const string HuangcunSourceMapId = "base:map_ch01_reference";
        const string ProbeMapLayoutId = "test:map_opening_population_probe";
        const string ProbeSpawnTableId = "test:spawn_opening_population_probe";

        static string BaseGamePath()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "..", "Content", "BaseGame"));
#else
            var fromEnvironment = Environment.GetEnvironmentVariable("XIANXIA_BASEGAME");
            return string.IsNullOrEmpty(fromEnvironment)
                ? Path.GetFullPath(Path.Combine("Content", "BaseGame"))
                : fromEnvironment;
#endif
        }

        static PlayableDayBootstrapResult Boot()
        {
            var loaded = new ContentPackageLoader().Load(new[] { BaseGamePath() });
            Assert.IsTrue(loaded.IsSuccess,
                loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            var started = new PlayableDayBootstrap().Start(loaded.Value, new PlayableDayOptions
            {
                DailyRequiredAmount = 10,
                OpeningScenarioId = ScenarioId
            });
            Assert.IsTrue(started.IsSuccess,
                started.IsFailure ? started.Error.ToString() : string.Empty);
            return started.Value;
        }

        static EntityId FindByDefinition(SimulationWorld world, string definitionId)
        {
            foreach (var entity in world.Entities.All)
            {
                if (string.Equals(entity.DefinitionId.ToString(), definitionId, StringComparison.Ordinal))
                    return entity.Id;
            }

            return EntityId.None;
        }

        static List<EntityId> OpeningSitePopulation(SimulationWorld world)
        {
            Assert.IsTrue(world.Strategic.Sites.TryGet(OpeningSiteId, out var site));
            var list = new List<EntityId>();
            StrategicWorldSitePopulationService.CollectCharacterIdsPresentAtWorldSite(
                world, site, null, list);
            return list;
        }

        // ---------------------------------------------------------------- A
        [Test]
        public void A_OpeningCompanionsAreBackgroundResidentsOfOpeningSite()
        {
            var boot = Boot();
            var world = boot.World;
            var protagonist = FindByDefinition(world, ProtagonistDefinitionId);
            var companionA = FindByDefinition(world, CompanionADefinitionId);
            var companionB = FindByDefinition(world, CompanionBDefinitionId);

            Assert.IsFalse(protagonist.IsNone, "protagonist entity missing");
            Assert.IsFalse(companionA.IsNone, "companion_a entity missing");
            Assert.IsFalse(companionB.IsNone, "companion_b entity missing");

            Assert.IsTrue(world.WorldPresence.TryGet(companionA, out var presenceA));
            Assert.AreEqual(PartyWorldPresenceMode.AtSite, presenceA.Mode);
            Assert.AreEqual(OpeningSiteId, presenceA.SiteId);
            Assert.IsTrue(world.WorldPresence.TryGet(companionB, out var presenceB));
            Assert.AreEqual(PartyWorldPresenceMode.AtSite, presenceB.Mode);
            Assert.AreEqual(OpeningSiteId, presenceB.SiteId);

            // 三名 opening character 都必须属于 opening site 的期望 population（= materialize 输入集）。
            var population = OpeningSitePopulation(world);
            Assert.Contains(protagonist, population);
            Assert.Contains(companionA, population);
            Assert.Contains(companionB, population);

            // 同伴绝不进入 PlayerPartyTravel 的 traveling members（只主控随队）。
            Assert.IsTrue(world.PlayerPartyTravel.TravelingMembers.Contains(protagonist));
            Assert.IsFalse(world.PlayerPartyTravel.TravelingMembers.Contains(companionA));
            Assert.IsFalse(world.PlayerPartyTravel.TravelingMembers.Contains(companionB));
        }

        // ---------------------------------------------------------------- B
        [Test]
        public void B_SpawnZoneOnContinuousOutdoorSourceMapGetsPresenceImmediately()
        {
            var boot = Boot();
            var world = boot.World;
            var registry = boot.Registry;

            // 真实 content 的荒村 source map 必须解析为唯一 Continuous Outdoor Site（否则 SpawnZone
            // 永远无法建立 presence）。
            Assert.IsTrue(ContinuousOutdoorSpawnPresenceResolver
                    .TryResolveContinuousOutdoorSiteForSourceMap(
                        world, HuangcunSourceMapId, out var resolvedSite, out var ambiguity),
                "resolver failed: " + ambiguity);
            Assert.AreEqual(OpeningSiteId, resolvedSite.SiteId);

            // 合成一个挂在荒村 source map 上的 spawnZone，验证 mutation 路径本身。
            // 荒村真实 LocalMapId 临时重定向到 probe map（仅内存、finally 恢复）：resolver 按
            // "source map → site.localMapId" 解析，不重定向就无法用合成 spawnZone 走到真实 mutation。
            Assert.IsTrue(world.Strategic.Sites.TryGet(OpeningSiteId, out var huangcun));
            var originalLocalMapId = huangcun.LocalMapId;
            try
            {
                huangcun.LocalMapId = ProbeMapLayoutId;

                RegisterProbeSpawnTable(registry, CompanionADefinitionId);
                var layout = RegisterProbeMapLayout(registry);

                var before = new HashSet<ulong>();
                foreach (var entity in world.Entities.All)
                    before.Add(entity.Id.Value);

                var applied = SpawnZoneApplier.ApplyMap(world, registry, layout, world.Random);
                Assert.IsTrue(applied.IsSuccess,
                    applied.IsFailure ? applied.Error.ToString() : string.Empty);

                EntityId spawned = EntityId.None;
                foreach (var entity in world.Entities.All)
                    if (!before.Contains(entity.Id.Value))
                    {
                        spawned = entity.Id;
                        break;
                    }

                Assert.IsFalse(spawned.IsNone, "spawnZone produced no entity");
                Assert.IsTrue(world.WorldPresence.TryGet(spawned, out var presence),
                    "spawnZone NPC has no WorldPresence (would never materialize in Continuous Outdoor)");
                Assert.AreEqual(PartyWorldPresenceMode.AtSite, presence.Mode);
                Assert.AreEqual(OpeningSiteId, presence.SiteId);
                Assert.IsTrue(presence.HasContinuousWorldPosition,
                    "spawnZone NPC must carry canonical continuous anchor, not only legacy LocalMap coords");
            }
            finally
            {
                huangcun.LocalMapId = originalLocalMapId;
            }
        }

        // ---------------------------------------------------------------- C
        [Test]
        public void C_NormalizeFillsMissingPresenceFromAuthoredLocation()
        {
            var boot = Boot();
            var world = boot.World;
            var registry = boot.Registry;

            var created = world.Entities.CreateNpc(
                DefinitionId.Parse(CompanionBDefinitionId).Value, "normalize_probe");
            Assert.IsTrue(created.IsSuccess,
                created.IsFailure ? created.Error.ToString() : string.Empty);
            var probe = created.Value;
            var location = new EntityLocationComponent();
            location.LocationId = LaborYardLocationId;
            Assert.IsTrue(probe.AddComponent(location).IsSuccess);
            Assert.IsFalse(world.WorldPresence.TryGet(probe.Id, out _),
                "probe must start without presence");

            var report = ContinuousOutdoorOpeningPopulationBootstrap.Normalize(
                world, registry, OpeningSiteId);

            Assert.IsTrue(world.WorldPresence.TryGet(probe.Id, out var presence),
                "normalize pass did not fill missing presence");
            Assert.AreEqual(PartyWorldPresenceMode.AtSite, presence.Mode);
            Assert.AreEqual(OpeningSiteId, presence.SiteId);
            Assert.GreaterOrEqual(report.NormalizedAtSite, 1);
        }

        // ---------------------------------------------------------------- D
        [Test]
        public void D_IndependentSpaceNpcIsNeverPulledIntoOutdoor()
        {
            var boot = Boot();
            var world = boot.World;
            var registry = boot.Registry;

            var caveShade = FindByDefinition(world, CaveShadeDefinitionId);
            Assert.IsFalse(caveShade.IsNone, "cave NPC entity missing");
            Assert.IsFalse(world.WorldPresence.TryGet(caveShade, out _),
                "cave NPC must not receive outdoor macro presence");

            // normalize 之后仍然不得被塞进 Outdoor。
            ContinuousOutdoorOpeningPopulationBootstrap.Normalize(world, registry, OpeningSiteId);
            Assert.IsFalse(world.WorldPresence.TryGet(caveShade, out _),
                "normalize pass pulled a cave NPC into the outdoor surface");

            // cave 的 LocationId 也不得解析成 Outdoor Site。
            var index = ContinuousOutdoorSitePlaceIndex.Build(registry);
            Assert.IsFalse(ContinuousOutdoorSpawnPresenceResolver.TryResolveSiteForEntityLocation(
                    world, index, CaveChamberLocationId, out _, out _),
                "cave location resolved to an outdoor site");
        }

        // ---------------------------------------------------------------- E
        [Test]
        public void E_StartupPopulationCheckRequiresEveryExpectedEntity()
        {
            var expected = new List<EntityId>
            {
                new EntityId(1), new EntityId(2), new EntityId(3), new EntityId(4), new EntityId(5)
            };
            var materialized = new HashSet<ulong> { 1, 2, 3, 4 };
            var views = new HashSet<ulong> { 1, 2, 3, 4 };
            var missing = new List<EntityId>();

            var complete = ContinuousOutdoorStartupPlanner.TryCheckPopulationComplete(
                expected,
                id => materialized.Contains(id.Value),
                id => views.Contains(id.Value),
                missing);

            Assert.IsFalse(complete, "4/5 materialized must NOT pass the startup invariant");
            Assert.AreEqual(1, missing.Count);
            Assert.AreEqual(5UL, missing[0].Value);

            // 全部就位时通过。
            materialized.Add(5);
            views.Add(5);
            missing.Clear();
            Assert.IsTrue(ContinuousOutdoorStartupPlanner.TryCheckPopulationComplete(
                expected,
                id => materialized.Contains(id.Value),
                id => views.Contains(id.Value),
                missing));
            Assert.AreEqual(0, missing.Count);
        }

        // ---------------------------------------------------------------- F
        [Test]
        public void F_NormalSimulationTicksNeverTriggerPopulationReconcile()
        {
            var boot = Boot();
            var world = boot.World;
            var before = world.ContinuousOutdoorMaterialization.EntityReconcileRevision;
            for (var i = 0; i < 60; i++)
            {
                var tick = boot.Loop.TickOnce();
                Assert.IsTrue(tick.IsSuccess, tick.IsFailure ? tick.Error.ToString() : string.Empty);
            }

            Assert.AreEqual(before, world.ContinuousOutdoorMaterialization.EntityReconcileRevision,
                "normal simulation tick must not reconcile continuous outdoor population " +
                "(that would reintroduce NPC teleporting / per-tick cost)");
        }

        // ---------------------------------------------------------------- G
        /// <summary>
        /// 制作人复验 blocker：NewGame 荒村缺 1 人（杂役主管）。
        /// 根因：该 NPC 是驻荒村的 Hex FormalArmy 成员（army:formal_huangcun_labor_garrison），
        /// 经 army-at-site 路径计入人口并被 runtime materialize，但 IsEntityVisible 里
        /// 「Hex FormalArmy 残留 AtSite presence」守卫将其隐藏 → Expected=N Materialized=N Views=N-1。
        /// 语义：materialize 集合（loaded scope 权威）内的 Continuous Site 人口必须可见；
        /// 未 materialize 的残留 presence 仍按原守卫隐藏。
        /// </summary>
        [Test]
        public void G_HexArmyGarrisonAtContinuousSiteIsVisibleOnceMaterialized()
        {
            var boot = Boot();
            var world = boot.World;
            var garrisonMember = FindByDefinition(world, "base:character_ch01_ref_supervisor");
            Assert.AreNotEqual(EntityId.None, garrisonMember,
                "荒村驻军成员（杂役主管）必须存在");
            Assert.IsTrue(ArmyService.TryGetArmyForCharacter(world, garrisonMember, out var army));
            Assert.IsTrue(army.UsesHexStrategicPosition,
                "该 NPC 必须是 Hex FormalArmy 成员（即本修复命中的那条路径）");
            Assert.IsTrue(world.Strategic.Sites.TryGet(OpeningSiteId, out var site));
            Assert.IsTrue(site.OccupiesHex(army.CurrentHex),
                "该驻军必须物理位于荒村 footprint 内");

            // continuous startup 状态：legacy Site／LocalMap focus 已清。
            world.PartyWorld.ClearSiteFocus();
            world.PartyWorld.LocalMapId = string.Empty;
            world.LocalMap.ActiveMapLayoutId = string.Empty;

            // 未 materialize：残留 AtSite presence 不得泄漏进图（旧守卫语义必须保留）。
            Assert.IsFalse(LocalMapVisibility.IsEntityVisible(world, garrisonMember),
                "未 materialize 的 hex army 残留 presence 仍必须隐藏");

            // runtime materialize（loaded scope 权威）后必须可见，否则 EntityView 永远不产生。
            var desired = new HashSet<EntityId> { garrisonMember };
            world.ContinuousOutdoorMaterialization.ReconcileEntities(desired, null, null);
            Assert.IsTrue(LocalMapVisibility.IsEntityVisible(world, garrisonMember),
                "materialized 的 Continuous Site 驻军必须可见（materialized-but-no-view 缺口）");
        }

        // ------------------------------------------------------- synthetic content
        static void RegisterProbeSpawnTable(DefinitionRegistry registry, string definitionId)
        {
            var table = new SpawnTableDefinition
            {
                Id = DefinitionId.Parse(ProbeSpawnTableId).Value,
                Name = "opening population probe"
            };
            table.Entries.Add(new SpawnTableEntry
            {
                DefinitionId = definitionId,
                Weight = 1,
                CountMin = 1,
                CountMax = 1
            });
            var registered = registry.RegisterSpawnTable(table);
            Assert.IsTrue(registered.IsSuccess,
                registered.IsFailure ? registered.Error.ToString() : string.Empty);
        }

        static MapLayoutDefinition RegisterProbeMapLayout(DefinitionRegistry registry)
        {
            var source = new MapLayoutDefinition
            {
                Id = DefinitionId.Parse(ProbeMapLayoutId).Value,
                Name = "opening population probe",
                OriginX = 0f,
                OriginY = 0f,
                CellSize = 1f,
                Width = 8,
                Height = 8
            };
            source.Placements.Add(new MapPlacement
            {
                Id = "zone_a",
                Kind = "spawnZone",
                X = 1,
                Y = 1,
                W = 2,
                H = 2,
                SpawnTableId = ProbeSpawnTableId,
                BoundLocationId = LaborYardLocationId
            });
            var registered = registry.RegisterMapLayout(source);
            Assert.IsTrue(registered.IsSuccess,
                registered.IsFailure ? registered.Error.ToString() : string.Empty);
            return source;
        }
    }
}
