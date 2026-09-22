using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using XianXia.Core.Domain.Ids;
using XianXia.Core.World;

namespace XianXia.Core.World.Strategic
{
    /// <summary>
    /// PlayerParty Continuous Surface position and travel authority.
    /// It stores exact world positions and has no derived grid state.
    /// </summary>
    public sealed class PlayerPartyWorldMotion
    {
        readonly List<EntityId> _travelingMembers = new List<EntityId>(6);
        readonly List<WorldVec2> _continuousSurfaceRoute = new List<WorldVec2>(128);
        ReadOnlyCollection<WorldVec2> _continuousSurfaceRouteView;

        public PlayerPartyLocationKind LocationKind { get; private set; } =
            PlayerPartyLocationKind.AtWorldSite;
        public PlayerPartyMovementKind MovementKind { get; private set; } =
            PlayerPartyMovementKind.Idle;
        public PlayerPartyTravelExecutionMode ExecutionMode { get; private set; } =
            PlayerPartyTravelExecutionMode.None;

        public string SiteId { get; private set; } = string.Empty;
        public string SurfaceId { get; private set; } = string.Empty;
        public WorldVec2 WorldPosition { get; private set; }
        public bool HasPosition { get; private set; }

        public string DestinationSiteId { get; private set; } = string.Empty;
        public bool HasContinuousPhysicalDestination { get; private set; }
        public WorldVec2 ContinuousPhysicalDestination { get; private set; }
        public float ContinuousPhysicalArrivalRadius { get; private set; }
        public int TravelPlanVersion { get; private set; }
        public int ContinuousSurfaceRouteIndex { get; private set; }
        public IReadOnlyList<WorldVec2> ContinuousSurfaceRoute =>
            _continuousSurfaceRouteView ??
            (_continuousSurfaceRouteView = _continuousSurfaceRoute.AsReadOnly());
        public bool HasContinuousSurfaceRoute => _continuousSurfaceRoute.Count > 1;
        public bool IsMoving => MovementKind == PlayerPartyMovementKind.AutoTravel;
        public IReadOnlyList<EntityId> TravelingMembers => _travelingMembers;

        /// <summary>
        /// Physical WorldSite region containing the exact outdoor position, when any.
        /// This is context, never a second location authority.
        /// </summary>
        public string CurrentOutdoorWorldSiteId { get; private set; } = string.Empty;

        public void SetCurrentOutdoorWorldSiteContext(string siteId) =>
            CurrentOutdoorWorldSiteId = siteId ?? string.Empty;

        public void CaptureTravelingMembers(IReadOnlyList<EntityId> members)
        {
            _travelingMembers.Clear();
            if (members == null)
                return;
            for (var i = 0; i < members.Count; i++)
                if (!members[i].IsNone)
                    _travelingMembers.Add(members[i]);
        }

        /// <summary>
        /// Places the party at an exact Continuous Surface position and clears stale travel state.
        /// </summary>
        public void SetAtSurfacePosition(string surfaceId, WorldVec2 worldPosition)
        {
            if (string.IsNullOrWhiteSpace(surfaceId))
                throw new ArgumentException("surfaceId required.", nameof(surfaceId));
            SurfaceId = surfaceId;
            WorldPosition = worldPosition;
            LocationKind = PlayerPartyLocationKind.AtWorldPosition;
            SiteId = string.Empty;
            HasPosition = true;
            CurrentOutdoorWorldSiteId = string.Empty;
            InvalidateTravelPlanAndClearMovement();
        }

        public void BeginSurfaceAutoTravel(
            string surfaceId,
            WorldVec2 destinationWorldPosition,
            string destinationSiteId,
            float arrivalRadius,
            IReadOnlyList<WorldVec2> continuousRoute)
        {
            if (string.IsNullOrWhiteSpace(surfaceId))
                throw new ArgumentException("surfaceId required.", nameof(surfaceId));
            TravelPlanVersion++;
            SurfaceId = surfaceId;
            DestinationSiteId = destinationSiteId ?? string.Empty;
            HasContinuousPhysicalDestination = true;
            ContinuousPhysicalDestination = destinationWorldPosition;
            ContinuousPhysicalArrivalRadius = Math.Max(0.001f, arrivalRadius);
            _continuousSurfaceRoute.Clear();
            if (continuousRoute != null)
                for (var i = 0; i < continuousRoute.Count; i++)
                    _continuousSurfaceRoute.Add(continuousRoute[i]);
            ContinuousSurfaceRouteIndex = _continuousSurfaceRoute.Count > 1 ? 1 : 0;
            MovementKind = PlayerPartyMovementKind.AutoTravel;
            ExecutionMode = PlayerPartyTravelExecutionMode.SurfaceVisible;
        }

        public bool TryGetContinuousSurfaceWaypoint(out WorldVec2 waypoint)
        {
            waypoint = default;
            if (ContinuousSurfaceRouteIndex < 0 ||
                ContinuousSurfaceRouteIndex >= _continuousSurfaceRoute.Count)
                return false;
            waypoint = _continuousSurfaceRoute[ContinuousSurfaceRouteIndex];
            return true;
        }

        public void AdvanceContinuousSurfaceWaypoint()
        {
            if (ContinuousSurfaceRouteIndex < _continuousSurfaceRoute.Count)
                ContinuousSurfaceRouteIndex++;
        }

        public void AdvanceContinuousSurfaceRouteTo(int nextIndex)
        {
            if (nextIndex > ContinuousSurfaceRouteIndex)
                ContinuousSurfaceRouteIndex = Math.Min(nextIndex, _continuousSurfaceRoute.Count);
        }

        /// <summary>Accepts a legal physical step while preserving the active route.</summary>
        public void UpdateSurfaceWorldPosition(string surfaceId, WorldVec2 position)
        {
            if (string.IsNullOrWhiteSpace(surfaceId) ||
                !string.Equals(SurfaceId, surfaceId, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Surface position provenance changed during travel.");
            WorldPosition = position;
            LocationKind = PlayerPartyLocationKind.AtWorldPosition;
            SiteId = string.Empty;
            HasPosition = true;
        }

        public void CancelAutoTravelPreservePosition() =>
            InvalidateTravelPlanAndClearMovement();

        void InvalidateTravelPlanAndClearMovement()
        {
            TravelPlanVersion++;
            DestinationSiteId = string.Empty;
            HasContinuousPhysicalDestination = false;
            ContinuousPhysicalDestination = default;
            ContinuousPhysicalArrivalRadius = 0f;
            _continuousSurfaceRoute.Clear();
            ContinuousSurfaceRouteIndex = 0;
            MovementKind = PlayerPartyMovementKind.Idle;
            ExecutionMode = PlayerPartyTravelExecutionMode.None;
        }
    }
}
