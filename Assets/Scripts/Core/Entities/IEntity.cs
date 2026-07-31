using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Entities
{
    public interface IEntity
    {
        EntityId Id { get; }

        DefinitionId DefinitionId { get; }

        string DisplayName { get; }

        EntityTag Tags { get; }

        bool TryGet<T>(out T component) where T : class, IComponent;

        T Get<T>() where T : class, IComponent;
    }
}
