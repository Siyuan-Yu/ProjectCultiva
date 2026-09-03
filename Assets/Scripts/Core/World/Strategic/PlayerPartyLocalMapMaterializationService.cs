using System;
using System.Collections.Generic;
using XianXia.Core.Combat;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;
using XianXia.Core.Exploration;
using XianXia.Core.Persistence;
using XianXia.Core.Results;
using XianXia.Core.Simulation;
using XianXia.Core.World;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
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
        public static Result MaterializePartyOnResolvedLocalMap(
            SimulationWorld world,
            IReadOnlyList<EntityId> partyMembers) =>
            MaterializePartyOnResolvedLocalMap(world, partyMembers, null, null, PlayerPartySiteMaterializeMode.Default);

        /// <param name="wildernessPlayableBounds">
        /// Wilderness 展开时用于 WorldPosition→Local 投影的可玩矩形；Site 展开可省略。
        /// </param>
        public static Result MaterializePartyOnResolvedLocalMap(
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
        public static Result MaterializePartyOnResolvedLocalMap(
            SimulationWorld world,
            IReadOnlyList<EntityId> partyMembers,
            WildernessLocalWorldProjection.WildernessLocalMapBounds? wildernessPlayableBounds,
            WorldSiteSpatialMapping.WorldSiteLocalMapBounds? siteBounds,
            PlayerPartySiteMaterializeMode siteMode)
        {
            if (world?.LocalMap == null || partyMembers == null || partyMembers.Count == 0)
                return Result.Failure(ErrorCode.InvalidOperation, "Materialize no-op: invalid world/party members.");

            var mapId = world.PartyWorld?.LocalMapId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(mapId))
                return Result.Failure(ErrorCode.InvalidOperation, "Materialize no-op: no PartyWorld.LocalMapId.");

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
                        {
                            if (!motion.TryUpdateWorldPositionWithinSite(motion.SiteId, bootstrapped))
                            {
                                PlayerPartySiteIngressTrace.Log(
                                    "BootstrapFailed",
                                    "canonical commit rejected (site=" + motion.SiteId + " kind=" +
                                    motion.LocationKind + ") -> token NOT consumed");
                                return Result.Failure(
                                    ErrorCode.InvalidOperation,
                                    "Initial site bootstrap: canonical commit rejected.");
                            }
                        }
                        else
                        {
                            PlayerPartySiteIngressTrace.Log(
                                "BootstrapFailed",
                                "local->world mapping failed (startLocal=" + px + "," + pz +
                                ") -> token NOT consumed");
                            return Result.Failure(
                                ErrorCode.InvalidOperation,
                                "Initial site bootstrap: local->world mapping failed.");
                        }

                        break;
                    case PlayerPartySiteMaterializeMode.ProjectCanonicalWorldToLocal:
                        // Phase 5R-B3B.4.2：数值核实 —— 打印完整输入面，回答"为什么 boundary 映射到这个
                        // local 点"（footprint domain / normalized u,v / MapLayout bounds / startLoc）。
                        var w2lBoundary = motion.HasPosition ? motion.WorldPosition : default;
                        var w2lStartLoc = hasStart && startLoc != null
                            ? startLoc.PresentationX.ToString("0.###") + "," +
                              startLoc.PresentationZ.ToString("0.###")
                            : "n/a";
                        WorldSiteSpatialMapping.TryComputeFootprintWorldDomain(
                            siteCtx, hexSize,
                            out var w2lDomMinX, out var w2lDomMaxX,
                            out var w2lDomMinY, out var w2lDomMaxY);
                        var w2lDomW = w2lDomMaxX - w2lDomMinX;
                        var w2lDomH = w2lDomMaxY - w2lDomMinY;
                        var w2lU = w2lDomW > 0.0001f ? (w2lBoundary.X - w2lDomMinX) / w2lDomW : 0f;
                        var w2lV = w2lDomH > 0.0001f ? (w2lBoundary.Y - w2lDomMinY) / w2lDomH : 0f;
                        if (motion.HasPosition &&
                            WorldSiteSpatialMapping.TryWorldSurfaceToLocal(
                                siteCtx, siteBounds.Value, motion.WorldPosition, hexSize, out var projected))
                        {
                            PlayerPartySiteIngressTrace.Log(
                                "WorldToLocal",
                                "success=true" +
                                " boundary=" + w2lBoundary +
                                " footprintHexes=" + (siteCtx != null ? siteCtx.OccupiedHexes.Count.ToString() : "n/a") +
                                " domain=[" + w2lDomMinX.ToString("0.###") + "," + w2lDomMaxX.ToString("0.###") +
                                "," + w2lDomMinY.ToString("0.###") + "," + w2lDomMaxY.ToString("0.###") + "]" +
                                " u=" + w2lU.ToString("0.###") + " v=" + w2lV.ToString("0.###") +
                                " bounds=[" + siteBounds.Value.MinX.ToString("0.###") + "," +
                                siteBounds.Value.MaxX.ToString("0.###") + "," +
                                siteBounds.Value.MinY.ToString("0.###") + "," +
                                siteBounds.Value.MaxY.ToString("0.###") + "]" +
                                " local=" + projected +
                                " startLoc=" + w2lStartLoc);
                            px = projected.X;
                            pz = projected.Y;
                            hasStart = false;

                            // Phase 5R-B3C1.2：Safe Ingress Landing —— 真实 boundary local 已映射到
                            // 目标图边缘（WorldToLocal 正确，非 mapping 问题）；此处按 Transition
                            // policy 沿正式 ingress 的 inward 方向推进到 SafeInterior
                            // （!IsInExitTriggerBand && IsInSafeInterior）最小点。
                            // 不修改 Mapping 数学（boundary↔local 关系不变）；Landing 是 Transition
                            // policy，两层分离。inward 来自正式 SurfaceExitConnection（跨 ingress 保存于
                            // SurfaceEdgeGate，见 PlayerPartySurfaceEdgeGate.SetIngressContext）：
                            // SourceHex=footprint、DestinationHex=来向荒野 → LocalDirection 指向出口
                            // （outward），inward = -LocalDirection。绝不按 CurrentHex / WorldToHex /
                            // Anchor / Presence 重猜入口方向。目标矩形内缩
                            // inset = max(NearEdgeMargin, NormalizeDepth(depth))，均由现有正式几何
                            // 决定，无 magic 数值。
                            var landingOk = false;
                            var landingX = px;
                            var landingY = pz;
                            var landingBounds = new WildernessLocalWorldProjection.WildernessLocalMapBounds(
                                siteBounds.Value.MinX,
                                siteBounds.Value.MaxX,
                                siteBounds.Value.MinY,
                                siteBounds.Value.MaxY);
                            var landingDepth = SurfaceExitZoneCalculator.ResolveDepthFromSession(
                                world, landingBounds);
                            var landingInsetX = Math.Max(
                                WildernessLocalWorldProjection.NearEdgeMarginX(landingBounds), landingDepth);
                            var landingInsetY = Math.Max(
                                WildernessLocalWorldProjection.NearEdgeMarginY(landingBounds), landingDepth);
                            var ingressGate = motion.SurfaceEdgeGate;
                            if (ingressGate != null && ingressGate.HasIngressContext)
                            {
                                landingOk = WildernessLocalWorldProjection.TryResolveSafeIngressLanding(
                                    landingBounds,
                                    landingInsetX,
                                    landingInsetY,
                                    px,
                                    pz,
                                    -ingressGate.IngressDirectionLocalX,
                                    -ingressGate.IngressDirectionLocalY,
                                    out landingX,
                                    out landingY);
                            }

                            if (!landingOk)
                            {
                                // 无正式 ingress context（WorldMap reopen 等非 EdgeTransition 进入）或
                                // 推进失败：fallback 用现有正式 Entry Inset 算法（方向 = Entry 边）。
                                if (ingressGate != null && ingressGate.LastExitDirection >= 0)
                                {
                                    WildernessLocalWorldProjection.GetLocalPositionNearEdge(
                                        landingBounds,
                                        WildernessLocalWorldProjection.OppositeDirection(
                                            ingressGate.LastExitDirection),
                                        out landingX,
                                        out landingY);
                                }
                            }

                            px = landingX;
                            pz = landingY;

                            // Canonical 与 Local 保持一一对应：landingLocal → LocalToWorld →
                            // motion.WorldPosition（不产生双真源）。BoundaryContact → AtSite commit →
                            // 沿 inward 向目的地内部推进最小 landing 距离。
                            if (WorldSiteSpatialMapping.TryLocalToWorldSurface(
                                    siteCtx, siteBounds.Value, new WorldVec2(px, pz), hexSize,
                                    out var landingWorld))
                            {
                                if (motion.TryUpdateWorldPositionWithinSite(motion.SiteId, landingWorld))
                                {
                                    PlayerPartySiteIngressTrace.Log(
                                        "SafeLanding",
                                        "local=" + px.ToString("0.###") + "," + pz.ToString("0.###") +
                                        " world=" + landingWorld);
                                }
                            }
                        }
                        else
                        {
                            PlayerPartySiteIngressTrace.Log(
                                "WorldToLocal",
                                "success=false" +
                                " boundary=" + w2lBoundary +
                                " footprintHexes=" + (siteCtx != null ? siteCtx.OccupiedHexes.Count.ToString() : "n/a") +
                                " domain=[" + w2lDomMinX.ToString("0.###") + "," + w2lDomMaxX.ToString("0.###") +
                                "," + w2lDomMinY.ToString("0.###") + "," + w2lDomMaxY.ToString("0.###") + "]" +
                                " u=" + w2lU.ToString("0.###") + " v=" + w2lV.ToString("0.###") +
                                " boundsValid=" + siteBounds.Value.IsValid +
                                " startLoc=" + w2lStartLoc);
                            // Phase 5R-B3B.5：正式 Wilderness→Site ingress 的 mapping failure
                            // <b>不得静默 fallback DefaultStart</b>（px/pz 保持 StartLocation 会让角色
                            // "看起来还能正常出生"，掩盖 mapping bug）。明确失败：不写任何 StartLocation
                            // 派生位置，保留可诊断状态，等上游修正（footprint / bounds / site id /
                            // boundary 归属）。LegacyRestore 的隔离 fallback 不受影响。
                            PlayerPartySiteIngressTrace.Log(
                                "IngressAborted",
                                "reason=WorldToLocal failure (no DefaultStart fallback) site=" +
                                (world.PartyWorld?.SiteId ?? string.Empty));
                            PlayerPartySiteIngressTrace.EndIngress();
                            return Result.Failure(
                                ErrorCode.InvalidOperation,
                                "Site ingress WorldToLocal failure (no DefaultStart fallback).");
                        }

                        break;
                    case PlayerPartySiteMaterializeMode.LegacyRestoreLocal:
                        break; // per-member：见循环内 i==0 的 snapshot placement bootstrap。
                }
            }

            var materializedIds = new List<EntityId>(partyMembers.Count);
            for (var i = 0; i < partyMembers.Count; i++)
            {
                var id = partyMembers[i];
                if (id.IsNone)
                    continue;

                // Incapacitated / Corpse（非 Alive）成员：逻辑上仍 party.IsMember，但已不属于
                // 「正在随队旅行的人」——不由本 service 生成（由 StrategicResidual 在倒下 hex 负责，
                // 见 LocalCombatCasualtyHandoffService / LoadedStrategicPopulationMaterializer）。
                // entity 缺失同样无法 materialize 表现，跳过。
                if (!world.Entities.TryGet(id, out var materializeEnt) || materializeEnt == null ||
                    !CombatLifeStateService.CanFight(materializeEnt))
                    continue;
                materializedIds.Add(id);

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
                var isFollower = i > 0;
                var fromSnapshotPlacement = false;
                if (useWildernessProjection)
                {
                    if (isFollower)
                        ApplyPartyFormationOffset(i, ref memberX, ref memberZ);
                }
                else
                {
                    // WorldSite：Snapshot 保留每个 member 自己的 Saved 落点（不加 offset）；
                    // DefaultStart / ProjectCanonical fresh placement 的 follower 必须加 formation
                    // offset —— 否则 Active + Followers 全部落在 px,pz 同一点（root cause 1）。
                    fromSnapshotPlacement =
                        LoadedLocalMapPlacementSnapshotRestore.TryResolveWorldSiteSpawnPosition(
                            id,
                            mapId,
                            px,
                            pz,
                            out memberX,
                            out memberZ,
                            out placementSource);
                    if (!fromSnapshotPlacement && isFollower)
                        ApplyPartyFormationOffset(i, ref memberX, ref memberZ);
                }

                // Formation candidate 必须留在 SafeInterior（不得因 offset 被推回边缘 / exit band）。
                // Snapshot placement 不 clamp —— 除非 Host safety validator 判定 invalid/unwalkable。
                if (isFollower && !fromSnapshotPlacement)
                {
                    if (useWildernessProjection && wildernessPlayableBounds.HasValue)
                    {
                        ClampFormationCandidateToSafeInterior(
                            memberX, memberZ, wildernessPlayableBounds.Value, ref memberX, ref memberZ);
                    }
                    else if (isSiteExpand && siteBounds.HasValue)
                    {
                        var sitePlacementBounds =
                            new WildernessLocalWorldProjection.WildernessLocalMapBounds(
                                siteBounds.Value.MinX,
                                siteBounds.Value.MaxX,
                                siteBounds.Value.MinY,
                                siteBounds.Value.MaxY);
                        ClampFormationCandidateToSafeInterior(
                            memberX, memberZ, sitePlacementBounds, ref memberX, ref memberZ);
                    }
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

                PlayerPartyTransitionMembership.LogMaterializeMember(
                    id,
                    mapId,
                    spawned: true,
                    followReboundHint: isFollower);
            }

            // 清空 ingress trace id（保持与下一次 ingress 隔离）。
            PlayerPartySiteIngressTrace.EndIngress();

            // 保持 TravelingMembers 与当前展开队伍对齐（仅真正 materialized 的 living 成员；
            // 弥留/尸体由 StrategicResidual 管，绝不写回 TravelingMembers）。
            if (world.PlayerPartyTravel != null)
                world.PlayerPartyTravel.CaptureTravelingMembers(materializedIds);

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
                     world.PlayerPartyTravel.SurfaceEdgeGate.TransitionInProgress &&
                     siteBounds.HasValue)
            {
                // Phase 5R-B3C1.2：删除 fake 40×40 —— 用真实 Site bounds（同 min/max）完成 Gate。
                // px/pz 已是 Safe Landing（SafeInterior 内），CompleteEdgeTransitionPresentation
                // 不会重算 → Gate.LastLocal == 角色真实落点（禁止角色在 boundary 而 Gate 记另一个点）。
                var realSiteBounds = new WildernessLocalWorldProjection.WildernessLocalMapBounds(
                    siteBounds.Value.MinX,
                    siteBounds.Value.MaxX,
                    siteBounds.Value.MinY,
                    siteBounds.Value.MaxY);
                PlayerPartyWildernessTransitionService.CompleteEdgeTransitionPresentation(
                    world, realSiteBounds, px, pz);
            }

            // IngressContext 是 one-shot：本次 destination materialize + final landing 完成即消费，
            // 防止 WorldSite→WorldSite / 无新 SetIngressContext 的 materialize 读到旧 ingress direction。
            world.PlayerPartyTravel?.SurfaceEdgeGate?.ConsumeIngressContext();

            return Result.Success();
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

                // WorldSite 调用方曾传 materializeBounds 而实际使用 siteBounds 导致漏查；
                // 现统一按传入的实际 bounds 同时断言 SafeInterior（Core 不查 WalkGrid）。
                if (!WildernessLocalWorldProjection.IsInSafeInterior(x, z, b))
                {
                    error = "Active presentation outside SafeInterior (near-edge band).";
                    return false;
                }
            }

            return true;
        }

        static void ApplyPartyFormationOffset(int memberIndex, ref float x, ref float z)
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
        /// 通用纯函数：formation candidate 收敛到 SafeInterior（wilderness / WorldSite 通用，
        /// 用同一正式几何 NearEdgeMarginX/Y，无 magic world distance）。先 clamp 到近缘带内侧，
        /// 若仍不在 SafeInterior（极小图）则回退 bounds 中心。
        /// </summary>
        public static void ClampFormationCandidateToSafeInterior(
            float x,
            float z,
            WildernessLocalWorldProjection.WildernessLocalMapBounds bounds,
            ref float outX,
            ref float outZ)
        {
            var marginX = WildernessLocalWorldProjection.NearEdgeMarginX(bounds);
            var marginY = WildernessLocalWorldProjection.NearEdgeMarginY(bounds);
            var loX = bounds.MinX + marginX;
            var hiX = bounds.MaxX - marginX;
            var loY = bounds.MinY + marginY;
            var hiY = bounds.MaxY - marginY;
            outX = x < loX ? loX : (x > hiX ? hiX : x);
            outZ = z < loY ? loY : (z > hiY ? hiY : z);
            if (!WildernessLocalWorldProjection.IsInSafeInterior(outX, outZ, bounds))
            {
                outX = bounds.CenterX;
                outZ = bounds.CenterY;
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
