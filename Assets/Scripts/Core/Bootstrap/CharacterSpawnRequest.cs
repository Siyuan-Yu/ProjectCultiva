using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Core.Bootstrap
{
    /// <summary>
    /// Core-side spawn request. Content packages map into this; Data must not compute Final.
    /// </summary>
    public sealed class CharacterSpawnRequest
    {
        public DefinitionId DefinitionId { get; set; }
        public string Name { get; set; }
        public Dictionary<string, int> BaseAttributes { get; set; } = new Dictionary<string, int>();
        public List<string> PersonalityTags { get; set; } = new List<string>();
        public string SpiritRootPlaceholder { get; set; }
        public string InitialRealmPlaceholder { get; set; }
    }
}
