using System;
using System.Collections.Generic;

namespace XianXia.Core.Inventory
{
    public sealed class InventorySlot
    {
        public string ItemId { get; set; } = string.Empty;
        public int Count { get; set; }

        public bool IsEmpty => string.IsNullOrEmpty(ItemId) || Count <= 0;

        public void Clear()
        {
            ItemId = string.Empty;
            Count = 0;
        }
    }

    public sealed class InventoryItemInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int MaxStack { get; set; } = 99;
        /// <summary>非空则可用为功法秘籍。</summary>
        public string TeachesManualId { get; set; } = string.Empty;
        /// <summary>非空则可用为斗技秘本。</summary>
        public string TeachesArtId { get; set; } = string.Empty;
        public List<string> Tags { get; } = new List<string>();
    }

    /// <summary>Display／stack rules for bag items (from content resources＋items).</summary>
    public sealed class InventoryCatalog
    {
        readonly Dictionary<string, InventoryItemInfo> _items =
            new Dictionary<string, InventoryItemInfo>(StringComparer.Ordinal);

        /// <summary>清空静态物品定义；不影响背包内已保存的 ItemId／数量。</summary>
        public void Clear() => _items.Clear();

        public void Register(
            string id,
            string name,
            int maxStack,
            IEnumerable<string> tags,
            string teachesManualId = null,
            string teachesArtId = null)
        {
            if (string.IsNullOrEmpty(id))
                return;
            var info = new InventoryItemInfo
            {
                Id = id,
                Name = string.IsNullOrEmpty(name) ? id : name,
                MaxStack = maxStack < 1 ? 1 : maxStack,
                TeachesManualId = teachesManualId ?? string.Empty,
                TeachesArtId = teachesArtId ?? string.Empty
            };
            if (tags != null)
            {
                foreach (var t in tags)
                {
                    if (!string.IsNullOrEmpty(t))
                        info.Tags.Add(t);
                }
            }

            _items[id] = info;
        }

        public bool TryGet(string id, out InventoryItemInfo info)
        {
            info = null;
            if (string.IsNullOrEmpty(id))
                return false;
            return _items.TryGetValue(id, out info);
        }

        public string GetName(string id)
        {
            if (TryGet(id, out var info))
                return info.Name;
            return string.IsNullOrEmpty(id) ? "?" : id;
        }

        public int GetMaxStack(string id)
        {
            if (TryGet(id, out var info))
                return info.MaxStack;
            return 99;
        }

        public string GetTeachesManualId(string id)
        {
            if (TryGet(id, out var info))
                return info.TeachesManualId ?? string.Empty;
            return string.Empty;
        }

        public string GetTeachesArtId(string id)
        {
            if (TryGet(id, out var info))
                return info.TeachesArtId ?? string.Empty;
            return string.Empty;
        }

        public bool IsManualTome(string id) =>
            !string.IsNullOrEmpty(GetTeachesManualId(id));

        public bool IsCombatArtTome(string id) =>
            !string.IsNullOrEmpty(GetTeachesArtId(id));

        public bool HasTag(string id, string tag)
        {
            if (string.IsNullOrEmpty(tag) || !TryGet(id, out var info))
                return false;
            for (var i = 0; i < info.Tags.Count; i++)
            {
                if (string.Equals(info.Tags[i], tag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }

    /// <summary>Shared party bag: fixed slot capacity, stackable entries.</summary>
    public sealed class PartyInventory
    {
        public const int DefaultSlotCapacity = 50;

        readonly List<InventorySlot> _slots = new List<InventorySlot>(DefaultSlotCapacity);
        readonly InventoryCatalog _catalog;

        public PartyInventory(InventoryCatalog catalog = null, int slotCapacity = DefaultSlotCapacity)
        {
            _catalog = catalog ?? new InventoryCatalog();
            SlotCapacity = slotCapacity < 1 ? DefaultSlotCapacity : slotCapacity;
            EnsureSlots();
        }

        public InventoryCatalog Catalog => _catalog;

        public int SlotCapacity { get; private set; }

        public IReadOnlyList<InventorySlot> Slots => _slots;

        public int UsedSlotCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < _slots.Count; i++)
                {
                    if (!_slots[i].IsEmpty)
                        n++;
                }

                return n;
            }
        }

        public void SetSlotCapacity(int capacity)
        {
            if (capacity < 1)
                capacity = DefaultSlotCapacity;
            SlotCapacity = capacity;
            EnsureSlots();
        }

        public int GetCount(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return 0;
            var total = 0;
            for (var i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                if (!s.IsEmpty && string.Equals(s.ItemId, itemId, StringComparison.Ordinal))
                    total += s.Count;
            }

            return total;
        }

        /// <summary>无副作用查询：当前背包还能完整接纳多少个指定物品。</summary>
        public int GetAddCapacity(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
                return 0;
            var maxStack = _catalog.GetMaxStack(itemId);
            var capacity = 0;
            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.IsEmpty)
                    capacity += maxStack;
                else if (string.Equals(slot.ItemId, itemId, StringComparison.Ordinal))
                    capacity += Math.Max(0, maxStack - slot.Count);
            }
            return capacity;
        }

        public bool CanAddAll(string itemId, int amount) =>
            amount >= 0 && GetAddCapacity(itemId) >= amount;

        /// <summary>Returns how many were actually added (0 if full／invalid).</summary>
        public int TryAdd(string itemId, int amount)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0)
                return 0;

            var maxStack = _catalog.GetMaxStack(itemId);
            var remaining = amount;

            // Fill existing stacks first.
            for (var i = 0; i < _slots.Count && remaining > 0; i++)
            {
                var s = _slots[i];
                if (s.IsEmpty || !string.Equals(s.ItemId, itemId, StringComparison.Ordinal))
                    continue;
                var space = maxStack - s.Count;
                if (space <= 0)
                    continue;
                var put = remaining < space ? remaining : space;
                s.Count += put;
                remaining -= put;
            }

            // Then empty slots.
            for (var i = 0; i < _slots.Count && remaining > 0; i++)
            {
                var s = _slots[i];
                if (!s.IsEmpty)
                    continue;
                var put = remaining < maxStack ? remaining : maxStack;
                s.ItemId = itemId;
                s.Count = put;
                remaining -= put;
            }

            return amount - remaining;
        }

        public bool TryAddAll(string itemId, int amount) =>
            TryAdd(itemId, amount) == amount;

        /// <summary>Returns how many were removed.</summary>
        public int TryRemove(string itemId, int amount)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0)
                return 0;
            var need = amount;
            for (var i = _slots.Count - 1; i >= 0 && need > 0; i--)
            {
                var s = _slots[i];
                if (s.IsEmpty || !string.Equals(s.ItemId, itemId, StringComparison.Ordinal))
                    continue;
                var take = s.Count < need ? s.Count : need;
                s.Count -= take;
                need -= take;
                if (s.Count <= 0)
                    s.Clear();
            }

            return amount - need;
        }

        public bool TryRemoveAll(string itemId, int amount) =>
            GetCount(itemId) >= amount && TryRemove(itemId, amount) == amount;

        /// <summary>Merge stacks and sort by category then name then id.</summary>
        public void Organize()
        {
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                if (s.IsEmpty)
                    continue;
                totals.TryGetValue(s.ItemId, out var cur);
                totals[s.ItemId] = cur + s.Count;
                s.Clear();
            }

            var ids = new List<string>(totals.Keys);
            ids.Sort(CompareItems);

            var slot = 0;
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                var left = totals[id];
                var maxStack = _catalog.GetMaxStack(id);
                while (left > 0 && slot < _slots.Count)
                {
                    var put = left < maxStack ? left : maxStack;
                    _slots[slot].ItemId = id;
                    _slots[slot].Count = put;
                    left -= put;
                    slot++;
                }
            }
        }

        /// <summary>
        /// 仅当按当前内容的 MaxStack 重新整理不会超出槽位时才整理，避免内容壳重建丢失存档物品数量。
        /// </summary>
        public bool TryOrganizeWithoutLoss()
        {
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.IsEmpty)
                    continue;
                totals.TryGetValue(slot.ItemId, out var current);
                totals[slot.ItemId] = current + slot.Count;
            }

            var requiredSlots = 0;
            foreach (var pair in totals)
            {
                var maxStack = _catalog.GetMaxStack(pair.Key);
                requiredSlots += (pair.Value + maxStack - 1) / maxStack;
                if (requiredSlots > SlotCapacity)
                    return false;
            }

            Organize();
            return true;
        }

        int CompareItems(string a, string b)
        {
            var ca = PrimaryCategory(a);
            var cb = PrimaryCategory(b);
            var c = string.CompareOrdinal(ca, cb);
            if (c != 0)
                return c;
            c = string.CompareOrdinal(_catalog.GetName(a), _catalog.GetName(b));
            if (c != 0)
                return c;
            return string.CompareOrdinal(a, b);
        }

        string PrimaryCategory(string id)
        {
            if (_catalog.HasTag(id, "consumable"))
                return "2_consumable";
            if (_catalog.HasTag(id, "resource"))
                return "1_resource";
            return "9_other";
        }

        void EnsureSlots()
        {
            while (_slots.Count < SlotCapacity)
                _slots.Add(new InventorySlot());
            while (_slots.Count > SlotCapacity)
            {
                // Drop overflow empty first; if filled, keep data in last kept by refusing shrink of filled — simple: only shrink empty tail.
                var last = _slots[_slots.Count - 1];
                if (!last.IsEmpty)
                    break;
                _slots.RemoveAt(_slots.Count - 1);
            }
        }
    }
}
