using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// FormalArmy 世界位置 + 旅行状态真源（Phase 3）。
    /// WorldPosition 为 Wilderness 权威；AtWorldSite 时投影 Representative Hex（Anchor/Presence）。
    /// </summary>
    public sealed class FormalArmyWorldMotion
    {
        readonly List<HexCoord> _hexPath = new List<HexCoord>(32);
        ReadOnlyCollection<HexCoord> _hexPathView;

        public FormalArmyLocationKind LocationKind { get; private set; } = FormalArmyLocationKind.Unknown;
        public FormalArmyMovementKind MovementKind { get; private set; } = FormalArmyMovementKind.Idle;
        public FormalArmyOrderKind CurrentOrderKind { get; private set; } = FormalArmyOrderKind.None;
        public string OrderTargetArmyId { get; private set; } = string.Empty;
        public string SiteId { get; private set; } = string.Empty;
        public WorldVec2 WorldPosition { get; private set; }
        public HexCoord CurrentHex { get; private set; }
        public HexCoord DestinationHex { get; private set; }
        public string DestinationSiteId { get; private set; } = string.Empty;
        public HexTravelMode TravelMode { get; private set; } = HexTravelMode.Ground;
        public int SegmentIndex { get; private set; }
        public float SegmentProgress { get; private set; }
        public bool HasPosition { get; private set; }
        public ulong LastProcessedWorldTick { get; set; }

        public bool IsMoving => MovementKind == FormalArmyMovementKind.AutoTravel;

        public bool IsSiteDeparturePending { get; private set; }
        public WorldVec2 SiteDepartureVirtualPosition { get; private set; }
        public WorldVec2 SiteDepartureBoundaryEntry { get; private set; }
        public HexCoord SiteDepartureFootprintHex { get; private set; }
        public HexCoord SiteDepartureExitHex { get; private set; }

        public int HexPathCount => _hexPath.Count;

        public IReadOnlyList<HexCoord> HexPath =>
            _hexPathView ?? (_hexPathView = _hexPath.AsReadOnly());

        public void ClearTravel()
        {
            _hexPath.Clear();
            SegmentIndex = 0;
            SegmentProgress = 0f;
            MovementKind = FormalArmyMovementKind.Idle;
            CurrentOrderKind = FormalArmyOrderKind.None;
            OrderTargetArmyId = string.Empty;
            DestinationHex = CurrentHex;
            DestinationSiteId = string.Empty;
            ClearSiteDeparturePending();
        }

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

        public void SetAtWorldSite(string siteId, HexCoord representativeHex, float hexSize)
        {
            LocationKind = FormalArmyLocationKind.AtWorldSite;
            SiteId = siteId ?? string.Empty;
            CurrentHex = representativeHex;
            HexMath.ToWorldPosition(representativeHex, hexSize, out var x, out var y);
            WorldPosition = new WorldVec2(x, y);
            HasPosition = true;
            ClearTravel();
        }

        public void SetAtWorldPosition(WorldVec2 worldPos, HexCoord derivedHex)
        {
            LocationKind = FormalArmyLocationKind.AtWorldPosition;
            SiteId = string.Empty;
            WorldPosition = worldPos;
            CurrentHex = derivedHex;
            HasPosition = true;
            ClearTravel();
        }

        public void BeginAutoTravel(
            FormalArmyOrderKind orderKind,
            IReadOnlyList<HexCoord> path,
            HexCoord destinationHex,
            string destinationSiteId,
            HexTravelMode mode)
        {
            TravelMode = mode;
            CurrentOrderKind = orderKind;
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
            if (_hexPath.Count < 2)
            {
                ClearTravel();
                return;
            }

            MovementKind = FormalArmyMovementKind.AutoTravel;
        }

        public void BeginSiteDepartureTravel(
            FormalArmyOrderKind orderKind,
            IReadOnlyList<HexCoord> path,
            HexCoord destinationHex,
            string destinationSiteId,
            HexCoord footprintHex,
            HexCoord exitHex,
            WorldVec2 footprintCenterWorld,
            WorldVec2 boundaryEntryWorld,
            HexTravelMode mode)
        {
            BeginAutoTravel(orderKind, path, destinationHex, destinationSiteId, mode);
            if (!IsMoving)
                return;

            IsSiteDeparturePending = true;
            SiteDepartureFootprintHex = footprintHex;
            SiteDepartureExitHex = exitHex;
            SiteDepartureBoundaryEntry = boundaryEntryWorld;
            SiteDepartureVirtualPosition = footprintCenterWorld;
        }

        public void SetWorldPositionInternal(WorldVec2 pos, HexCoord derivedHex)
        {
            WorldPosition = pos;
            CurrentHex = derivedHex;
            LocationKind = FormalArmyLocationKind.AtWorldPosition;
            SiteId = string.Empty;
            HasPosition = true;
        }

        public void SetAttackOrder(string targetArmyId)
        {
            OrderTargetArmyId = targetArmyId ?? string.Empty;
            CurrentOrderKind = FormalArmyOrderKind.AttackFormalArmy;
        }

        public void ClearOrderTarget()
        {
            OrderTargetArmyId = string.Empty;
            if (CurrentOrderKind == FormalArmyOrderKind.AttackFormalArmy)
                CurrentOrderKind = FormalArmyOrderKind.None;
        }

        public void SetSegment(int index, float progress)
        {
            SegmentIndex = index;
            SegmentProgress = Math.Max(0f, Math.Min(1f, progress));
        }

        public void IncrementPathIndex() => SegmentIndex++;

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

        public bool TryGetActiveSegmentWorld(
            float hexSize,
            out WorldVec2 fromPos,
            out WorldVec2 toPos)
        {
            fromPos = WorldPosition;
            toPos = WorldPosition;
            if (!IsMoving || _hexPath.Count < 2 || SegmentIndex >= _hexPath.Count - 1)
                return false;

            var toHex = _hexPath[SegmentIndex + 1];
            HexMath.ToWorldPosition(toHex, hexSize, out var tx, out var ty);
            toPos = new WorldVec2(tx, ty);

            if (SegmentIndex == 0)
            {
                fromPos = IsSiteDeparturePending ? SiteDepartureVirtualPosition : WorldPosition;
                return true;
            }

            var fromHex = _hexPath[SegmentIndex];
            HexMath.ToWorldPosition(fromHex, hexSize, out var fx, out var fy);
            fromPos = new WorldVec2(fx, fy);
            return true;
        }

        internal void CopyPathInto(List<HexCoord> into)
        {
            into.Clear();
            for (var i = 0; i < _hexPath.Count; i++)
                into.Add(_hexPath[i]);
        }

        internal void RestorePath(
            FormalArmyOrderKind orderKind,
            IReadOnlyList<HexCoord> path,
            HexCoord destinationHex,
            string destinationSiteId,
            int segmentIndex,
            float segmentProgress,
            string orderTargetArmyId = null)
        {
            CurrentOrderKind = orderKind;
            OrderTargetArmyId = orderTargetArmyId ?? string.Empty;
            if (orderKind != FormalArmyOrderKind.AttackFormalArmy)
                OrderTargetArmyId = string.Empty;
            DestinationHex = destinationHex;
            DestinationSiteId = destinationSiteId ?? string.Empty;
            _hexPath.Clear();
            if (path != null)
            {
                for (var i = 0; i < path.Count; i++)
                    _hexPath.Add(path[i]);
            }

            SegmentIndex = segmentIndex;
            SegmentProgress = segmentProgress;
            MovementKind = _hexPath.Count >= 2
                ? FormalArmyMovementKind.AutoTravel
                : FormalArmyMovementKind.Idle;
        }
    }
}
