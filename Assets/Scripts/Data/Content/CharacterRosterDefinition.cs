using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    /// <summary>
    /// Level Tester／试玩名册：谁出场。人物本体仍在 Characters/；本表只列出场条目。
    /// </summary>
    public sealed class CharacterRosterDefinition
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; }
        public List<OpeningSpawnEntry> Entries { get; set; } = new List<OpeningSpawnEntry>();
    }
}
