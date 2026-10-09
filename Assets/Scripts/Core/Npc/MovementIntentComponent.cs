using XianXia.Core.Entities;

namespace XianXia.Core.Npc
{
    public enum MovementHostPathState
    {
        Pending = 0,
        RetryablePathUnavailable = 1,
        Arrived = 2,
        PermanentFailure = 3
    }

    /// <summary>
    /// Core↔Host move bridge for MoveAction. Host pathfinds and sets <see cref="HostArrived"/>.
    /// Session-only; not in Snapshot v1.
    /// </summary>
    public sealed class MovementIntentComponent : IComponent
    {
        public bool Active { get; set; }
        public string TargetLocationId { get; set; } = string.Empty;
        public string TargetWorkAreaId { get; set; } = string.Empty;
        public bool HasWorldTarget { get; private set; }
        public string TargetSurfaceId { get; private set; } = string.Empty;
        public float TargetWorldX { get; private set; }
        public float TargetWorldY { get; private set; }
        /// <summary>Stable identity for exact world targets whose coordinates may be close.</summary>
        public string DestinationKey { get; private set; } = string.Empty;
        /// <summary>Core-authoritative world-space arrival radius; zero uses the Host's generic policy.</summary>
        public float ArrivalRadiusWorld { get; private set; }
        public float SpeedMultiplier { get; private set; } = 1f;
        public uint Revision { get; private set; }
        public MovementHostPathState HostPathState { get; private set; }
        public bool HostPathRequested { get; private set; }
        public bool HostPathAccepted { get; private set; }
        public string HostPathFailureReason { get; private set; } = string.Empty;
        public bool HostArrived
        {
            get => HostPathState == MovementHostPathState.Arrived;
            set
            {
                if (value) HostPathState = MovementHostPathState.Arrived;
                else if (HostPathState == MovementHostPathState.Arrived)
                    HostPathState = MovementHostPathState.Pending;
            }
        }
        /// <summary>Soft work slot; Host picks interact spot / ring offset.</summary>
        public int SlotIndex { get; set; } = -1;

        public void Begin(string locationId, string workAreaId, int slotIndex = -1, float speedMultiplier = 1f)
        {
            Revision++;
            Active = true;
            HasWorldTarget = false;
            DestinationKey = string.Empty;
            ArrivalRadiusWorld = 0f;
            TargetLocationId = locationId ?? string.Empty;
            TargetWorkAreaId = workAreaId ?? string.Empty;
            SlotIndex = slotIndex;
            SpeedMultiplier = SanitizeSpeedMultiplier(speedMultiplier);
            HostPathState = MovementHostPathState.Pending;
            HostPathRequested = false;
            HostPathAccepted = false;
            HostPathFailureReason = string.Empty;
        }

        public void BeginWorldPoint(string surfaceId, float worldX, float worldY, float speedMultiplier = 1f,
            string destinationKey = "", float arrivalRadiusWorld = 0f)
        {
            Revision++;
            Active = true;
            TargetLocationId = TargetWorkAreaId = string.Empty;
            SlotIndex = -1;
            HasWorldTarget = true;
            TargetSurfaceId = surfaceId ?? string.Empty;
            TargetWorldX = worldX;
            TargetWorldY = worldY;
            DestinationKey = destinationKey ?? string.Empty;
            ArrivalRadiusWorld = SanitizeArrivalRadius(arrivalRadiusWorld);
            SpeedMultiplier = SanitizeSpeedMultiplier(speedMultiplier);
            HostPathState = MovementHostPathState.Pending;
            HostPathRequested = false;
            HostPathAccepted = false;
            HostPathFailureReason = string.Empty;
        }

        public void MarkPathRequested()
        {
            if (!Active) return;
            HostPathRequested = true;
            HostPathAccepted = true;
            HostPathFailureReason = string.Empty;
            HostPathState = MovementHostPathState.Pending;
        }

        public void MarkRetryablePathUnavailable(string reason = "")
        {
            if (!Active) return;
            HostPathRequested = true;
            HostPathAccepted = false;
            HostPathFailureReason = reason ?? string.Empty;
            HostPathState = MovementHostPathState.RetryablePathUnavailable;
        }

        public void MarkPermanentFailure()
        {
            Active = false;
            HostPathRequested = true;
            HostPathAccepted = false;
            HostPathFailureReason = "Permanent path failure.";
            HostPathState = MovementHostPathState.PermanentFailure;
        }

        public void Clear()
        {
            if (!Active && !HasWorldTarget && string.IsNullOrEmpty(TargetLocationId) &&
                string.IsNullOrEmpty(TargetWorkAreaId) && !HostPathRequested && SpeedMultiplier == 1f)
                return;
            Revision++;
            Active = false;
            HasWorldTarget = false;
            TargetSurfaceId = string.Empty;
            TargetLocationId = string.Empty;
            TargetWorkAreaId = string.Empty;
            DestinationKey = string.Empty;
            ArrivalRadiusWorld = 0f;
            SlotIndex = -1;
            SpeedMultiplier = 1f;
            HostPathState = MovementHostPathState.Pending;
            HostPathRequested = false;
            HostPathAccepted = false;
            HostPathFailureReason = string.Empty;
        }

        static float SanitizeSpeedMultiplier(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) || value <= 0f ? 1f : value;

        static float SanitizeArrivalRadius(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) || value < 0f ? 0f : value;
    }
}
