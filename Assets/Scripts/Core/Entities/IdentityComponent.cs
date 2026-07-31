using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Entities
{
    public sealed class IdentityComponent : IComponent
    {
        public IdentityComponent(DefinitionId definitionId, string displayName)
        {
            DefinitionId = definitionId;
            DisplayName = displayName ?? string.Empty;
        }

        public DefinitionId DefinitionId { get; }

        public string DisplayName { get; set; }
    }
}
