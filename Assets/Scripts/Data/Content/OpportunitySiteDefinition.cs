using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    /// <summary>
    /// Content-only abstract OpportunitySite. No coordinates / map.
    /// </summary>
    public sealed class OpportunitySiteDefinition
    {
        public DefinitionId Id { get; set; }
        public string Name { get; set; }
        public string NameKey { get; set; }
        public string Description { get; set; }
        public bool AllowsCultivation { get; set; }
        /// <summary>Optional cultivation manual DefinitionId offered after discovery.</summary>
        public string OfferedManualId { get; set; }
    }
}
