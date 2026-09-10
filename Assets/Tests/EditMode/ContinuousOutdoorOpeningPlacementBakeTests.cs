using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Data.Bootstrap;
using XianXia.Data.Content;

namespace XianXia.Tests.EditMode
{
    /// <summary>
    /// Opening NPC authored placement fidelity（迁移 Part A，targeted regression A–D）。
    ///
    /// <para>
    /// 契约：opening NPC 的落点 = 「旧 Site source LocalMap 中的 authored placement」经与
    /// SitePlacements／SitePlaces <b>完全相同</b>的 bake transform（
    /// <see cref="WorldSiteOutdoorBakeTransform"/>）得到，且该落点位于对应的 migrated
    /// building／bound location 内部。不得退化成 SitePlace center／arrival／随便一个 walkable point。
    /// </para>
    /// </summary>
    public sealed class ContinuousOutdoorOpeningPlacementBakeTests
    {
        const string ScenarioId = "base:scenario_ch01_reference";
        const string SiteId = "base:site_huangcun";
        const string SurfaceId = "base:surface_main_wilderness_v1";
        const string HousesLocationId = "base:loc_ref_houses";
        const string GuardHousingLocationId = "base:loc_ref_guard_housing";
        const string SupervisorHousingLocationId = "base:loc_ref_supervisor_housing";

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
            Assert.IsTrue(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.ToString() : string.Empty);
            var started = new PlayableDayBootstrap().Start(loaded.Value, new PlayableDayOptions
            {
                DailyRequiredAmount = 10,
                OpeningScenarioId = ScenarioId
            });
            Assert.IsTrue(started.IsSuccess, started.IsFailure ? started.Error.ToString() : string.Empty);
            return started.Value;
        }

        static OutdoorWorldSurfaceDefinition Surface(DefinitionRegistry registry)
        {
            foreach (var entry in registry.OutdoorSurfaces)
                if (entry.Value != null && entry.Value.SurfaceId == SurfaceId)
                    return entry.Value;
            Assert.Fail("main continuous surface missing");
            return null;
        }

        static List<OpeningPlacementPlanRow> BuildPlan(
            PlayableDayBootstrapResult boot,
            out OutdoorWorldSurfaceDefinition surface,
            out WorldSite site,
            out List<EntityId> population)
        {
            surface = Surface(boot.Registry);
            Assert.IsTrue(boot.World.Strategic.Sites.TryGet(SiteId, out site), "huangcun site missing");
            population = new List<EntityId>();
            StrategicWorldSitePopulationService.CollectCharacterIdsPresentAtWorldSite(
                boot.World, site, null, population);
            population.Sort((a, b) => a.Value.CompareTo(b.Value));
            var plan = new List<OpeningPlacementPlanRow>();
            Assert.IsTrue(ContinuousOutdoorOpeningPlacementResolver.TryBuildPlan(
                boot.World, boot.Registry, surface, site, population, plan, out var failure), failure);
            return plan;
        }

        // ---------------------------------------------------------------- A
        /// <summary>
        /// §5：每个 opening spawn 必须存在<b>唯一</b> checked-in anchor，且
        /// <c>source local point → shared bake transform ≈ checked-in anchor</c>。
        /// 「anchor 存在」本身不算通过。
        /// </summary>
        [Test]
        public void A_SourceLocalPositionBakesToCheckedInAnchor()
        {
            var boot = Boot();
            var plan = BuildPlan(boot, out var surface, out var site, out var population);
            Assert.Greater(plan.Count, 0);

            Assert.IsTrue(ContinuousOutdoorOpeningPlacementResolver.TryValidateBakedAnchors(
                    boot.World, boot.Registry, surface, site, population, plan, out var failure),
                "§5 bake validation 必须通过：" + failure);

            foreach (var row in plan)
            {
                Assert.AreNotEqual(OpeningPlacementAuthority.Unresolved, row.Authority,
                    row.SpawnKey + " 必须有 authored source placement：" + row.FailureReason);
                Assert.IsTrue(row.HasCheckedInAnchor, row.SpawnKey + " 必须有 checked-in anchor");
                Assert.IsTrue(row.AnchorMatchesBake,
                    row.SpawnKey + " anchor 必须等于 shared bake，delta=" + row.AnchorDelta);

                // 再独立复算一次（不依赖 plan 内部结果），确认是同一 truth。
                Assert.IsTrue(ContinuousOutdoorStartupPlanner.TryResolveSiteSourceLayout(
                    boot.Registry, surface, SiteId, out var sourceLayout, out var layoutFailure), layoutFailure);
                var bounds = WorldSiteSpatialMapping.WorldSiteLocalMapBounds.FromOriginSize(
                    sourceLayout.OriginX, sourceLayout.OriginY, sourceLayout.CellSize,
                    sourceLayout.Width, sourceLayout.Height);
                Assert.IsTrue(WorldSiteOutdoorBakeTransform.TryBake(
                    site.OccupiedHexes, boot.World.HexWorld.HexSize, bounds,
                    row.SourceLocalPosition, out var expected));
                Assert.AreEqual(expected.X, row.CheckedInWorldPosition.X, 1e-4f, row.SpawnKey);
                Assert.AreEqual(expected.Y, row.CheckedInWorldPosition.Y, 1e-4f, row.SpawnKey);
            }
        }

        // ---------------------------------------------------------------- A2
        /// <summary>
        /// §5 真源必须是 <b>Content</b>：Continuous Outdoor 正常运行时不会加载 legacy
        /// <c>WorldRegion</c> place set（这正是迁移的目的），启动 invariant 若依赖它就会误报
        /// 「authored place missing from WorldRegion」（制作人 Play 实测的
        /// <c>[ContinuousStartupInvariantFailure] Opening Site baked anchors invalid</c>）。
        /// 本测试刻意清空 WorldRegion 后重跑，要求结果逐条相同。
        /// </summary>
        [Test]
        public void A2_AnchorValidationDoesNotDependOnRuntimeWorldRegion()
        {
            var boot = Boot();
            var loadedPlan = BuildPlan(boot, out var surface, out var site, out var population);
            Assert.IsTrue(ContinuousOutdoorOpeningPlacementResolver.TryValidateBakedAnchors(
                boot.World, boot.Registry, surface, site, population, loadedPlan, out var loadedFailure),
                loadedFailure);

            // 模拟 Continuous Outdoor 启动时刻：legacy WorldRegion 完全没有加载。
            boot.World.WorldRegion.ClearLocations();
            Assert.IsFalse(boot.World.WorldRegion.TryGet("base:loc_ref_houses", out _),
                "本测试前提：WorldRegion 必须为空");

            var plan = new List<OpeningPlacementPlanRow>();
            Assert.IsTrue(ContinuousOutdoorOpeningPlacementResolver.TryBuildPlan(
                boot.World, boot.Registry, surface, site, population, plan, out var planFailure),
                "plan 不得依赖运行时 WorldRegion：" + planFailure);

            Assert.IsTrue(ContinuousOutdoorOpeningPlacementResolver.TryValidateBakedAnchors(
                    boot.World, boot.Registry, surface, site, population, plan, out var failure),
                "§5 校验不得依赖运行时 WorldRegion：" + failure);

            Assert.AreEqual(loadedPlan.Count, plan.Count);
            for (var i = 0; i < plan.Count; i++)
            {
                var expected = loadedPlan.Single(r => r.SpawnKey == plan[i].SpawnKey);
                Assert.AreNotEqual(OpeningPlacementAuthority.Unresolved, plan[i].Authority,
                    plan[i].SpawnKey + "：" + plan[i].FailureReason);
                Assert.AreEqual(expected.SourcePlaceId, plan[i].SourcePlaceId, plan[i].SpawnKey);
                Assert.AreEqual(expected.Authority, plan[i].Authority, plan[i].SpawnKey);
                Assert.AreEqual(expected.SlotIndex, plan[i].SlotIndex, plan[i].SpawnKey);
                Assert.AreEqual(expected.BakedWorldPosition.X, plan[i].BakedWorldPosition.X, 1e-6f, plan[i].SpawnKey);
                Assert.AreEqual(expected.BakedWorldPosition.Y, plan[i].BakedWorldPosition.Y, 1e-6f, plan[i].SpawnKey);

                // 同时覆盖 materialize 侧（Host 实际走的那条）：WorldRegion 为空时仍必须命中
                // checked-in BakedOpeningEntityAnchor，而不是退回 SitePlace／arrival。
                var assignedLocationId = string.Empty;
                if (boot.World.Entities.TryGet(plan[i].EntityId, out var entity) &&
                    entity.TryGet<EntityLocationComponent>(out var loc) && loc != null)
                    assignedLocationId = loc.LocationId;
                var source = ContinuousOutdoorOpeningAnchorResolver.ResolveInitialPlacement(
                    surface, SiteId, plan[i].SpawnKey, assignedLocationId,
                    hasRuntimeAnchor: false, 0f, 0f, out var anchor, out _, out _);
                Assert.AreEqual(OpeningInitialPlacementSource.BakedOpeningEntityAnchor, source,
                    plan[i].SpawnKey + " 在 WorldRegion 为空时仍必须命中 baked anchor，而不是 " + source);
                // checked-in anchor 是 5 位小数，因此用生产侧 epsilon 比较。
                Assert.AreEqual(expected.BakedWorldPosition.X, anchor.X,
                    ContinuousOutdoorOpeningPlacementResolver.AnchorBakeEpsilon, plan[i].SpawnKey);
                Assert.AreEqual(expected.BakedWorldPosition.Y, anchor.Y,
                    ContinuousOutdoorOpeningPlacementResolver.AnchorBakeEpsilon, plan[i].SpawnKey);
            }
        }

        // ---------------------------------------------------------------- A3
        /// <summary>
        /// 普通 Site（没有 checked-in opening anchor，例如青石镇）不得被 §5 校验误判。
        /// </summary>
        [Test]
        public void A3_SiteWithoutCheckedInAnchorsIsNotReportedAsInvalid()
        {
            var boot = Boot();
            var surface = Surface(boot.Registry);
            Assert.IsFalse(ContinuousOutdoorOpeningPlacementResolver.HasCheckedInAnchors(
                surface, "base:site_chengzhen"));
            Assert.IsTrue(boot.World.Strategic.Sites.TryGet("base:site_chengzhen", out var chengzhen));
            var population = new List<EntityId>();
            StrategicWorldSitePopulationService.CollectCharacterIdsPresentAtWorldSite(
                boot.World, chengzhen, null, population);
            var plan = new List<OpeningPlacementPlanRow>();
            Assert.IsTrue(ContinuousOutdoorOpeningPlacementResolver.TryValidateBakedAnchors(
                boot.World, boot.Registry, surface, chengzhen, population, plan, out var failure),
                "没有 opening anchor 的 Site 不得报 invalid：" + failure);
        }

        // ---------------------------------------------------------------- A4
        /// <summary>
        /// §5 校验不得依赖「当前在场 population」。startup invariant 运行时 Party 成员已经是
        /// <c>AtWorldPosition</c>（不属于 site population），若 slot 按在场者排序就会整体错位
        /// —— 制作人 Play 实测的「village_recruit slot=2 却等于主角 anchor」正是此症状。
        /// </summary>
        [Test]
        public void A4_AnchorValidationIsIndependentOfPopulationMembership()
        {
            var boot = Boot();
            var world = boot.World;
            var fullPlan = BuildPlan(boot, out var surface, out var site, out var fullPopulation);
            Assert.IsTrue(ContinuousOutdoorOpeningPlacementResolver.TryValidateBakedAnchors(
                world, boot.Registry, surface, site, fullPopulation, fullPlan, out var fullFailure),
                fullFailure);

            // 复刻 Play：主角（+ 已入队的同伴）在 invariant 时刻已 AtWorldPosition。
            var protagonist = fullPopulation.Single(id =>
                world.Entities.TryGet(id, out var e) && e.DefinitionId.ToString() == "base:character_protagonist");
            world.WorldPresence.SetAtWorldPosition(
                protagonist, world.PlayerPartyTravel.WorldPosition, world.PlayerPartyTravel.CurrentHex);

            var reduced = new List<EntityId>();
            StrategicWorldSitePopulationService.CollectCharacterIdsPresentAtWorldSite(
                world, site, null, reduced);
            Assert.IsFalse(reduced.Contains(protagonist),
                "本测试前提：Party 成员必须不再属于 site population（AtWorldPosition）");
            Assert.Less(reduced.Count, fullPopulation.Count);

            var plan = new List<OpeningPlacementPlanRow>();
            Assert.IsTrue(ContinuousOutdoorOpeningPlacementResolver.TryBuildPlan(
                world, boot.Registry, surface, site, reduced, plan, out var planFailure), planFailure);
            Assert.IsTrue(ContinuousOutdoorOpeningPlacementResolver.TryValidateBakedAnchors(
                    world, boot.Registry, surface, site, reduced, plan, out var failure),
                "在场人数变化不得让 §5 校验失败（slot 必须来自 authored anchor，而不是在场顺序）：" + failure);

            // 在场者的 slot / 落点必须与完整 population 时逐条一致。
            foreach (var row in plan)
            {
                var expected = fullPlan.Single(r => r.SpawnKey == row.SpawnKey);
                Assert.AreEqual(expected.SlotIndex, row.SlotIndex,
                    row.SpawnKey + " 的 slot 不得因为别人不在场而变化");
                Assert.AreEqual(expected.SourcePlaceId, row.SourcePlaceId, row.SpawnKey);
                Assert.AreEqual(expected.CheckedInWorldPosition.X, row.CheckedInWorldPosition.X, 1e-6f, row.SpawnKey);
                Assert.AreEqual(expected.CheckedInWorldPosition.Y, row.CheckedInWorldPosition.Y, 1e-6f, row.SpawnKey);
            }
        }

        // ---------------------------------------------------------------- B
        /// <summary>
        /// §6 / target B：原本安排在住房里的 NPC，Continuous first materialize 后必须落在对应住房
        /// （migrated bound location）内部，且该位置在旧 MapLayout 语义下可走
        /// （房间内部可行走，只有墙／障碍阻挡）。
        /// </summary>
        [Test]
        public void B_HousingOpeningNpcsSpawnInsideTheirAuthoredHome()
        {
            var boot = Boot();
            var plan = BuildPlan(boot, out var surface, out var site, out var population);
            Assert.IsTrue(ContinuousOutdoorStartupPlanner.TryResolveSiteSourceLayout(
                boot.Registry, surface, SiteId, out var sourceLayout, out var layoutFailure), layoutFailure);

            var housing = new HashSet<string>(StringComparer.Ordinal)
            {
                HousesLocationId, GuardHousingLocationId, SupervisorHousingLocationId
            };

            var byPlace = plan
                .Where(r => r.Authority != OpeningPlacementAuthority.Unresolved)
                .GroupBy(r => r.SourcePlaceId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

            // (c) 混合权威：退化到 StartLocationId 的人必须改用 homeWorkAreaId 的居住点。
            string[] expectedHouses =
            {
                "base:character_ch01_ref_farmer_b", "base:character_ch01_ref_farmer_c",
                "base:character_ch01_ref_herb_b", "base:character_ch01_ref_herb_c",
                "base:character_ch01_ref_wood_c", "base:character_ch01_ref_mortal_a"
            };
            foreach (var definitionId in expectedHouses)
            {
                var row = plan.Single(r => r.DefinitionId == definitionId);
                Assert.AreEqual(HousesLocationId, row.SourcePlaceId,
                    definitionId + " 原来安排在凡人住房，必须出生在凡人住房");
            }

            foreach (var definitionId in new[] { "base:character_ch01_ref_guard_b", "base:character_ch01_ref_guard_c" })
            {
                var row = plan.Single(r => r.DefinitionId == definitionId);
                Assert.AreEqual(GuardHousingLocationId, row.SourcePlaceId,
                    definitionId + " 原来安排在巡卫住房，必须出生在巡卫住房");
            }

            // 每个 housing NPC 的 anchor 必须落在该住房 migrated placement 矩形内部。
            var checkedRows = 0;
            foreach (var pair in byPlace)
            {
                var place = surface.SitePlacements.Find(p => p != null &&
                    string.Equals(p.SiteId, SiteId, StringComparison.Ordinal) &&
                    string.Equals(p.BoundLocationId, pair.Key, StringComparison.Ordinal));
                Assert.IsNotNull(place, pair.Key + " 必须有 migrated placement");
                var minX = Math.Min(place.WorldX, place.WorldX + place.WorldWidth);
                var maxX = Math.Max(place.WorldX, place.WorldX + place.WorldWidth);
                var minY = Math.Min(place.WorldY, place.WorldY + place.WorldHeight);
                var maxY = Math.Max(place.WorldY, place.WorldY + place.WorldHeight);

                var insideAny = pair.Value.Any(r =>
                    r.CheckedInWorldPosition.X >= minX && r.CheckedInWorldPosition.X <= maxX &&
                    r.CheckedInWorldPosition.Y >= minY && r.CheckedInWorldPosition.Y <= maxY);
                if (housing.Contains(pair.Key))
                    Assert.IsTrue(insideAny, pair.Key + " 的 anchor 必须落在该住房矩形内部");

                // 旧 MapLayout walkability 语义：房间内部不得被 BlocksMovement placement 覆盖。
                foreach (var row in pair.Value)
                {
                    Assert.IsFalse(IsSourceCellBlocked(sourceLayout, row.SourceLocalPosition.X, row.SourceLocalPosition.Y),
                        row.SpawnKey + " 落在了 source MapLayout 的 blocker 上（房间内部应可行走）");
                    Assert.IsFalse(IsCoveredByBlockingSitePlacement(surface, row.CheckedInWorldPosition),
                        row.SpawnKey + " 落在了 CompositeWalkGrid 的 blocker rect 上" +
                        "（§6：应修 blocker bake，而不是把 NPC 吸到建筑外）");
                    checkedRows++;
                }
            }

            Assert.Greater(checkedRows, 0);
        }

        // ---------------------------------------------------------------- C
        /// <summary>
        /// §3 / target C：有 precise authored anchor 时不得 fallback 到 SitePlace center／arrival。
        /// </summary>
        [Test]
        public void C_PreciseAuthoredAnchorIsNeverDegradedToSitePlaceCenter()
        {
            var boot = Boot();
            var plan = BuildPlan(boot, out var surface, out var site, out var population);

            var degraded = 0;
            foreach (var row in plan)
            {
                Assert.AreNotEqual(OpeningPlacementAuthority.Unresolved, row.Authority, row.SpawnKey);

                var definitionId = row.DefinitionId;
                var assignedLocationId = string.Empty;
                if (boot.World.Entities.TryGet(row.EntityId, out var entity) &&
                    entity.TryGet<EntityLocationComponent>(out var loc) && loc != null)
                    assignedLocationId = loc.LocationId;

                var anchorSource = ContinuousOutdoorOpeningAnchorResolver.ResolveInitialPlacement(
                    surface, SiteId, row.SpawnKey, assignedLocationId,
                    hasRuntimeAnchor: false, 0f, 0f,
                    out var anchor, out _, out _);
                Assert.AreEqual(OpeningInitialPlacementSource.BakedOpeningEntityAnchor, anchorSource,
                    row.SpawnKey + " 必须命中 BakedOpeningEntityAnchor，而不是 " + anchorSource);
                Assert.AreEqual(row.CheckedInWorldPosition.X, anchor.X, 1e-6f);
                Assert.AreEqual(row.CheckedInWorldPosition.Y, anchor.Y, 1e-6f);
                if (anchorSource == OpeningInitialPlacementSource.BakedSitePlace ||
                    anchorSource == OpeningInitialPlacementSource.SiteArrivalFallback)
                    degraded++;
            }

            Assert.AreEqual(0, degraded, "不允许退化到 SitePlace center／arrival fallback");

            // 同一 location 多人时，只有 slot 0 允许与该 Location center 重合；
            // 其余 slot 必须与 Location center 不同（否则就是「用 Location center 覆盖 precise LocalPosition」）。
            var grouped = plan
                .Where(r => r.Authority != OpeningPlacementAuthority.Unresolved)
                .GroupBy(r => r.SourcePlaceId, StringComparer.Ordinal);
            foreach (var group in grouped)
            {
                var rows = group.OrderBy(r => r.SlotIndex).ToList();
                Assert.AreEqual(rows.Count, rows.Select(r => r.SlotIndex).Distinct().Count(),
                    group.Key + " 的 slot index 不得重复");
                var distinct = rows
                    .Select(r => r.CheckedInWorldPosition.X.ToString("F6") + "|" + r.CheckedInWorldPosition.Y.ToString("F6"))
                    .Distinct()
                    .Count();
                Assert.AreEqual(rows.Count, distinct, group.Key + " 内每个 spawn 的 anchor 必须互不重合");

                if (rows.Count <= 1)
                    continue;
                Assert.IsTrue(ContinuousOutdoorOpeningAnchorResolver.TryGetBakedSitePlace(
                    surface, SiteId, group.Key, out var center), group.Key);
                for (var i = 1; i < rows.Count; i++)
                {
                    var dx = rows[i].CheckedInWorldPosition.X - center.X;
                    var dy = rows[i].CheckedInWorldPosition.Y - center.Y;
                    Assert.Greater(Math.Sqrt(dx * dx + dy * dy), 1e-4,
                        group.Key + " 非首个 slot 不得与 Location center 重合");
                }
            }
        }

        // ---------------------------------------------------------------- D
        /// <summary>
        /// §4 / target D：<c>SpawnStableKey ≠ DefinitionId</c>。两个相同 DefinitionId、不同 spawnKey
        /// 的 spawn 必须解析到各自不同的 anchor —— 不得因为 DefinitionId 相同而吃 first-match。
        /// </summary>
        [Test]
        public void D_TwoSpawnsWithSameDefinitionDifferentKeysResolveDifferentAnchors()
        {
            const string definitionId = "base:character_duplicate_probe";
            var surface = new OutdoorWorldSurfaceDefinition
            {
                SurfaceId = SurfaceId,
                OpeningEntityAnchors = new List<WorldSiteOpeningEntityAnchorDefinition>
                {
                    new WorldSiteOpeningEntityAnchorDefinition
                    {
                        SiteId = SiteId, SpawnKey = definitionId, DefinitionId = definitionId,
                        SourceLocationId = HousesLocationId, WorldX = 1.5f, WorldY = 2.5f
                    },
                    new WorldSiteOpeningEntityAnchorDefinition
                    {
                        SiteId = SiteId, SpawnKey = definitionId + "#1", DefinitionId = definitionId,
                        SourceLocationId = GuardHousingLocationId, WorldX = 7.5f, WorldY = 8.5f
                    }
                }
            };

            Assert.IsTrue(ContinuousOutdoorOpeningAnchorResolver.TryGetBakedEntityAnchor(
                surface, SiteId, definitionId, out var first));
            Assert.IsTrue(ContinuousOutdoorOpeningAnchorResolver.TryGetBakedEntityAnchor(
                surface, SiteId, definitionId + "#1", out var second));
            Assert.AreNotEqual(first.X, second.X, "同 Definition 的不同 spawnKey 必须给出各自的 anchor");
            Assert.AreEqual(1.5f, first.X, 1e-6f);
            Assert.AreEqual(7.5f, second.X, 1e-6f);

            // 关键：key 不存在时不能靠 DefinitionId 兜底命中别人的 anchor。
            Assert.IsFalse(ContinuousOutdoorOpeningAnchorResolver.TryGetBakedEntityAnchor(
                    surface, SiteId, definitionId + "#2", out _),
                "spawnKey 缺失时不得用 DefinitionId first-match 命中别人的 anchor");

            // identity board：能从 spawned Entity 反查 stable key（不用 runtime order／InstanceId）。
            var board = new OpeningSpawnIdentityBoard();
            var a = new EntityId(41);
            var b = new EntityId(42);
            board.Register(a, OpeningSpawnIdentityBoard.BuildStableKey(definitionId, 0));
            board.Register(b, OpeningSpawnIdentityBoard.BuildStableKey(definitionId, 1));
            Assert.IsTrue(board.TryGetSpawnKey(a, out var keyA));
            Assert.IsTrue(board.TryGetSpawnKey(b, out var keyB));
            Assert.AreEqual(definitionId, keyA);
            Assert.AreEqual(definitionId + "#1", keyB);
            Assert.AreNotEqual(keyA, keyB);
            Assert.IsTrue(board.TryGetEntity(keyB, out var back));
            Assert.AreEqual(b, back);
            Assert.IsTrue(OpeningSpawnIdentityBoard.TryParseAuthoredIndex(keyB, definitionId, out var index));
            Assert.AreEqual(1, index);
        }

        /// <summary>
        /// 运行时 CompositeWalkGrid 的 Site blocker 来自 <c>sitePlacements</c> 中
        /// <c>blocksMovement=true</c> 的 rect（ContinuousOutdoorSurfaceRuntime.BuildSiteBlockerGrid）。
        /// 这里用同一份 checked-in 数据复核 anchor 没被压进 blocker。
        /// </summary>
        static bool IsCoveredByBlockingSitePlacement(
            OutdoorWorldSurfaceDefinition surface, WorldVec2 anchor)
        {
            if (surface?.SitePlacements == null)
                return false;
            for (var i = 0; i < surface.SitePlacements.Count; i++)
            {
                var p = surface.SitePlacements[i];
                if (p == null || !p.BlocksMovement ||
                    !string.Equals(p.SiteId, SiteId, StringComparison.Ordinal))
                    continue;
                var minX = Math.Min(p.WorldX, p.WorldX + p.WorldWidth);
                var maxX = Math.Max(p.WorldX, p.WorldX + p.WorldWidth);
                var minY = Math.Min(p.WorldY, p.WorldY + p.WorldHeight);
                var maxY = Math.Max(p.WorldY, p.WorldY + p.WorldHeight);
                if (anchor.X >= minX && anchor.X <= maxX && anchor.Y >= minY && anchor.Y <= maxY)
                    return true;
            }

            return false;
        }

        static bool IsSourceCellBlocked(MapLayoutDefinition layout, float localX, float localY)
        {
            var cell = layout.CellSize > 0f ? layout.CellSize : 1f;
            var cx = (int)Math.Floor((localX - layout.OriginX) / cell);
            var cy = (int)Math.Floor((localY - layout.OriginY) / cell);
            foreach (var placement in layout.Placements)
            {
                if (placement == null || !placement.BlocksMovement)
                    continue;
                var w = placement.W > 0 ? placement.W : 1;
                var h = placement.H > 0 ? placement.H : 1;
                if (cx >= placement.X && cx < placement.X + w &&
                    cy >= placement.Y && cy < placement.Y + h)
                    return true;
            }

            return false;
        }
    }
}
