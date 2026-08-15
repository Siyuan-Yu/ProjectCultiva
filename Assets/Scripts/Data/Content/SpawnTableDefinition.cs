using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    /// <summary>刷怪表：按权重抽角色定义，供 mapLayout.spawnZone 引用。</summary>
    public sealed class SpawnTableDefinition
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; }
        public List<SpawnTableEntry> Entries { get; set; } = new List<SpawnTableEntry>();
    }

    public sealed class SpawnTableEntry
    {
        public string DefinitionId { get; set; }
        public int Weight { get; set; } = 1;
        public int CountMin { get; set; } = 1;
        public int CountMax { get; set; } = 1;
    }
}
