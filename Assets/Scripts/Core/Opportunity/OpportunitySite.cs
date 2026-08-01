using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Opportunity
{
    /// <summary>
    /// Abstract discoverable site (no coordinates / map). Rules-only.
    /// </summary>
    public sealed class OpportunitySite
    {
        public OpportunitySite(
            DefinitionId id,
            bool allowsCultivation,
            DefinitionId? offeredManualId = null,
            string nameKey = null,
            string description = null)
        {
            Id = id;
            AllowsCultivation = allowsCultivation;
            OfferedManualId = offeredManualId;
            NameKey = nameKey ?? string.Empty;
            Description = description ?? string.Empty;
        }

        public DefinitionId Id { get; }

        public bool AllowsCultivation { get; }

        /// <summary>Manual offered as cultivation entry after discovery (optional).</summary>
        public DefinitionId? OfferedManualId { get; }

        public string NameKey { get; }

        public string Description { get; }
    }
}
