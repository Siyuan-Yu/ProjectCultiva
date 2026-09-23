using System.Collections.Generic;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    public sealed class WorldOpportunityDirectorDefinition
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SurfaceId { get; set; } = string.Empty;
        public int TargetActiveMin { get; set; }
        public int TargetActiveMax { get; set; }
    }

    public sealed class WorldOpportunityDefinition
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SurfaceId { get; set; } = string.Empty;
        public int Weight { get; set; } = 1;
        public int MaxActive { get; set; } = 1;
        public string SpawnTableId { get; set; } = string.Empty;
        public int DurationDays { get; set; } = 1;
        public float MinPlayerDistanceWorld { get; set; }
        public float MaxPlayerDistanceWorld { get; set; }
        public bool AllowInsideWorldSite { get; set; }
        public string DiscoveryMode { get; set; } = "worldVisible";
        public string PublicNoticeText { get; set; } = string.Empty;
        public string PublicNoticeTitle { get; set; } = string.Empty;
        public bool PublicNoticeRevealExactLocation { get; set; }
        public List<ContentCondition> Conditions { get; } = new List<ContentCondition>();
        public List<ContentOutcome> ExpireOutcomes { get; } = new List<ContentOutcome>();
    }
}
