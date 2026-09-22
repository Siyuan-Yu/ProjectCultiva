using System.Collections.Generic;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>Background Character Continuous Surface route state.</summary>
    public sealed class BackgroundCharacterTravelMotion
    {
        readonly List<WorldVec2> _surfacePath = new List<WorldVec2>(64);

        public BackgroundCharacterTravelMovementKind MovementKind { get; private set; }
        public string DestinationSiteId { get; private set; } = string.Empty;
        public IReadOnlyList<WorldVec2> SurfacePath => _surfacePath;
        public string SurfaceId { get; private set; } = string.Empty;
        public WorldVec2 SurfaceDestination { get; private set; }
        public int SegmentIndex { get; private set; }
        public float SegmentProgress { get; private set; }
        public ulong LastProcessedWorldTick { get; set; }
        public bool IsMoving => MovementKind == BackgroundCharacterTravelMovementKind.Traveling;
        public bool IsSurfaceRoute => IsMoving && !string.IsNullOrEmpty(SurfaceId);

        public void ClearTravel()
        {
            _surfacePath.Clear();
            SurfaceId = string.Empty;
            SurfaceDestination = default;
            DestinationSiteId = string.Empty;
            SegmentIndex = 0;
            SegmentProgress = 0f;
            LastProcessedWorldTick = 0;
            MovementKind = BackgroundCharacterTravelMovementKind.Idle;
        }

        public void BeginSurfaceTravel(
            IReadOnlyList<WorldVec2> route,
            string surfaceId,
            WorldVec2 destination,
            string destinationSiteId)
        {
            ClearTravel();
            if (route == null || route.Count < 1 || string.IsNullOrEmpty(surfaceId))
                return;
            for (var i = 0; i < route.Count; i++)
                _surfacePath.Add(route[i]);
            SurfaceId = surfaceId;
            SurfaceDestination = destination;
            DestinationSiteId = destinationSiteId ?? string.Empty;
            SegmentIndex = _surfacePath.Count > 1 ? 1 : 0;
            MovementKind = BackgroundCharacterTravelMovementKind.Traveling;
        }

        public bool TryGetSurfaceWaypoint(out WorldVec2 waypoint)
        {
            waypoint = default;
            if (!IsSurfaceRoute || SegmentIndex < 0 || SegmentIndex >= _surfacePath.Count)
                return false;
            waypoint = _surfacePath[SegmentIndex];
            return true;
        }

        public void IncrementPathIndex()
        {
            SegmentIndex++;
            SegmentProgress = 0f;
        }

        public void SetSegmentProgress(float progress) =>
            SegmentProgress = progress < 0f ? 0f : progress > 1f ? 1f : progress;

        public void CancelTravelPreserveProgress() => ClearTravel();
    }

    public enum BackgroundCharacterTravelMovementKind
    {
        Idle = 0,
        Traveling = 1,
    }
}
