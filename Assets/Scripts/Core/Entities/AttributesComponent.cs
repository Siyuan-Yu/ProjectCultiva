using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Entities
{
    /// <summary>
    /// Minimal attribute bag for Phase 6. Full AttributeModifier pipe arrives in Phase 7.
    /// </summary>
    public sealed class AttributesComponent : IComponent
    {
        readonly Dictionary<string, int> _base = new Dictionary<string, int>();

        public IReadOnlyDictionary<string, int> BaseValues => _base;

        public void SetBase(string attributeId, int value) => _base[attributeId] = value;

        public int GetBase(string attributeId) =>
            _base.TryGetValue(attributeId, out var v) ? v : 0;
    }
}
