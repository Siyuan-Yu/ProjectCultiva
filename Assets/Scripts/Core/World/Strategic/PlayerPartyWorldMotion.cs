using XianXia.Core.World;

using System;

using System.Collections.Generic;

using System.Collections.ObjectModel;

using XianXia.Core.Domain.Ids;

using XianXia.Core.World.Hex;



namespace XianXia.Core.World.Strategic

{

    /// <summary>

    /// LEGACY OUTDOOR LOCALMAP COMPATIBILITY ONLY：PlayerParty WorldSite departure 细分阶段。

    /// 将「计划离开 Site」与「已经进入 Boundary Transition」拆开，避免单一 bool 让 B4

    /// ownership 模糊（approaching 时角色仍 AtWorldSite、LocalVisible owns，B4 必须继续；

    /// 仅 TransitionCommit 时 transition authority 接管、B4 停止）。

    /// </summary>

    public enum LegacyPlayerPartyDeparturePhase

    {

        /// <summary>无 departure。</summary>

        None = 0,



        /// <summary>WorldMap 已接受外部 Travel Order，DeparturePlan 已形成（route/first outside hex

        /// 已解析）。WorldMap open 期间仅 plan，不虚拟推进 Canonical。</summary>

        Planned = 1,



        /// <summary>WorldMap close，LocalVisible 驱动角色在 Site LocalMap 内自动走向正式出口。

        /// 期间角色仍 AtWorldSite + SiteId 不变，LocalVisible owns → B4 Local→Canonical 继续。</summary>

        Approaching = 2,



        /// <summary>已到达正式 SurfaceExit，transition authority 接管 → B4 停止，随后正式 egress

        /// （AtWorldSite → AtWorldPosition + EnterLegacyWildernessLocalMap）。</summary>

        TransitionCommit = 3,

    }



    /// <summary>

    /// PlayerParty Continuous Outdoor 位置与移动 authority。正常运行使用 SurfaceId、WorldPosition、

    /// SurfaceVisible 与 ContinuousSurfaceRoute；LegacyCurrentHex 只是兼容摘要。正常 Surface 中它是

    /// 派生 metadata；旧路径中它可表示旧路线已提交格或旧上下文缓存，并不保证始终是即时位置投影。

    /// </summary>

    public sealed class PlayerPartyWorldMotion

    {

        readonly List<HexCoord> _hexPath = new List<HexCoord>(32);

        readonly List<EntityId> _travelingMembers = new List<EntityId>(6);

        readonly List<WorldVec2> _continuousSurfaceRoute = new List<WorldVec2>(128);

        ReadOnlyCollection<WorldVec2> _continuousSurfaceRouteView;

        ReadOnlyCollection<HexCoord> _hexPathView;



        public PlayerPartyLocationKind LocationKind { get; private set; } = PlayerPartyLocationKind.AtWorldSite;

        public PlayerPartyMovementKind MovementKind { get; private set; } = PlayerPartyMovementKind.Idle;

        /// <summary>Phase 5B：谁推进 AutoTravel；不复制 Path/Progress/Position。</summary>

        public PlayerPartyTravelExecutionMode ExecutionMode { get; private set; } =

            PlayerPartyTravelExecutionMode.None;

        public string SiteId { get; private set; } = string.Empty;

        /// <summary>正常 Continuous Outdoor 位置 provenance；Hex 兼容路径保持为空。</summary>

        public string SurfaceId { get; private set; } = string.Empty;

        public WorldVec2 WorldPosition { get; private set; }

        public HexTravelMode LegacyHexTravelMode { get; private set; } = HexTravelMode.Ground;

        public HexCoord LegacyDestinationHex { get; private set; }

        public string LegacyDestinationSiteId { get; private set; } = string.Empty;

        /// <summary>

        /// LEGACY OUTDOOR LOCALMAP COMPATIBILITY ONLY：旧分段旅行保存的原始最终目标。

        /// 正常 Continuous Surface 路线直接使用连续世界目标。

        /// </summary>

        public HexCoord LegacyFinalDestinationHex { get; private set; }

        public string LegacyFinalDestinationSiteId { get; private set; } = string.Empty;

        /// <summary>Continuous Outdoor 的正式物理目标；正常 Surface 路线使用 ContinuousSurfaceRoute，LegacyHexPath 仅供旧兼容。</summary>

        public bool HasContinuousPhysicalDestination { get; private set; }

        public WorldVec2 ContinuousPhysicalDestination { get; private set; }

        public float ContinuousPhysicalArrivalRadius { get; private set; }

        /// <summary>Host 用于区分正式路线替换；不作为存档 identity。</summary>

        public int TravelPlanVersion { get; private set; }

        public int ContinuousSurfaceRouteIndex { get; private set; }

        public IReadOnlyList<WorldVec2> ContinuousSurfaceRoute =>

            _continuousSurfaceRouteView ?? (_continuousSurfaceRouteView = _continuousSurfaceRoute.AsReadOnly());

        public bool HasContinuousSurfaceRoute => _continuousSurfaceRoute.Count > 1;



        public int LegacyHexSegmentIndex { get; private set; }

        public float LegacyHexSegmentProgress { get; private set; }

        public bool HasPosition { get; private set; }



        /// <summary>

        /// 兼容摘要：正常 Surface 中为派生 metadata；旧路径中可表示已提交路线格或旧上下文缓存，

        /// 因此不保证始终等于 WorldPosition 的即时 Hex 投影。

        /// </summary>

        public HexCoord LegacyCurrentHex { get; private set; }



        /// <summary>LEGACY OUTDOOR LOCALMAP COMPATIBILITY ONLY：Site departure presentation transient。</summary>

        public bool IsLegacySiteDeparturePending { get; private set; }

        public WorldVec2 LegacySiteDepartureVirtualPosition { get; private set; }

        public WorldVec2 LegacySiteDepartureBoundaryEntry { get; private set; }

        public HexCoord LegacySiteDepartureFootprintHex { get; private set; }

        public HexCoord LegacySiteDepartureExitHex { get; private set; }



        /// <summary>

        /// LEGACY OUTDOOR LOCALMAP COMPATIBILITY ONLY：WorldSite departure 细分阶段。

        ///  <see cref="LegacyPlayerPartyDeparturePhase.Planned"/>：WorldMap 接受外部 order，plan 已形成（WorldMap open 仅 plan，不虚拟推进）；

        ///  <see cref="LegacyPlayerPartyDeparturePhase.Approaching"/>：WorldMap close，LocalVisible 驱动角色在 Site LocalMap 内走向正式出口

        ///      —— 期间角色仍 AtWorldSite、LocalVisible owns，<b>B4 Local→Canonical 必须继续</b>；

        ///  <see cref="LegacyPlayerPartyDeparturePhase.TransitionCommit"/>：到达出口，transition authority 接管（B4 停止）。

        /// <see cref="IsLegacySiteDeparturePending"/> 保留为“已有 departure 意图”的兼容聚合（!= None）。

        /// </summary>

        public LegacyPlayerPartyDeparturePhase LegacyDeparturePhase { get; private set; }



        public void SetLegacyDeparturePhase(LegacyPlayerPartyDeparturePhase phase)

        {

            LegacyDeparturePhase = phase;

            IsLegacySiteDeparturePending = phase != LegacyPlayerPartyDeparturePhase.None;

        }



        /// <summary>LEGACY OUTDOOR LOCALMAP COMPATIBILITY ONLY：Destination Site presentation transient。</summary>

        public bool LegacyUsesTravelPresentation { get; private set; }

        public WorldVec2 LegacyTravelPresentationPosition { get; private set; }



        public bool IsMoving => MovementKind == PlayerPartyMovementKind.AutoTravel;



        public int LegacyHexPathCount => _hexPath.Count;



        /// <summary>LEGACY OUTDOOR LOCALMAP COMPATIBILITY ONLY.</summary>

        public IReadOnlyList<HexCoord> LegacyHexPath =>

            _hexPathView ?? (_hexPathView = _hexPath.AsReadOnly());



        public IReadOnlyList<EntityId> TravelingMembers => _travelingMembers;



        /// <summary>LEGACY OUTDOOR LOCALMAP COMPATIBILITY ONLY：Surface LocalMap 边界跨格防抖。</summary>

        public PlayerPartySurfaceEdgeGate LegacySurfaceEdgeGate { get; } = new PlayerPartySurfaceEdgeGate();



        /// <summary>Outdoor physical-region context only. It is derived from canonical

        /// WorldPosition and is never a second location authority.</summary>

        public string CurrentOutdoorWorldSiteId { get; private set; } = string.Empty;



        public void SetCurrentOutdoorWorldSiteContext(string siteId) =>

            CurrentOutdoorWorldSiteId = siteId ?? string.Empty;



        public void Clear()

        {

            TravelPlanVersion++;

            _hexPath.Clear();

            _travelingMembers.Clear();

            _continuousSurfaceRoute.Clear();

            ContinuousSurfaceRouteIndex = 0;

            LegacyHexSegmentIndex = 0;

            LegacyHexSegmentProgress = 0f;

            MovementKind = PlayerPartyMovementKind.Idle;

            ExecutionMode = PlayerPartyTravelExecutionMode.None;

            LegacyDestinationHex = LegacyCurrentHex;

            LegacyDestinationSiteId = string.Empty;

            LegacyFinalDestinationHex = LegacyCurrentHex;

            LegacyFinalDestinationSiteId = string.Empty;

            HasContinuousPhysicalDestination = false;

            ContinuousPhysicalDestination = default;

            ContinuousPhysicalArrivalRadius = 0f;

            LegacyHexTravelMode = HexTravelMode.Ground;

            ClearLegacySiteDeparturePending();

            LegacyUsesTravelPresentation = false;

        }



        public void SetLegacyExecutionMode(PlayerPartyTravelExecutionMode mode) =>

            ExecutionMode = mode;



        public void ClearLegacySiteDeparturePending()

        {

            IsLegacySiteDeparturePending = false;

            LegacyDeparturePhase = LegacyPlayerPartyDeparturePhase.None;

            LegacySiteDepartureVirtualPosition = default;

            LegacySiteDepartureBoundaryEntry = default;

            LegacySiteDepartureFootprintHex = default;

            LegacySiteDepartureExitHex = default;

        }



        public void SetLegacySiteDepartureVirtualPosition(WorldVec2 pos) =>

            LegacySiteDepartureVirtualPosition = pos;



        public void SetLegacySiteDepartureVirtualPosition(WorldVec2 pos, float hexSize)

        {

            LegacySiteDepartureVirtualPosition = pos;

            LegacyCurrentHex = HexMath.WorldToHex(pos.X, pos.Y, hexSize > 0f ? hexSize : 1f);

            HasPosition = true;

        }



        /// <summary>跨入 Destination Site Footprint：Authority 立即 AtWorldSite，Presentation 继续沿路径。</summary>

        public void CommitLegacySiteArrivalAuthority(

            string siteId,

            WorldVec2 presentationPos,

            HexCoord presentationHex)

        {

            LocationKind = PlayerPartyLocationKind.AtWorldSite;

            SiteId = siteId ?? string.Empty;

            ClearLegacySiteDeparturePending();

            SetLegacyTravelPresentation(presentationPos, presentationHex);

            WorldPosition = presentationPos;

        }



        /// <summary>

        /// Phase 5R-B7A：经过非目标 WorldSite 时，为同一条 AutoTravel 建立穿越离场计划。

        /// 只写 Site departure transient；不重建/清空 LegacyHexPath，不改变 Destination、MovementKind、

        /// ExecutionMode 或 Segment。调用方须先用 <see cref="CommitLegacySiteArrivalAuthority"/> 提交

        /// AtWorldSite Context，并保证 footprintHex → exitHex 是当前路线中的相邻正式跨界。

        /// </summary>

        public void PlanLegacyThroughSiteDeparture(

            HexCoord footprintHex,

            HexCoord exitHex,

            WorldVec2 boundaryEntryWorld)

        {

            IsLegacySiteDeparturePending = true;

            LegacyDeparturePhase = LegacyPlayerPartyDeparturePhase.Planned;

            LegacySiteDepartureFootprintHex = footprintHex;

            LegacySiteDepartureExitHex = exitHex;

            LegacySiteDepartureBoundaryEntry = boundaryEntryWorld;

            LegacySiteDepartureVirtualPosition = WorldPosition;

        }



        public void ReplaceLegacySiteDeparturePlan(

            IReadOnlyList<HexCoord> path,

            HexCoord footprintHex,

            HexCoord exitHex,

            WorldVec2 boundaryEntry)

        {

            _hexPath.Clear();

            if (path != null)

                for (var i = 0; i < path.Count; i++) _hexPath.Add(path[i]);

            LegacyHexSegmentIndex = 0;

            LegacyHexSegmentProgress = 0f;

            LegacySiteDepartureFootprintHex = footprintHex;

            LegacySiteDepartureExitHex = exitHex;

            LegacySiteDepartureBoundaryEntry = boundaryEntry;

            LegacyDeparturePhase = LegacyPlayerPartyDeparturePhase.Planned;

            IsLegacySiteDeparturePending = true;

        }



        public void SetLegacyTravelPresentation(WorldVec2 pos, HexCoord derivedHex)

        {

            LegacyTravelPresentationPosition = pos;

            LegacyCurrentHex = derivedHex;

            LegacyUsesTravelPresentation = true;

            HasPosition = true;

        }



        public WorldVec2 ResolveLegacyTravelPresentationWorld(float hexSize)

        {

            if (IsLegacySiteDeparturePending)

                return LegacySiteDepartureVirtualPosition;

            if (LegacyUsesTravelPresentation)

                return LegacyTravelPresentationPosition;

            return WorldPosition;

        }



        public void SetAtLegacyWorldSite(string siteId, HexCoord presenceHex, float hexSize)

        {

            LocationKind = PlayerPartyLocationKind.AtWorldSite;

            SiteId = siteId ?? string.Empty;

            SurfaceId = string.Empty;

            LegacyCurrentHex = presenceHex;

            HexMath.ToWorldPosition(presenceHex, hexSize, out var x, out var y);

            WorldPosition = new WorldVec2(x, y);

            HasPosition = true;

            ClearMovementKeepMembers();

            ClearLegacySiteDeparturePending();

            LegacyUsesTravelPresentation = false;

        }



        /// <summary>

        /// Legacy Snapshot 的静止 WorldSite 恢复入口。WorldPosition 是已经保存的 Canonical 位置，

        /// 因而不得调用 <see cref="SetAtLegacyWorldSite"/> 再吸回 LegacyPresenceHex 中心。

        /// 该入口只恢复静止态：不恢复路径、离场计划或执行器所有权。

        /// </summary>

        public bool RestoreIdleAtLegacyWorldSite(

            string siteId,

            WorldVec2 worldPosition,

            HexCoord currentHex)

        {

            if (string.IsNullOrEmpty(siteId) ||

                float.IsNaN(worldPosition.X) || float.IsNaN(worldPosition.Y))

                return false;



            LocationKind = PlayerPartyLocationKind.AtWorldSite;

            SiteId = siteId;

            SurfaceId = string.Empty;

            WorldPosition = worldPosition;

            LegacyCurrentHex = currentHex;

            HasPosition = true;

            ClearMovementKeepMembers();

            LegacyUsesTravelPresentation = false;

            return true;

        }



        public void SetAtLegacyWorldPosition(WorldVec2 worldPos, HexCoord derivedHex)

        {

            LocationKind = PlayerPartyLocationKind.AtWorldPosition;

            SiteId = string.Empty;

            SurfaceId = string.Empty;

            WorldPosition = worldPos;

            LegacyCurrentHex = derivedHex;

            CurrentOutdoorWorldSiteId = string.Empty;

            HasPosition = true;

            ClearMovementKeepMembers();

            ClearLegacySiteDeparturePending();

            LegacyUsesTravelPresentation = false;

        }



        public void SetIdleAtLegacyHexCenter(HexCoord hex)

        {

            // Phase 2B compat：视为停在该格中心的开世界位置。

            HexMath.ToWorldPosition(hex, 1f, out var x, out var y);

            SetAtLegacyWorldPosition(new WorldVec2(x, y), hex);

        }



        public void CaptureTravelingMembers(IReadOnlyList<EntityId> members)

        {

            _travelingMembers.Clear();

            if (members == null)

                return;

            for (var i = 0; i < members.Count; i++)

            {

                if (!members[i].IsNone)

                    _travelingMembers.Add(members[i]);

            }

        }



        public void BeginLegacyHexAutoTravel(

            IReadOnlyList<HexCoord> path,

            HexCoord destinationHex,

            string destinationSiteId,

            HexTravelMode mode,

            float hexSize)

        {

            LoadAutoTravelPlan(path, destinationHex, destinationSiteId, mode);

            if (_hexPath.Count < 1)

            {

                CompleteMove();

                return;

            }



            // Site Exit：Authority 保持 AtWorldSite；Presentation 由 LegacySiteDepartureVirtualPosition 承载。

            // 非 Site 出发的 AutoTravel 不在此切换 LocationKind。



            if (_hexPath.Count == 1 || LegacyCurrentHex == destinationHex)

            {

                SnapToLegacyHexCenter(destinationHex, hexSize);

                CompleteMove();

                return;

            }



            // Phase 2C：path[0]==LegacyCurrentHex 且 off-center 时，段 0 从 live WorldPosition 出发（TryGetActiveLegacyHexSegmentWorld），不在此 snap。



            StartAutoTravel(PlayerPartyTravelExecutionMode.World);

        }



        /// <summary>正常 Continuous Surface 的 canonical placement。</summary>

        public void SetAtSurfacePosition(string surfaceId, WorldVec2 worldPos, HexCoord derivedCompatibilityHex)

        {

            if (string.IsNullOrWhiteSpace(surfaceId))

                throw new ArgumentException("surfaceId required.", nameof(surfaceId));

            SetAtLegacyWorldPosition(worldPos, derivedCompatibilityHex);

            SurfaceId = surfaceId;

        }



        public void BeginSurfaceAutoTravel(

            string surfaceId, WorldVec2 destinationWorldPosition, string destinationSiteId,

            float arrivalRadius, IReadOnlyList<WorldVec2> continuousRoute,

            HexCoord derivedCompatibilityDestinationHex)

        {

            if (string.IsNullOrWhiteSpace(surfaceId))

                throw new ArgumentException("surfaceId required.", nameof(surfaceId));

            TravelPlanVersion++;

            SurfaceId = surfaceId;

            LegacyDestinationSiteId = destinationSiteId ?? string.Empty;

            LegacyFinalDestinationSiteId = LegacyDestinationSiteId;

            LegacyDestinationHex = derivedCompatibilityDestinationHex;

            LegacyFinalDestinationHex = derivedCompatibilityDestinationHex;

            HasContinuousPhysicalDestination = true;

            ContinuousPhysicalDestination = destinationWorldPosition;

            ContinuousPhysicalArrivalRadius = Math.Max(0.001f, arrivalRadius);

            _hexPath.Clear();

            LegacyHexSegmentIndex = 0;

            LegacyHexSegmentProgress = 0f;

            _continuousSurfaceRoute.Clear();

            if (continuousRoute != null)

                for (var i = 0; i < continuousRoute.Count; i++)

                    _continuousSurfaceRoute.Add(continuousRoute[i]);

            ContinuousSurfaceRouteIndex = _continuousSurfaceRoute.Count > 1 ? 1 : 0;

            StartAutoTravel(PlayerPartyTravelExecutionMode.SurfaceVisible);

        }



        public bool TryGetContinuousSurfaceWaypoint(out WorldVec2 waypoint)

        {

            waypoint = default;

            if (ContinuousSurfaceRouteIndex < 0 || ContinuousSurfaceRouteIndex >= _continuousSurfaceRoute.Count)

                return false;

            waypoint = _continuousSurfaceRoute[ContinuousSurfaceRouteIndex];

            return true;

        }



        public void AdvanceContinuousSurfaceWaypoint()

        {

            if (ContinuousSurfaceRouteIndex < _continuousSurfaceRoute.Count)

                ContinuousSurfaceRouteIndex++;

        }



        /// <summary>Monotonic guidance advance after the Host physically reaches a later route point.</summary>

        public void AdvanceContinuousSurfaceRouteTo(int nextIndex)

        {

            if (nextIndex <= ContinuousSurfaceRouteIndex)

                return;

            ContinuousSurfaceRouteIndex = Math.Min(nextIndex, _continuousSurfaceRoute.Count);

        }



        void LoadAutoTravelPlan(

            IReadOnlyList<HexCoord> path,

            HexCoord destinationHex,

            string destinationSiteId,

            HexTravelMode mode)

        {

            TravelPlanVersion++;

            LegacyHexTravelMode = mode;

            LegacyDestinationHex = destinationHex;

            LegacyDestinationSiteId = destinationSiteId ?? string.Empty;

            LegacyFinalDestinationHex = destinationHex;

            LegacyFinalDestinationSiteId = destinationSiteId ?? string.Empty;

            HasContinuousPhysicalDestination = false;

            ContinuousPhysicalDestination = default;

            ContinuousPhysicalArrivalRadius = 0f;

            _continuousSurfaceRoute.Clear();

            ContinuousSurfaceRouteIndex = 0;

            _hexPath.Clear();

            if (path != null)

                for (var i = 0; i < path.Count; i++)

                    _hexPath.Add(path[i]);

            LegacyHexSegmentIndex = 0;

            LegacyHexSegmentProgress = 0f;

        }



        void StartAutoTravel(PlayerPartyTravelExecutionMode executionMode)

        {

            MovementKind = PlayerPartyMovementKind.AutoTravel;

            ExecutionMode = executionMode;

        }



        public void BeginLegacySiteDepartureTravel(

            IReadOnlyList<HexCoord> path,

            HexCoord destinationHex,

            string destinationSiteId,

            HexCoord footprintHex,

            HexCoord exitHex,

            WorldVec2 footprintCenterWorld,

            WorldVec2 boundaryEntryWorld,

            HexTravelMode mode,

            float hexSize)

        {

            BeginLegacyHexAutoTravel(path, destinationHex, destinationSiteId, mode, hexSize);

            if (!IsMoving)

                return;



            IsLegacySiteDeparturePending = true;

            LegacyDeparturePhase = LegacyPlayerPartyDeparturePhase.Planned;

            LegacySiteDepartureFootprintHex = footprintHex;

            LegacySiteDepartureExitHex = exitHex;

            LegacySiteDepartureBoundaryEntry = boundaryEntryWorld;

            LegacySiteDepartureVirtualPosition = footprintCenterWorld;

            LegacyUsesTravelPresentation = false;

        }



        internal void CompleteMove()

        {

            TravelPlanVersion++;

            _hexPath.Clear();

            LegacyHexSegmentIndex = 0;

            LegacyHexSegmentProgress = 0f;

            LegacyDestinationHex = LegacyCurrentHex;

            LegacyDestinationSiteId = string.Empty;

            LegacyFinalDestinationHex = LegacyCurrentHex;

            LegacyFinalDestinationSiteId = string.Empty;

            HasContinuousPhysicalDestination = false;

            ContinuousPhysicalDestination = default;

            ContinuousPhysicalArrivalRadius = 0f;

            _continuousSurfaceRoute.Clear();

            ContinuousSurfaceRouteIndex = 0;

            MovementKind = PlayerPartyMovementKind.Idle;

            ExecutionMode = PlayerPartyTravelExecutionMode.None;

            ClearLegacySiteDeparturePending();

            LegacyUsesTravelPresentation = false;

        }



        public void CancelAutoTravelPreservePosition() => CompleteMove();



        /// <summary>

        /// Phase 5C-W2 Takeover Canonical State：仅把派生 LegacyCurrentHex 对齐到指定格（TravelPlan 段真源）。

        /// 不改 WorldPosition / Path / Segment / MovementKind / ExecutionMode，不 Snap、不 Teleport。

        /// 用于 World → LocalVisible 接管前消除 WorldToHex 实时派生与 LegacyHexSegmentIndex 的分叉。

        /// </summary>

        public void AlignLegacyCurrentHex(HexCoord hex)

        {

            LegacyCurrentHex = hex;

            HasPosition = true;

        }



        public void SetLegacyWorldPositionAndHex(WorldVec2 pos, HexCoord derivedHex)

        {

            WorldPosition = pos;

            LegacyCurrentHex = derivedHex;

            LocationKind = PlayerPartyLocationKind.AtWorldPosition;

            SiteId = string.Empty;

            SurfaceId = string.Empty;

            HasPosition = true;

            ClearLegacySiteDeparturePending();

            LegacyUsesTravelPresentation = false;

        }



        /// <summary>Surface presentation accepted a legal physical step; travel intent is preserved.</summary>

        public void UpdateSurfaceWorldPosition(string surfaceId, WorldVec2 pos, HexCoord derivedCompatibilityHex)

        {

            if (string.IsNullOrWhiteSpace(surfaceId) ||

                !string.Equals(SurfaceId, surfaceId, StringComparison.Ordinal))

                throw new InvalidOperationException("Surface position provenance changed during travel.");

            WorldPosition = pos;

            LegacyCurrentHex = derivedCompatibilityHex;

            LocationKind = PlayerPartyLocationKind.AtWorldPosition;

            SiteId = string.Empty;

            HasPosition = true;

            ClearLegacySiteDeparturePending();

            LegacyUsesTravelPresentation = false;

        }



        /// <summary>

        /// Phase 5R-B2A：Site LocalMap 内移动时仅同步 Canonical WorldPosition 的

        /// Context-preserving API。仅当当前 <see cref="LocationKind"/> == AtWorldSite 且

        /// <see cref="SiteId"/> == expectedSiteId 时才允许更新。

        /// 只改 <see cref="WorldPosition"/> + <see cref="HasPosition"/>；

        /// 保持 AtWorldSite Context（LocationKind / SiteId 不变）。

        /// 禁止：SetAtLegacyWorldPosition、清 SiteId、ClearMovement、CompleteMove、Snap、

        /// 改 PartyWorld / Presence / LocalMap / Destination / Segment。

        /// <b>不修改 <see cref="LegacyCurrentHex"/></b>——LegacyCurrentHex 仍混合

        /// CurrentWildernessHex / DerivedSurfaceHex / RouteCommittedHex 三职责，分类留 5R-C；

        /// Site 内具体 footprint Hex 需要时经

        /// <see cref="WorldSiteHexFootprintSpatialMapping.TryResolveDerivedFootprintHex"/> 即时派生。

        /// </summary>

        /// <summary>返回是否成功更新；失败（非 AtWorldSite 或 SiteId 不匹配）时不改任何状态。</summary>

        public bool TryUpdateLegacyWorldPositionWithinSite(string expectedSiteId, WorldVec2 worldPosition)

        {

            if (LocationKind != PlayerPartyLocationKind.AtWorldSite)

                return false;

            if (!string.Equals(SiteId, expectedSiteId ?? string.Empty, StringComparison.Ordinal))

                return false;

            WorldPosition = worldPosition;

            HasPosition = true;

            return true;

        }



        /// <summary>

        /// Phase 5R-B3B：Final Arrival / Site Enter 的 Context-preserving AtSite 提交。

        /// 与 <see cref="SetAtLegacyWorldSite"/>（snap 到 hex center）不同：<b>绝不</b>把

        /// <see cref="WorldPosition"/> 设为 LegacyPresenceHex / LegacyAnchorHex / ingress hex center。

        /// 只改 LocationKind / SiteId / WorldPosition / HasPosition；

        /// <b>不修改 <see cref="LegacyCurrentHex"/></b>（三职责分类留 5R-C）。

        /// 结束 travel transient（ClearMovementKeepMembers，含 LegacyUsesTravelPresentation=false /

        /// ClearLegacySiteDeparturePending），用于正式进入 Site / 到达收尾（区别于

        /// <see cref="CommitLegacySiteArrivalAuthority"/> 的 mid-travel 保留 presentation）。

        /// 调用方必须先确认已有可信 Canonical（HasPosition）——本方法不做来源判断，

        /// 仅校验 siteId 非空与 worldPosition 非 NaN；无 Canonical 时由调用方决定缺口处理。

        /// </summary>

        public bool TrySetAtLegacyWorldSitePreservingWorldPosition(string siteId, WorldVec2 worldPosition)

        {

            if (string.IsNullOrEmpty(siteId) || float.IsNaN(worldPosition.X) || float.IsNaN(worldPosition.Y))

                return false;

            LocationKind = PlayerPartyLocationKind.AtWorldSite;

            SiteId = siteId;

            SurfaceId = string.Empty;

            WorldPosition = worldPosition;

            HasPosition = true;

            ClearMovementKeepMembers();

            return true;

        }



        public void SnapToLegacyHexCenter(HexCoord hex, float hexSize)

        {

            HexMath.ToWorldPosition(hex, hexSize, out var x, out var y);

            SetLegacyWorldPositionAndHex(new WorldVec2(x, y), hex);

        }



        internal void IncrementLegacyHexPathIndex() => LegacyHexSegmentIndex++;



        public void SetLegacyHexSegment(int index, float progress)

        {

            LegacyHexSegmentIndex = index;

            LegacyHexSegmentProgress = Math.Max(0f, Math.Min(1f, progress));

        }



        public bool TryGetActiveLegacyHexStep(out HexCoord from, out HexCoord to)

        {

            from = LegacyCurrentHex;

            to = LegacyCurrentHex;

            if (_hexPath.Count < 2 || LegacyHexSegmentIndex < 0 || LegacyHexSegmentIndex >= _hexPath.Count - 1)

                return false;

            from = _hexPath[LegacyHexSegmentIndex];

            to = _hexPath[LegacyHexSegmentIndex + 1];

            return true;

        }



        /// <summary>当前 AutoTravel 段的世界几何：fromPos → toCenter（首段 from 可为任意 WorldPosition）。</summary>

        public bool TryGetActiveLegacyHexSegmentWorld(

            float hexSize,

            out WorldVec2 fromPos,

            out WorldVec2 toPos)

        {

            fromPos = WorldPosition;

            toPos = WorldPosition;

            if (!IsMoving || _hexPath.Count < 2)

                return false;



            if (LegacyHexSegmentIndex >= _hexPath.Count - 1)

                return false;



            var toHex = _hexPath[LegacyHexSegmentIndex + 1];

            HexMath.ToWorldPosition(toHex, hexSize, out var tx, out var ty);

            toPos = new WorldVec2(tx, ty);



            if (LegacyHexSegmentIndex == 0)

            {

                fromPos = ResolveLegacyTravelPresentationWorld(hexSize);

                return true;

            }



            var fromHex = _hexPath[LegacyHexSegmentIndex];

            HexMath.ToWorldPosition(fromHex, hexSize, out var fx, out var fy);

            fromPos = new WorldVec2(fx, fy);

            return true;

        }



        void ClearMovementKeepMembers()

        {

            TravelPlanVersion++;

            _hexPath.Clear();

            LegacyHexSegmentIndex = 0;

            LegacyHexSegmentProgress = 0f;

            MovementKind = PlayerPartyMovementKind.Idle;

            ExecutionMode = PlayerPartyTravelExecutionMode.None;

            LegacyDestinationHex = LegacyCurrentHex;

            LegacyDestinationSiteId = string.Empty;

            LegacyFinalDestinationHex = LegacyCurrentHex;

            LegacyFinalDestinationSiteId = string.Empty;

            HasContinuousPhysicalDestination = false;

            ContinuousPhysicalDestination = default;

            ContinuousPhysicalArrivalRadius = 0f;

            _continuousSurfaceRoute.Clear();

            ContinuousSurfaceRouteIndex = 0;

            ClearLegacySiteDeparturePending();

            LegacyUsesTravelPresentation = false;

        }

    }

}
