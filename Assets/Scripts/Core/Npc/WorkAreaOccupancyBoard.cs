using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Npc
{
    /// <summary>
    /// Soft work-area slot occupancy: capacity limits who can work the same area;
    /// Host maps slot index → interact spot / ring offset (not hard collision).
    /// </summary>
    public sealed class WorkAreaOccupancyBoard
    {
        readonly Dictionary<string, Dictionary<int, ulong>> _slotsByArea =
            new Dictionary<string, Dictionary<int, ulong>>(System.StringComparer.Ordinal);
        readonly Dictionary<ulong, (string AreaId, int Slot)> _byEntity =
            new Dictionary<ulong, (string, int)>();

        public int CountOccupied(string workAreaId)
        {
            if (string.IsNullOrEmpty(workAreaId) ||
                !_slotsByArea.TryGetValue(workAreaId, out var slots))
                return 0;
            return slots.Count;
        }

        public bool HasFreeSlot(string workAreaId, int capacity)
        {
            if (capacity <= 0)
                capacity = 1;
            return CountOccupied(workAreaId) < capacity;
        }

        public bool TryFindFreeSlot(string workAreaId, int capacity, out int slot)
        {
            slot = -1;
            if (capacity <= 0)
                capacity = 1;
            if (!_slotsByArea.TryGetValue(workAreaId, out var slots))
            {
                slot = 0;
                return true;
            }

            for (var i = 0; i < capacity; i++)
            {
                if (slots.ContainsKey(i))
                    continue;
                slot = i;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Reserve a slot for <paramref name="entity"/>. Same area keeps existing slot;
        /// switching areas releases the old one first.
        /// </summary>
        public bool TryReserve(string workAreaId, EntityId entity, int capacity, out int slot)
        {
            slot = -1;
            if (string.IsNullOrEmpty(workAreaId) || entity.IsNone)
                return false;
            if (capacity <= 0)
                capacity = 1;

            if (_byEntity.TryGetValue(entity.Value, out var cur) &&
                string.Equals(cur.AreaId, workAreaId, System.StringComparison.Ordinal))
            {
                slot = cur.Slot;
                return true;
            }

            Release(entity);

            if (!_slotsByArea.TryGetValue(workAreaId, out var slots))
            {
                slots = new Dictionary<int, ulong>();
                _slotsByArea[workAreaId] = slots;
            }

            for (var i = 0; i < capacity; i++)
            {
                if (slots.ContainsKey(i))
                    continue;
                slots[i] = entity.Value;
                _byEntity[entity.Value] = (workAreaId, i);
                slot = i;
                return true;
            }

            return false;
        }

        public void Release(EntityId entity)
        {
            if (entity.IsNone)
                return;
            if (!_byEntity.TryGetValue(entity.Value, out var cur))
                return;
            _byEntity.Remove(entity.Value);
            if (_slotsByArea.TryGetValue(cur.AreaId, out var slots))
            {
                slots.Remove(cur.Slot);
                if (slots.Count == 0)
                    _slotsByArea.Remove(cur.AreaId);
            }
        }

        public bool TryGet(EntityId entity, out string workAreaId, out int slot)
        {
            workAreaId = string.Empty;
            slot = -1;
            if (entity.IsNone || !_byEntity.TryGetValue(entity.Value, out var cur))
                return false;
            workAreaId = cur.AreaId;
            slot = cur.Slot;
            return true;
        }

        public void Clear()
        {
            _slotsByArea.Clear();
            _byEntity.Clear();
        }
    }
}
