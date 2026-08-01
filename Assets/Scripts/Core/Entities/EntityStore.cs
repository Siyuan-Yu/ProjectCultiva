using System.Collections.Generic;
using XianXia.Core.Concealment;
using XianXia.Core.Cultivation;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Labor;
using XianXia.Core.Opportunity;
using XianXia.Core.Results;
using XianXia.Core.Social;

namespace XianXia.Core.Entities
{
    public sealed class EntityStore
    {
        readonly Dictionary<EntityId, Entity> _entities = new Dictionary<EntityId, Entity>();
        readonly EntityIdFactory _ids;

        public EntityStore(EntityIdFactory ids = null)
        {
            _ids = ids ?? new EntityIdFactory();
        }

        public EntityIdFactory Ids => _ids;

        public int Count => _entities.Count;

        public Result<Entity> CreateCharacter(DefinitionId definitionId, string displayName = null)
        {
            var entity = new Entity(_ids.Next(), definitionId, EntityTag.Character, displayName ?? definitionId.ToString());
            entity.AddComponent(new IdentityComponent(definitionId, entity.DisplayName));
            entity.AddComponent(new AttributesComponent());
            entity.AddComponent(new LifecycleComponent(LifecycleState.Alive));
            entity.AddComponent(new ActionStateComponent());
            entity.AddComponent(new CultivationComponent());
            entity.AddComponent(new DailyTaskComponent());
            entity.AddComponent(new KnownSitesComponent());
            entity.AddComponent(new PersonalConcealmentRiskComponent());
            entity.AddComponent(new PersonalityProfileComponent());
            _entities.Add(entity.Id, entity);
            return Result.Ok(entity);
        }

        public bool TryGet(EntityId id, out Entity entity) => _entities.TryGetValue(id, out entity);

        public Result AddExisting(Entity entity)
        {
            if (entity == null)
                return Result.Failure(ErrorCode.InvalidArgument, "Entity is null.");
            if (_entities.ContainsKey(entity.Id))
                return Result.Failure(ErrorCode.AlreadyExists, "Entity id already present.", entity.Id.ToString());

            _entities.Add(entity.Id, entity);
            return Result.Success();
        }

        public Result MarkRemoved(EntityId id)
        {
            if (!_entities.TryGetValue(id, out var entity))
                return Result.Failure(ErrorCode.EntityNotFound, "Entity not found.", id.ToString());

            if (!entity.TryGet<LifecycleComponent>(out var life))
                return Result.Failure(ErrorCode.ComponentMissing, "LifecycleComponent missing.");

            life.State = LifecycleState.Removed;
            return Result.Success();
        }

        public IEnumerable<Entity> All => _entities.Values;
    }
}
