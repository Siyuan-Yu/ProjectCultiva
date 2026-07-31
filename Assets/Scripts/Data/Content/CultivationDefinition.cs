using System.Collections.Generic;
using XianXia.Core.Domain.Ids;

namespace XianXia.Data.Content
{
    /// <summary>
    /// Content-only cultivation / manual template. Modifier grants are config for Core to apply later.
    /// Data must not compute Final attributes.
    /// </summary>
    public sealed class CultivationDefinition
    {
        public DefinitionId Id { get; set; }
        /// <summary>Author-facing display name (not full localization).</summary>
        public string Name { get; set; }
        public string DisplayNameKey { get; set; }
        public string NameKey { get; set; }
        /// <summary>Placeholder required realm; validated as optional DefinitionId ref when namespaced.</summary>
        public string RequiredRealm { get; set; }
        public List<ModifierGrantDefinition> GrantedModifiers { get; set; } = new List<ModifierGrantDefinition>();
        public List<string> Tags { get; set; } = new List<string>();
    }

    /// <summary>
    /// Serializable modifier grant config. Interpreted by Core AttributePipe, not by Data.
    /// </summary>
    public sealed class ModifierGrantDefinition
    {
        public string TargetAttribute { get; set; }
        public string Operation { get; set; }
        public int Value { get; set; }
        public string StackingKey { get; set; }
    }
}
