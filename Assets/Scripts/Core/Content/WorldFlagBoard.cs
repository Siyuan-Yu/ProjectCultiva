using System;
using System.Collections.Generic;

namespace XianXia.Core.Content
{
    /// <summary>Session story／content flags (not in Snapshot v1).</summary>
    public sealed class WorldFlagBoard
    {
        readonly HashSet<string> _flags = new HashSet<string>(StringComparer.Ordinal);
        readonly List<string> _history = new List<string>();

        public bool Has(string flag) =>
            !string.IsNullOrEmpty(flag) && _flags.Contains(flag);

        public bool Set(string flag)
        {
            if (string.IsNullOrEmpty(flag))
                return false;
            return _flags.Add(flag);
        }

        public bool Clear(string flag)
        {
            if (string.IsNullOrEmpty(flag))
                return false;
            return _flags.Remove(flag);
        }

        public void RecordHistory(string entry)
        {
            if (!string.IsNullOrEmpty(entry))
                _history.Add(entry);
        }

        public IReadOnlyCollection<string> All => _flags;

        public IReadOnlyList<string> History => _history;
    }
}
