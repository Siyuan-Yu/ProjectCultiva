using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    /// <summary>
    /// Content-only item template. No inventory gameplay logic.
    /// </summary>
    public sealed class ItemDefinition
    {
        public DefinitionId Id { get; set; }
        /// <summary>Author-facing display name (not full localization).</summary>
        public string Name { get; set; }
        public string DisplayNameKey { get; set; }
        public string NameKey { get; set; }
        public int MaxStack { get; set; } = 1;
        public List<string> Tags { get; set; } = new List<string>();
    }
}
