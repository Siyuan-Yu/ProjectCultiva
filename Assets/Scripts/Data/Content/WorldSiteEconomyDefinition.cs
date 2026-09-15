using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    public sealed class WorldSiteEconomyStockEntry
    {
        public string ResourceId { get; set; } = string.Empty;
        public int Amount { get; set; }
    }

    /// <summary>New-game defaults for one WorldSite's public stock.</summary>
    public sealed class WorldSiteEconomyDefinition
    {
        public DefinitionId Id { get; set; }
        public string SiteId { get; set; } = string.Empty;
        public List<WorldSiteEconomyStockEntry> InitialPublicStock { get; } =
            new List<WorldSiteEconomyStockEntry>();
    }
}
