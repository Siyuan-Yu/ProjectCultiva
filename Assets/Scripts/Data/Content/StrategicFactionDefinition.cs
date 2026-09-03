using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    /// <summary>
    /// 战略势力 Content 定义（Data/Factions/factions.json，type = strategicFaction）。
    /// 只描述「这个势力是谁」：id / 展示名 / 地图色 / 作者元数据。
    /// 不保存成员、领土、WorldSite —— 那些属于 CharacterSpawn / HexWorld / Territory 各自 authority。
    /// </summary>
    public sealed class StrategicFactionDefinition
    {
        public DefinitionId Id { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>#RRGGBB（0..255）；加载期严格校验格式。</summary>
        public string MapColor { get; set; } = "#B3945C";

        /// <summary>
        /// 是否可作为 authored Territory 的 Controller（如山匪 = false）。
        /// 仍是合法 faction：Army / Character 可以属于它，只是不默认出现在 Territory Brush 列表。
        /// </summary>
        public bool TerritorySelectable { get; set; } = true;

        public int SortOrder { get; set; }
    }
}
