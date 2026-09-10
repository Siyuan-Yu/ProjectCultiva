using System;
using System.Collections.Generic;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.Social;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>
    /// New Game：按 OpeningSpawnEntry 解析每个 spawn 的<b>初始 macro presence</b>。
    ///
    /// 与 <c>CollectOpeningCharacterEntityIds</c>（Player／character travel bootstrap 用途）严格分开：
    /// NPC 绝不进入 <c>PlayerPartyTravel</c> 的 traveling members。
    ///
    /// 解析顺序（§3）：
    /// <list type="bullet">
    /// <item>A. <c>spawn.WorldSiteId</c> 非空 = 最高 authored authority（character／npc 同等）。</item>
    /// <item>B. character 未写 worldSiteId → DefaultStartSiteId（既有语义，不变）。</item>
    /// <item>C. npc 未写 worldSiteId → 从其 authored／placed LocalPlace 反查所属 Outdoor WorldSite；
    ///     无法正向解析（无 place／非 Opening Outdoor Site／独立空间）→ 不臆造 presence。</item>
    /// </list>
    ///
    /// §4：Interior／Cave／其它独立空间的 NPC 绝不因为「没写 worldSiteId」而被放进 Outdoor。
    /// 判定依据是 Content 语义（place 的 <see cref="WorldLocationState.LocalMapId"/> 属于另一张
    /// LocalMap；以及既有 <c>PersonalityProfile</c> tag "cave"），不是启发式。
    ///
    /// §5/§6/§7：<c>spawn.LocalPosition</c> 是 legacy Site LocalMap presentation 坐标，
    /// 必须经 source LocalMap bounds ／ <see cref="WorldSiteSpatialMapping"/> 转换为 canonical
    /// Outdoor WorldPosition 后才作为锚点保存（绝不直接当 Continuous 坐标）。
    /// </summary>
    public static class OpeningSpawnWorldPresenceApplier
    {
        public static Result Apply(
            SimulationWorld world,
            DefinitionRegistry registry,
            OpeningScenarioDefinition scenario,
            GameStartLookup lookup,
            IList<OpeningSpawnEntry> spawnEntries)
        {
            if (world == null)
                return Result.Failure(ErrorCode.InvalidArgument, "OpeningSpawn presence args null.");

            var entries = spawnEntries ?? scenario?.Spawns;
            if (entries == null || entries.Count == 0)
                return Result.Success();

            var defaultSiteId = HexStrategicSessionBootstrap.DefaultStartSiteId;
            Dictionary<string, string> siteIdByPlaceId = null;

            for (var i = 0; i < entries.Count; i++)
            {
                var spawn = entries[i];
                if (spawn == null || string.IsNullOrWhiteSpace(spawn.DefinitionId))
                    continue;
                if (lookup == null || !lookup.TryGetEntity(spawn.DefinitionId, out var entityId) || entityId.IsNone)
                    continue;

                var entityKind = string.IsNullOrEmpty(spawn.EntityKind)
                    ? "character"
                    : spawn.EntityKind.Trim();

                // A. 显式 authored WorldSite：character／npc 同等，最高 authority。
                if (!string.IsNullOrWhiteSpace(spawn.WorldSiteId))
                {
                    var authoredSiteId = spawn.WorldSiteId.Trim();
                    if (!world.Strategic.Sites.TryGet(authoredSiteId, out var authoredSite) ||
                        authoredSite == null)
                    {
                        return Result.Failure(
                            ErrorCode.NotFound,
                            "Authored spawn worldSiteId missing.",
                            spawn.WorldSiteId);
                    }

                    ApplyPresence(world, registry, authoredSite, spawn, entityId);
                    continue;
                }

                // B. character 未写 worldSiteId：既有默认开局 Site 语义（不改）。
                if (string.Equals(entityKind, "character", StringComparison.OrdinalIgnoreCase))
                {
                    if (world.Strategic.Sites.TryGet(defaultSiteId, out var defaultSite) && defaultSite != null)
                        ApplyPresence(world, registry, defaultSite, spawn, entityId);
                    continue;
                }

                // C. npc 未写 worldSiteId：解析 authored／placed LocalPlace 的所属 Outdoor Site。
                var placeId = ResolveAuthoredPlaceId(spawn, world, entityId);
                if (string.IsNullOrEmpty(placeId))
                    continue;

                if (!TryResolveOutdoorSiteForPlace(
                        world, registry, placeId, ref siteIdByPlaceId, out var placeSite))
                    continue;

                if (IsIndependentSpaceNpc(world, entityId, placeSite, placeId))
                    continue;

                ApplyPresence(world, registry, placeSite, spawn, entityId);
            }

            return Result.Success();
        }

        static void ApplyPresence(
            SimulationWorld world,
            DefinitionRegistry registry,
            WorldSite site,
            OpeningSpawnEntry spawn,
            EntityId entityId)
        {
            if (TryResolveAuthoredAnchor(world, registry, site, spawn, out var anchor))
                world.WorldPresence.SetAtSiteWithAnchor(entityId, site.SiteId, anchor);
            else
                world.WorldPresence.SetAtSite(entityId, site.SiteId);
        }

        /// <summary>§6：LocalLocationId 优先作为 place identity；否则用 bootstrap 实际落地的 place。</summary>
        static string ResolveAuthoredPlaceId(
            OpeningSpawnEntry spawn,
            SimulationWorld world,
            EntityId entityId)
        {
            if (!string.IsNullOrWhiteSpace(spawn.LocalLocationId))
                return spawn.LocalLocationId.Trim();

            if (world.Entities.TryGet(entityId, out var entity) &&
                entity.TryGet<EntityLocationComponent>(out var loc) &&
                !string.IsNullOrEmpty(loc.LocationId))
                return loc.LocationId;

            return string.Empty;
        }

        /// <summary>
        /// place → Outdoor WorldSite。Content authority 顺序：
        /// 1) Outdoor surface 的 sitePlaces[locationId].siteId（最直接的地点→Site 绑定）；
        /// 2) place 自身归属的 LocalMap（Legacy LocalPlaceSet 的 mapLayoutId ↔ WorldSite.LocalMapId）；
        /// 3) 当前 Opening LocalPlaceSet 的 mapLayoutId ↔ WorldSite.LocalMapId。
        /// 三者都要求目标 Site 是 Continuous Outdoor 且存在；否则不解析（不猜）。
        /// </summary>
        static bool TryResolveOutdoorSiteForPlace(
            SimulationWorld world,
            DefinitionRegistry registry,
            string placeId,
            ref Dictionary<string, string> siteIdByPlaceId,
            out WorldSite site)
        {
            site = null;
            if (string.IsNullOrEmpty(placeId) || world?.Strategic?.Sites == null)
                return false;

            if (siteIdByPlaceId == null)
            {
                siteIdByPlaceId = new Dictionary<string, string>(StringComparer.Ordinal);
                if (registry?.OutdoorSurfaces != null)
                {
                    foreach (var surfaceEntry in registry.OutdoorSurfaces)
                    {
                        var surface = surfaceEntry.Value;
                        if (surface?.SitePlaces == null)
                            continue;
                        for (var i = 0; i < surface.SitePlaces.Count; i++)
                        {
                            var place = surface.SitePlaces[i];
                            if (place == null || string.IsNullOrWhiteSpace(place.LocationId) ||
                                string.IsNullOrWhiteSpace(place.SiteId))
                                continue;
                            if (!siteIdByPlaceId.ContainsKey(place.LocationId.Trim()))
                                siteIdByPlaceId[place.LocationId.Trim()] = place.SiteId.Trim();
                        }
                    }
                }
            }

            string boundSiteId = null;
            if (siteIdByPlaceId.TryGetValue(placeId, out var directSiteId))
            {
                boundSiteId = directSiteId;
            }
            else if (world.WorldRegion.TryGet(placeId, out var placeState) && placeState != null)
            {
                var owningMapId = !string.IsNullOrEmpty(placeState.LocalMapId)
                    ? placeState.LocalMapId
                    : world.WorldRegion.ActiveMapLayoutId;
                boundSiteId = ResolveSiteIdByLocalMapId(world, owningMapId);
            }

            if (string.IsNullOrEmpty(boundSiteId))
                return false;
            if (!world.Strategic.Sites.TryGet(boundSiteId, out site) || site == null)
                return false;

            // 只有 Continuous Outdoor WorldSite 才通过 Outdoor surface 呈现。
            return WorldSiteOutdoorMigrationPolicy.UsesContinuousOutdoorSurface(site);
        }

        static string ResolveSiteIdByLocalMapId(SimulationWorld world, string localMapId)
        {
            if (string.IsNullOrEmpty(localMapId))
                return null;
            foreach (var kv in world.Strategic.Sites.Sites)
            {
                var candidate = kv.Value;
                if (candidate == null || string.IsNullOrEmpty(candidate.SiteId))
                    continue;
                if (!string.Equals(candidate.LocalMapId, localMapId, StringComparison.Ordinal))
                    continue;
                return candidate.SiteId;
            }

            return null;
        }

        /// <summary>
        /// §4：独立空间（Cave／Interior／其它 LocalMap）NPC 不放到 Outdoor。
        /// 依据 Content 语义：place 归属另一张 LocalMap（且与 Site 的 Outdoor source layout 不同），
        /// 或既有 PersonalityProfile tag "cave"。
        /// </summary>
        static bool IsIndependentSpaceNpc(
            SimulationWorld world,
            EntityId entityId,
            WorldSite site,
            string placeId)
        {
            if (world.Entities.TryGet(entityId, out var entity) && IsCaveBoundNpc(entity))
                return true;

            if (!world.WorldRegion.TryGet(placeId, out var placeState) || placeState == null)
                return false;

            if (string.IsNullOrEmpty(placeState.LocalMapId))
                return false;

            // place 明确属于某张 LocalMap：只有当它就是该 Site 的 Outdoor source layout 时才算地表。
            return !string.Equals(placeState.LocalMapId, site.LocalMapId, StringComparison.Ordinal);
        }

        static bool IsCaveBoundNpc(XianXia.Core.Entities.Entity entity)
        {
            if (entity == null)
                return false;
            if (!entity.TryGet<PersonalityProfileComponent>(out var profile))
                return false;
            return profile.HasTag("cave");
        }

        /// <summary>
        /// §5/§6/§7：authored LocalPosition（legacy Site LocalMap presentation 坐标）
        /// → source LocalMap bounds ＋ <see cref="WorldSiteSpatialMapping"/> V2 → canonical WorldPosition。
        /// 无 LocalPosition／无 source layout／映射失败 → false（退回 Site-only presence，不伪造锚点）。
        /// </summary>
        static bool TryResolveAuthoredAnchor(
            SimulationWorld world,
            DefinitionRegistry registry,
            WorldSite site,
            OpeningSpawnEntry spawn,
            out WorldVec2 anchor)
        {
            anchor = default;
            if (spawn?.LocalPosition == null || site == null || world?.HexWorld == null)
                return false;
            if (string.IsNullOrEmpty(site.LocalMapId) || registry == null)
                return false;
            var parsed = DefinitionId.Parse(site.LocalMapId);
            if (parsed.IsFailure || !registry.TryGetMapLayout(parsed.Value, out var layout) || layout == null)
                return false;

            var bounds = WorldSiteSpatialMapping.WorldSiteLocalMapBounds.FromOriginSize(
                layout.OriginX,
                layout.OriginY,
                layout.CellSize,
                layout.Width,
                layout.Height);
            if (!bounds.IsValid)
                return false;

            var hexSize = world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : HexWorldScale.DefaultHexOuterRadius;

            return WorldSiteSpatialMapping.TryLocalToWorldSurface(
                site,
                bounds,
                new WorldVec2(spawn.LocalPosition.X, spawn.LocalPosition.Z),
                hexSize,
                out anchor);
        }
    }
}
