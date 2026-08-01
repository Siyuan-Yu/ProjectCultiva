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
