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
        /// <summary>Offset from location presentation center (content data, not code hardcode).</summary>
        public float OffsetX { get; set; }
        public float OffsetZ { get; set; }
    }
}
