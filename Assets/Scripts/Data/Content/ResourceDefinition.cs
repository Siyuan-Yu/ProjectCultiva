using XianXia.Core.Domain.Ids;
using System.Collections.Generic;

namespace XianXia.Data.Content
{
    public sealed class ResourceDefinition
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; }
        public string NameKey { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
    }
}
