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
        public MovementHostPathState HostPathState { get; private set; }
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

        public void Begin(string locationId, string workAreaId, int slotIndex = -1)
        {
            Active = true;
            TargetLocationId = locationId ?? string.Empty;
            TargetWorkAreaId = workAreaId ?? string.Empty;
            SlotIndex = slotIndex;
            HostPathState = MovementHostPathState.Pending;
        }

        public void MarkPathRequested()
        {
            if (Active) HostPathState = MovementHostPathState.Pending;
        }

        public void MarkRetryablePathUnavailable()
        {
            if (Active) HostPathState = MovementHostPathState.RetryablePathUnavailable;
        }

        public void MarkPermanentFailure()
        {
            Active = false;
            HostPathState = MovementHostPathState.PermanentFailure;
        }

        public void Clear()
        {
            Active = false;
            TargetLocationId = string.Empty;
            TargetWorkAreaId = string.Empty;
            SlotIndex = -1;
            HostPathState = MovementHostPathState.Pending;
        }
    }
}
