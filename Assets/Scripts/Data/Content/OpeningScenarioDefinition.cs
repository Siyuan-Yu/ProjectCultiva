using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    /// <summary>Spawn instance 的可选 LocalMap 初始呈现坐标；(0,0) 也是合法值。</summary>
    public sealed class OpeningLocalPositionDefinition
    {
        public float X { get; set; }
        public float Z { get; set; }
    }

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
        /// <summary>正式 Hex 战略大地图 content id。</summary>
        public string OpeningHexWorldId { get; set; }
        /// <summary>Chapter Production: optional opening chapter definition id.</summary>
        public string OpeningChapterId { get; set; }
        public List<OpeningSpawnEntry> Spawns { get; set; } = new List<OpeningSpawnEntry>();
        public List<OpeningRelationEntry> OpeningRelations { get; set; } = new List<OpeningRelationEntry>();

        /// <summary>Phase 5S：开局实例化的 FormalArmyDefinition ids（顺序即创建顺序）。</summary>
        public List<string> InitialFormalArmyIds { get; set; } = new List<string>();
    }

    public sealed class OpeningSpawnEntry
    {
        public string DefinitionId { get; set; }
        /// <summary>character | npc</summary>
        public string EntityKind { get; set; } = "character";
        public string DisplayName { get; set; }
        public bool AssignOpeningFaction { get; set; }
        public string FactionRole { get; set; }
        /// <summary>可选：覆盖 scenario.openingFactionId（如主角团 vs 压迫宗门 NPC）。</summary>
        public string FactionId { get; set; }
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

        /// <summary>
        /// Optional authored starting macro presence: New Game 开始时该 entity
        /// 的 WorldPresence = AtSite(worldSiteId)。远程 authored population
        /// （不在 DefaultStartSite）靠它获得世界 authority，而不是被默认塞到荒村。
        /// </summary>
        public string WorldSiteId { get; set; } = string.Empty;

        /// <summary>
        /// Optional authored LocalMap presentation location：进入该 WorldSite 的
        /// LocalMap 后，EntityLocationComponent.LocationId = localLocationId，
        /// 决定 LocalMap 呈现位置。只在 WorldRegion 激活该地点表时可见。
        /// </summary>
        public string LocalLocationId { get; set; } = string.Empty;

        /// <summary>
        /// 可选的 spawn instance 精确初始呈现坐标；与 LocalLocationId 可同时存在。
        /// 它不表达 WorldSite、地点语义或移动目的地。
        /// </summary>
        public OpeningLocalPositionDefinition LocalPosition { get; set; }
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
