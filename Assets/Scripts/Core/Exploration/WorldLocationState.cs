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
        /// <summary>该地点所属 LocalMap；空＝地表／默认图。</summary>
        public string LocalMapId { get; set; } = string.Empty;
        /// <summary>从此处可进入的 LocalMap（洞口等）。</summary>
        public string EnterLocalMapId { get; set; } = string.Empty;
        /// <summary>进入后队伍落点地点 id。</summary>
        public string EnterSpawnLocationId { get; set; } = string.Empty;
        /// <summary>已废弃：勘查半径＝角色神识，不再用门槛。</summary>
        public int SurveySenseRequired { get; set; }
    }
}
