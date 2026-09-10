using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>NewGame 完成后对 opening population 的一次轻量 census（Domain 侧计数，不含 EntityView）。</summary>
    public struct ContinuousOutdoorOpeningCensus
    {
        public string OpeningSiteId;
        public int OpeningCharacterCount;
        public int OpeningNpcCount;
        public int WorldPresenceAtOpeningSiteCount;
        public int FormalArmyCharacterAtOpeningSiteCount;
        public int ExpectedContinuousPopulationCount;

        public string Describe() =>
            "OpeningSite=" + (OpeningSiteId ?? string.Empty) +
            " OpeningCharacter=" + OpeningCharacterCount +
            " OpeningNpc=" + OpeningNpcCount +
            " PresenceAtSite=" + WorldPresenceAtOpeningSiteCount +
            " ArmyAtSite=" + FormalArmyCharacterAtOpeningSiteCount +
            " ExpectedPopulation=" + ExpectedContinuousPopulationCount;
    }

    /// <summary>normalize pass 结果（诊断用；不参与 gameplay）。</summary>
    public struct ContinuousOutdoorOpeningPopulationReport
    {
        public int NormalizedAtSite;
        public int NormalizedAtSiteWithAnchor;
        public int SkippedExistingPresence;
        public int SkippedArmyMember;
        public int SkippedIndependentSpace;
        public int Unresolved;
        public List<string> Ambiguities;
    }

    /// <summary>
    /// NewGame opening population 的 presence 归一化（§5–§9）。
    ///
    /// 只补「完全没有 WorldPresence」的实体，绝不覆盖已有 authority（AtSite／AtWorldPosition／
    /// AtHex／InEncounter／FormalArmy member 战略位置）。解析必须唯一：LocationId 跨 Site 复用而
    /// 无法用 source LocalMap 消解时 **不猜**，记入 ambiguity（Content validation error）。
    ///
    /// 它不决定「谁在运行中被 materialize」——那只属于 Continuous Outdoor 的 loaded neighborhood；
    /// 这里只保证 opening 实体在 domain 上拥有正确的初始 macro presence。
    /// </summary>
    public static class ContinuousOutdoorOpeningPopulationBootstrap
    {
        public static Result Apply(
            SimulationWorld world,
            DefinitionRegistry registry,
            string openingSiteId,
            List<string> diagnostics = null)
        {
            if (world == null || registry == null)
                return Result.Success();

            var report = Normalize(world, registry, openingSiteId, diagnostics);
            if (diagnostics == null)
                return Result.Success();

            diagnostics.Add(
                "[OpeningPopulation] normalized=" + report.NormalizedAtSite +
                " withAnchor=" + report.NormalizedAtSiteWithAnchor +
                " skippedExisting=" + report.SkippedExistingPresence +
                " skippedArmy=" + report.SkippedArmyMember +
                " skippedIndependent=" + report.SkippedIndependentSpace +
                " unresolved=" + report.Unresolved);
            return Result.Success();
        }

        public static ContinuousOutdoorOpeningPopulationReport Normalize(
            SimulationWorld world,
            DefinitionRegistry registry,
            string openingSiteId,
            List<string> diagnostics = null)
        {
            var report = new ContinuousOutdoorOpeningPopulationReport
            {
                Ambiguities = new List<string>(0)
            };
            if (world == null || registry == null)
                return report;

            var index = ContinuousOutdoorSitePlaceIndex.Build(registry);
            foreach (var ambiguity in index.ValidateAmbiguities())
            {
                report.Ambiguities.Add(ambiguity);
                diagnostics?.Add("[OpeningPopulationAmbiguity] " + ambiguity);
            }

            WorldSite openingSite = null;
            if (!string.IsNullOrEmpty(openingSiteId))
                world.Strategic?.Sites.TryGet(openingSiteId, out openingSite);

            var entities = new List<Entity>(world.Entities.All);
            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (entity == null || entity.Id.IsNone)
                    continue;

                // §6：已有任何 presence（含 Army 战略位置）→ 一律不覆盖。
                if (world.WorldPresence.TryGet(entity.Id, out var existing) && existing != null)
                {
                    report.SkippedExistingPresence++;
                    continue;
                }

                var isCharacter = (entity.Tags & EntityTag.Character) != 0;
                var isNpc = (entity.Tags & EntityTag.Npc) != 0;
                if (!isCharacter && !isNpc)
                    continue;

                // FormalArmy member 的战略位置由 FormalArmyDefinition.factionId／Army context 决定；
                // normalize 绝不为它臆造 AtSite（否则会把部队成员钉进某个 Site population）。
                if (ArmyService.TryGetArmyForCharacter(world, entity.Id, out _))
                {
                    report.SkippedArmyMember++;
                    continue;
                }

                if (IsCaveBound(entity))
                {
                    report.SkippedIndependentSpace++;
                    continue;
                }

                var loc = default(EntityLocationComponent);
                var hasLocation = entity.TryGet<EntityLocationComponent>(out loc) && loc != null;
                var locationId = hasLocation && loc.HasLocation ? loc.LocationId : string.Empty;
                var sourceMapId = string.Empty;
                if (!string.IsNullOrEmpty(locationId) &&
                    world.WorldRegion.TryGet(locationId, out var placeState) && placeState != null &&
                    !string.IsNullOrEmpty(placeState.LocalMapId))
                    sourceMapId = placeState.LocalMapId;

                WorldSite site = null;
                var ambiguity = string.Empty;
                if (!string.IsNullOrEmpty(locationId))
                    ContinuousOutdoorSpawnPresenceResolver.TryResolveSiteForEntityLocation(
                        world, index, locationId, out site, out ambiguity);

                // §9 opening character contract：opening character（未写 worldSiteId → 默认开局 Site）
                // 若因任何原因没有 presence，这里补上；同伴因此仍是 Background AtSite，不进 PlayerParty。
                if (site == null && isCharacter && openingSite != null)
                {
                    if (string.IsNullOrEmpty(sourceMapId) ||
                        string.Equals(sourceMapId, openingSite.LocalMapId, StringComparison.Ordinal))
                        site = openingSite;
                }

                if (site == null)
                {
                    report.Unresolved++;
                    if (!string.IsNullOrEmpty(ambiguity))
                    {
                        report.Ambiguities.Add(ambiguity);
                        diagnostics?.Add("[OpeningPopulationAmbiguity] " + ambiguity);
                    }

                    continue;
                }

                var anchored = false;
                if (hasLocation && loc.HasPresentationOverride)
                {
                    var layout = ResolveSourceLayout(registry, site);
                    if (layout != null &&
                        ContinuousOutdoorSpawnPresenceResolver.TryResolveCanonicalAnchor(
                            world, site, layout, loc.PresentationOverrideX, loc.PresentationOverrideZ,
                            out var anchor))
                    {
                        world.WorldPresence.SetAtSiteWithAnchor(entity.Id, site.SiteId, anchor);
                        report.NormalizedAtSiteWithAnchor++;
                        report.NormalizedAtSite++;
                        anchored = true;
                    }
                }

                if (!anchored)
                {
                    world.WorldPresence.SetAtSite(entity.Id, site.SiteId);
                    report.NormalizedAtSite++;
                }
            }

            return report;
        }

        public static ContinuousOutdoorOpeningCensus BuildCensus(
            SimulationWorld world,
            string openingSiteId)
        {
            var census = new ContinuousOutdoorOpeningCensus { OpeningSiteId = openingSiteId ?? string.Empty };
            if (world == null)
                return census;

            var characterIds = new List<EntityId>();
            foreach (var entity in world.Entities.All)
            {
                if (entity == null || entity.Id.IsNone)
                    continue;
                if ((entity.Tags & EntityTag.Character) != 0)
                {
                    census.OpeningCharacterCount++;
                    characterIds.Add(entity.Id);
                }
                else if ((entity.Tags & EntityTag.Npc) != 0)
                    census.OpeningNpcCount++;
            }

            if (string.IsNullOrEmpty(openingSiteId) ||
                !world.Strategic.Sites.TryGet(openingSiteId, out var site) || site == null)
                return census;

            foreach (var kv in world.WorldPresence.All)
            {
                var presence = kv.Value;
                if (presence == null || presence.Mode != PartyWorldPresenceMode.AtSite)
                    continue;
                if (!string.Equals(presence.SiteId, openingSiteId, StringComparison.Ordinal))
                    continue;
                census.WorldPresenceAtOpeningSiteCount++;
            }

            var expected = new List<EntityId>();
            StrategicWorldSitePopulationService.CollectCharacterIdsPresentAtWorldSite(world, site, null, expected);
            census.ExpectedContinuousPopulationCount = expected.Count;
            for (var i = 0; i < expected.Count; i++)
            {
                if (ArmyService.TryGetArmyForCharacter(world, expected[i], out _))
                    census.FormalArmyCharacterAtOpeningSiteCount++;
            }

            return census;
        }

        static MapLayoutDefinition ResolveSourceLayout(DefinitionRegistry registry, WorldSite site)
        {
            if (registry == null || site == null || string.IsNullOrEmpty(site.LocalMapId))
                return null;
            var parsed = DefinitionId.Parse(site.LocalMapId);
            if (parsed.IsFailure)
                return null;
            return registry.TryGetMapLayout(parsed.Value, out var layout) ? layout : null;
        }

        static bool IsCaveBound(Entity entity)
        {
            if (entity == null)
                return false;
            return entity.TryGet<PersonalityProfileComponent>(out var profile) && profile != null &&
                   profile.HasTag("cave");
        }
    }
}
