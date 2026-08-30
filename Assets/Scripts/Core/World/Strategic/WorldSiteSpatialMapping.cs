using System;
using System.Collections.Generic;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Phase 5R-B3A（Unified）→ 5R-B3C3（V2 Kernel Radial）：WorldSite LocalMap 空间 ↔ HexWorld
    /// 连续世界表面空间映射。
    ///
    /// 5R-B3C3 起内部<b>正式切到 <see cref="HexFootprintSpatialGeometry"/>（star-shaped radial）</b>：
    ///  - Local 矩形归一化到 square [-1,1]² → 从 footprint kernel 沿方向 ray → polygon boundary，
    ///    不再经过 AABB 空洞 + nearest projection（V1 collapse 根因，见 5R-B3C2.1 验收 FAIL）。
    ///  - 前提：footprint 必须非空 kernel（star-shaped）。kernel-empty → 明确失败，不 fallback V1。
    ///  - <see cref="TryBuildGeometry"/> 一次性构建 geometry（B4 每帧调用复用）；
    ///    既有 site+bounds API 保留，内部走同一 geometry。
    ///  - 独立投影（最近 footprint 点）降级为 <see cref="ProjectWorldPointToFootprint"/>，不混入
    ///    coordinate mapping 主链。
    ///
    /// 职责边界（ADR-0027 §11）：只负责物理位置映射；不负责 Battle / Travel / PlayerParty Context /
    /// Army / Presence sync / Materialization / Ingress-Egress 行为。
    ///
    /// 设计约束（ADR-0027 + 5R-0.1 修正）：
    ///  - 不依赖 UnityEngine / XianXia.Data —— Core 程序集零引用。
    ///  - 不用 MapLayout 的 absolute coordinate 直接当 HexWorld 坐标；world domain 来自真实
    ///    OccupiedHexes + HexMath Pointy-Top / Odd-R 几何。
    ///  - AnchorHex / PresenceHex / DisplayName / SiteType 不参与物理映射。
    ///  - <b>OccupiedHexes 为空 / kernel 为空时所有 Physical Mapping API 明确失败</b>（不伪造
    ///    AnchorHex fake single-hex physical domain，不静默 fallback）。
    /// </summary>
    public static class WorldSiteSpatialMapping
    {
        /// <summary>
        /// WorldSite LocalMap 真实 playable bounds（LocalMap grid 世界单位）。
        /// localPosition 采用 <b>LocalMap 世界单位坐标</b>（与 MinX/MinY 同系，grid cell 坐标需
        /// 先乘 CellSize 再加 OriginX/OriginY）；归一化只使用相对 bounds。
        /// </summary>
        public readonly struct WorldSiteLocalMapBounds
        {
            public WorldSiteLocalMapBounds(float originX, float originY, float cellSize, int width, int height)
            {
                OriginX = originX;
                OriginY = originY;
                CellSize = cellSize;
                Width = width;
                Height = height;
            }

            public static WorldSiteLocalMapBounds FromOriginSize(
                float originX,
                float originY,
                float cellSize,
                int width,
                int height) => new WorldSiteLocalMapBounds(originX, originY, cellSize, width, height);

            public float OriginX { get; }
            public float OriginY { get; }
            public float CellSize { get; }
            public int Width { get; }
            public int Height { get; }

            public bool IsValid => CellSize > 0.0001f && Width > 0 && Height > 0;

            public float MinX => OriginX;
            public float MaxX => OriginX + Width * CellSize;
            public float MinY => OriginY;
            public float MaxY => OriginY + Height * CellSize;

            public float CenterX => (MinX + MaxX) * 0.5f;
            public float CenterY => (MinY + MaxY) * 0.5f;

            /// <summary>playable 世界跨度（防除零）。</summary>
            public float SpanX => Math.Max(0.0001f, MaxX - MinX);
            public float SpanY => Math.Max(0.0001f, MaxY - MinY);
        }

        /// <summary>
        /// Site footprint 解析：<see cref="WorldSite.OccupiedHexes"/>（缓存 ReadOnlyCollection，零分配）。
        /// <b>空 footprint 一律返回 false</b> —— AnchorHex / PresenceHex 不参与 physical mapping
        /// （ADR-0027 Decision #2/#7），不自动修 footprint，不产生 fake single-hex domain。
        /// </summary>
        static bool ResolveFootprint(
            WorldSite site,
            out IReadOnlyList<HexCoord> footprint)
        {
            footprint = null;
            if (site == null)
                return false;
            if (site.OccupiedHexes.Count == 0)
                return false;
            footprint = site.OccupiedHexes;
            return true;
        }

        /// <summary>
        /// 计算 Site footprint 的连续 world-surface domain：全部 OccupiedHexes 的真实 Hex 角点
        /// 的 axis-aligned 外接框。不做 nearest-hex-center 简化；irregular footprint 的包络空洞
        /// 由投影阶段处理（5R-B3A：delegate 到 HexFootprintSpatialMapping）。
        /// </summary>
        public static bool TryComputeFootprintWorldDomain(
            WorldSite site,
            float hexSize,
            out float minX,
            out float maxX,
            out float minY,
            out float maxY)
        {
            minX = maxX = minY = maxY = 0f;
            if (!ResolveFootprint(site, out var footprint))
                return false;
            return HexFootprintSpatialMapping.TryComputeWorldDomain(
                footprint, hexSize, out minX, out maxX, out minY, out maxY);
        }

        /// <summary>
        /// Phase 5R-B3C3：一次性构建 V2 geometry（boundary + kernel）。B4 每帧调用应在此处构建一次并
        /// 复用 <see cref="HexFootprintSpatialGeometry"/>（radial 方法零堆分配）。
        /// footprint 空 / kernel-empty → false（不 fallback V1）。
        /// </summary>
        public static bool TryBuildGeometry(
            WorldSite site,
            float hexSize,
            out HexFootprintSpatialGeometry geometry)
        {
            geometry = null;
            if (site == null || hexSize <= 0.0001f)
                return false;
            if (!ResolveFootprint(site, out var footprint))
                return false;
            return HexFootprintSpatialGeometry.TryBuild(footprint, hexSize, out geometry) && geometry.HasKernel;
        }

        /// <summary>V2 主映射：Local normalized (u,v) → footprint kernel radial → world surface。
        /// 内部构建 geometry（每调用一次）；高频路径应改用 <see cref="TryBuildGeometry"/> 复用。</summary>
        public static bool TryLocalToWorldSurface(
            WorldSite site,
            WorldSiteLocalMapBounds bounds,
            WorldVec2 localPosition,
            float hexSize,
            out WorldVec2 worldPosition)
        {
            worldPosition = default;
            if (site == null || !bounds.IsValid || hexSize <= 0.0001f)
                return false;
            if (!TryBuildGeometry(site, hexSize, out var geometry))
                return false;
            return geometry.TryLocalToWorldSurface(
                bounds.MinX, bounds.MaxX, bounds.MinY, bounds.MaxY, localPosition, out worldPosition);
        }

        /// <summary>V2 主映射（复用 geometry，零堆分配；B4 推荐路径）。</summary>
        public static bool TryLocalToWorldSurface(
            HexFootprintSpatialGeometry geometry,
            WorldSiteLocalMapBounds bounds,
            WorldVec2 localPosition,
            out WorldVec2 worldPosition)
        {
            worldPosition = default;
            if (geometry == null || !bounds.IsValid)
                return false;
            return geometry.TryLocalToWorldSurface(
                bounds.MinX, bounds.MaxX, bounds.MinY, bounds.MaxY, localPosition, out worldPosition);
        }

        /// <summary>便捷重载：hexSize = <see cref="HexWorldScale.DefaultHexOuterRadius"/>（1f）。</summary>
        public static bool TryLocalToWorldSurface(
            WorldSite site,
            WorldSiteLocalMapBounds bounds,
            WorldVec2 localPosition,
            out WorldVec2 worldPosition) =>
            TryLocalToWorldSurface(site, bounds, localPosition, HexWorldScale.DefaultHexOuterRadius, out worldPosition);

        /// <summary>
        /// V2 inverse：worldSurface → 同一 kernel radial authority → Local。
        /// footprint 外点由 radial 语义投影到 polygon（非 V1 AABB collapse；bijective 域内稳定）。
        /// </summary>
        public static bool TryWorldSurfaceToLocal(
            WorldSite site,
            WorldSiteLocalMapBounds bounds,
            WorldVec2 worldPosition,
            float hexSize,
            out WorldVec2 localPosition)
        {
            localPosition = default;
            if (site == null || !bounds.IsValid || hexSize <= 0.0001f)
                return false;
            if (!TryBuildGeometry(site, hexSize, out var geometry))
                return false;
            return geometry.TryWorldSurfaceToLocal(
                bounds.MinX, bounds.MaxX, bounds.MinY, bounds.MaxY, worldPosition, out localPosition);
        }

        /// <summary>V2 inverse（复用 geometry，零堆分配；B4 推荐路径）。</summary>
        public static bool TryWorldSurfaceToLocal(
            HexFootprintSpatialGeometry geometry,
            WorldSiteLocalMapBounds bounds,
            WorldVec2 worldPosition,
            out WorldVec2 localPosition)
        {
            localPosition = default;
            if (geometry == null || !bounds.IsValid)
                return false;
            return geometry.TryWorldSurfaceToLocal(
                bounds.MinX, bounds.MaxX, bounds.MinY, bounds.MaxY, worldPosition, out localPosition);
        }

        /// <summary>
        /// 独立 helper（非主 mapping）：world 点 → footprint 最近合法点。仅供 legacy / DerivedHex 等
        /// 需要"最近点"的场景；不再混入 coordinate mapping（5R-B3C3 §十）。
        /// </summary>
        public static bool ProjectWorldPointToFootprint(
            WorldSite site,
            WorldVec2 worldPosition,
            float hexSize,
            out WorldVec2 projected,
            out HexCoord nearestHex)
        {
            projected = default;
            nearestHex = default;
            if (site == null || hexSize <= 0.0001f)
                return false;
            if (!TryBuildGeometry(site, hexSize, out var geometry))
                return false;
            return geometry.TryProjectWorldPointToFootprint(worldPosition, out projected, out nearestHex);
        }

        /// <summary>
        /// DerivedPresenceHex：worldPosition → WorldToHex；若 ∈ footprint 直接返回；
        /// 数值边界歧义（polygon boundary / 空洞）用 footprint polygon containment 稳定解析
        /// （返回最近 occupied hex）。不使用 AnchorHex / PresenceHex / DisplayName / SiteType。
        /// </summary>
        public static bool TryResolveDerivedFootprintHex(
            WorldSite site,
            WorldVec2 worldPosition,
            float hexSize,
            out HexCoord footprintHex)
        {
            footprintHex = default;
            if (!ResolveFootprint(site, out var footprint))
                return false;
            return HexFootprintSpatialMapping.TryResolveFootprintHex(
                footprint, worldPosition, hexSize, out footprintHex);
        }
    }

    /// <summary>
    /// Phase 5R-B4：WorldSite LocalVisible → Canonical 的单向 Executor Ownership 判定输入。
    /// 纯数据快照（值类型，零依赖），由 Host 层在每个 sync 决策点组装。
    /// </summary>
    public readonly struct WorldSiteLocalVisibleSyncContext
    {
        public WorldSiteLocalVisibleSyncContext(
            bool inputBlocked,
            bool isWorldMapOpen,
            bool hasActiveView,
            bool isAtWorldSite,
            bool hasSiteId,
            bool isSiteDeparturePending,
            bool usesTravelPresentation,
            bool isMaterializeHeld,
            bool hasGeometry)
        {
            InputBlocked = inputBlocked;
            IsWorldMapOpen = isWorldMapOpen;
            HasActiveView = hasActiveView;
            IsAtWorldSite = isAtWorldSite;
            HasSiteId = hasSiteId;
            IsSiteDeparturePending = isSiteDeparturePending;
            UsesTravelPresentation = usesTravelPresentation;
            IsMaterializeHeld = isMaterializeHeld;
            HasGeometry = hasGeometry;
        }

        /// <summary>全局输入被阻塞（HostInputGate.BlockWorldInteraction）。</summary>
        public bool InputBlocked { get; }

        /// <summary>WorldMap 面板当前打开 → World executor owns，禁止 Local→Canonical。</summary>
        public bool IsWorldMapOpen { get; }

        /// <summary>Active Character 的 Local View 有效（EntityViewSpawner.Registry 可查）。</summary>
        public bool HasActiveView { get; }

        /// <summary>PlayerParty 当前 AtWorldSite。</summary>
        public bool IsAtWorldSite { get; }

        /// <summary>motion.SiteId 非空（与 AtWorldSite 配套防御）。</summary>
        public bool HasSiteId { get; }

        /// <summary>Site departure / transition ownership 进行中（BeginSiteDepartureTravel）→ 停止 B4。</summary>
        public bool IsSiteDeparturePending { get; }

        /// <summary>跨入 Destination Site 的 mid-travel presentation（非 LocalVisible owner）→ 停止 B4。</summary>
        public bool UsesTravelPresentation { get; }

        /// <summary>
        /// Materialize 完成帧标记（OnLocalMapMaterialized 置位）：本帧刚完成 Canonical→Local，
        /// 禁止同帧反写（ownership transition，不是「等一帧」——由 Host 用显式 held 状态表达）。
        /// </summary>
        public bool IsMaterializeHeld { get; }

        /// <summary>V2 geometry 已缓存可用。</summary>
        public bool HasGeometry { get; }
    }

    /// <summary>
    /// Phase 5R-B4：WorldSite LocalVisible → Canonical 单向 Executor Ownership 纯 policy。
    /// 决定「本帧是否允许 Local→Canonical」。纯函数、零依赖，可 EditMode / dotnet 直接测。
    ///
    /// 语义（B4 §三）：WorldMap CLOSED + AtWorldSite + Site LocalMap 已 Materialize +
    /// Active Character View 有效 → LocalVisible executor owns physical execution。
    /// 任一条件不满足 → false（保守不写 Canonical）。
    /// </summary>
    public static class WorldSiteLocalVisibleSyncPolicy
    {
        /// <summary>是否可以执行 Local→Canonical。</summary>
        public static bool CanSync(in WorldSiteLocalVisibleSyncContext ctx)
        {
            if (ctx.InputBlocked)
                return false;
            if (ctx.IsWorldMapOpen)
                return false; // WorldMap OPEN → World executor owns
            if (!ctx.HasActiveView)
                return false;
            if (!ctx.IsAtWorldSite)
                return false; // 仅 AtWorldSite 可写
            if (!ctx.HasSiteId)
                return false;
            if (ctx.IsSiteDeparturePending)
                return false; // departure/transition ownership → B4 停止
            if (ctx.UsesTravelPresentation)
                return false; // travel 跨入中（非 LocalVisible owner）
            if (ctx.IsMaterializeHeld)
                return false; // Materialize 完成帧：禁止同帧反写
            if (!ctx.HasGeometry)
                return false;
            return true;
        }

        /// <summary>
        /// Materialize 完成后 ownership 是否已建立（held=false 且其余条件满足）。
        /// 用于「Materialize complete → sync enabled」的显式状态判定（测试用）。
        /// </summary>
        public static bool IsOwnershipEstablished(in WorldSiteLocalVisibleSyncContext ctx) =>
            CanSync(new WorldSiteLocalVisibleSyncContext(
                ctx.InputBlocked,
                ctx.IsWorldMapOpen,
                ctx.HasActiveView,
                ctx.IsAtWorldSite,
                ctx.HasSiteId,
                ctx.IsSiteDeparturePending,
                ctx.UsesTravelPresentation,
                isMaterializeHeld: false,
                ctx.HasGeometry));
    }

    /// <summary>B4 LocalVisible → Canonical 同步结果。</summary>
    public enum WorldSiteSyncOutcome
    {
        /// <summary>已写入 Canonical WorldPosition。</summary>
        Synced = 0,

        /// <summary>Local 未变化（差异 &lt; epsilon），未写（避免无意义 mutation / dirty state）。</summary>
        SkippedEpsilon = 1,

        /// <summary>V2 映射失败：保留旧 Canonical，无 Anchor/Presence/StartLocation fallback。</summary>
        MappingFailed = 2,

        /// <summary><see cref="PlayerPartyWorldMotion.TryUpdateWorldPositionWithinSite"/> 拒绝
        /// （非 AtWorldSite 或 SiteId 不匹配等防御）→ 不写任何状态。</summary>
        SiteIdRejected = 3,
    }

    /// <summary>
    /// Phase 5R-B4：WorldSite LocalVisible → Canonical 同步的<b>单一 runtime 入口</b>。
    /// Local Position（Active Character 当前实际 Local Transform）→ WorldSiteSpatialMapping V2
    /// （复用已构建 geometry，零堆分配）→ PlayerPartyWorldMotion.TryUpdateWorldPositionWithinSite
    /// （唯一 Canonical 写入口）。
    ///
    /// 不关心「为什么角色移动」（WASD / RTS / AutoTravel 共用同一 writer）；
    /// 只读本帧最终 Local 位置。失败保留旧 Canonical，不做任何 fallback。
    /// 不修改 CurrentHex（TryUpdateWorldPositionWithinSite 语义保证）；LocationKind / SiteId 不变。
    /// </summary>
    public static class PlayerPartyWorldSiteLocalVisibleSync
    {
        /// <summary>写入阈值：新 Canonical 与旧 Canonical 差异低于该值 → 不写（减少 dirty state）。
        /// 远小于 V2 精度（0.0016）与 WASD 单帧位移（~0.09），不会导致「角色走几步才跳一次」。</summary>
        public const float PositionEpsilon = 1e-4f;

        /// <summary>Local → Canonical 单次同步。motion/geometry 为空、映射失败、epsilon 跳过、
        /// SiteId 拒绝均不修改任何状态。</summary>
        public static WorldSiteSyncOutcome TrySync(
            PlayerPartyWorldMotion motion,
            HexFootprintSpatialGeometry geometry,
            WorldSiteSpatialMapping.WorldSiteLocalMapBounds bounds,
            WorldVec2 localPosition,
            out WorldVec2 canonicalPosition)
        {
            canonicalPosition = motion != null ? motion.WorldPosition : default;
            if (motion == null || geometry == null || !geometry.HasKernel)
                return WorldSiteSyncOutcome.MappingFailed;
            if (!bounds.IsValid)
                return WorldSiteSyncOutcome.MappingFailed;

            if (!WorldSiteSpatialMapping.TryLocalToWorldSurface(
                    geometry, bounds, localPosition, out canonicalPosition))
                return WorldSiteSyncOutcome.MappingFailed;

            var prev = motion.WorldPosition;
            var dx = canonicalPosition.X - prev.X;
            var dy = canonicalPosition.Y - prev.Y;
            if (dx * dx + dy * dy < PositionEpsilon * PositionEpsilon)
                return WorldSiteSyncOutcome.SkippedEpsilon;

            if (!motion.TryUpdateWorldPositionWithinSite(motion.SiteId, canonicalPosition))
                return WorldSiteSyncOutcome.SiteIdRejected;

            return WorldSiteSyncOutcome.Synced;
        }
    }
}
