using System.Collections.Generic;
using XianXia.Core.Content;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    public sealed class WorldRegionDefinition
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; }
        public string StartLocationId { get; set; }
        public List<WorldLocationEntry> Locations { get; set; } = new List<WorldLocationEntry>();
    }

    public sealed class WorldLocationEntry
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Kind { get; set; }
        public List<string> AdjacentIds { get; set; } = new List<string>();
        public string ResourceOnExploreId { get; set; }
        public int ResourceOnExploreAmount { get; set; }
        public string OpportunitySiteId { get; set; }
        public string ResidentNpcDefinitionId { get; set; }
        public float PresentationX { get; set; }
        public float PresentationZ { get; set; }
        public List<ContentCondition> EnterConditions { get; set; } = new List<ContentCondition>();
        public List<string> QuestOfferIds { get; set; } = new List<string>();
        public List<string> Tags { get; set; } = new List<string>();
        public List<string> AllowedActivities { get; set; } = new List<string>();
        /// <summary>所属 LocalMap；空＝地表。</summary>
        public string LocalMapId { get; set; }
        /// <summary>洞口：进入的 mapLayout id。</summary>
        public string EnterLocalMapId { get; set; }
        /// <summary>进入后落点地点。</summary>
        public string EnterSpawnLocationId { get; set; }
        /// <summary>勘查所需神识；0＝默认 3。</summary>
        public int SurveySenseRequired { get; set; }
    }
}
