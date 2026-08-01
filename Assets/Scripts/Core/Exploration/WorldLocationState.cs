using System;
using System.Collections.Generic;

namespace XianXia.Core.Exploration
{
    public sealed class WorldLocationState
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public LocationKind Kind { get; set; }
        public List<string> AdjacentIds { get; } = new List<string>();
        public string ResourceOnExploreId { get; set; } = string.Empty;
        public int ResourceOnExploreAmount { get; set; }
        public string OpportunitySiteId { get; set; } = string.Empty;
        public string ResidentNpcDefinitionId { get; set; } = string.Empty;
        /// <summary>Presentation hint for Host top-down layout (not a gameplay rule).</summary>
        public float PresentationX { get; set; }
        public float PresentationZ { get; set; }
    }
}
