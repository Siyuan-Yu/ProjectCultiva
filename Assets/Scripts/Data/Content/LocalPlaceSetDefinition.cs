using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    /// <summary>
    /// 绑定某张 mapLayout 的村内逻辑地点表（取代 worldRegion 的正式职责）。
    /// 运行时仍灌入 SimulationWorld.WorldRegion 板。
    /// </summary>
    public sealed class LocalPlaceSetDefinition
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; }
        public string MapLayoutId { get; set; }
        public string StartLocationId { get; set; }
        public List<WorldLocationEntry> Locations { get; set; } = new List<WorldLocationEntry>();
    }
}
