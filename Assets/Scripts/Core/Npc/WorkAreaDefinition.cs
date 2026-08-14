using System.Collections.Generic;

namespace XianXia.Core.Npc
{
    /// <summary>Data-driven activity range: binds to a Location plus optional presentation offset.</summary>
    public sealed class WorkAreaDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string LocationId { get; set; } = string.Empty;
        public List<string> Tags { get; } = new List<string>();
        public List<string> AllowedActivities { get; } = new List<string>();
        /// <summary>Max concurrent workers at this area (soft slots). Default 4.</summary>
        public int Capacity { get; set; } = 4;
        /// <summary>
        /// If non-empty, only entities whose personality tags intersect may use this area
        /// for housing activities (Rest／Eat／Idle). Empty = open to all.
        /// </summary>
        public List<string> ResidentTags { get; } = new List<string>();
        /// <summary>Settlement control core (主管府): attackable hub — not housing.</summary>
        public bool IsControlCore { get; set; }
        /// <summary>Max durability when <see cref="IsControlCore"/>; 0 → default 100 at register.</summary>
        public int MaxDurability { get; set; }
        /// <summary>Flat damage reduction when struck as control core (御敌／防御).</summary>
        public int Defense { get; set; }
        /// <summary>Seconds party must stand on breached core to capture (default 10).</summary>
        public float OccupyHoldSeconds { get; set; } = 10f;
        /// <summary>Privileges granted on capture (e.g. manageHousing／manageSchedules).</summary>
        public List<string> GrantsPrivileges { get; } = new List<string>();
        /// <summary>Offset from location presentation center (content data, not code hardcode).</summary>
        public float OffsetX { get; set; }
        public float OffsetZ { get; set; }
    }
}
