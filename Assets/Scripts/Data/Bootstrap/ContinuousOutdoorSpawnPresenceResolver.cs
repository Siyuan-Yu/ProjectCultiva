using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Exploration;
using XianXia.Core.Simulation;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>
    /// 一个 <c>outdoorSurface.sitePlaces[]</c> 条目表达的「地点 → Site」绑定。
    /// 同时保留其 source LocalMap，用于歧义消解（同名 LocationId 出现在多张图时）。
    /// </summary>
    public readonly struct ContinuousOutdoorSitePlaceBinding
    {
        public ContinuousOutdoorSitePlaceBinding(string locationId, string siteId, string localMapId)
        {
            LocationId = locationId ?? string.Empty;
            SiteId = siteId ?? string.Empty;
            LocalMapId = localMapId ?? string.Empty;
        }

        public string LocationId { get; }
        public string SiteId { get; }
        public string LocalMapId { get; }
    }

    /// <summary>
    /// <c>LocationId → Outdoor WorldSite</c> 的解析索引。<b>禁止 first-wins</b>：
    /// 同一个 LocationId 出现在多个 Site 时，只有能用 source LocalMap 唯一消解才解析，
    /// 否则解析失败并给出明确的 Content validation 问题（绝不静默选第一个）。
    /// </summary>
    public sealed class ContinuousOutdoorSitePlaceIndex
    {
        readonly Dictionary<string, List<ContinuousOutdoorSitePlaceBinding>> _byLocationId =
            new Dictionary<string, List<ContinuousOutdoorSitePlaceBinding>>(StringComparer.Ordinal);

        public static ContinuousOutdoorSitePlaceIndex Build(DefinitionRegistry registry)
        {
            var index = new ContinuousOutdoorSitePlaceIndex();
            if (registry?.OutdoorSurfaces == null)
                return index;
            foreach (var entry in registry.OutdoorSurfaces)
            {
                var surface = entry.Value;
                if (surface?.SitePlaces == null)
                    continue;
                for (var i = 0; i < surface.SitePlaces.Count; i++)
                {
                    var place = surface.SitePlaces[i];
                    if (place == null || string.IsNullOrWhiteSpace(place.LocationId) ||
                        string.IsNullOrWhiteSpace(place.SiteId))
                        continue;
                    var locationId = place.LocationId.Trim();
                    if (!index._byLocationId.TryGetValue(locationId, out var list))
                    {
                        list = new List<ContinuousOutdoorSitePlaceBinding>(1);
                        index._byLocationId[locationId] = list;
                    }

                    var binding = new ContinuousOutdoorSitePlaceBinding(locationId, place.SiteId.Trim(), place.LocalMapId);
                    var duplicate = false;
                    for (var b = 0; b < list.Count; b++)
                        if (string.Equals(list[b].SiteId, binding.SiteId, StringComparison.Ordinal))
                        {
                            duplicate = true;
                            break;
                        }

                    if (!duplicate)
                        list.Add(binding);
                }
            }

            return index;
        }

        public bool Contains(string locationId) =>
            !string.IsNullOrEmpty(locationId) && _byLocationId.ContainsKey(locationId);

        public int BindingCountFor(string locationId) =>
            !string.IsNullOrEmpty(locationId) && _byLocationId.TryGetValue(locationId, out var list)
                ? list.Count
                : 0;

        /// <summary>
        /// 唯一解析。多候选时只能用 <paramref name="sourceLocalMapId"/> 消解（place 自身 LocalMapId
        /// 或调用方上下文里的 source map）。无法唯一解析 → false + <paramref name="ambiguity"/>。
        /// </summary>
        public bool TryResolveUniqueSite(
            string locationId,
            string sourceLocalMapId,
            out string siteId,
            out string ambiguity)
        {
            siteId = string.Empty;
            ambiguity = string.Empty;
            if (string.IsNullOrEmpty(locationId) || !_byLocationId.TryGetValue(locationId, out var list) ||
                list.Count == 0)
                return false;

            if (list.Count == 1)
            {
                siteId = list[0].SiteId;
                return !string.IsNullOrEmpty(siteId);
            }

            // 多候选：只接受「source LocalMap 唯一命中」的消解。
            var matched = string.Empty;
            var matches = 0;
            for (var i = 0; i < list.Count; i++)
            {
                var binding = list[i];
                var bindingMap = !string.IsNullOrEmpty(binding.LocalMapId)
                    ? binding.LocalMapId
                    : string.Empty;
                var candidateMap = !string.IsNullOrEmpty(sourceLocalMapId)
                    ? sourceLocalMapId
                    : bindingMap;
                if (string.IsNullOrEmpty(candidateMap) || !string.Equals(bindingMap, candidateMap, StringComparison.Ordinal))
                    continue;
                matched = binding.SiteId;
                matches++;
            }

            if (matches == 1)
            {
                siteId = matched;
                return true;
            }

            var sites = new List<string>(list.Count);
            for (var i = 0; i < list.Count; i++)
                sites.Add(list[i].SiteId);
            ambiguity = "sitePlace locationId '" + locationId + "' is bound to multiple sites (" +
                        string.Join(", ", sites) + ") and cannot be disambiguated by source map '" +
                        (sourceLocalMapId ?? string.Empty) + "'";
            return false;
        }

        /// <summary>Content validation：同名 LocationId 绑定到多个 Site（跨图复用）必须显式可消解。</summary>
        public List<string> ValidateAmbiguities()
        {
            var issues = new List<string>();
            foreach (var pair in _byLocationId)
            {
                if (pair.Value.Count <= 1)
                    continue;
                var sites = new List<string>(pair.Value.Count);
                var maps = new List<string>(pair.Value.Count);
                for (var i = 0; i < pair.Value.Count; i++)
                {
                    sites.Add(pair.Value[i].SiteId);
                    maps.Add(pair.Value[i].LocalMapId);
                }

                // 每个候选都必须带自己的 source map，否则解析时无法唯一消解。
                for (var i = 0; i < maps.Count; i++)
                {
                    if (!string.IsNullOrEmpty(maps[i]))
                        continue;
                    issues.Add("sitePlace locationId '" + pair.Key + "' is bound to multiple sites (" +
                               string.Join(", ", sites) + ") but a binding has no source localMapId");
                    break;
                }
            }

            return issues;
        }
    }

    /// <summary>
    /// SpawnZone／opening normalize 共用的 continuous outdoor presence 解析入口：
    /// <list type="bullet">
    /// <item>source MapLayoutId → Outdoor WorldSite（<c>site.localMapId</c> 精确匹配 + Continuous Outward 资格）。</item>
    /// <item>legacy Site LocalMap presentation 坐标 → canonical Outdoor WorldPosition。</item>
    /// </list>
    /// 独立空间（洞穴／室内／遭遇图等）绝不因为「有 LocationId」被塞进 Outdoor。
    /// </summary>
    public static class ContinuousOutdoorSpawnPresenceResolver
    {
        /// <summary>
        /// source MapLayout → 唯一 Continuous Outdoor WorldSite。
        /// 多个 Site 共用同一 layout 时拒绝解析（歧义不猜）。
        /// </summary>
        public static bool TryResolveContinuousOutdoorSiteForSourceMap(
            SimulationWorld world,
            string sourceMapLayoutId,
            out WorldSite site,
            out string ambiguity)
        {
            site = null;
            ambiguity = string.Empty;
            if (world?.Strategic?.Sites == null || string.IsNullOrWhiteSpace(sourceMapLayoutId))
                return false;

            WorldSite matched = null;
            var matches = 0;
            var names = new List<string>(2);
            foreach (var kv in world.Strategic.Sites.Sites)
            {
                var candidate = kv.Value;
                if (candidate == null || string.IsNullOrEmpty(candidate.SiteId))
                    continue;
                if (!string.Equals(candidate.LocalMapId, sourceMapLayoutId, StringComparison.Ordinal))
                    continue;
                if (!WorldSiteOutdoorMigrationPolicy.UsesContinuousOutdoorSurface(candidate))
                    continue;
                matched = candidate;
                matches++;
                names.Add(candidate.SiteId);
            }

            if (matches == 1)
            {
                site = matched;
                return true;
            }

            if (matches > 1)
                ambiguity = "mapLayout '" + sourceMapLayoutId + "' is the outdoor source of multiple sites (" +
                            string.Join(", ", names) + ")";
            return false;
        }

        /// <summary>
        /// legacy Site LocalMap presentation 坐标 → canonical Outdoor WorldPosition。
        /// 无 LocalMap bounds／映射失败 → false（调用方退回 Site-only presence，不伪造锚点）。
        ///
        /// <para>§2：必须与 SitePlacements／SitePlaces／OpeningEntityAnchors 走<b>同一个</b>
        /// <see cref="WorldSiteOutdoorBakeTransform"/>（authored bake truth），不得另开一套。</para>
        /// </summary>
        public static bool TryResolveCanonicalAnchor(
            SimulationWorld world,
            WorldSite site,
            MapLayoutDefinition sourceLayout,
            float presentationX,
            float presentationZ,
            out WorldVec2 anchor)
        {
            anchor = default;
            if (world?.HexWorld == null || site == null || sourceLayout == null)
                return false;

            var bounds = WorldSiteSpatialMapping.WorldSiteLocalMapBounds.FromOriginSize(
                sourceLayout.OriginX,
                sourceLayout.OriginY,
                sourceLayout.CellSize,
                sourceLayout.Width,
                sourceLayout.Height);
            if (!bounds.IsValid)
                return false;

            var hexSize = world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : HexWorldScale.DefaultHexOuterRadius;

            return WorldSiteOutdoorBakeTransform.TryBake(
                site,
                hexSize,
                bounds,
                new WorldVec2(presentationX, presentationZ),
                out anchor);
        }

        /// <summary>
        /// 实体 authored 地点 → 唯一 Continuous Outdoor Site（normalize pass 用）。
        /// source LocalMap 取 place 自身 LocalMapId／ActiveMapLayoutId；歧义 → false + reason。
        /// </summary>
        public static bool TryResolveSiteForEntityLocation(
            SimulationWorld world,
            ContinuousOutdoorSitePlaceIndex index,
            string locationId,
            out WorldSite site,
            out string ambiguity)
        {
            site = null;
            ambiguity = string.Empty;
            if (world?.Strategic?.Sites == null || index == null || string.IsNullOrWhiteSpace(locationId))
                return false;

            var sourceMapId = string.Empty;
            if (world.WorldRegion.TryGet(locationId, out var placeState) && placeState != null &&
                !string.IsNullOrEmpty(placeState.LocalMapId))
                sourceMapId = placeState.LocalMapId;

            if (!index.TryResolveUniqueSite(locationId, sourceMapId, out var siteId, out ambiguity))
                return false;

            if (!world.Strategic.Sites.TryGet(siteId, out site) || site == null)
                return false;

            // 独立空间：place 明确属于另一张 LocalMap（且不是该 Site 的 Outdoor source）→ 不塞进 Outdoor。
            if (placeState != null && !string.IsNullOrEmpty(placeState.LocalMapId) &&
                !string.Equals(placeState.LocalMapId, site.LocalMapId, StringComparison.Ordinal))
            {
                site = null;
                return false;
            }

            return WorldSiteOutdoorMigrationPolicy.UsesContinuousOutdoorSurface(site);
        }
    }
}
