using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Npc;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;
using XianXia.Core.World.Strategic;
using XianXia.Data.Content;

namespace XianXia.Data.Bootstrap
{
    /// <summary>opening spawn 的 authored source placement 权威来源。</summary>
    public enum OpeningPlacementAuthority
    {
        /// <summary>解析失败（无 bound location／无 source layout／无 bake 资格）。</summary>
        Unresolved = 0,

        /// <summary>bootstrap 分配的 authored LocalPlace（resident place；非 StartLocationId 退化值）。</summary>
        AssignedLocalPlace = 1,

        /// <summary>人物 Content 的 <c>homeWorkAreaId</c> 指向的居住／常驻工区（§A-(c)：只有退化到
        /// StartLocationId 的人才用它）。</summary>
        HomeWorkArea = 2,

        /// <summary>LocalPlaceSet 的 startLocationId（无任何 authored placement 时的兜底）。</summary>
        StartLocation = 3
    }

    /// <summary>单个 opening spawn 的落点计划（bake 输入／输出＋checked-in anchor 对照）。</summary>
    public sealed class OpeningPlacementPlanRow
    {
        public EntityId EntityId;
        public string DefinitionId = string.Empty;
        public string SpawnKey = string.Empty;
        public string SourcePlaceId = string.Empty;
        public OpeningPlacementAuthority Authority = OpeningPlacementAuthority.Unresolved;
        public int SlotIndex;
        public WorldVec2 SourceLocalPosition;
        public WorldVec2 BakedWorldPosition;
        public bool HasBakedPosition;
        public bool HasCheckedInAnchor;
        public WorldVec2 CheckedInWorldPosition;
        public float AnchorDelta;
        public bool AnchorMatchesBake;
        public string FailureReason = string.Empty;
    }

    /// <summary>§5 bake validation 结果。</summary>
    public enum OpeningAnchorBakeValidationOutcome
    {
        /// <summary>已校验且通过。</summary>
        Validated = 0,

        /// <summary>该 Site 没有 checked-in opening anchor，或 Content 查不到 authored placement
        /// 真源（LocalPlaceSet／MapLayout）→ 不是 anchor 造假，不判失败。</summary>
        Skipped = 1,

        /// <summary>存在 anchor，但与 shared bake 不一致／缺失／重复 → startup invariant 必须失败。</summary>
        Failed = 2
    }

    /// <summary>
    /// Opening entity 落点的 authored 解析 + bake 校验（§1–§6）。
    ///
    /// <para>
    /// authored source place 权威顺序（制作人确认的 (c) 混合）：
    /// <list type="number">
    /// <item>bootstrap 实际分配的 LocalPlace，且不是 LocalPlaceSet 的 <c>startLocationId</c> 退化值
    /// （= authored <c>residentNpcDefinitionId</c> 归属；阿木→树林、阿柴→矿洞、阿青→药田）。</item>
    /// <item>人物 Content 的 <c>homeWorkAreaId</c> → 工区 <c>locationId</c>（原来安排的住房／常驻点；
    /// 阿土／阿禾／阿兰／阿杏／阿枝→凡人住房、巡卫乙／丙→巡卫住房）。</item>
    /// <item>LocalPlaceSet 的 <c>startLocationId</c>（无 authored placement 的兜底）。</item>
    /// </list>
    /// 每条都要求该 location 绑定到本 Site（<c>sitePlaces</c>）。
    /// </para>
    ///
    /// <para>
    /// 落点 = 该 location 的 authored presentation（source LocalMap 坐标）＋ 该 place 内按
    /// <see cref="OpeningSpawnIdentityBoard"/> spawnKey ordinal 排序得到的确定性 slot，
    /// 经 <see cref="WorldSiteOutdoorBakeTransform"/> 烘焙。绝不使用 Location center 覆盖。
    /// </para>
    ///
    /// <para>
    /// authored 真源一律取 <b>checked-in Content</b>（LocalPlaceSet ＋ MapLayout）；运行时
    /// <c>WorldRegion</c> 只是镜像，Continuous Outdoor 正常运行时可能整场都没加载它。
    /// </para>
    /// </summary>
    public static class ContinuousOutdoorOpeningPlacementResolver
    {
        /// <summary>checked-in anchor 与 shared bake 的允许误差（world units）。</summary>
        public const float AnchorBakeEpsilon = 1e-3f;

        public static string DescribeAuthority(OpeningPlacementAuthority authority)
        {
            switch (authority)
            {
                case OpeningPlacementAuthority.AssignedLocalPlace: return "AssignedLocalPlace";
                case OpeningPlacementAuthority.HomeWorkArea: return "HomeWorkArea";
                case OpeningPlacementAuthority.StartLocation: return "StartLocation";
                default: return "Unresolved";
            }
        }

        /// <summary>
        /// 解析一个 entity 的 authored source place。所有候选都必须绑定到本 Site
        /// （<c>surface.SitePlaces</c>），否则视为不可解析（不猜）。
        /// </summary>
        public static OpeningPlacementAuthority ResolveAuthoredPlace(
            SimulationWorld world,
            DefinitionRegistry registry,
            OutdoorWorldSurfaceDefinition surface,
            LocalPlaceSetDefinition placeSet,
            string siteId,
            EntityId id,
            out string sourcePlaceId,
            out string reason)
        {
            sourcePlaceId = string.Empty;
            reason = string.Empty;
            if (world == null || surface == null || string.IsNullOrWhiteSpace(siteId) || id.IsNone)
                return OpeningPlacementAuthority.Unresolved;

            string assignedLocationId = string.Empty;
            if (world.Entities.TryGet(id, out var entity) && entity != null &&
                entity.TryGet<EntityLocationComponent>(out var location) && location != null &&
                location.HasLocation)
                assignedLocationId = location.LocationId ?? string.Empty;

            // StartLocationId 是「没有 authored placement」的哨兵值。优先取运行时 WorldRegion，
            // 未加载（Continuous Outdoor 正常情形）时取 Content 的 LocalPlaceSet —— 两者同源，
            // 但绝不能因为运行时板为空就把「退化值」误判成 authored resident place。
            var startLocationId = world.WorldRegion?.StartLocationId ?? string.Empty;
            if (string.IsNullOrEmpty(startLocationId))
                startLocationId = placeSet?.StartLocationId ?? string.Empty;

            // 1. authored resident place（bootstrap 已解析）；StartLocationId 视为「无 authored placement」。
            if (!string.IsNullOrEmpty(assignedLocationId) &&
                !string.Equals(assignedLocationId, startLocationId, StringComparison.Ordinal) &&
                IsPlaceBoundToSite(surface, siteId, assignedLocationId))
            {
                sourcePlaceId = assignedLocationId;
                return OpeningPlacementAuthority.AssignedLocalPlace;
            }

            // 2. 人物 Content 的居住／常驻工区。
            if (entity != null && entity.TryGet<ActivityTendencyComponent>(out var tendency) &&
                tendency != null && !string.IsNullOrWhiteSpace(tendency.HomeWorkAreaId))
            {
                var parsed = DefinitionId.Parse(tendency.HomeWorkAreaId.Trim());
                if (parsed.IsSuccess &&
                    registry != null &&
                    registry.TryGetWorkArea(parsed.Value, out var workArea) &&
                    workArea != null &&
                    !string.IsNullOrWhiteSpace(workArea.LocationId) &&
                    IsPlaceBoundToSite(surface, siteId, workArea.LocationId.Trim()))
                {
                    sourcePlaceId = workArea.LocationId.Trim();
                    return OpeningPlacementAuthority.HomeWorkArea;
                }
            }

            // 3. StartLocationId 兜底。
            if (!string.IsNullOrEmpty(startLocationId) && IsPlaceBoundToSite(surface, siteId, startLocationId))
            {
                sourcePlaceId = startLocationId;
                return OpeningPlacementAuthority.StartLocation;
            }

            reason = "no authored placement resolvable for entity " + id.Value +
                     " (assigned='" + assignedLocationId + "', start='" + startLocationId + "')";
            return OpeningPlacementAuthority.Unresolved;
        }

        public static bool IsPlaceBoundToSite(
            OutdoorWorldSurfaceDefinition surface,
            string siteId,
            string locationId)
        {
            if (surface?.SitePlaces == null || string.IsNullOrEmpty(siteId) || string.IsNullOrEmpty(locationId))
                return false;
            for (var i = 0; i < surface.SitePlaces.Count; i++)
            {
                var place = surface.SitePlaces[i];
                if (place == null)
                    continue;
                if (string.Equals(place.SiteId, siteId, StringComparison.Ordinal) &&
                    string.Equals(place.LocationId, locationId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 为某 Continuous Outdoor Site 的全部 opening spawn 生成落点计划。
        /// slot index 由 place 内 spawnKey ordinal 排序给出（确定性，不用 runtime order）。
        /// </summary>
        public static bool TryBuildPlan(
            SimulationWorld world,
            DefinitionRegistry registry,
            OutdoorWorldSurfaceDefinition surface,
            WorldSite site,
            IReadOnlyList<EntityId> population,
            List<OpeningPlacementPlanRow> into,
            out string failure)
        {
            failure = string.Empty;
            if (into == null)
            {
                failure = "plan sink is null";
                return false;
            }

            into.Clear();
            if (world == null || surface == null || site == null || population == null)
            {
                failure = "opening placement plan inputs missing";
                return false;
            }

            var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : HexWorldScale.DefaultHexOuterRadius;
            if (!ContinuousOutdoorStartupPlanner.TryResolveSiteSourceLayout(
                    registry, surface, site.SiteId, out var sourceLayout, out var layoutFailure))
            {
                failure = layoutFailure;
                return false;
            }

            // authored placement 真源取 checked-in LocalPlaceSet；WorldRegion 只是运行时镜像，
            // Continuous Outdoor 正常运行时可能整场都没有加载它（启动 invariant 不得依赖它）。
            if (!ContinuousOutdoorStartupPlanner.TryResolveSiteSourcePlaceSet(
                    registry, surface, site.SiteId, out var placeSet, out var placeSetFailure))
            {
                failure = placeSetFailure;
                return false;
            }

            var sourceBounds = WorldSiteSpatialMapping.WorldSiteLocalMapBounds.FromOriginSize(
                sourceLayout.OriginX, sourceLayout.OriginY, sourceLayout.CellSize,
                sourceLayout.Width, sourceLayout.Height);
            if (!sourceBounds.IsValid)
            {
                failure = "source layout bounds invalid for site " + site.SiteId;
                return false;
            }

            // ---- pass 1：identity + authored place ----
            for (var i = 0; i < population.Count; i++)
            {
                var id = population[i];
                if (id.IsNone || !world.Entities.TryGet(id, out var entity) || entity == null)
                    continue;

                var row = new OpeningPlacementPlanRow
                {
                    EntityId = id,
                    DefinitionId = entity.DefinitionId.ToString()
                };
                if (!world.OpeningSpawnIdentities.TryGetSpawnKey(id, out var spawnKey))
                    spawnKey = OpeningSpawnIdentityBoard.BuildStableKey(row.DefinitionId, 0);
                row.SpawnKey = spawnKey;
                row.Authority = ResolveAuthoredPlace(
                    world, registry, surface, placeSet, site.SiteId, id, out var placeId, out var reason);
                row.SourcePlaceId = placeId;
                if (row.Authority == OpeningPlacementAuthority.Unresolved)
                    row.FailureReason = reason;
                into.Add(row);
            }

            // ---- pass 2：place 内 slot index ----
            // 关键：slot 必须由 **checked-in anchor** 的 authored 顺序决定，绝不能由「当前
            // 在场 population」决定。Party 成员在 startup invariant 运行时已是 AtWorldPosition
            // （不属于 site population），若按在场者排序就会整体错位 —— 制作人 Play 实测的
            // 「village_recruit slot=2 却等于主角 anchor」就是这么来的。
            var authoredSlots = BuildAuthoredSlotRegistry(surface, site.SiteId);
            var byPlace = new Dictionary<string, List<OpeningPlacementPlanRow>>(StringComparer.Ordinal);
            for (var i = 0; i < into.Count; i++)
            {
                var row = into[i];
                if (row.Authority == OpeningPlacementAuthority.Unresolved ||
                    string.IsNullOrEmpty(row.SourcePlaceId))
                    continue;
                if (!byPlace.TryGetValue(row.SourcePlaceId, out var list))
                {
                    list = new List<OpeningPlacementPlanRow>(4);
                    byPlace[row.SourcePlaceId] = list;
                }

                list.Add(row);
            }

            foreach (var pair in byPlace)
            {
                var ordered = pair.Value;
                ordered.Sort((a, b) =>
                {
                    var byKey = string.CompareOrdinal(a.SpawnKey, b.SpawnKey);
                    return byKey != 0 ? byKey : a.EntityId.Value.CompareTo(b.EntityId.Value);
                });

                authoredSlots.TryGetValue(pair.Key, out var authoredOrder);
                var taken = new HashSet<string>(StringComparer.Ordinal);
                if (authoredOrder != null)
                {
                    for (var slot = 0; slot < authoredOrder.Count; slot++)
                    {
                        var spawnKey = authoredOrder[slot];
                        taken.Add(spawnKey);
                        for (var r = 0; r < ordered.Count; r++)
                        {
                            if (string.Equals(ordered[r].SpawnKey, spawnKey, StringComparison.Ordinal))
                            {
                                ordered[r].SlotIndex = slot;
                                break;
                            }
                        }
                    }
                }

                // 未在 anchor 中登记的 spawn（例如尚未重烘的新 spawn）按 spawnKey ordinal 依次补位。
                var next = authoredOrder?.Count ?? 0;
                for (var r = 0; r < ordered.Count; r++)
                {
                    if (taken.Contains(ordered[r].SpawnKey))
                        continue;
                    ordered[r].SlotIndex = next++;
                }
            }

            // ---- pass 3：bake（shared transform）＋ checked-in anchor 对照 ----
            for (var i = 0; i < into.Count; i++)
            {
                var row = into[i];
                if (row.Authority == OpeningPlacementAuthority.Unresolved)
                    continue;

                // authored 点取 checked-in Content（LocalPlaceSet）；WorldRegion 只是镜像，未加载时
                // 不能因此判定「authored place missing」。
                var placeLocal = default(WorldVec2);
                var hasPlaceLocal = ContinuousOutdoorStartupPlanner.TryResolvePlaceLocalPosition(
                    placeSet, row.SourcePlaceId, out var placeLocalX, out var placeLocalZ);
                if (hasPlaceLocal)
                {
                    placeLocal = new WorldVec2(placeLocalX, placeLocalZ);
                }
                else if (world.WorldRegion.TryGet(row.SourcePlaceId, out var placeState) && placeState != null)
                {
                    placeLocal = new WorldVec2(placeState.PresentationX, placeState.PresentationZ);
                    hasPlaceLocal = true;
                }

                if (!hasPlaceLocal)
                {
                    row.FailureReason = "authored place '" + row.SourcePlaceId +
                                        "' missing from content LocalPlaceSet and WorldRegion";
                    row.Authority = OpeningPlacementAuthority.Unresolved;
                    continue;
                }
                var hasExtent = WorldSiteOutdoorOpeningAnchorBake.TryGetBoundPlacementLocalExtent(
                    sourceLayout, row.SourcePlaceId, out var minX, out var minY, out var maxX, out var maxY);
                if (!WorldSiteOutdoorOpeningAnchorBake.TryBakeSlotAnchor(
                        site.OccupiedHexes, hexSize, sourceBounds, placeLocal,
                        hasExtent, minX, minY, maxX, maxY, row.SlotIndex,
                        out var sourceLocal, out var anchor))
                {
                    row.FailureReason = "bake failed for place '" + row.SourcePlaceId +
                                        "' slot " + row.SlotIndex;
                    row.Authority = OpeningPlacementAuthority.Unresolved;
                    continue;
                }

                row.SourceLocalPosition = sourceLocal;
                row.BakedWorldPosition = anchor;
                row.HasBakedPosition = true;
                row.HasCheckedInAnchor = TryGetCheckedInAnchor(
                    surface, site.SiteId, row.SpawnKey, out var checkedIn);
                if (row.HasCheckedInAnchor)
                {
                    row.CheckedInWorldPosition = checkedIn;
                    var dx = checkedIn.X - anchor.X;
                    var dy = checkedIn.Y - anchor.Y;
                    row.AnchorDelta = (float)Math.Sqrt(dx * dx + dy * dy);
                    row.AnchorMatchesBake = row.AnchorDelta <= AnchorBakeEpsilon;
                }
            }

            return true;
        }

        /// <summary>
        /// §5 bake validation：每个 opening spawn 必须存在<b>唯一</b> checked-in
        /// <c>openingEntityAnchors</c>，且 <c>shared bake(source local point) ≈ checked-in anchor</c>。
        /// 「anchor 存在」本身不算通过。
        /// </summary>
        public static bool TryValidateBakedAnchors(
            SimulationWorld world,
            DefinitionRegistry registry,
            OutdoorWorldSurfaceDefinition surface,
            WorldSite site,
            IReadOnlyList<EntityId> population,
            List<OpeningPlacementPlanRow> plan,
            out string failure) =>
            ValidateBakedAnchors(world, registry, surface, site, population, plan, out failure) !=
            OpeningAnchorBakeValidationOutcome.Failed;

        /// <summary>
        /// §5 bake validation（带 outcome）。只有真正的 anchor 缺陷才算 <see cref="OpeningAnchorBakeValidationOutcome.Failed"/>；
        /// Content 查不到 authored 真源（LocalPlaceSet／MapLayout）或该 Site 没有 anchor →
        /// <see cref="OpeningAnchorBakeValidationOutcome.Skipped"/>，只在诊断里显示，不打断启动。
        /// </summary>
        public static OpeningAnchorBakeValidationOutcome ValidateBakedAnchors(
            SimulationWorld world,
            DefinitionRegistry registry,
            OutdoorWorldSurfaceDefinition surface,
            WorldSite site,
            IReadOnlyList<EntityId> population,
            List<OpeningPlacementPlanRow> plan,
            out string failure)
        {
            failure = string.Empty;
            var failures = new List<string>();
            if (surface == null || site == null)
            {
                failure = "opening anchor validation inputs missing";
                return OpeningAnchorBakeValidationOutcome.Skipped;
            }

            // 只有 Content 显式声明了该 Site 的 openingEntityAnchors 才校验。普通 Site
            // （例如青石镇）本来就没有 opening anchor，其落点按设计走 BakedSitePlace／resident place，
            // 不能因为在那个 Site 触发 invariant 就误报。
            if (!HasCheckedInAnchors(surface, site.SiteId))
            {
                failure = "no checked-in openingEntityAnchors for site " + site.SiteId;
                return OpeningAnchorBakeValidationOutcome.Skipped;
            }

            // (1) Content 侧：逐个 anchor 复算 shared bake（与在场实体无关）。
            if (!TryValidateAuthoredAnchors(registry, surface, site, out var contentFailure, out var contentResolved))
            {
                if (!contentResolved)
                {
                    // 真源查不到（LocalPlaceSet／MapLayout／HexSize）→ 不是 anchor 缺陷。
                    failure = contentFailure;
                    return OpeningAnchorBakeValidationOutcome.Skipped;
                }

                failures.Add(contentFailure);
            }

            if (!TryBuildPlan(world, registry, surface, site, population, plan, out var planFailure))
            {
                // 真源缺失（LocalPlaceSet／MapLayout 查不到）→ 不是 anchor 缺陷。
                failure = planFailure;
                return OpeningAnchorBakeValidationOutcome.Skipped;
            }

            // (2) 实体侧：GameStart 建立过 stable spawn key 的 opening spawn 必须能查到自己的 anchor。
            // 注意不能按「当前在场」判 slot、也不要求 Authority 可解析 —— Party 成员此时已是
            // AtWorldPosition／可能已走动，anchor 与 bake 的一致性已由 (1) 覆盖。
            for (var i = 0; i < plan.Count; i++)
            {
                var row = plan[i];
                if (!world.OpeningSpawnIdentities.TryGetSpawnKey(row.EntityId, out _))
                    continue; // 非 authored opening spawn（普通 background 居民）不要求有 anchor
                if (row.HasCheckedInAnchor)
                    continue;
                failures.Add("authored opening spawn " + row.SpawnKey + " (" + row.DefinitionId +
                             ") has no checked-in openingEntityAnchor at site " + site.SiteId);
            }

            if (failures.Count > 0)
            {
                failure = string.Join(" | ", failures);
                return OpeningAnchorBakeValidationOutcome.Failed;
            }

            return OpeningAnchorBakeValidationOutcome.Validated;
        }

        /// <summary>
        /// Content-authored slot 登记表：<c>sourceLocationId → 该 place 内按 spawnKey ordinal 排序的
        /// spawnKey 列表</c>。slot index 就是列表下标。
        ///
        /// <para>
        /// 它只依赖 checked-in <c>openingEntityAnchors</c>，因此与「当前谁在场」无关 ——
        /// Party 成员/暂时 dematerialize 的人不会让别人的 slot 错位，且 anchor 里声明过但当前
        /// 不在场的 spawn（例如主角）仍占据它自己的 slot。
        /// </para>
        /// </summary>
        public static Dictionary<string, List<string>> BuildAuthoredSlotRegistry(
            OutdoorWorldSurfaceDefinition surface,
            string siteId)
        {
            var registry = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            if (surface?.OpeningEntityAnchors == null || string.IsNullOrEmpty(siteId))
                return registry;
            for (var i = 0; i < surface.OpeningEntityAnchors.Count; i++)
            {
                var anchor = surface.OpeningEntityAnchors[i];
                if (anchor == null || !string.Equals(anchor.SiteId, siteId, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(anchor.SpawnKey) ||
                    string.IsNullOrWhiteSpace(anchor.SourceLocationId))
                    continue;
                if (!registry.TryGetValue(anchor.SourceLocationId, out var list))
                {
                    list = new List<string>(4);
                    registry[anchor.SourceLocationId] = list;
                }

                if (!list.Contains(anchor.SpawnKey))
                    list.Add(anchor.SpawnKey);
            }

            foreach (var pair in registry)
                pair.Value.Sort(StringComparer.Ordinal);
            return registry;
        }

        /// <summary>
        /// Content 侧 §5 校验：**完全不依赖实体／population**，逐个 checked-in anchor 复算
        /// 「authored source local point → shared bake」并比对。
        /// </summary>
        public static bool TryValidateAuthoredAnchors(
            DefinitionRegistry registry,
            OutdoorWorldSurfaceDefinition surface,
            WorldSite site,
            out string failure,
            out bool contentResolved)
        {
            failure = string.Empty;
            contentResolved = false;
            var failures = new List<string>();
            if (surface == null || site == null || registry == null)
            {
                failure = "opening anchor validation inputs missing";
                return false;
            }

            if (!ContinuousOutdoorStartupPlanner.TryResolveSiteSourceLayout(
                    registry, surface, site.SiteId, out var sourceLayout, out var layoutFailure))
            {
                failure = layoutFailure;
                return false;
            }

            if (!ContinuousOutdoorStartupPlanner.TryResolveSiteSourcePlaceSet(
                    registry, surface, site.SiteId, out var placeSet, out var placeSetFailure))
            {
                failure = placeSetFailure;
                return false;
            }

            if (!TryResolveContentHexSize(registry, site.SiteId, out var hexSize))
            {
                failure = "SiteHexSizeUnresolved site=" + site.SiteId;
                return false;
            }

            contentResolved = true;

            var sourceBounds = WorldSiteSpatialMapping.WorldSiteLocalMapBounds.FromOriginSize(
                sourceLayout.OriginX, sourceLayout.OriginY, sourceLayout.CellSize,
                sourceLayout.Width, sourceLayout.Height);
            if (!sourceBounds.IsValid)
            {
                contentResolved = false;
                failure = "source layout bounds invalid for site " + site.SiteId;
                return false;
            }

            var slots = BuildAuthoredSlotRegistry(surface, site.SiteId);
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < surface.OpeningEntityAnchors.Count; i++)
            {
                var anchor = surface.OpeningEntityAnchors[i];
                if (anchor == null || !string.Equals(anchor.SiteId, site.SiteId, StringComparison.Ordinal))
                    continue;
                if (string.IsNullOrWhiteSpace(anchor.SpawnKey))
                {
                    failures.Add("openingEntityAnchor with empty spawnKey for site " + site.SiteId);
                    continue;
                }

                if (seen.TryGetValue(anchor.SpawnKey, out var previous))
                {
                    failures.Add("duplicate openingEntityAnchor spawnKey '" + anchor.SpawnKey +
                                 "' (entries " + previous + " and " + i + ")");
                    continue;
                }

                seen[anchor.SpawnKey] = i;

                if (string.IsNullOrWhiteSpace(anchor.SourceLocationId))
                {
                    failures.Add("anchor " + anchor.SpawnKey + " has no sourceLocationId");
                    continue;
                }

                if (!ContinuousOutdoorStartupPlanner.TryResolvePlaceLocalPosition(
                        placeSet, anchor.SourceLocationId, out var placeLocalX, out var placeLocalZ))
                {
                    failures.Add("anchor " + anchor.SpawnKey + " references authored place '" +
                                 anchor.SourceLocationId + "' missing from content LocalPlaceSet");
                    continue;
                }

                if (slots == null || !slots.TryGetValue(anchor.SourceLocationId, out var ordered))
                {
                    failures.Add("anchor " + anchor.SpawnKey + " place '" + anchor.SourceLocationId +
                                 "' has no authored slot registry");
                    continue;
                }

                var slot = ordered.IndexOf(anchor.SpawnKey);
                if (slot < 0)
                {
                    failures.Add("anchor " + anchor.SpawnKey + " not present in its own place slot registry");
                    continue;
                }

                var placeLocal = new WorldVec2(placeLocalX, placeLocalZ);
                var hasExtent = WorldSiteOutdoorOpeningAnchorBake.TryGetBoundPlacementLocalExtent(
                    sourceLayout, anchor.SourceLocationId, out var minX, out var minY, out var maxX, out var maxY);
                if (!WorldSiteOutdoorOpeningAnchorBake.TryBakeSlotAnchor(
                        site.OccupiedHexes, hexSize, sourceBounds, placeLocal,
                        hasExtent, minX, minY, maxX, maxY, slot,
                        out var sourceLocal, out var expected))
                {
                    failures.Add("anchor " + anchor.SpawnKey + " bake failed (place=" +
                                 anchor.SourceLocationId + " slot=" + slot + ")");
                    continue;
                }

                var dx = expected.X - anchor.WorldX;
                var dy = expected.Y - anchor.WorldY;
                var delta = (float)Math.Sqrt(dx * dx + dy * dy);
                if (delta > AnchorBakeEpsilon)
                {
                    failures.Add("anchor " + anchor.SpawnKey + " (" +
                                 anchor.WorldX.ToString("F6") + "," + anchor.WorldY.ToString("F6") +
                                 ") != shared bake (" + expected.X.ToString("F6") + "," +
                                 expected.Y.ToString("F6") + ") from source local (" +
                                 sourceLocal.X.ToString("F4") + "," + sourceLocal.Y.ToString("F4") +
                                 ") place=" + anchor.SourceLocationId + " slot=" + slot +
                                 " delta=" + delta.ToString("F6"));
                }
            }

            if (failures.Count > 0)
            {
                failure = string.Join(" | ", failures);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Site 所属 HexWorld 的 <c>hexSize</c>（anchor bake 的 domain 由此决定）。
        /// 多张地图含同一 Site → 拒绝（不猜）。
        /// </summary>
        public static bool TryResolveContentHexSize(
            DefinitionRegistry registry,
            string siteId,
            out float hexSize)
        {
            hexSize = 0f;
            if (registry?.HexWorldContents == null || string.IsNullOrEmpty(siteId))
                return false;
            var matches = 0;
            var found = 0f;
            foreach (var entry in registry.HexWorldContents)
            {
                var world = entry.Value;
                if (world?.Sites == null)
                    continue;
                for (var i = 0; i < world.Sites.Count; i++)
                {
                    var site = world.Sites[i];
                    if (site == null || !string.Equals(site.SiteId, siteId, StringComparison.Ordinal))
                        continue;
                    matches++;
                    found = world.HexSize > 0.0001f ? world.HexSize : HexWorldScale.DefaultHexOuterRadius;
                    break;
                }
            }

            if (matches != 1)
                return false;
            hexSize = found;
            return true;
        }

        /// <summary>该 Site 在 checked-in Content 里是否声明了 openingEntityAnchors。</summary>
        public static bool HasCheckedInAnchors(OutdoorWorldSurfaceDefinition surface, string siteId)
        {
            if (surface?.OpeningEntityAnchors == null || string.IsNullOrEmpty(siteId))
                return false;
            for (var i = 0; i < surface.OpeningEntityAnchors.Count; i++)
            {
                var anchor = surface.OpeningEntityAnchors[i];
                if (anchor != null && string.Equals(anchor.SiteId, siteId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public static bool TryGetCheckedInAnchor(
            OutdoorWorldSurfaceDefinition surface,
            string siteId,
            string spawnKey,
            out WorldVec2 anchor)
        {
            anchor = default;
            if (surface?.OpeningEntityAnchors == null || string.IsNullOrEmpty(siteId) ||
                string.IsNullOrEmpty(spawnKey))
                return false;
            for (var i = 0; i < surface.OpeningEntityAnchors.Count; i++)
            {
                var entry = surface.OpeningEntityAnchors[i];
                if (entry == null || !string.Equals(entry.SiteId, siteId, StringComparison.Ordinal))
                    continue;
                if (!string.Equals(entry.SpawnKey, spawnKey, StringComparison.Ordinal))
                    continue;
                anchor = new WorldVec2(entry.WorldX, entry.WorldY);
                return true;
            }

            return false;
        }
    }
}
