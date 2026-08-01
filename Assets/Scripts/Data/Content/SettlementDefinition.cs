using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    public sealed class SettlementDefinition
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; }
        public List<SettlementStockEntry> InitialStock { get; set; } = new List<SettlementStockEntry>();
        public List<string> FacilityIds { get; set; } = new List<string>();
    }

    public sealed class SettlementStockEntry
    {
        public string ResourceId { get; set; }
        public int Amount { get; set; }
    }
}
