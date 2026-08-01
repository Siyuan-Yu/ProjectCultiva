using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    public sealed class ResourceDefinition
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; }
        public string NameKey { get; set; }
    }
}
