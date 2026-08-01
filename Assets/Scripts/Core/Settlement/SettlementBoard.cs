using System;
using System.Collections.Generic;

namespace XianXia.Core.Settlement
{
    /// <summary>World-level settlement registry (not persisted in Snapshot v1).</summary>
    public sealed class SettlementBoard
    {
        readonly Dictionary<string, SettlementState> _settlements =
            new Dictionary<string, SettlementState>(StringComparer.Ordinal);

        public string PrimarySettlementId { get; set; } = string.Empty;

        public IReadOnlyDictionary<string, SettlementState> All => _settlements;

        public bool TryGet(string id, out SettlementState state)
        {
            state = null;
            if (string.IsNullOrEmpty(id))
                return false;
            return _settlements.TryGetValue(id, out state);
        }

        public bool TryGetPrimary(out SettlementState state)
        {
            state = null;
            if (string.IsNullOrEmpty(PrimarySettlementId))
                return false;
            return TryGet(PrimarySettlementId, out state);
        }

        public void Register(SettlementState settlement, bool asPrimary = false)
        {
            if (settlement == null || string.IsNullOrEmpty(settlement.Id))
                throw new ArgumentException("SettlementState requires Id.");
            _settlements[settlement.Id] = settlement;
            if (asPrimary || string.IsNullOrEmpty(PrimarySettlementId))
                PrimarySettlementId = settlement.Id;
        }
    }
}
