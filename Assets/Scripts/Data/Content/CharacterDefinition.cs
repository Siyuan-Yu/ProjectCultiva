using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    public sealed class CharacterDefinition
    {
        public DefinitionId Id { get; set; }
        public string DisplayNameKey { get; set; }
        public Dictionary<string, int> BaseAttributes { get; set; } = new Dictionary<string, int>();
    }
}
