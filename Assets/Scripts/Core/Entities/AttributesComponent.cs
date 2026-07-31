using System;
using System.Collections.Generic;
using XianXia.Core.Attributes;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Results;

namespace XianXia.Core.Entities
{
    /// <summary>
    /// Attribute container + modifier attach point. Final values come only from AttributePipe.
    /// </summary>
    public sealed class AttributesComponent : IComponent
    {
        readonly Dictionary<AttributeId, int> _base = new Dictionary<AttributeId, int>();
        readonly List<AttributeModifier> _modifiers = new List<AttributeModifier>();
        readonly ModifierIdFactory _modifierIds;

        public AttributesComponent(ModifierIdFactory modifierIds = null)
        {
            _modifierIds = modifierIds ?? new ModifierIdFactory();
        }

        public IReadOnlyDictionary<AttributeId, int> BaseValues => _base;

        public IReadOnlyList<AttributeModifier> Modifiers => _modifiers;

        public void SetBase(AttributeId id, int value) => _base[id] = value;

        /// <summary>Compatibility helper for content string keys such as "MaxHp".</summary>
        public void SetBase(string attributeName, int value)
        {
            if (!Enum.TryParse(attributeName, true, out AttributeId id))
                throw new ArgumentException("Unknown AttributeId: " + attributeName, nameof(attributeName));
            SetBase(id, value);
        }

        public int GetBase(AttributeId id) => _base.TryGetValue(id, out var v) ? v : 0;

        public int GetBase(string attributeId)
        {
            if (!Enum.TryParse(attributeId, true, out AttributeId id))
                return 0;
            return GetBase(id);
        }

        public Result<AttributeModifier> AddModifier(
            AttributeId target,
            ModifierOperation operation,
            double value,
            SourceRef source)
        {
            var modifier = new AttributeModifier(_modifierIds.Next(), target, operation, value, source);
            _modifiers.Add(modifier);
            return Result.Ok(modifier);
        }

        public int RemoveBySource(SourceRef source)
        {
            return _modifiers.RemoveAll(m => m.Source.Equals(source));
        }

        public int GetFinal(AttributeId id, int? min = null, int? max = null)
        {
            return AttributePipe.Compute(GetBase(id), Enumerate(id), min, max);
        }

        public List<AttributeContribution> Explain(AttributeId id) =>
            AttributePipe.Explain(GetBase(id), Enumerate(id));

        IEnumerable<AttributeModifier> Enumerate(AttributeId id)
        {
            foreach (var m in _modifiers)
            {
                if (m.Target == id)
                    yield return m;
            }
        }
    }
}
