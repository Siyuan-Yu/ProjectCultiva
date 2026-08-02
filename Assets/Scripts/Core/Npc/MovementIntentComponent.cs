using XianXia.Core.Entities;

namespace XianXia.Core.Npc
{
    /// <summary>
    /// Core↔Host move bridge for MoveAction. Host pathfinds and sets <see cref="HostArrived"/>.
    /// Session-only; not in Snapshot v1.
    /// </summary>
    public sealed class MovementIntentComponent : IComponent
    {
        public bool Active { get; set; }
        public string TargetLocationId { get; set; } = string.Empty;
        public string TargetWorkAreaId { get; set; } = string.Empty;
        public bool HostArrived { get; set; }

        public void Begin(string locationId, string workAreaId)
        {
            Active = true;
            TargetLocationId = locationId ?? string.Empty;
            TargetWorkAreaId = workAreaId ?? string.Empty;
            HostArrived = false;
        }

        public void Clear()
        {
            Active = false;
            TargetLocationId = string.Empty;
            TargetWorkAreaId = string.Empty;
            HostArrived = false;
        }
    }
}
