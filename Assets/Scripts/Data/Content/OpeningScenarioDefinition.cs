using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    /// <summary>
    /// Spawn 势力归属三模式：
    /// CharacterDefault = 继承 CharacterDefinition.defaultFaction*（缺省即此值，不写 JSON）；
    /// Override = 本次 Spawn 显式覆盖（必须带 factionId/factionRole）；
    /// Unaffiliated = 本次 Spawn 明确无势力（禁止 factionId/factionRole）。
    /// </summary>
    public enum OpeningFactionMode
    {
        CharacterDefault = 0,
        Override = 1,
        Unaffiliated = 2
    }

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
        /// <summary>[Legacy Content compatibility] 旧 spawn 未显式 factionId 时的回退势力。新 Content 禁止写入。</summary>
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
        /// <summary>[Legacy Content compatibility] 新 Content 以 FactionId 非空表示要赋予 Membership。</summary>
        public bool AssignOpeningFaction { get; set; }
        /// <summary>FactionId 非空时必须为非 None 的 FactionRoleKind；FactionId 空时必须为空。</summary>
        public string FactionRole { get; set; }
        /// <summary>开局实例的显式势力归属；不从 CharacterDefinition 或 WorldSite 推断。</summary>
        public string FactionId { get; set; }

        /// <summary>
        /// 正式 Spawn 的势力三模式（缺省 = CharacterDefault）：
        /// CharacterDefault（继承人物默认）/ Override（本 Spawn 覆盖）/ Unaffiliated（本次明确无势力）。
        /// 旧 Content 缺省 + factionId 非空 = Legacy Explicit Override（兼容）。
        /// </summary>
        public OpeningFactionMode FactionMode { get; set; } = OpeningFactionMode.CharacterDefault;

        /// <summary>JSON 是否显式写了 factionMode（用于区分「缺省 CharacterDefault」与「显式 CharacterDefault」）。</summary>
        public bool FactionModeExplicit { get; set; }
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
