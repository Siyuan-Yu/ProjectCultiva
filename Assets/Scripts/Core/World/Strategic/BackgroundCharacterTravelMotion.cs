using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using XianXia.Core.World.Hex;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// Background Character 世界旅行运动状态（Phase 2D）。
    /// 与 WorldLocation（WorldPresence）分离；TravelState 不充当位置真源。
    /// </summary>
    public sealed class BackgroundCharacterTravelMotion
    {
        readonly List<HexCoord> _hexPath = new List<HexCoord>(32);
        ReadOnlyCollection<HexCoord> _hexPathView;

        public BackgroundCharacterTravelMovementKind MovementKind { get; private set; } =
            BackgroundCharacterTravelMovementKind.Idle;

        public HexTravelMode TravelMode { get; private set; } = HexTravelMode.Ground;
        public HexCoord DestinationHex { get; private set; }
        public string DestinationSiteId { get; private set; } = string.Empty;
        public int SegmentIndex { get; private set; }
        public float SegmentProgress { get; private set; }

        /// <summary>上次 Scheduler 处理时的 Simulation WorldTick（绝对值）。</summary>
        public ulong LastProcessedWorldTick { get; set; }

        public bool IsMoving => MovementKind == BackgroundCharacterTravelMovementKind.Traveling;

        public int HexPathCount => _hexPath.Count;

        public IReadOnlyList<HexCoord> HexPath =>
            _hexPathView ?? (_hexPathView = _hexPath.AsReadOnly());

        /// <summary>WorldSite 出发过渡期：仍 AtWorldSite，用虚拟世界坐标推进至 Boundary Entry。</summary>
        public bool IsSiteDeparturePending { get; private set; }

        public WorldVec2 SiteDepartureVirtualPosition { get; private set; }

        public WorldVec2 SiteDepartureBoundaryEntry { get; private set; }

        public HexCoord SiteDepartureFootprintHex { get; private set; }

        public HexCoord SiteDepartureExitHex { get; private set; }

        public void ClearTravel()
        {
            _hexPath.Clear();
            SegmentIndex = 0;
            SegmentProgress = 0f;
            LastProcessedWorldTick = 0;
            MovementKind = BackgroundCharacterTravelMovementKind.Idle;
            DestinationHex = default;
            DestinationSiteId = string.Empty;
            TravelMode = HexTravelMode.Ground;
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

        public void BeginTravel(
            IReadOnlyList<HexCoord> path,
            HexCoord destinationHex,
            string destinationSiteId,
            HexTravelMode mode)
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
            if (_hexPath.Count < 2)
            {
                ClearTravel();
                return;
            }

            MovementKind = BackgroundCharacterTravelMovementKind.Traveling;
        }

        public void BeginSiteDepartureTravel(
            IReadOnlyList<HexCoord> path,
            HexCoord destinationHex,
            string destinationSiteId,
            HexCoord footprintHex,
            HexCoord exitHex,
            WorldVec2 footprintCenterWorld,
            WorldVec2 boundaryEntryWorld,
            HexTravelMode mode)
        {
            BeginTravel(path, destinationHex, destinationSiteId, mode);
            if (!IsMoving)
                return;

            IsSiteDeparturePending = true;
            SiteDepartureFootprintHex = footprintHex;
            SiteDepartureExitHex = exitHex;
            SiteDepartureBoundaryEntry = boundaryEntryWorld;
            SiteDepartureVirtualPosition = footprintCenterWorld;
        }

        public void CancelTravelPreserveProgress() => ClearTravel();

        public void SetSegment(int index, float progress)
        {
            SegmentIndex = index;
            SegmentProgress = Math.Max(0f, Math.Min(1f, progress));
        }

        public void IncrementPathIndex() => SegmentIndex++;

        public bool TryGetActiveSegmentWorld(
            WorldVec2 worldPosition,
            float hexSize,
            out WorldVec2 fromPos,
            out WorldVec2 toPos)
        {
            fromPos = worldPosition;
            toPos = worldPosition;
            if (!IsMoving || _hexPath.Count < 2)
                return false;
            if (SegmentIndex >= _hexPath.Count - 1)
                return false;

            var toHex = _hexPath[SegmentIndex + 1];
            HexMath.ToWorldPosition(toHex, hexSize, out var tx, out var ty);
            toPos = new WorldVec2(tx, ty);

            if (SegmentIndex == 0)
            {
                fromPos = worldPosition;
                return true;
            }

            var fromHex = _hexPath[SegmentIndex];
            HexMath.ToWorldPosition(fromHex, hexSize, out var fx, out var fy);
            fromPos = new WorldVec2(fx, fy);
            return true;
        }
    }

    public enum BackgroundCharacterTravelMovementKind
    {
        Idle = 0,
        Traveling = 1,
    }
}
