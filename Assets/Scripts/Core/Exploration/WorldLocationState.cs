using System.Collections.Generic;
using XianXia.Core.Content;

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
        /// <summary>Must all pass before Travel into this location.</summary>
        public List<ContentCondition> EnterConditions { get; } = new List<ContentCondition>();
        /// <summary>Quest ids to TryStart on arrive (offer conditions still apply).</summary>
        public List<string> QuestOfferIds { get; } = new List<string>();
        /// <summary>Content tags for WorkArea／Job matching (e.g. herb, mine).</summary>
        public List<string> Tags { get; } = new List<string>();
        /// <summary>ScheduleActivity names allowed here (Labor, Patrol, …). Empty = unrestricted.</summary>
        public List<string> AllowedActivities { get; } = new List<string>();
    }
}
