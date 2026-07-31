using System;
using System.Collections.Generic;

namespace XianXia.Core.Entities
{
    public sealed class EntityQuery
    {
        readonly EntityStore _store;

        public EntityQuery(EntityStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public List<Entity> WithTag(EntityTag tag)
        {
            var list = new List<Entity>();
            foreach (var e in _store.All)
            {
                if ((e.Tags & tag) != 0)
                    list.Add(e);
            }
            return list;
        }

        public List<Entity> WithLifecycle(LifecycleState state)
        {
            var list = new List<Entity>();
            foreach (var e in _store.All)
            {
                if (e.TryGet<LifecycleComponent>(out var life) && life.State == state)
                    list.Add(e);
            }
            return list;
        }

        public List<Entity> WithComponent<T>() where T : class, IComponent
        {
            var list = new List<Entity>();
            foreach (var e in _store.All)
            {
                if (e.TryGet<T>(out _))
                    list.Add(e);
            }
            return list;
        }
    }
}
