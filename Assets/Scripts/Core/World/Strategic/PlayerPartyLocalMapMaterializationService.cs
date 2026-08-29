using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Persistence;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase 5R-B2：Site LocalMap Materialization 初始化 ownership 的 transient 模式。
    /// 只在 Materialize 调用阶段区分「第一次建立 Canonical WorldPosition」的来源；
    /// 不落盘、不进入 <see cref="PlayerPartyWorldMotion"/>、不是长期位置真源。
    /// </summary>
    public enum PlayerPartySiteMaterializeMode
    {
        /// <summary>不改变既有行为（非 Site 展开或调用方未指定）。</summary>
        Default = 0,
        /// <summary>NewGame 首次出生在 Site：StartLocation Local → LocalToWorld → 建立真实 Canonical。</summary>
        BootstrapFromAuthoredLocal = 1,
        /// <summary>已有可信 Canonical（Wilderness→Site 进入 / WorldMap 重开 / 新格式 save）：World → WorldToLocal。</summary>
        ProjectCanonicalWorldToLocal = 2,
        /// <summary>Legacy save 无可信 Canonical：仅当 snapshot 提供 Local placement 时 Local → LocalToWorld 一次性 bootstrap。</summary>
        LegacyRestoreLocal = 3,
    }

    /// <summary>
    /// World Position → Resolve LocalMap 之后：把当前 PlayerParty Materialize 到该次 LocalMap 表现。
    /// Site / Wilderness 共用；不创建 Character／Army，不重置 Party Membership／Active。
    /// </summary>
    public static class PlayerPartyLocalMapMaterializationService
    {
        /// <summary>
        /// 将 party 成员落到当前 PartyWorld.LocalMapId 对应的近景（startLocation 或原点）。
        /// 调用前须已设置 PartyWorld.LocalMapId（以及 AtSite／AtHex WorldPresence）。
        /// </summary>
        public static void MaterializePartyOnResolvedLocalMap(
            SimulationWorld world,
            IReadOnlyList<EntityId> partyMembers) =>
            MaterializePartyOnResolvedLocalMap(world, partyMembers, null, null, PlayerPartySiteMaterializeMode.Default);

        /// <param name="wildernessPlayableBounds">
        /// Wilderness 展开时用于 WorldPosition→Local 投影的可玩矩形；Site 展开可省略。
        /// </param>
        public static void MaterializePartyOnResolvedLocalMap(
            SimulationWorld world,
            IReadOnlyList<EntityId> partyMembers,
            WildernessLocalWorldProjection.WildernessLocalMapBounds? wildernessPlayableBounds) =>
            MaterializePartyOnResolvedLocalMap(
                world, partyMembers, wildernessPlayableBounds, null, PlayerPartySiteMaterializeMode.Default);

        /// <param name="siteBounds">
        /// Phase 5R-B2：Site 展开时由 Data/Unity 调用层从真实 MapLayoutDefinition 构造的
        /// <see cref="WorldSiteSpatialMapping.WorldSiteLocalMapBounds"/>（Core 不引用 Data 层）。
        /// </param>
        /// <param name="siteMode">Phase 5R-B2：Site 初始化 ownership（transient，见 <see cref="PlayerPartySiteMaterializeMode"/>）。</param>
        public static void MaterializePartyOnResolvedLocalMap(
            SimulationWorld world,
            IReadOnlyList<EntityId> partyMembers,
            WildernessLocalWorldProjection.WildernessLocalMapBounds? wildernessPlayableBounds,
            WorldSiteSpatialMapping.WorldSiteLocalMapBounds? siteBounds,
            PlayerPartySiteMaterializeMode siteMode)
        {
            if (world?.LocalMap == null || partyMembers == null || partyMembers.Count == 0)
                return;

            var mapId = world.PartyWorld?.LocalMapId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(mapId))
                return;

            world.LocalMap.ActiveMapLayoutId = mapId;
            world.LocalMap.OverworldMapLayoutId = mapId;

            var startId = world.WorldRegion != null ? world.WorldRegion.StartLocationId : string.Empty;
            WorldLocationState startLoc = null;
            var hasStart = !string.IsNullOrEmpty(startId) &&
                           world.WorldRegion != null &&
                           world.WorldRegion.TryGet(startId, out startLoc) &&
                           startLoc != null;
            var px = hasStart ? startLoc.PresentationX : 0f;
            var pz = hasStart ? startLoc.PresentationZ : 0f;

            var motion = world.PlayerPartyTravel;
            var hexSize = world.HexWorld != null && world.HexWorld.HexSize > 0f
                ? world.HexWorld.HexSize
                : 1f;
            // Phase 5B Mid-Segment LocalVisible: project continuous presentation (incl. site-departure).
            // Idle materialize keeps AtWorldPosition-only rule.
            var midTravelLocalVisible = motion != null &&
                                        motion.IsMoving &&
                                        motion.ExecutionMode == PlayerPartyTravelExecutionMode.LocalVisible;
            var useWildernessProjection = IsWildernessLocalExpand(world) &&
                                          motion != null &&
                                          motion.HasPosition &&
                                          wildernessPlayableBounds.HasValue &&
                                          (motion.LocationKind == PlayerPartyLocationKind.AtWorldPosition ||
                                           midTravelLocalVisible);
            var projectWorld = motion != null && motion.IsMoving
                ? motion.ResolveTravelPresentationWorld(hexSize)
                : (motion != null ? motion.WorldPosition : default);
            if (useWildernessProjection &&
                WildernessLocalWorldProjection.TryProjectWorldToLocal(
                    motion.CurrentHex,
                    projectWorld,
                    wildernessPlayableBounds.Value,
                    hexSize,
                    out var activeLocalX,
                    out var activeLocalY))
            {
                px = activeLocalX;
                pz = activeLocalY;
                hasStart = false;

                // Edge Transition 后：若投影仍落在近缘带，推到 Entry Interior Inset。
                var gate = motion.SurfaceEdgeGate;
                if (gate != null &&
                    (gate.TransitionInProgress || !gate.EdgeArmed) &&
                    gate.LastExitDirection >= 0 &&
                    !WildernessLocalWorldProjection.IsInSafeInterior(
                        px, pz, wildernessPlayableBounds.Value))
                {
                    var currentHex = motion.CurrentHex;
                    var cameFromHex = HexMath.Neighbor(
                        currentHex,
                        WildernessLocalWorldProjection.OppositeDirection(gate.LastExitDirection));
                    var depth = SurfaceExitZoneCalculator.ResolveDepthFromSession(
                        world, wildernessPlayableBounds.Value);
                    WildernessLocalWorldProjection.GetLocalPositionNearEdge(
                        wildernessPlayableBounds.Value,
                        currentHex,
                        cameFromHex,
                        hexSize,
                        depth,
                        out px,
                        out pz);
                }
            }
            else
            {
                useWildernessProjection = false;
            }

            // Phase 5R-B2：Site Spatial Initialization Handshake（transient，不落盘、不成为长期真源）。
            // ownership：NewGame = Local→World bootstrap（StartLocation 为准，拒绝 legacy presenceHex 覆盖）；
            // ExistingCanonical = World→Local（startLocation/default spawn 仅作 fallback）；
            // LegacyRestore = 仅 snapshot local placement 时 Local→World 一次性 bootstrap（见循环内 i==0）。
            WorldSite siteCtx = null;
            var isSiteExpand = !string.IsNullOrWhiteSpace(world.PartyWorld?.SiteId) &&
                               motion != null &&
                               motion.LocationKind == PlayerPartyLocationKind.AtWorldSite &&
                               !string.IsNullOrEmpty(motion.SiteId) &&
                               world.Strategic?.Sites != null &&
                               world.Strategic.Sites.TryGet(motion.SiteId, out siteCtx) &&
                               siteCtx != null &&
                               siteBounds.HasValue && siteBounds.Value.IsValid;
            if (isSiteExpand)
            {
                switch (siteMode)
                {
                    case PlayerPartySiteMaterializeMode.BootstrapFromAuthoredLocal:
                        if (WorldSiteSpatialMapping.TryLocalToWorldSurface(
                                siteCtx, siteBounds.Value, new WorldVec2(px, pz), hexSize, out var bootstrapped))
                            motion.TryUpdateWorldPositionWithinSite(motion.SiteId, bootstrapped);
                        break;
                    case PlayerPartySiteMaterializeMode.ProjectCanonicalWorldToLocal:
                        if (motion.HasPosition &&
                            WorldSiteSpatialMapping.TryWorldSurfaceToLocal(
                                siteCtx, siteBounds.Value, motion.WorldPosition, hexSize, out var projected))
                        {
                            px = projected.X;
                            pz = projected.Y;
                            hasStart = false;
                        }
                        break;
                    case PlayerPartySiteMaterializeMode.LegacyRestoreLocal:
                        break; // per-member：见循环内 i==0 的 snapshot placement bootstrap。
                }
            }

            for (var i = 0; i < partyMembers.Count; i++)
            {
                var id = partyMembers[i];
                if (id.IsNone)
                    continue;

                world.LocalMap.AddOccupant(id);

                if (!world.Entities.TryGet(id, out var ent) || ent == null)
                    continue;

                if (!ent.TryGet<EntityLocationComponent>(out var loc) || loc == null)
                {
                    loc = new EntityLocationComponent();
                    ent.AddComponent(loc);
                }

                var memberX = px;
                var memberZ = pz;
                var placementSource = LoadedLocalMapPlacementSnapshotRestore.SpawnPlacementSource.DefaultStart;
                if (useWildernessProjection)
                {
                    ApplyFollowerPresentationOffset(i, ref memberX, ref memberZ);
                }
                else
                {
                    LoadedLocalMapPlacementSnapshotRestore.TryResolveWorldSiteSpawnPosition(
                        id,
                        mapId,
                        px,
                        pz,
                        out memberX,
                        out memberZ,
                        out placementSource);
                }

                // Phase 5R-B2：Legacy save 无可信 Canonical → 仅当 snapshot 提供了 Local placement 时，
                // 用主控（i==0）的 snapshot local 一次性 Local→World bootstrap Canonical。
                if (i == 0 && isSiteExpand &&
                    siteMode == PlayerPartySiteMaterializeMode.LegacyRestoreLocal &&
                    placementSource ==
                    LoadedLocalMapPlacementSnapshotRestore.SpawnPlacementSource.SnapshotLocalPlacement &&
                    WorldSiteSpatialMapping.TryLocalToWorldSurface(
                        siteCtx, siteBounds.Value, new WorldVec2(memberX, memberZ), hexSize,
                        out var legacyRestored))
                {
                    motion.TryUpdateWorldPositionWithinSite(motion.SiteId, legacyRestored);
                }

                if (hasStart &&
                    placementSource ==
                    LoadedLocalMapPlacementSnapshotRestore.SpawnPlacementSource.DefaultStart)
                    loc.LocationId = startId;
                loc.SetPresentationOverride(memberX, memberZ);

                var isFollower = i > 0;
                PlayerPartyTransitionMembership.LogMaterializeMember(
                    id,
                    mapId,
                    spawned: true,
                    followReboundHint: isFollower);
            }

            // 保持 TravelingMembers 与当前展开队伍对齐，供可见性／后续 Close→Expand 复用。
            if (world.PlayerPartyTravel != null)
                world.PlayerPartyTravel.CaptureTravelingMembers(partyMembers);

            // Edge Gate：Materialize 完成后 Disarm（不改 WorldPosition）。
            if (useWildernessProjection &&
                wildernessPlayableBounds.HasValue &&
                world.PlayerPartyTravel?.SurfaceEdgeGate != null &&
                world.PlayerPartyTravel.SurfaceEdgeGate.TransitionInProgress)
            {
                PlayerPartyWildernessTransitionService.CompleteEdgeTransitionPresentation(
                    world, wildernessPlayableBounds.Value, px, pz);
            }
            else if (world.PlayerPartyTravel?.SurfaceEdgeGate != null &&
                     world.PlayerPartyTravel.SurfaceEdgeGate.TransitionInProgress)
            {
                // Site 展开：用 start / 原点完成 Gate。
                PlayerPartyWildernessTransitionService.CompleteEdgeTransitionPresentation(
                    world,
                    WildernessLocalWorldProjection.WildernessLocalMapBounds.FromOriginSize(
                        -20f, -20f, 1f, 40, 40),
                    px,
                    pz);
            }
        }

        /// <summary>
        /// Active 在当前已展开 LocalMap 上应恰好有一份 presentation，且落点在可玩矩形内。
        /// </summary>
        public static bool TryAssertActiveMaterializedOnce(
            SimulationWorld world,
            EntityId activeId,
            WildernessLocalWorldProjection.WildernessLocalMapBounds? playableBounds,
            out string error)
        {
            error = string.Empty;
            if (world?.LocalMap == null || activeId.IsNone)
            {
                error = "Invalid world/active.";
                return false;
            }

            var loaded = world.LocalMap.ActiveMapLayoutId?.Trim() ?? string.Empty;
            var focus = world.PartyWorld?.LocalMapId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(loaded) || !string.Equals(loaded, focus, System.StringComparison.Ordinal))
            {
                error = "Active LocalMapId mismatch: loaded=" + loaded + " focus=" + focus;
                return false;
            }

            if (!world.LocalMap.ContainsOccupant(activeId))
            {
                error = "Active not occupant of resolved LocalMap.";
                return false;
            }

            if (!world.Entities.TryGet(activeId, out var ent) ||
                ent == null ||
                !ent.TryGet<EntityLocationComponent>(out var loc) ||
                loc == null ||
                !loc.HasPresentationOverride)
            {
                error = "Active missing LocalMap presentation override.";
                return false;
            }

            if (playableBounds.HasValue)
            {
                var b = playableBounds.Value;
                var x = loc.PresentationOverrideX;
                var z = loc.PresentationOverrideZ;
                if (x < b.MinX - 0.01f || x > b.MaxX + 0.01f ||
                    z < b.MinY - 0.01f || z > b.MaxY + 0.01f)
                {
                    error = "Active presentation outside playable bounds.";
                    return false;
                }
            }

            return true;
        }

        static void ApplyFollowerPresentationOffset(int memberIndex, ref float x, ref float z)
        {
            if (memberIndex == 0)
                return;
            switch (memberIndex % 4)
            {
                case 1:
                    x -= 0.9f;
                    break;
                case 2:
                    x += 0.9f;
                    break;
                case 3:
                    z -= 0.9f;
                    break;
                default:
                    z += 0.9f;
                    break;
            }
        }

        /// <summary>
        /// Wilderness／Hex 展开近景：AtHex 成员是否应显示在当前 Active LocalMap。
        /// </summary>
        public static bool IsWildernessPartyMemberVisibleOnActiveLocalMap(
            SimulationWorld world,
            EntityId characterId,
            WorldAgentPresence presence)
        {
            if (world?.LocalMap == null || characterId.IsNone || presence == null)
                return false;
            if (presence.Mode != PartyWorldPresenceMode.AtHex || !presence.UsesHexPresence)
                return false;

            var activeMap = world.LocalMap.ActiveMapLayoutId?.Trim() ?? string.Empty;
            var focusMap = world.PartyWorld?.LocalMapId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(activeMap) || string.IsNullOrEmpty(focusMap))
                return false;
            if (!string.Equals(activeMap, focusMap, System.StringComparison.Ordinal))
                return false;

            // Wilderness expand：焦点不是 WorldSite（SiteId 空）。
            if (!string.IsNullOrEmpty(world.PartyWorld.SiteId))
                return false;

            var motion = world.PlayerPartyTravel;
            if (motion == null || !motion.HasPosition)
                return false;
            if (presence.ResidualHex != motion.CurrentHex)
                return false;

            var members = motion.TravelingMembers;
            if (members != null && members.Count > 0)
            {
                for (var i = 0; i < members.Count; i++)
                {
                    if (members[i] == characterId)
                        return true;
                }

                return false;
            }

            return world.LocalMap.ContainsOccupant(characterId);
        }

        /// <summary>
        /// 当前展开是否为 Wilderness Fallback（非 Site 焦点）。
        /// </summary>
        public static bool IsWildernessLocalExpand(SimulationWorld world)
        {
            if (world?.PartyWorld == null)
                return false;
            if (string.IsNullOrWhiteSpace(world.PartyWorld.LocalMapId))
                return false;
            return string.IsNullOrEmpty(world.PartyWorld.SiteId);
        }
    }
}
