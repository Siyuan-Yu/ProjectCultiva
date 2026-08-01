using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Unity.Host
{
    /// <summary>EntityId → EntityView map for the playable host.</summary>
    public sealed class EntityViewRegistry
    {
        readonly Dictionary<ulong, EntityView> _views = new Dictionary<ulong, EntityView>();

        public int Count => _views.Count;

        public bool Register(EntityId id, EntityView view)
        {
            if (id.IsNone || view == null)
                return false;
            _views[id.Value] = view;
            return true;
        }

        public bool TryGet(EntityId id, out EntityView view)
        {
            view = null;
            if (id.IsNone)
                return false;
            return _views.TryGetValue(id.Value, out view) && view != null;
        }

        public bool Contains(EntityId id) => !id.IsNone && _views.ContainsKey(id.Value);

        public IEnumerable<EntityView> All
        {
            get
            {
                foreach (var kv in _views)
                {
                    if (kv.Value != null)
                        yield return kv.Value;
                }
            }
        }

        public void Clear() => _views.Clear();
    }
}
