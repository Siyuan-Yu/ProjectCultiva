using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    /// <summary>VS0.7: Content-driven opening／playable-day assembly (no Core rules).</summary>
    public sealed class OpeningScenarioDefinition
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; }
        public string ScheduleId { get; set; }
        public string OpeningFactionId { get; set; }
        /// <summary>VS0.8: optional opening settlement definition id.</summary>
        public string OpeningSettlementId { get; set; }
        /// <summary>VS0.9: optional opening world region（旧 VS；正式 Ch01 用 localPlaceSet）。</summary>
        public string OpeningWorldRegionId { get; set; }
        /// <summary>村内地点表（绑 mapLayout）。</summary>
        public string OpeningLocalPlaceSetId { get; set; }
        /// <summary>宏观 WorldGraph id（[113]）。</summary>
        public string OpeningWorldGraphId { get; set; }
        /// <summary>Chapter Production: optional opening chapter definition id.</summary>
        public string OpeningChapterId { get; set; }
        public List<OpeningSpawnEntry> Spawns { get; set; } = new List<OpeningSpawnEntry>();
        public List<OpeningRelationEntry> OpeningRelations { get; set; } = new List<OpeningRelationEntry>();
    }

    public sealed class OpeningSpawnEntry
    {
        public string DefinitionId { get; set; }
        /// <summary>character | npc</summary>
        public string EntityKind { get; set; } = "character";
        public string DisplayName { get; set; }
        public bool AssignOpeningFaction { get; set; }
        public string FactionRole { get; set; }
        public bool BindSchedule { get; set; } = true;
        public bool BindDailyTask { get; set; } = true;
        public bool Recruitable { get; set; }
        /// <summary>VS0.8: Labor | Gather | Cultivate (optional).</summary>
        public string WorkRole { get; set; }
        /// <summary>Optional per-spawn schedule override (Reference Level AI).</summary>
        public string ScheduleId { get; set; }
        /// <summary>Mortal | Cultivator | Supervisor (Reference Level AI archetype).</summary>
        public string AiRole { get; set; }
        /// <summary>NPC Simulation: JobDefinition id (e.g. base:job_herb_farmer).</summary>
        public string JobId { get; set; }
    }

    public sealed class OpeningRelationEntry
    {
        public string FromDefinitionId { get; set; }
        public string ToDefinitionId { get; set; }
        public int Delta { get; set; }
        public string ReasonTag { get; set; }
        public bool Mutual { get; set; } = true;
    }
}
