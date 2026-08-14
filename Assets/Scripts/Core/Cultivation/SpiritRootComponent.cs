using System.Collections.Generic;
using XianXia.Core.Attributes;
using XianXia.Core.Entities;

namespace XianXia.Core.Cultivation
{
    /// <summary>Per-axis spirit-root affinities (content-driven; not Snapshot v1 required).</summary>
    public sealed class SpiritRootComponent : IComponent
    {
        readonly Dictionary<SpiritRootKind, int> _values = new Dictionary<SpiritRootKind, int>();

        public const int DefaultMax = 30;

        public IReadOnlyDictionary<SpiritRootKind, int> Values => _values;

        public int Get(SpiritRootKind kind) => _values.TryGetValue(kind, out var v) ? v : 0;

        public void Set(SpiritRootKind kind, int value)
        {
            if (value < 0)
                value = 0;
            _values[kind] = value;
        }

        public void Clear() => _values.Clear();
    }
}
