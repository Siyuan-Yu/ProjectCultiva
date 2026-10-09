using System;
using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Npc
{
    /// <summary>Persistent one-injured-per-spot delivery identity. Active rescue reservations stay on MortalCivilianState.</summary>
    public sealed class RecoverySpotDeliveryBoard
    {
        readonly Dictionary<string, EntityId> _placements =
            new Dictionary<string, EntityId>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, EntityId> Placements => _placements;

        public void Clear() => _placements.Clear();

        public bool TryGetInjured(string recoverySpotId, out EntityId injured) =>
            _placements.TryGetValue(recoverySpotId ?? string.Empty, out injured);

        public bool ContainsInjured(EntityId injured)
        {
            foreach (var value in _placements.Values)
                if (value == injured) return true;
            return false;
        }

        public bool TryPlace(string recoverySpotId, EntityId injured)
        {
            if (string.IsNullOrWhiteSpace(recoverySpotId) || injured.IsNone ||
                _placements.ContainsKey(recoverySpotId) || ContainsInjured(injured)) return false;
            _placements.Add(recoverySpotId, injured);
            return true;
        }

        public bool TryRestore(string recoverySpotId, EntityId injured) => TryPlace(recoverySpotId, injured);

        public void RemoveSpot(string recoverySpotId)
        {
            if (!string.IsNullOrWhiteSpace(recoverySpotId)) _placements.Remove(recoverySpotId);
        }

        public void RemoveInjured(EntityId injured)
        {
            string found = null;
            foreach (var pair in _placements)
                if (pair.Value == injured) { found = pair.Key; break; }
            if (found != null) _placements.Remove(found);
        }
    }
}
