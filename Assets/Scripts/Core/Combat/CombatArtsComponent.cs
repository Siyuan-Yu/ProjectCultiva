using System.Collections.Generic;
using XianXia.Core.Domain.Ids;
using XianXia.Core.Entities;

namespace XianXia.Core.Combat
{
    /// <summary>已学斗技（可多）＋装备栏最多 6 格（快捷键 1–6）。</summary>
    public sealed class CombatArtsComponent : IComponent
    {
        public const int MaxEquippedSlots = 6;

        readonly List<DefinitionId> _learned = new List<DefinitionId>(12);
        readonly DefinitionId?[] _equipped = new DefinitionId?[MaxEquippedSlots];

        public IReadOnlyList<DefinitionId> Learned => _learned;

        /// <summary>兼容旧测试：第一格非空装备。</summary>
        public DefinitionId? EquippedArtId
        {
            get
            {
                for (var i = 0; i < MaxEquippedSlots; i++)
                {
                    if (_equipped[i].HasValue)
                        return _equipped[i];
                }

                return null;
            }
            set
            {
                if (!value.HasValue)
                {
                    ClearSlot(0);
                    return;
                }

                TryEquipToSlot(0, value.Value);
            }
        }

        public bool HasEquipped => EquippedArtId.HasValue;

        public DefinitionId? GetEquipped(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxEquippedSlots)
                return null;
            return _equipped[slotIndex];
        }

        public bool Knows(DefinitionId id)
        {
            if (string.IsNullOrEmpty(id.Namespace))
                return false;
            for (var i = 0; i < _learned.Count; i++)
            {
                if (_learned[i].Equals(id))
                    return true;
            }

            return false;
        }

        public bool TryLearn(DefinitionId id)
        {
            if (string.IsNullOrEmpty(id.Namespace) || Knows(id))
                return false;
            _learned.Add(id);
            TryEquipFirstEmpty(id);
            return true;
        }

        public bool TryEquip(DefinitionId id) => TryEquipFirstEmpty(id);

        public bool TryEquipToSlot(int slotIndex, DefinitionId id)
        {
            if (slotIndex < 0 || slotIndex >= MaxEquippedSlots ||
                string.IsNullOrEmpty(id.Namespace) || !Knows(id))
                return false;
            // 同一技能只占一格：先从其他格卸下
            for (var i = 0; i < MaxEquippedSlots; i++)
            {
                if (_equipped[i].HasValue && _equipped[i].Value.Equals(id))
                    _equipped[i] = null;
            }

            _equipped[slotIndex] = id;
            return true;
        }

        public bool TryEquipFirstEmpty(DefinitionId id)
        {
            if (string.IsNullOrEmpty(id.Namespace) || !Knows(id))
                return false;
            for (var i = 0; i < MaxEquippedSlots; i++)
            {
                if (_equipped[i].HasValue && _equipped[i].Value.Equals(id))
                    return true;
            }

            for (var i = 0; i < MaxEquippedSlots; i++)
            {
                if (!_equipped[i].HasValue)
                {
                    _equipped[i] = id;
                    return true;
                }
            }

            return false;
        }

        public void ClearSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxEquippedSlots)
                return;
            _equipped[slotIndex] = null;
        }
    }
}
