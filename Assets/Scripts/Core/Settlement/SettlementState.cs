using System;
using System.Collections.Generic;

namespace XianXia.Core.Settlement
{
    /// <summary>Runtime settlement (洞府／据点). Session-only; not Snapshot schema.</summary>
    public sealed class SettlementState
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, int> Stock { get; } =
            new Dictionary<string, int>(StringComparer.Ordinal);
        public List<FacilityRuntime> Facilities { get; } = new List<FacilityRuntime>();

        public int GetStock(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId))
                return 0;
            return Stock.TryGetValue(resourceId, out var n) ? n : 0;
        }

        public void AddStock(string resourceId, int delta)
        {
            if (string.IsNullOrEmpty(resourceId) || delta == 0)
                return;
            var next = GetStock(resourceId) + delta;
            if (next < 0)
                next = 0;
            Stock[resourceId] = next;
        }
    }
}
