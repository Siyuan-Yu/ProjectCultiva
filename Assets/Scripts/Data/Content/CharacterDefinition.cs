using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    /// <summary>
    /// Content-only character template. No gameplay calculation.
    /// </summary>
    public sealed class CharacterDefinition
    {
        public DefinitionId Id { get; set; }
        /// <summary>Author-facing display name (not full localization).</summary>
        public string Name { get; set; }
        public string DisplayNameKey { get; set; }
        public string NameKey { get; set; }
        public Dictionary<string, int> BaseAttributes { get; set; } = new Dictionary<string, int>();
        public List<string> Tags { get; set; } = new List<string>();
        /// <summary>VS0.1 placeholder; no spirit-root formula in Data.</summary>
        public string SpiritRootPlaceholder { get; set; }
        /// <summary>VS0.1 placeholder; no breakthrough / realm gameplay in Data.</summary>
        public string InitialRealmPlaceholder { get; set; }
    }
}
