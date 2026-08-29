using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// PlayerParty 世界位置 + 移动状态真源（Phase 2C）。
    /// WorldPosition 为开世界真源；CurrentHex 由 WorldToHex 派生；AtWorldSite 时投影 PresenceHex。
    /// </summary>
    public sealed class PlayerPartyWorldMotion
    {
        readonly List<HexCoord> _hexPath = new List<HexCoord>(32);
        readonly List<EntityId> _travelingMembers = new List<EntityId>(6);
        ReadOnlyCollection<HexCoord> _hexPathView;

        public PlayerPartyLocationKind LocationKind { get; private set; } = PlayerPartyLocationKind.AtWorldSite;
        public PlayerPartyMovementKind MovementKind { get; private set; } = PlayerPartyMovementKind.Idle;
        /// <summary>Phase 5B：谁推进 AutoTravel；不复制 Path/Progress/Position。</summary>
        public PlayerPartyTravelExecutionMode ExecutionMode { get; private set; } =
            PlayerPartyTravelExecutionMode.None;
        public string SiteId { get; private set; } = string.Empty;
        public WorldVec2 WorldPosition { get; private set; }
        public HexTravelMode TravelMode { get; private set; } = HexTravelMode.Ground;
        public HexCoord DestinationHex { get; private set; }
        public string DestinationSiteId { get; private set; } = string.Empty;
        public int SegmentIndex { get; private set; }
        public float SegmentProgress { get; private set; }
        public bool HasPosition { get; private set; }

        /// <summary>派生格：Travel Presentation 或 Authority 投影。</summary>
        public HexCoord CurrentHex { get; private set; }

        /// <summary>Site 内 Travel Presentation（Authority 仍为 AtWorldSite）。</summary>
        public bool IsSiteDeparturePending { get; private set; }
        public WorldVec2 SiteDepartureVirtualPosition { get; private set; }
        public WorldVec2 SiteDepartureBoundaryEntry { get; private set; }
        public HexCoord SiteDepartureFootprintHex { get; private set; }
        public HexCoord SiteDepartureExitHex { get; private set; }

        /// <summary>跨入 Destination Site 后 Footprint 内 Presentation（Authority 已为 AtWorldSite）。</summary>
        public bool UsesTravelPresentation { get; private set; }
        public WorldVec2 TravelPresentationPosition { get; private set; }

        public bool IsMoving => MovementKind == PlayerPartyMovementKind.AutoTravel;

        public int HexPathCount => _hexPath.Count;

        public IReadOnlyList<HexCoord> HexPath =>
            _hexPathView ?? (_hexPathView = _hexPath.AsReadOnly());

        public IReadOnlyList<EntityId> TravelingMembers => _travelingMembers;

        /// <summary>Surface LocalMap 边界跨格防抖（不改 WorldPosition）。</summary>
        public PlayerPartySurfaceEdgeGate SurfaceEdgeGate { get; } = new PlayerPartySurfaceEdgeGate();

        // ---- Phase 2B compat aliases ----
        public int CurrentPathIndex => SegmentIndex;
        public float StepProgress => SegmentProgress;
        public int StepRemainingTicks { get; private set; }
        public int StepTotalTicks { get; private set; }

        public void Clear()
        {
            _hexPath.Clear();
            _travelingMembers.Clear();
            SegmentIndex = 0;
            SegmentProgress = 0f;
            StepRemainingTicks = 0;
            StepTotalTicks = 0;
            MovementKind = PlayerPartyMovementKind.Idle;
            ExecutionMode = PlayerPartyTravelExecutionMode.None;
            DestinationHex = CurrentHex;
            DestinationSiteId = string.Empty;
            TravelMode = HexTravelMode.Ground;
            ClearSiteDeparturePending();
            UsesTravelPresentation = false;
        }

        public void SetExecutionMode(PlayerPartyTravelExecutionMode mode) =>
            ExecutionMode = mode;

        public void ClearSiteDeparturePending()
        {
            IsSiteDeparturePending = false;
            SiteDepartureVirtualPosition = default;
            SiteDepartureBoundaryEntry = default;
            SiteDepartureFootprintHex = default;
            SiteDepartureExitHex = default;
        }

        public void SetSiteDepartureVirtualPosition(WorldVec2 pos) =>
            SiteDepartureVirtualPosition = pos;

        public void SetSiteDepartureVirtualPosition(WorldVec2 pos, float hexSize)
        {
            SiteDepartureVirtualPosition = pos;
            CurrentHex = HexMath.WorldToHex(pos.X, pos.Y, hexSize > 0f ? hexSize : 1f);
            HasPosition = true;
        }

        /// <summary>跨入 Destination Site Footprint：Authority 立即 AtWorldSite，Presentation 继续沿路径。</summary>
        public void CommitSiteArrivalAuthority(
            string siteId,
            WorldVec2 presentationPos,
            HexCoord presentationHex)
        {
            LocationKind = PlayerPartyLocationKind.AtWorldSite;
            SiteId = siteId ?? string.Empty;
            ClearSiteDeparturePending();
            SetTravelPresentation(presentationPos, presentationHex);
            WorldPosition = presentationPos;
        }

        public void SetTravelPresentation(WorldVec2 pos, HexCoord derivedHex)
        {
            TravelPresentationPosition = pos;
            CurrentHex = derivedHex;
            UsesTravelPresentation = true;
            HasPosition = true;
        }

        public void ClearTravelPresentation() => UsesTravelPresentation = false;

        public WorldVec2 ResolveTravelPresentationWorld(float hexSize)
        {
            if (IsSiteDeparturePending)
                return SiteDepartureVirtualPosition;
            if (UsesTravelPresentation)
                return TravelPresentationPosition;
            return WorldPosition;
        }

        public void SetAtWorldSite(string siteId, HexCoord presenceHex, float hexSize)
        {
            LocationKind = PlayerPartyLocationKind.AtWorldSite;
            SiteId = siteId ?? string.Empty;
            CurrentHex = presenceHex;
            HexMath.ToWorldPosition(presenceHex, hexSize, out var x, out var y);
            WorldPosition = new WorldVec2(x, y);
            HasPosition = true;
            ClearMovementKeepMembers();
            ClearSiteDeparturePending();
            UsesTravelPresentation = false;
        }

        public void SetAtWorldPosition(WorldVec2 worldPos, HexCoord derivedHex)
        {
            LocationKind = PlayerPartyLocationKind.AtWorldPosition;
            SiteId = string.Empty;
            WorldPosition = worldPos;
            CurrentHex = derivedHex;
            HasPosition = true;
            ClearMovementKeepMembers();
            ClearSiteDeparturePending();
            UsesTravelPresentation = false;
        }

        public void SetIdleAt(HexCoord hex)
        {
            // Phase 2B compat：视为停在该格中心的开世界位置。
            HexMath.ToWorldPosition(hex, 1f, out var x, out var y);
            SetAtWorldPosition(new WorldVec2(x, y), hex);
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

        public void BeginAutoTravel(
            IReadOnlyList<HexCoord> path,
            HexCoord destinationHex,
            string destinationSiteId,
            HexTravelMode mode,
            float hexSize)
        {
            TravelMode = mode;
            DestinationHex = destinationHex;
            DestinationSiteId = destinationSiteId ?? string.Empty;
            _hexPath.Clear();
            if (path != null)
            {
                for (var i = 0; i < path.Count; i++)
                    _hexPath.Add(path[i]);
            }

            SegmentIndex = 0;
            SegmentProgress = 0f;
            if (_hexPath.Count < 1)
            {
                CompleteMove();
                return;
            }

            // Site Exit：Authority 保持 AtWorldSite；Presentation 由 SiteDepartureVirtualPosition 承载。
            // 非 Site 出发的 AutoTravel 不在此切换 LocationKind。

            if (_hexPath.Count == 1 || CurrentHex == destinationHex)
            {
                SnapToHexCenter(destinationHex, hexSize);
                CompleteMove();
                return;
            }

            // Phase 2C：path[0]==CurrentHex 且 off-center 时，段 0 从 live WorldPosition 出发（TryGetActiveSegmentWorld），不在此 snap。

            MovementKind = PlayerPartyMovementKind.AutoTravel;
            ExecutionMode = PlayerPartyTravelExecutionMode.World;
            StepTotalTicks = Math.Max(4, 8);
            StepRemainingTicks = StepTotalTicks;
        }

        public void BeginSiteDepartureTravel(
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
            BeginAutoTravel(path, destinationHex, destinationSiteId, mode, hexSize);
            if (!IsMoving)
                return;

            IsSiteDeparturePending = true;
            SiteDepartureFootprintHex = footprintHex;
            SiteDepartureExitHex = exitHex;
            SiteDepartureBoundaryEntry = boundaryEntryWorld;
            SiteDepartureVirtualPosition = footprintCenterWorld;
            UsesTravelPresentation = false;
        }

        /// <summary>Phase 2B compat path setter.</summary>
        internal void SetHexPath(IReadOnlyList<HexCoord> path, HexCoord destination, HexTravelMode mode) =>
            BeginAutoTravel(path, destination, string.Empty, mode, 1f);

        internal void CompleteMove()
        {
            _hexPath.Clear();
            SegmentIndex = 0;
            SegmentProgress = 0f;
            StepRemainingTicks = 0;
            StepTotalTicks = 0;
            DestinationHex = CurrentHex;
            DestinationSiteId = string.Empty;
            MovementKind = PlayerPartyMovementKind.Idle;
            ExecutionMode = PlayerPartyTravelExecutionMode.None;
            ClearSiteDeparturePending();
            UsesTravelPresentation = false;
        }

        internal void CancelAtCurrentHex() => CompleteMove();

        public void CancelAutoTravelPreservePosition() => CompleteMove();

        internal void AdvanceToHex(HexCoord hex)
        {
            CurrentHex = hex;
            HasPosition = true;
        }

        public void SetWorldPositionInternal(WorldVec2 pos, HexCoord derivedHex)
        {
            WorldPosition = pos;
            CurrentHex = derivedHex;
            LocationKind = PlayerPartyLocationKind.AtWorldPosition;
            SiteId = string.Empty;
            HasPosition = true;
            ClearSiteDeparturePending();
            UsesTravelPresentation = false;
        }

        public void SnapToHexCenter(HexCoord hex, float hexSize)
        {
            HexMath.ToWorldPosition(hex, hexSize, out var x, out var y);
            SetWorldPositionInternal(new WorldVec2(x, y), hex);
        }

        internal void SetStep(int total, int remaining, float progress)
        {
            StepTotalTicks = total;
            StepRemainingTicks = remaining;
            SegmentProgress = progress;
            StepProgressCompat(progress);
        }

        void StepProgressCompat(float progress) => SegmentProgress = progress;

        internal void IncrementPathIndex() => SegmentIndex++;

        public void SetSegment(int index, float progress)
        {
            SegmentIndex = index;
            SegmentProgress = Math.Max(0f, Math.Min(1f, progress));
        }

        public bool TryGetActiveStepHexes(out HexCoord from, out HexCoord to)
        {
            from = CurrentHex;
            to = CurrentHex;
            if (_hexPath.Count < 2 || SegmentIndex < 0 || SegmentIndex >= _hexPath.Count - 1)
                return false;
            from = _hexPath[SegmentIndex];
            to = _hexPath[SegmentIndex + 1];
            return true;
        }

        /// <summary>当前 AutoTravel 段的世界几何：fromPos → toCenter（首段 from 可为任意 WorldPosition）。</summary>
        public bool TryGetActiveSegmentWorld(
            float hexSize,
            out WorldVec2 fromPos,
            out WorldVec2 toPos)
        {
            fromPos = WorldPosition;
            toPos = WorldPosition;
            if (!IsMoving || _hexPath.Count < 2)
                return false;

            if (SegmentIndex >= _hexPath.Count - 1)
                return false;

            var toHex = _hexPath[SegmentIndex + 1];
            HexMath.ToWorldPosition(toHex, hexSize, out var tx, out var ty);
            toPos = new WorldVec2(tx, ty);

            if (SegmentIndex == 0)
            {
                fromPos = ResolveTravelPresentationWorld(hexSize);
                return true;
            }

            var fromHex = _hexPath[SegmentIndex];
            HexMath.ToWorldPosition(fromHex, hexSize, out var fx, out var fy);
            fromPos = new WorldVec2(fx, fy);
            return true;
        }

        void ClearMovementKeepMembers()
        {
            _hexPath.Clear();
            SegmentIndex = 0;
            SegmentProgress = 0f;
            StepRemainingTicks = 0;
            StepTotalTicks = 0;
            MovementKind = PlayerPartyMovementKind.Idle;
            ExecutionMode = PlayerPartyTravelExecutionMode.None;
            DestinationHex = CurrentHex;
            DestinationSiteId = string.Empty;
            ClearSiteDeparturePending();
            UsesTravelPresentation = false;
        }
    }
}
