using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Unity.Host
{
    /// <summary>
    /// Unity Host presentation-layer selection set. Not a Core component.
    /// </summary>
    public sealed class HostSelectionState
    {
        readonly List<EntityId> _ordered = new List<EntityId>();
        readonly HashSet<ulong> _ids = new HashSet<ulong>();

        public int Count => _ordered.Count;

        public IReadOnlyList<EntityId> SelectedIds => _ordered;

        public bool Contains(EntityId id) => !id.IsNone && _ids.Contains(id.Value);

        public void Clear()
        {
            _ordered.Clear();
            _ids.Clear();
        }

        /// <summary>Replace the entire selection (used by normal click and box select).</summary>
        public void Replace(IEnumerable<EntityId> ids)
        {
            Clear();
            if (ids == null)
                return;

            foreach (var id in ids)
                AddInternal(id);
        }

        /// <summary>Replace with a single entity (or clear if None).</summary>
        public void ReplaceOne(EntityId id)
        {
            Clear();
            AddInternal(id);
        }

        /// <summary>Shift-click: toggle membership.</summary>
        public void Toggle(EntityId id)
        {
            if (id.IsNone)
                return;

            if (_ids.Contains(id.Value))
            {
                _ids.Remove(id.Value);
                _ordered.RemoveAll(x => x.Value == id.Value);
            }
            else
            {
                AddInternal(id);
            }
        }

        void AddInternal(EntityId id)
        {
            if (id.IsNone || _ids.Contains(id.Value))
                return;
            _ids.Add(id.Value);
            _ordered.Add(id);
        }
    }
}
