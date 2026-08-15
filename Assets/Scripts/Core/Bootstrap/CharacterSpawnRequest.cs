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
        public SpawnEntityKind EntityKind { get; set; } = SpawnEntityKind.Character;
        public Dictionary<string, bool> ActivityCapabilities { get; set; } = new Dictionary<string, bool>();
        public Dictionary<string, int> ActivityPriorities { get; set; } = new Dictionary<string, int>();
        public List<string> PreferredWorkAreaIds { get; set; } = new List<string>();
        public string HomeWorkAreaId { get; set; } = string.Empty;
        public Dictionary<string, int> SpiritRoots { get; set; } = new Dictionary<string, int>();
        public string Hometown { get; set; } = string.Empty;
        public int Reputation { get; set; }
        public List<string> Goals { get; set; } = new List<string>();
        public List<string> Desires { get; set; } = new List<string>();
        /// <summary>击倒时写入 encounter:{id}；空则无。</summary>
        public string DefeatEncounterId { get; set; } = string.Empty;
    }
}
