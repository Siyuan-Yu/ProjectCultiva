using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
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
    /// NewGame Continuous Outdoor Opening Spatial Placement（§17 A–G）。
    /// Verifies checked-in Surface opening anchors and materialized positions remain distinct.
    /// </summary>
    public sealed class ContinuousOutdoorOpeningSpatialTests
    {
        const string ScenarioId = "base:scenario_ch01_reference";
        const string SiteId = "base:site_huangcun";
        const string SurfaceId = "base:surface_main_wilderness_v1";
        const string ChengzhenSiteId = "base:site_chengzhen";
        const string ChengzhenStartLocationId = "base:loc_site_chengzhen_start";
        const string ProtagonistDefinitionId = "base:character_protagonist";
        const string CompanionADefinitionId = "base:character_companion_a";
        const string CompanionBDefinitionId = "base:character_companion_b";
        const string LaborYardLocationId = "base:loc_ref_labor_yard";
        const string AcceptanceLegacySpawnDefinitionId = "base:character_qingshi_acceptance_unaffiliated_a";

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

        static OutdoorWorldSurfaceDefinition Surface(DefinitionRegistry registry)
        {
            foreach (var entry in registry.OutdoorSurfaces)
                if (entry.Value != null &&
                    string.Equals(entry.Value.SurfaceId, SurfaceId, StringComparison.Ordinal))
                    return entry.Value;
            Assert.Fail("main continuous surface missing");
            return null;
        }

        static EntityId FindByDefinition(SimulationWorld world, string definitionId)
        {
            foreach (var entity in world.Entities.All)
                if (string.Equals(entity.DefinitionId.ToString(), definitionId, StringComparison.Ordinal))
                    return entity.Id;
            return EntityId.None;
        }

        static List<EntityId> SitePopulation(SimulationWorld world, string siteId)
        {
            Assert.IsTrue(world.Strategic.Sites.TryGet(siteId, out var site), "site missing: " + siteId);
            var list = new List<EntityId>();
            StrategicWorldSitePopulationService.CollectCharacterIdsPresentAtWorldSite(
                world, site, null, list);
            return list;
        }

        /// <summary>复刻 materialize 的最终落点（含 §9 第四级 deterministic fallback）。</summary>
        static WorldVec2 ResolveMaterializeTarget(
            SimulationWorld world,
            OutdoorWorldSurfaceDefinition surface,
            EntityId id,
            string siteId,
            out OpeningInitialPlacementSource source)
        {
            Assert.IsTrue(world.Entities.TryGet(id, out var entity));
            var locationId = entity.TryGet<EntityLocationComponent>(out var loc) && loc != null
                ? loc.LocationId ?? string.Empty
                : string.Empty;
            Assert.IsTrue(world.OpeningSpawnIdentities.TryGetSpawnKey(id, out var spawnKey));
            source = ContinuousOutdoorOpeningAnchorResolver.ResolveInitialPlacement(
                surface,
                siteId,
                spawnKey,
                locationId,
                false,
                0f,
                0f,
                out var anchor,
                out _,
                out _);
            if (source != OpeningInitialPlacementSource.SiteArrivalFallback)
                return anchor;

            Assert.IsTrue(surface.SiteRegions.Any(r => r != null &&
                string.Equals(r.SiteId, siteId, StringComparison.Ordinal)));
            var region = surface.SiteRegions.Find(r => r != null &&
                string.Equals(r.SiteId, siteId, StringComparison.Ordinal));
            var wx = region.ArrivalWorldX;
            var wy = region.ArrivalWorldY;
            var ordinal = (int)(id.Value % 23UL) + 1;
            var angle = ordinal * 2.39996322972865332;
            var radius = Math.Max(surface.CellSize * 2f, surface.CellSize * (2f + ordinal / 6f));
            return new WorldVec2(wx + (float)Math.Cos(angle) * radius,
                                 wy + (float)Math.Sin(angle) * radius);
        }

        // ---------------------------------------------------------------- A
        /// <summary>
        /// §4/§5：Continuous Outdoor opening presence uses the checked-in Surface anchor.
        /// </summary>
        [Test]
        public void A_OpeningPresenceUsesCheckedInSurfaceAnchor()
        {
            var boot = Boot();
            var world = boot.World;
            var id = FindByDefinition(world, AcceptanceLegacySpawnDefinitionId);
            Assert.AreNotEqual(EntityId.None, id,
                "ch01_reference 必须仍有带 legacy localPosition 的 acceptance spawn（本测试的前提）");

            Assert.IsTrue(world.WorldPresence.TryGet(id, out var presence));
            Assert.AreEqual(PartyWorldPresenceMode.AtSite, presence.Mode);
            Assert.AreEqual(ChengzhenSiteId, presence.SiteId);
            Assert.IsTrue(presence.HasContinuousWorldPosition,
                "Normal NewGame must use its checked-in Surface anchor");
            Assert.AreEqual(SurfaceId, presence.PersonalSurfaceId);
            var surface = Surface(boot.Registry);
            var target = ResolveMaterializeTarget(world, surface, id, ChengzhenSiteId, out var source);
            Assert.AreEqual(OpeningInitialPlacementSource.BakedOpeningEntityAnchor, source);
            Assert.AreEqual(target.X, presence.WorldPosX, 1e-5f);
            Assert.AreEqual(target.Y, presence.WorldPosY, 1e-5f);
        }

        // ---------------------------------------------------------------- B
        /// <summary>
        /// §6/§7/§17-B：opening entity 使用 checked-in baked anchor；全部互不重合，
        /// 且落在该 Site 的 baked physical envelope 内。
        /// </summary>
        [Test]
        public void B_OpeningAnchorsAreBakedDistinctAndInsideSiteEnvelope()
        {
            var boot = Boot();
            var world = boot.World;
            var surface = Surface(boot.Registry);
            Assert.Greater(surface.OpeningEntityAnchors.Count, 0,
                "content 必须带 checked-in openingEntityAnchors");

            var population = SitePopulation(world, SiteId);
            Assert.Greater(population.Count, 0);

            var positions = new List<string>();
            foreach (var id in population)
            {
                var target = ResolveMaterializeTarget(world, surface, id, SiteId, out var source);
                Assert.IsTrue(
                    source == OpeningInitialPlacementSource.BakedOpeningEntityAnchor ||
                    source == OpeningInitialPlacementSource.BakedSitePlace,
                    "opening entity 必须使用 baked 真源，而不是 arrival fallback：" + source);
                Assert.IsTrue(
                    ContinuousOutdoorOpeningAnchorResolver.IsInsideSiteBakedEnvelope(
                        surface, SiteId, target.X, target.Y),
                    "opening anchor 必须落在该 Site 的 baked envelope 内：(" +
                    target.X.ToString("F4") + "," + target.Y.ToString("F4") + ")");

                positions.Add(target.X.ToString("F5") + "|" + target.Y.ToString("F5"));
            }

            Assert.AreEqual(population.Count, positions.Distinct().Count(),
                "opening entity 的 materialize 落点不得重合（12 人压成一点即本 bug 的视觉症状）");
        }

        // ---------------------------------------------------------------- C
        /// <summary>§9/§14：同伴甲乙作为背景 resident 有各自的 baked anchor，且不进 PlayerParty。</summary>
        [Test]
        public void C_CompanionsGetDistinctBakedAnchorsWithoutJoiningPlayerParty()
        {
            var boot = Boot();
            var world = boot.World;
            var surface = Surface(boot.Registry);

            var ids = new List<EntityId>
            {
                FindByDefinition(world, ProtagonistDefinitionId),
                FindByDefinition(world, CompanionADefinitionId),
                FindByDefinition(world, CompanionBDefinitionId)
            };
            foreach (var id in ids)
            {
                Assert.AreNotEqual(EntityId.None, id);
                Assert.IsTrue(world.WorldPresence.TryGet(id, out var presence));
                Assert.AreEqual(SiteId, presence.SiteId);
                var target = ResolveMaterializeTarget(world, surface, id, SiteId, out var source);
                Assert.AreEqual(OpeningInitialPlacementSource.BakedOpeningEntityAnchor, source);
                Assert.IsTrue(ContinuousOutdoorOpeningAnchorResolver.IsInsideSiteBakedEnvelope(
                    surface, SiteId, target.X, target.Y));
            }

            var distinct = new HashSet<string>();
            foreach (var id in ids)
            {
                var target = ResolveMaterializeTarget(world, surface, id, SiteId, out _);
                distinct.Add(target.X.ToString("F5") + "|" + target.Y.ToString("F5"));
            }

            Assert.AreEqual(3, distinct.Count, "主角与同伴必须各有不同落点");
            Assert.IsTrue(world.PlayerPartyTravel.TravelingMembers.Contains(ids[0]));
            Assert.IsFalse(world.PlayerPartyTravel.TravelingMembers.Contains(ids[1]));
            Assert.IsFalse(world.PlayerPartyTravel.TravelingMembers.Contains(ids[2]));
        }

        // ---------------------------------------------------------------- D
        /// <summary>§13：普通（非 opening）Site 的 resident 落点路径不受影响，仍走 baked SitePlace。</summary>
        [Test]
        public void D_NonOpeningSiteResidentStillUsesBakedSitePlace()
        {
            var boot = Boot();
            var surface = Surface(boot.Registry);

            Assert.IsTrue(ContinuousOutdoorOpeningAnchorResolver.TryGetBakedSitePlace(
                surface, ChengzhenSiteId, ChengzhenStartLocationId, out var place),
                "青石镇 baked SitePlace 必须存在（普通 Site 路径不能因为本轮修复而失效）");
            Assert.IsTrue(ContinuousOutdoorOpeningAnchorResolver.IsInsideSiteBakedEnvelope(
                surface, ChengzhenSiteId, place.X, place.Y));

            var source = ContinuousOutdoorOpeningAnchorResolver.ResolveInitialPlacement(
                surface,
                ChengzhenSiteId,
                "base:character_never_baked_anchor_probe",
                ChengzhenStartLocationId,
                false,
                0f,
                0f,
                out var anchor,
                out _,
                out _);
            Assert.AreEqual(OpeningInitialPlacementSource.BakedSitePlace, source);
            Assert.AreEqual(place.X, anchor.X);
            Assert.AreEqual(place.Y, anchor.Y);
        }

        // ---------------------------------------------------------------- E
        /// <summary>§11：不属于本 Site baked envelope 的 runtime anchor 必须被拒绝（不删 Domain entity）。</summary>
        [Test]
        public void E_RuntimeAnchorOutsideBakedEnvelopeIsRejected()
        {
            var boot = Boot();
            var surface = Surface(boot.Registry);

            var companionId = FindByDefinition(boot.World, CompanionADefinitionId);
            Assert.IsTrue(boot.World.OpeningSpawnIdentities.TryGetSpawnKey(companionId, out var companionKey));
            var source = ContinuousOutdoorOpeningAnchorResolver.ResolveInitialPlacement(
                surface,
                SiteId,
                companionKey,
                LaborYardLocationId,
                hasRuntimeAnchor: true,
                runtimeX: 20f,
                runtimeY: 20f,
                out var anchor,
                out var reason,
                out var rejected);

            Assert.IsTrue(rejected, "远处的 runtime anchor 必须被判拒绝");
            Assert.IsFalse(string.IsNullOrEmpty(reason));
            Assert.IsTrue(reason.Contains("outside baked envelope"), reason);
            Assert.AreNotEqual(OpeningInitialPlacementSource.RuntimePreciseAnchor, source);
            Assert.AreEqual(OpeningInitialPlacementSource.BakedOpeningEntityAnchor, source);

            // 正对照：真的落在 envelope 内的 runtime anchor 仍优先（§9 第一级不被削弱）。
            Assert.IsTrue(ContinuousOutdoorOpeningAnchorResolver.TryGetBakedEntityAnchor(
                surface, SiteId, companionKey, out var baked));
            var accepted = ContinuousOutdoorOpeningAnchorResolver.ResolveInitialPlacement(
                surface,
                SiteId,
                companionKey,
                LaborYardLocationId,
                hasRuntimeAnchor: true,
                runtimeX: baked.X,
                runtimeY: baked.Y,
                out var acceptedAnchor,
                out _,
                out var acceptedRejected);
            Assert.IsFalse(acceptedRejected);
            Assert.AreEqual(OpeningInitialPlacementSource.RuntimePreciseAnchor, accepted);
            Assert.AreEqual(baked.X, acceptedAnchor.X);
            Assert.AreEqual(baked.Y, acceptedAnchor.Y);
        }

        // ---------------------------------------------------------------- F
        /// <summary>
        /// §17-F：Expected=5／Materialized=5／Views=5 但 SpatialValid=4 时 startup postcondition 必须 FAIL。
        /// </summary>
        [Test]
        public void F_SpatialInvariantFailsWhenOneEntityIsSpatiallyInvalid()
        {
            var expected = new List<EntityId>
            {
                new EntityId(1), new EntityId(2), new EntityId(3), new EntityId(4), new EntityId(5)
            };
            var spatiallyValid = new HashSet<ulong> { 1, 2, 3, 4 };
            var invalid = new List<EntityId>();

            var ok = ContinuousOutdoorStartupPlanner.TryCheckPopulationSpatiallyValid(
                expected,
                id => spatiallyValid.Contains(id.Value),
                invalid);

            Assert.IsFalse(ok, "4/5 spatial valid 绝不能算通过");
            Assert.AreEqual(1, invalid.Count);
            Assert.AreEqual(5UL, invalid[0].Value);

            spatiallyValid.Add(5);
            invalid.Clear();
            Assert.IsTrue(ContinuousOutdoorStartupPlanner.TryCheckPopulationSpatiallyValid(
                expected,
                id => spatiallyValid.Contains(id.Value),
                invalid));
            Assert.AreEqual(0, invalid.Count);
        }

        // ---------------------------------------------------------------- G
        /// <summary>Checked-in Surface opening anchors are stable and distinct.</summary>
        [Test]
        public void G_CheckedInOpeningAnchorsAreDeterministicAndDistinct()
        {
            var boot = Boot();
            var world = boot.World;
            var surface = Surface(boot.Registry);
            Assert.IsTrue(world.Strategic.Sites.TryGet(SiteId, out var site));
            var population = SitePopulation(world, SiteId);
            var first = new List<OpeningPlacementPlanRow>();
            var second = new List<OpeningPlacementPlanRow>();
            Assert.IsTrue(ContinuousOutdoorOpeningPlacementResolver.TryBuildPlan(
                world, boot.Registry, surface, site, population, first, out var firstFailure), firstFailure);
            Assert.IsTrue(ContinuousOutdoorOpeningPlacementResolver.TryBuildPlan(
                world, boot.Registry, surface, site, population, second, out var secondFailure), secondFailure);
            Assert.AreEqual(first.Count, second.Count);
            var seen = new HashSet<string>();
            for (var i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].EntityId, second[i].EntityId);
                Assert.AreEqual(first[i].SpawnKey, second[i].SpawnKey);
                Assert.AreEqual(first[i].BakedWorldPosition, second[i].BakedWorldPosition);
                if (string.IsNullOrEmpty(first[i].SpawnKey)) continue;
                Assert.IsTrue(first[i].HasCheckedInAnchor, first[i].FailureReason);
                Assert.IsTrue(seen.Add(first[i].BakedWorldPosition.ToString()),
                    "opening anchors must be distinct");
            }
            Assert.Greater(seen.Count, 0);
            Assert.AreEqual(OpeningAnchorBakeValidationOutcome.Validated,
                ContinuousOutdoorOpeningPlacementResolver.ValidateBakedAnchors(
                    world, boot.Registry, surface, site, population, first, out var validationFailure),
                validationFailure);
        }
    }
}
