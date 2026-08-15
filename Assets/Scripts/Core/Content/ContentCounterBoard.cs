using System;
using System.Collections.Generic;

namespace XianXia.Core.Content
{
    /// <summary>
    /// Session counters for quest／event progress（对弈胜场等）. Not in Snapshot v1.
    /// </summary>
    public sealed class ContentCounterBoard
    {
        readonly Dictionary<string, int> _counts = new Dictionary<string, int>(StringComparer.Ordinal);

        public int Get(string id)
        {
            if (string.IsNullOrEmpty(id))
                return 0;
            return _counts.TryGetValue(id, out var n) ? n : 0;
        }

        public int Add(string id, int delta)
        {
            if (string.IsNullOrEmpty(id) || delta == 0)
                return Get(id);
            var next = Get(id) + delta;
            if (next < 0)
                next = 0;
            _counts[id] = next;
            return next;
        }

        public void Set(string id, int value)
        {
            if (string.IsNullOrEmpty(id))
                return;
            _counts[id] = value < 0 ? 0 : value;
        }

        public void Clear() => _counts.Clear();
    }
}
